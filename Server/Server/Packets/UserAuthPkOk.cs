using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    public class UserAuthPkOk : IPacket<UserAuthPkOk>
    {
        public uint PacketSequence { get; set; }
        public PacketType Type { get => PacketType; }

        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_USERAUTH_PK_OK;
            }
        }

        public required string AlgoName { get; set; }
        public required byte[] Key { get; set; }





        public static UserAuthPkOk Load(ByteReader reader)
        {
            return new()
            {
                AlgoName = reader.GetString(),
                Key = reader.GetMPInt()
            };
        }

        public void InternalGetBytes(ByteWriter writer)
        {
            writer.WriteString(AlgoName);
            writer.WriteBytes(Key);
        }
    }
}
