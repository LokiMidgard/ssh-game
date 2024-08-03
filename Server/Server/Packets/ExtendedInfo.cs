using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    public class ExtendedInfo : IPacket<ExtendedInfo>
    {
        public uint PacketSequence { get; set; }
        public PacketType Type { get => PacketType; }

        public Dictionary<string, string> Extensions { get; set; } = [];


        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_EXT_INFO;
            }
        }




        public static ExtendedInfo Load(ByteReader reader)
        {
            var numberOfExtensions = reader.GetUInt32();
            Dictionary<string, string> extensions = [];
            for (int i = 0; i < numberOfExtensions; i++)
            {
                var key = reader.GetString();
                var value = reader.GetString();
                extensions[key] = value;
            }
            return new()
            {
                Extensions = extensions,
            };
        }

        public void InternalGetBytes(ByteWriter writer)
        {
            writer.WriteUInt32((uint)Extensions.Count);
            foreach (var (key, value) in Extensions)
            {
                writer.WriteString(key);
                writer.WriteString(value);
            }
        }
    }
}
