using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    internal partial interface IPacket
    {
        public const int MaxPacketSize = 35_000;
        // public const int MaxPacketSize = 3_503_445_504;

        public const int PacketHeaderSize = 5;

        public uint PacketSequence { get; set; }
        public PacketType Type { get; }


        private static readonly Dictionary<PacketType, Func<ByteReader, IPacket>> PacketTypes = [];

        static IPacket()
        {
            PreparePackages();
        }
        [AutoInvoke.FindAndInvoke(CallForAbstractClasses = true)]
        public static void PreparePackages<T>() where T : IPacket<T>
        {
            PacketTypes.Add(T.PacketType, (b) => T.Load(b));
        }
        static IPacket Load(PacketType type, ByteReader reader)
        {
            return PacketTypes[type](reader);
        }
        static bool IsSupported(PacketType type)
        {
            return PacketTypes.ContainsKey(type);
        }

        void InternalGetBytes(ByteWriter writer);
    }
    internal interface IPacket<T> : IPacket
    where T : IPacket<T>
    {
        // https://tools.ietf.org/html/rfc4253#section-6.1


        public static abstract PacketType PacketType { get; }





        static abstract T Load(ByteReader reader);


    }

    internal static class PacketExtensions
    {
        public static byte[] GetBytes(this IPacket packet)

        {
            using (ByteWriter writer = new())
            {
                writer.WritePacketType(packet.Type);
                packet.InternalGetBytes(writer);
                return writer.ToByteArray();
            }
        }
    }
}
