using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    public class NewKeys : IPacket<NewKeys>
    {
        public uint PacketSequence { get; set; }
        public PacketType Type { get => PacketType; }

        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_NEWKEYS;
            }
        }

        public void InternalGetBytes(ByteWriter writer)
        {
            // No data, nothing to write
        }

        public static NewKeys Load(ByteReader reader)
        {
            // No data, nothing to load
            return new NewKeys();
        }
    }
}
