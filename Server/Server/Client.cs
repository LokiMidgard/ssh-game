using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Extensions.Logging;
using SshGame.Server.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SshGame.Server.KexAlgorithms;
using System.Threading;
using System.Security.Cryptography;
using SshGame.Server.Services;
using Org.BouncyCastle.Math.EC.Rfc7748;

namespace SshGame.Server
{
    internal class Client
    {
        private Socket? m_Socket;
        private ILogger m_Logger;

        private bool m_HasCompletedProtocolVersionExchange = false;

        public KexInit KexInitServerToClient { get; } = new KexInit();
        public string ProtocolVersionExchange { get; private set; }

        private byte[]? m_SessionId = null;
        public ReadOnlySpan<byte> SessionId { get => m_SessionId; }
        private int m_CurrentSentPacketNumber = -1;
        private int m_CurrentReceivedPacketNumber = -1;

        private long m_TotalBytesTransferred = 0;
        private DateTime m_KeyTimeout = DateTime.UtcNow.AddHours(1);

        private ExchangeContext m_ActiveExchangeContextIncomming = new ExchangeContext();
        private ExchangeContext m_ActiveExchangeContextOutgoing = new ExchangeContext();

        private Service currentService;
        public string User { get; set; }
        public byte[] UserIdentity { get; set; }

        // We are considered connected if we have a valid socket object
        public bool IsConnected { get { return m_Socket != null; } }

        public bool SupportUTF8 { get; internal set; }
        public TerminalColor Colors { get; internal set; }

