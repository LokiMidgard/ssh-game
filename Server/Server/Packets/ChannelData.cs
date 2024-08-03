using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    public class ChannelData : IPacket<ChannelData>
    {
        public uint PacketSequence { get; set; }
        public PacketType Type { get => PacketType; }

        public UInt32 RecipientChannel { get; set; }
        public required byte[] Data { get; set; }


        public int Offset { get; set; }
        public int Length { get; set; } = -1;

        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_CHANNEL_DATA;
            }
        }




        public static ChannelData Load(ByteReader reader)
        {
            var result = new ChannelData()
            {
                RecipientChannel = reader.GetUInt32(),
                Data = reader.GetLengthPrefixedBytes(),
            };
            result.Length = result.Data.Length;
            return result;
        }

        public void InternalGetBytes(ByteWriter writer)
        {
            writer.WriteUInt32(RecipientChannel);
            if (Length >= 0)
            {
                writer.WriteBytes(Data[Offset..Length]);
            }
            else
            {
                writer.WriteBytes(Data);
            }

        }
    }
}
