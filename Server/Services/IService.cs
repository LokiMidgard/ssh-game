using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using SshGame.Server;
using SshGame.Server.KexAlgorithms;
using SshGame.Server.Packets;

namespace SshGame.Server.Services;

interface IServiceFactory
{
    public Service Create(Client client, ILogger logger);

}

abstract class Service : IDisposable
{
    protected ExchangeContext? m_PendingExchangeContext;
    private KexInit? m_KexInitClientToServer;


    public Service(Client client, ILogger logger)
    {
        this.client = client;
        this.logger = logger;
    }
    private bool disposedValue;
    protected readonly Client client;
    protected readonly ILogger logger;


    internal virtual void Init()
    {

    }

    public void ReExchange()
    {
        logger.LogDebug("Trigger re-exchange from server");
        m_PendingExchangeContext = new ExchangeContext();
        client.Send(client.KexInitServerToClient);
    }

    protected abstract bool HandlePacketInternal(IPacket packet);
    public void HandlePacket(IPacket packet)
    {
        if (HandlePacketInternal(packet))
        {
            return;// nothing todo 
        }

        // handlinof some default packats
        if (packet is Disconnect disconnect)
        {
            client.Disconnect(disconnect.Reason, disconnect.Description);
        }
        else if (packet is KexInit kexInit)
        {
            HandleSpecificPacket(kexInit);
        }
        else if (packet is KexDHInit kexDHInit)
        {
            HandleSpecificPacket(kexDHInit);
        }
        else if (packet is NewKeys newKeys)
        {
            HandleSpecificPacket(newKeys);
        }
        else if (packet is ServiceRequest serviceRequest)
        {
            HandleSpecificPacket(serviceRequest);
        }
        else
        {
            logger.LogWarning($"Unhandled packet type: {packet.Type}");

            Unimplemented unimplemented = new Unimplemented()
            {
                RejectedPacketNumber = packet.PacketSequence
            };
            client.Send(unimplemented);
        }
    }




    private void HandleSpecificPacket(ServiceRequest packet)
    {
        logger.LogInformation($"Requested Service {packet.ServiceName}");
        if (!Server.SupportedServices.TryGetValue(packet.ServiceName, out var factory))
        {
            client.Disconnect(DisconnectReason.SSH_DISCONNECT_SERVICE_NOT_AVAILABLE, $"Not supported {packet.ServiceName}");
            return;
        }
        client.SwitchService(factory().Create(this.client, this.logger));
        client.Send(new Packets.ServiceAccept() { ServiceName = packet.ServiceName });
    }

    private void HandleSpecificPacket(KexInit packet)
    {
        logger.LogDebug("Received KexInit");

        if (m_PendingExchangeContext == null)
        {
            logger.LogDebug("Trigger re-exchange from client");
            m_PendingExchangeContext = new ExchangeContext();
            client.Send(client.KexInitServerToClient);
        }

        m_KexInitClientToServer = packet;

        m_PendingExchangeContext.KexAlgorithm = packet.PickKexAlgorithm();
        m_PendingExchangeContext.HostKeyAlgorithm = packet.PickHostKeyAlgorithm();
        m_PendingExchangeContext.CipherClientToServer = packet.PickCipherClientToServer();
        m_PendingExchangeContext.CipherServerToClient = packet.PickCipherServerToClient();
        m_PendingExchangeContext.MACAlgorithmClientToServer = packet.PickMACAlgorithmClientToServer();
        m_PendingExchangeContext.MACAlgorithmServerToClient = packet.PickMACAlgorithmServerToClient();
        m_PendingExchangeContext.CompressionClientToServer = packet.PickCompressionAlgorithmClientToServer();
        m_PendingExchangeContext.CompressionServerToClient = packet.PickCompressionAlgorithmServerToClient();


        //  logger.LogDebug($"Selected KexAlgorithm: {m_PendingExchangeContext.KexAlgorithm.Name()}");
        //  logger.LogDebug($"Selected HostKeyAlgorithm: {m_PendingExchangeContext.HostKeyAlgorithm.Name}");
        logger.LogDebug($"Selected CipherClientToServer: {m_PendingExchangeContext.CipherClientToServer.Name}");
        logger.LogDebug($"Selected CipherServerToClient: {m_PendingExchangeContext.CipherServerToClient.Name}");
        //  logger.LogDebug($"Selected MACAlgorithmClientToServer: {m_PendingExchangeContext.MACAlgorithmClientToServer.Name}");
        //  logger.LogDebug($"Selected MACAlgorithmServerToClient: {m_PendingExchangeContext.MACAlgorithmServerToClient.Name}");
        //  logger.LogDebug($"Selected CompressionClientToServer: {m_PendingExchangeContext.CompressionClientToServer.Name}");
        //  logger.LogDebug($"Selected CompressionServerToClient: {m_PendingExchangeContext.CompressionServerToClient.Name}");
    }