        public Client(Socket socket, ILogger logger)
        {
            m_Socket = socket;
            m_Logger = logger;

            KexInitServerToClient.KexAlgorithms.AddRange(Server.SupportedKexAlgorithms.Keys.Concat(["ext-info-s"]));
            KexInitServerToClient.ServerHostKeyAlgorithms.AddRange(Server.SupportedHostKeyAlgorithms.Keys);
            KexInitServerToClient.EncryptionAlgorithmsClientToServer.AddRange(Server.SupportedCiphers.Keys);
            KexInitServerToClient.EncryptionAlgorithmsServerToClient.AddRange(Server.SupportedCiphers.Keys);
            KexInitServerToClient.MacAlgorithmsClientToServer.AddRange(Server.SupportedMACAlgorithms.Keys);
            KexInitServerToClient.MacAlgorithmsServerToClient.AddRange(Server.SupportedMACAlgorithms.Keys);
            KexInitServerToClient.CompressionAlgorithmsClientToServer.AddRange(Server.SupportedCompressions.Keys);
            KexInitServerToClient.CompressionAlgorithmsServerToClient.AddRange(Server.SupportedCompressions.Keys);

            const int socketBufferSize = 2 * IPacket.MaxPacketSize;
            m_Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, socketBufferSize);
            m_Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, socketBufferSize);
            m_Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            m_Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);

            currentService = new InitalService(this, m_Logger);
            // 4.2.Protocol Version Exchange - https://tools.ietf.org/html/rfc4253#section-4.2
            Send($"{Server.ProtocolVersionExchange}\r\n");

            // 7.1.  Algorithm Negotiation - https://tools.ietf.org/html/rfc4253#section-7.1
            Send(KexInitServerToClient);

        }

        internal bool TrySetSessionId(byte[] sessionId)
        {
            if (m_SessionId is not null)
            {
                return false;
            }
            m_SessionId = sessionId;
            return true;
        }
        public void Poll()
        {
            if (!IsConnected)
                return;

            bool dataAvailable = m_Socket.Poll(0, SelectMode.SelectRead);
            if (dataAvailable)
            {
                int read = m_Socket.Available;
                if (read < 1)
                {
                    Disconnect(DisconnectReason.SSH_DISCONNECT_CONNECTION_LOST, "The client disconnected.");
                    return;
                }

                if (!m_HasCompletedProtocolVersionExchange)
                {
                    // Wait for CRLF
                    try
                    {
                        ReadProtocolVersionExchange();
                        if (m_HasCompletedProtocolVersionExchange)
                        {
                            m_Logger.LogDebug($"Received ProtocolVersionExchange: {ProtocolVersionExchange}");
                            ValidateProtocolVersionExchange();
                        }
                    }
                    catch (Exception ex)
                    {
                        m_Logger.LogError(ex.Message);
                        Disconnect(DisconnectReason.SSH_DISCONNECT_PROTOCOL_VERSION_NOT_SUPPORTED, "Failed to get the protocol version exchange.");
                        return;
                    }
                }

                if (m_HasCompletedProtocolVersionExchange)
                {
                    try
                    {
                        IPacket packet = ReadPacket();
                        while (packet != null)
                        {
                            m_Logger.LogDebug($"Received Packet: {packet.Type}");
                            currentService.HandlePacket(packet);
                            packet = ReadPacket();
                        }

                        ConsiderReExchange();
                    }
                    catch (SSHServerException ex)
                    {
                        m_Logger.LogError(ex.Message);
                        Disconnect(ex.Reason, ex.Message);
                        return;
                    }
                    catch (Exception ex)
                    {
                        m_Logger.LogError(ex.Message);
                        Disconnect(DisconnectReason.SSH_DISCONNECT_PROTOCOL_ERROR, ex.Message);
                        return;
                    }
                }
            }
        }

        public void Disconnect(DisconnectReason reason, string message)
        {
            m_Logger.LogDebug($"Disconnected - {reason} - {message}");
            if (m_Socket != null)
            {
                if (reason != DisconnectReason.None)
                {
                    try
                    {
                        Disconnect disconnect = new Disconnect()
                        {
                            Reason = reason,
                            Description = message
                        };
                        Send(disconnect);
                    }
                    catch (Exception) { }
                }

                try
                {
                    m_Socket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception) { }

                m_Socket = null;
            }
        }


        private void Send(string message)
        {
            m_Logger.LogDebug($"Sending raw string: {message.Trim()}");
            SendRaw(Encoding.UTF8.GetBytes(message));
        }

        private void SendRaw(byte[] data)
        {
            if (!IsConnected)
                return;

            // Increase bytes transferred
            m_TotalBytesTransferred += data.Length;

            m_Socket.Send(data);
            
        }

        public void Send(IPacket packet)
        {
            packet.PacketSequence = GetSentPacketNumber();

            byte[] payload = m_ActiveExchangeContextOutgoing.CompressionServerToClient.Compress(packet.GetBytes());

            uint blockSize = m_ActiveExchangeContextOutgoing.CipherServerToClient.BlockSize;

            byte paddingLength = (byte)(blockSize - (payload.Length + 5) % blockSize);
            if (paddingLength < 4)
                paddingLength += (byte)blockSize;

            byte[] padding = new byte[paddingLength];
            RandomNumberGenerator.Create().GetBytes(padding);

            uint packetLength = (uint)(payload.Length + paddingLength + 1);

            using (ByteWriter writer = new ByteWriter())
            {
                writer.WriteUInt32(packetLength);
                writer.WriteByte(paddingLength);
                writer.WriteRawBytes(payload);
                writer.WriteRawBytes(padding);

                payload = writer.ToByteArray();
            }

            byte[] encryptedPayload = m_ActiveExchangeContextOutgoing.CipherServerToClient.Encrypt(payload);
            if (m_ActiveExchangeContextOutgoing.MACAlgorithmServerToClient != null)
            {
                byte[] mac = m_ActiveExchangeContextOutgoing.MACAlgorithmServerToClient.ComputeHash(packet.PacketSequence, payload);
                encryptedPayload = encryptedPayload.Concat(mac).ToArray();
            }

            SendRaw(encryptedPayload);
            this.ConsiderReExchange();
        }

        private uint GetSentPacketNumber()
        {
            return (uint)Interlocked.Increment(ref m_CurrentSentPacketNumber);
        }

        private uint GetReceivedPacketNumber()
        {
            return (uint)Interlocked.Increment(ref m_CurrentReceivedPacketNumber);
        }

        // Read 1 byte from the socket until we find "\r\n"
        private void ReadProtocolVersionExchange()
        {
            NetworkStream stream = new NetworkStream(m_Socket, false);
            string result = null;

            List<byte> data = new List<byte>();

            bool foundCR = false;
            int value = stream.ReadByte();
            while (value != -1)
            {
                if (foundCR && (value == '\n'))
                {
                    // DONE
                    result = Encoding.UTF8.GetString(data.ToArray());
                    m_HasCompletedProtocolVersionExchange = true;
                    break;
                }

                if (value == '\r')
                    foundCR = true;
                else
                {
                    foundCR = false;
                    data.Add((byte)value);
                }

                value = stream.ReadByte();
            }

            ProtocolVersionExchange += result;
        }

        public IPacket? ReadPacket()
        {
            if (m_Socket == null)
                return null;

            uint blockSize = m_ActiveExchangeContextIncomming.CipherClientToServer.BlockSize;

            // We must have at least 1 block to read
            if (m_Socket.Available < blockSize)
                return null;  // Packet not here

            byte[] firstBlock = new byte[blockSize];
            int bytesRead = m_Socket.Receive(firstBlock);
            if (bytesRead != blockSize)
                throw new SSHServerException(DisconnectReason.SSH_DISCONNECT_CONNECTION_LOST, "Failed to read from socket.");
            // var unencripted = firstBlock.ToArray();
            firstBlock = m_ActiveExchangeContextIncomming.CipherClientToServer.Decrypt(firstBlock);

            uint packetLength = 0;
            byte paddingLength = 0;
            using (ByteReader reader = new ByteReader(firstBlock))
            {
                // uint32    packet_length
                // packet_length
                //     The length of the packet in bytes, not including 'mac' or the
                //     'packet_length' field itself.
                packetLength = reader.GetUInt32();
                if (packetLength > IPacket.MaxPacketSize)
                    throw new SSHServerException(DisconnectReason.SSH_DISCONNECT_PROTOCOL_ERROR, $"Client tried to send a packet bigger than MaxPacketSize ({IPacket.MaxPacketSize} bytes): {packetLength} bytes");

                // byte      padding_length
                // padding_length
                //    Length of 'random padding' (bytes).
                paddingLength = reader.GetByte();
            }

            // byte[n1]  payload; n1 = packet_length - padding_length - 1
            // payload
            //    The useful contents of the packet.  If compression has been
            //    negotiated, this field is compressed.  Initially, compression
            //    MUST be "none".
            uint bytesToRead = packetLength - blockSize + 4;

            byte[] restOfPacket = new byte[bytesToRead];
            bytesRead = m_Socket.Receive(restOfPacket);
            if (bytesRead != bytesToRead)
                throw new SSHServerException(DisconnectReason.SSH_DISCONNECT_CONNECTION_LOST, "Failed to read from socket.");
            if (bytesRead > 0)
                restOfPacket = m_ActiveExchangeContextIncomming.CipherClientToServer.Decrypt(restOfPacket);

            uint payloadLength = packetLength - paddingLength - 1;
            byte[] fullPacket = firstBlock.Concat(restOfPacket).ToArray();

            // Track total bytes read
            m_TotalBytesTransferred += fullPacket.Length;

            byte[] payload = fullPacket.Skip(IPacket.PacketHeaderSize).Take((int)(packetLength - paddingLength - 1)).ToArray();

            // byte[n2]  random padding; n2 = padding_length
            // random padding
            //    Arbitrary-length padding, such that the total length of
            //    (packet_length || padding_length || payload || random padding)
            //    is a multiple of the cipher block size or 8, whichever is
            //    larger.  There MUST be at least four bytes of padding.  The
            //    padding SHOULD consist of random bytes.  The maximum amount of
            //    padding is 255 bytes.

            // byte[m]   mac (Message Authentication Code - MAC); m = mac_length
            // mac
            //    Message Authentication Code.  If message authentication has
            //    been negotiated, this field contains the MAC bytes.  Initially,
            //    the MAC algorithm MUST be "none".

            uint packetNumber = GetReceivedPacketNumber();
            if (m_ActiveExchangeContextIncomming.MACAlgorithmClientToServer != null)
            {
                byte[] clientMac = new byte[m_ActiveExchangeContextIncomming.MACAlgorithmClientToServer.DigestLength];
                bytesRead = m_Socket.Receive(clientMac);
                if (bytesRead != m_ActiveExchangeContextIncomming.MACAlgorithmClientToServer.DigestLength)
                    throw new SSHServerException(DisconnectReason.SSH_DISCONNECT_CONNECTION_LOST, "Failed to read from socket.");

                var mac = m_ActiveExchangeContextIncomming.MACAlgorithmClientToServer.ComputeHash(packetNumber, fullPacket);
                if (!clientMac.SequenceEqual(mac))
                {
                    throw new SSHServerException(DisconnectReason.SSH_DISCONNECT_MAC_ERROR, "MAC from client is invalid");
                }
            }

            payload = m_ActiveExchangeContextIncomming.CompressionClientToServer.Decompress(payload);

            using (ByteReader packetReader = new ByteReader(payload))
            {
                PacketType type = (PacketType)packetReader.GetByte();

                if (IPacket.IsSupported(type))
                {
                    var packet = IPacket.Load(type, packetReader);
                    packet.PacketSequence = packetNumber;
                    return packet;
                }

                m_Logger.LogWarning($"Unimplemented packet type: {type}");

                Unimplemented unimplemented = new Unimplemented()
                {
                    RejectedPacketNumber = packetNumber
                };
                Send(unimplemented);
            }

            return null;
        }

        private void ConsiderReExchange()
        {
            const long OneGB = (1024 * 1024 * 1024);
            if ((m_TotalBytesTransferred > OneGB) || (m_KeyTimeout < DateTime.UtcNow))
            {
                // Time to get new keys!
                m_TotalBytesTransferred = 0;
                m_KeyTimeout = DateTime.UtcNow.AddHours(1);

                currentService.ReExchange();
            }
        }

        private void ValidateProtocolVersionExchange()
        {
            // https://tools.ietf.org/html/rfc4253#section-4.2
            //SSH-protoversion-softwareversion SP comments

            string[] pveParts = ProtocolVersionExchange.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (pveParts.Length == 0)
                throw new UnauthorizedAccessException("Invalid Protocol Version Exchange was received - No Data");

            string[] versionParts = pveParts[0].Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (versionParts.Length < 3)
                throw new UnauthorizedAccessException($"Invalid Protocol Version Exchange was received - Not enough dashes - {pveParts[0]}");

            if (versionParts[1] != "2.0")
                throw new UnauthorizedAccessException($"Invalid Protocol Version Exchange was received - Unsupported Version - {versionParts[1]}");

            // If we get here, all is well!
        }

        internal void SetNewExchangeContextIncomming(ExchangeContext m_PendingExchangeContext)
        {
            m_ActiveExchangeContextIncomming = m_PendingExchangeContext;
            m_TotalBytesTransferred = 0;
            m_KeyTimeout = DateTime.UtcNow.AddHours(1);
        }
        internal void SetNewExchangeContextOutgoing(ExchangeContext m_PendingExchangeContext)
        {
            m_ActiveExchangeContextOutgoing = m_PendingExchangeContext;
            m_TotalBytesTransferred = 0;
            m_KeyTimeout = DateTime.UtcNow.AddHours(1);
        }

        internal void SwitchService(Service service)
        {
            m_Logger.LogInformation($"Switch Service: {currentService} => {service}");
            currentService?.Dispose();
            currentService = service;
            service.Init();
        }
    }
}
