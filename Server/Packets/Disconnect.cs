using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    public class Disconnect : IPacket<Disconnect>
    {
        public uint PacketSequence { get; set; }
        public  PacketType Type { get=>PacketType; }

        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_DISCONNECT;
            }
        }

        public DisconnectReason Reason { get; set; }
        public required string Description { get; set; }
        public string Language { get; set; } = "en";

        public static Disconnect Load(ByteReader reader)
        {
            return new Disconnect()
            {

                Reason = (DisconnectReason)reader.GetUInt32(),
                Description = reader.GetString(Encoding.UTF8),
                Language = !reader.IsEOF ? reader.GetString() : "en"
            };
        }

        public void InternalGetBytes(ByteWriter writer)
        {
            writer.WriteUInt32((uint)Reason);
            writer.WriteString(Description, Encoding.UTF8);
            writer.WriteString(Language);
        }
    }
}
