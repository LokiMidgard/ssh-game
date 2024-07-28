using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    public class ChannelFailur : IPacket<ChannelFailur>
    {
        public uint PacketSequence { get; set; }
        public PacketType Type { get => PacketType; }

        public UInt32 RecipientChannel { get; set; }

        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_CHANNEL_FAILURE;
            }
        }




        public static ChannelFailur Load(ByteReader reader)
        {
            return new()
            {
                RecipientChannel = reader.GetUInt32(),
            };
        }

        public void InternalGetBytes(ByteWriter writer)
        {
            writer.WriteUInt32(RecipientChannel);
        }
    }
}
