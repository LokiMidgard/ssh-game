using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    public class ServiceAccept : IPacket<ServiceAccept>
    {
        public uint PacketSequence { get; set; }
        public PacketType Type { get => PacketType; }

        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_SERVICE_ACCEPT;
            }
        }


        public required string ServiceName { get; set; }


        public static ServiceAccept Load(ByteReader reader)
        {
            return new ServiceAccept()
            {
                ServiceName = reader.GetString(),
            };
        }

        public void InternalGetBytes(ByteWriter writer)
        {
            writer.WriteString(ServiceName);
        }
    }
}
