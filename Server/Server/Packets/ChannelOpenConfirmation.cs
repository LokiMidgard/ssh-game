using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    public class ChannelOpenConfirmation : IPacket<ChannelOpenConfirmation>
    {


        //   uint32    recipient channel
        //   uint32    sender channel
        //   uint32    initial window size
        //   uint32    maximum packet size



        public uint PacketSequence { get; set; }
        public PacketType Type { get => PacketType; }

        public UInt32 RecipientChannel { get; set; }
        public UInt32 SenderChannel { get; set; }
        public UInt32 InitialWindowSize { get; set; }
        public UInt32 MaxPacketSize { get; set; }

        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_CHANNEL_OPEN_CONFIRMATION;
            }
        }




        public static ChannelOpenConfirmation Load(ByteReader reader)
        {
            return new()
            {
                RecipientChannel = reader.GetUInt32(),
                SenderChannel = reader.GetUInt32(),
                InitialWindowSize = reader.GetUInt32(),
                MaxPacketSize = reader.GetUInt32(),
            };
        }

        public void InternalGetBytes(ByteWriter writer)
        {
            writer.WriteUInt32(RecipientChannel);
            writer.WriteUInt32(SenderChannel);
            writer.WriteUInt32(InitialWindowSize);
            writer.WriteUInt32(MaxPacketSize);
        }
    }
}