    private void HandleSpecificPacket(KexDHInit packet)
    {
        logger.LogDebug("Received KexDHInit");

        if ((m_PendingExchangeContext == null) || (m_PendingExchangeContext.KexAlgorithm == null))
        {
            throw new SSHServerException(DisconnectReason.SSH_DISCONNECT_PROTOCOL_ERROR, "Server did not receive SSH_MSG_KEX_INIT as expected.");
        }

        // 1. C generates a random number x (1 < x < q) and computes e = g ^ x mod p.  C sends e to S.
        // 2. S receives e.  It computes K = e^y mod p
        byte[] sharedSecret = m_PendingExchangeContext.KexAlgorithm.DecryptKeyExchange(packet.ClientValue);

        // 2. S generates a random number y (0 < y < q) and computes f = g ^ y mod p.
        byte[] serverKeyExchange = m_PendingExchangeContext.KexAlgorithm.CreateKeyExchange();

        byte[] hostKey = m_PendingExchangeContext.HostKeyAlgorithm.CreateKeyAndCertificatesData();

        // H = hash(V_C || V_S || I_C || I_S || K_S || e || f || K)
        byte[] exchangeHash = ComputeExchangeHash(
            m_PendingExchangeContext.KexAlgorithm,
            hostKey,
            packet.ClientValue,
            serverKeyExchange,
            sharedSecret);

        m_PendingExchangeContext.ExchangeHash = exchangeHash;


        client.TrySetSessionId(exchangeHash);

        // https://tools.ietf.org/html/rfc4253#section-7.2

        // Initial IV client to server: HASH(K || H || "A" || session_id)
        // (Here K is encoded as mpint and "A" as byte and session_id as raw
        // data.  "A" means the single character A, ASCII 65).
        byte[] clientCipherIV = ComputeEncryptionKey(
            m_PendingExchangeContext.KexAlgorithm,
            exchangeHash,
            m_PendingExchangeContext.CipherClientToServer.BlockSize,
            sharedSecret, 'A');

        // Initial IV server to client: HASH(K || H || "B" || session_id)
        byte[] serverCipherIV = ComputeEncryptionKey(
            m_PendingExchangeContext.KexAlgorithm,
            exchangeHash,
            m_PendingExchangeContext.CipherServerToClient.BlockSize,
            sharedSecret, 'B');

        // Encryption key client to server: HASH(K || H || "C" || session_id)
        byte[] clientCipherKey = ComputeEncryptionKey(
            m_PendingExchangeContext.KexAlgorithm,
            exchangeHash,
            m_PendingExchangeContext.CipherClientToServer.KeySize,
            sharedSecret, 'C');

        // Encryption key server to client: HASH(K || H || "D" || session_id)
        byte[] serverCipherKey = ComputeEncryptionKey(
            m_PendingExchangeContext.KexAlgorithm,
            exchangeHash,
            m_PendingExchangeContext.CipherServerToClient.KeySize,
            sharedSecret, 'D');

        // Integrity key client to server: HASH(K || H || "E" || session_id)
        byte[] clientHmacKey = ComputeEncryptionKey(
            m_PendingExchangeContext.KexAlgorithm,
            exchangeHash,
            m_PendingExchangeContext.MACAlgorithmClientToServer.KeySize,
            sharedSecret, 'E');

        // Integrity key server to client: HASH(K || H || "F" || session_id)
        byte[] serverHmacKey = ComputeEncryptionKey(
            m_PendingExchangeContext.KexAlgorithm,
            exchangeHash,
            m_PendingExchangeContext.MACAlgorithmServerToClient.KeySize,
            sharedSecret, 'F');

        // Set all keys we just generated
        m_PendingExchangeContext.CipherClientToServer.SetKey(clientCipherKey, clientCipherIV);
        m_PendingExchangeContext.CipherServerToClient.SetKey(serverCipherKey, serverCipherIV);
        m_PendingExchangeContext.MACAlgorithmClientToServer.SetKey(clientHmacKey);
        m_PendingExchangeContext.MACAlgorithmServerToClient.SetKey(serverHmacKey);

        // Send reply to client!
        KexDHReply reply = new KexDHReply()
        {
            ServerHostKey = hostKey,
            ServerValue = serverKeyExchange,
            Signature = m_PendingExchangeContext.HostKeyAlgorithm.CreateSignatureData(exchangeHash)
        };

        client.Send(reply);
        client.Send(new NewKeys());
        client.SetNewExchangeContextOutgoing(m_PendingExchangeContext);

        var extensionList = new ByteWriter();
        extensionList.WriteStringList(Server.SupportedHostKeyAlgorithms.Keys);
        var extensionData = extensionList.ToByteArray();
        client.Send(new ExtendedInfo()
        {
            Extensions = (((string, string)[])[("server-sig-algs", string.Join(",",Server.SupportedHostKeyAlgorithms.Keys))]).ToDictionary(x => x.Item1, X => X.Item2)
        });

    }

