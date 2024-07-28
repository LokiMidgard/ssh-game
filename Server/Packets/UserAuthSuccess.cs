using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    public class UserAuthSuccess : IPacket<UserAuthSuccess>
    {
        public uint PacketSequence { get; set; }
        public PacketType Type { get => PacketType; }

        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_USERAUTH_SUCCESS;
            }
        }




        public static UserAuthSuccess Load(ByteReader reader)
        {
            return new();
        }

        public void InternalGetBytes(ByteWriter writer)
        {
        }
    }
}
