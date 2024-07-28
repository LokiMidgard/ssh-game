using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    internal class Unimplemented : IPacket<Unimplemented>
    {
        public uint PacketSequence { get; set; }
     public  PacketType Type { get=>PacketType; }

        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_UNIMPLEMENTED;
            }
        }

        public uint RejectedPacketNumber { get; set; }

        public static Unimplemented Load(ByteReader reader)
        {
            return new Unimplemented
            {

                RejectedPacketNumber = reader.GetUInt32()
            };
        }

        public void InternalGetBytes(ByteWriter writer)
        {
            // uint32 packet sequence number of rejected message
            writer.WriteUInt32(RejectedPacketNumber);
        }
    }
}