    private void HandleSpecificPacket(NewKeys packet)
    {
        logger.LogDebug("Received NewKeys");
        if (m_PendingExchangeContext is null)
        {
            client.Disconnect(DisconnectReason.SSH_DISCONNECT_PROTOCOL_ERROR, $"Got {packet.Type} but was not in an Exchange.");
            return;
        }
        client.SetNewExchangeContextIncomming(m_PendingExchangeContext);
        m_PendingExchangeContext = null;

        // Reset re-exchange values
    }



    private byte[] ComputeExchangeHash(IKexAlgorithm kexAlgorithm, byte[] hostKeyAndCerts, byte[] clientExchangeValue, byte[] serverExchangeValue, byte[] sharedSecret)
    {
        if (m_KexInitClientToServer is null)
        {
            throw new SSHServerException(DisconnectReason.SSH_DISCONNECT_PROTOCOL_ERROR, $"{PacketType.SSH_MSG_KEXINIT} was not recived");
        }
        // H = hash(V_C || V_S || I_C || I_S || K_S || e || f || K)
        using ByteWriter writer = new ByteWriter();
        writer.WriteString(client.ProtocolVersionExchange);
        writer.WriteString(Server.ProtocolVersionExchange);

        writer.WriteBytes(m_KexInitClientToServer.GetBytes());
        writer.WriteBytes(client.KexInitServerToClient.GetBytes());
        writer.WriteBytes(hostKeyAndCerts);

        writer.WriteMPInt(clientExchangeValue);
        writer.WriteMPInt(serverExchangeValue);
        writer.WriteMPInt(sharedSecret);

        return kexAlgorithm.ComputeHash(writer.ToByteArray());
    }

    private byte[] ComputeEncryptionKey(IKexAlgorithm kexAlgorithm, byte[] exchangeHash, uint keySize, byte[] sharedSecret, char letter)
    {
        // K(X) = HASH(K || H || X || session_id)

        // Prepare the buffer
        byte[] keyBuffer = new byte[keySize];
        int keyBufferIndex = 0;
        int currentHashLength = 0;
        byte[] currentHash = null;

        // We can stop once we fill the key buffer
        while (keyBufferIndex < keySize)
        {
            using (ByteWriter writer = new ByteWriter())
            {
                // Write "K"
                writer.WriteMPInt(sharedSecret);

                // Write "H"
                writer.WriteRawBytes(exchangeHash);

                if (currentHash == null)
                {
                    // If we haven't done this yet, add the "X" and session_id
                    writer.WriteByte((byte)letter);
                    writer.WriteRawBytes(client.SessionId);
                }
                else
                {
                    // If the key isn't long enough after the first pass, we need to
                    // write the current hash as described here:
                    //      K1 = HASH(K || H || X || session_id)   (X is e.g., "A")
                    //      K2 = HASH(K || H || K1)
                    //      K3 = HASH(K || H || K1 || K2)
                    //      ...
                    //      key = K1 || K2 || K3 || ...
                    writer.WriteRawBytes(currentHash);
                }

                currentHash = kexAlgorithm.ComputeHash(writer.ToByteArray());
            }

            currentHashLength = Math.Min(currentHash.Length, (int)(keySize - keyBufferIndex));
            Array.Copy(currentHash, 0, keyBuffer, keyBufferIndex, currentHashLength);

            keyBufferIndex += currentHashLength;
        }

        return keyBuffer;
    }


















    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }


    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

}

