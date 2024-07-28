﻿using SshGame.Server.Ciphers;
using SshGame.Server.Compressions;
using SshGame.Server.HostKeyAlgorithms;
using SshGame.Server.KexAlgorithms;
using SshGame.Server.MACAlgorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    internal class KexInit : IPacket<KexInit>
    {
        public PacketType Type { get => PacketType; }

        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_KEXINIT;
            }
        }

        public uint PacketSequence { get; set; }



        public byte[] Cookie { get; set; } = new byte[16];
        public List<string> KexAlgorithms { get; private set; } = new List<string>();
        public List<string> ServerHostKeyAlgorithms { get; private set; } = new List<string>();
        public List<string> EncryptionAlgorithmsClientToServer { get; private set; } = new List<string>();
        public List<string> EncryptionAlgorithmsServerToClient { get; private set; } = new List<string>();
        public List<string> MacAlgorithmsClientToServer { get; private set; } = new List<string>();
        public List<string> MacAlgorithmsServerToClient { get; private set; } = new List<string>();
        public List<string> CompressionAlgorithmsClientToServer { get; private set; } = new List<string>();
        public List<string> CompressionAlgorithmsServerToClient { get; private set; } = new List<string>();
        public List<string> LanguagesClientToServer { get; private set; } = new List<string>();
        public List<string> LanguagesServerToClient { get; private set; } = new List<string>();
        public bool FirstKexPacketFollows { get; set; }

        public KexInit()
        {
            RandomNumberGenerator.Create().GetBytes(Cookie);
        }

        public void InternalGetBytes(ByteWriter writer)
        {
            writer.WriteRawBytes(Cookie);
            writer.WriteStringList(KexAlgorithms);
            writer.WriteStringList(ServerHostKeyAlgorithms);
            writer.WriteStringList(EncryptionAlgorithmsClientToServer);
            writer.WriteStringList(EncryptionAlgorithmsServerToClient);
            writer.WriteStringList(MacAlgorithmsClientToServer);
            writer.WriteStringList(MacAlgorithmsServerToClient);
            writer.WriteStringList(CompressionAlgorithmsClientToServer);
            writer.WriteStringList(CompressionAlgorithmsServerToClient);
            writer.WriteStringList(LanguagesClientToServer);
            writer.WriteStringList(LanguagesServerToClient);
            writer.WriteByte(FirstKexPacketFollows ? (byte)0x01 : (byte)0x00);
            writer.WriteUInt32(0);
        }

        public static KexInit Load(ByteReader reader)
        {
            var result = new KexInit
            {
                Cookie = reader.GetBytes(16),
                KexAlgorithms = reader.GetNameList(),
                ServerHostKeyAlgorithms = reader.GetNameList(),
                EncryptionAlgorithmsClientToServer = reader.GetNameList(),
                EncryptionAlgorithmsServerToClient = reader.GetNameList(),
                MacAlgorithmsClientToServer = reader.GetNameList(),
                MacAlgorithmsServerToClient = reader.GetNameList(),
                CompressionAlgorithmsClientToServer = reader.GetNameList(),
                CompressionAlgorithmsServerToClient = reader.GetNameList(),
                LanguagesClientToServer = reader.GetNameList(),
                LanguagesServerToClient = reader.GetNameList(),
                FirstKexPacketFollows = reader.GetBoolean(),
            };
            /*
              uint32       0 (reserved for future extension)
            */
            uint reserved = reader.GetUInt32();
            return result;
        }

        private T MatchAlgo<T>(string type, IEnumerable<string> remoteAlos, IReadOnlyDictionary<string, Func<T>> supported)
        {
            foreach (string algo in remoteAlos)
            {
                if (supported.TryGetValue(algo, out var selectedAlgoActivator))
                {
                    T selectedAlgo = selectedAlgoActivator();
                    return selectedAlgo;
                }
            }
            throw new SSHServerException(DisconnectReason.SSH_DISCONNECT_KEY_EXCHANGE_FAILED, $"Could not find a shared Algorithm for {type}, offered: {string.Join(", ", supported.Keys)}\nprovided {string.Join(", ", remoteAlos)}");
        }

        public IKexAlgorithm PickKexAlgorithm() => MatchAlgo("Kex", this.KexAlgorithms, Server.SupportedKexAlgorithms);
        public IHostKeyAlgorithm PickHostKeyAlgorithm() => MatchAlgo("Host", this.ServerHostKeyAlgorithms, Server.SupportedHostKeyAlgorithms);
        public ICipher PickCipherClientToServer() => MatchAlgo("cipher", this.EncryptionAlgorithmsClientToServer, Server.SupportedCiphers);
        public ICipher PickCipherServerToClient() => MatchAlgo("cipher", this.EncryptionAlgorithmsServerToClient, Server.SupportedCiphers);
        public IMACAlgorithm PickMACAlgorithmClientToServer() => MatchAlgo("MAC", this.MacAlgorithmsClientToServer, Server.SupportedMACAlgorithms);
        public IMACAlgorithm PickMACAlgorithmServerToClient() => MatchAlgo("MAC", this.MacAlgorithmsServerToClient, Server.SupportedMACAlgorithms);
        public ICompression PickCompressionAlgorithmClientToServer() => MatchAlgo("Compression", this.CompressionAlgorithmsClientToServer, Server.SupportedCompressions);
        public ICompression PickCompressionAlgorithmServerToClient() => MatchAlgo("Compression", this.CompressionAlgorithmsServerToClient, Server.SupportedCompressions);

    }
}
