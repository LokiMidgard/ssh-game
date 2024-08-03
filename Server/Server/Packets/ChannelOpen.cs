using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    public class ChannelOpen : IPacket<ChannelOpen>
    {
        //   byte      SSH_MSG_CHANNEL_OPEN
        //   string    channel type in US-ASCII only
        //   uint32    sender channel
        //   uint32    initial window size
        //   uint32    maximum packet size
        public uint PacketSequence { get; set; }
        public PacketType Type { get => PacketType; }

        public required string ChannelType { get; set; }

        public UInt32 SenderChannel { get; set; }
        public UInt32 InitialWindowSize { get; set; }
        public UInt32 MaxPacketSize { get; set; }

        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_CHANNEL_OPEN;
            }
        }




        public static ChannelOpen Load(ByteReader reader)
        {
            return new()
            {
                ChannelType = reader.GetString(),
                SenderChannel = reader.GetUInt32(),
                InitialWindowSize = reader.GetUInt32(),
                MaxPacketSize = reader.GetUInt32(),
            };
        }

        public void InternalGetBytes(ByteWriter writer)
        {
            writer.WriteString(ChannelType);
            writer.WriteUInt32(SenderChannel);
            writer.WriteUInt32(InitialWindowSize);
            writer.WriteUInt32(MaxPacketSize);
        }
    }
}
