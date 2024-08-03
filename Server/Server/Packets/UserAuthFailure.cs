using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    public class UserAuthFailure : IPacket<UserAuthFailure>
    {
        public uint PacketSequence { get; set; }
        public PacketType Type { get => PacketType; }

        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_USERAUTH_FAILURE;
            }
        }



        public required List<string> AvailableAuthentications { get; set; }
        public required bool PartialSuccess { get; set; }


        public static UserAuthFailure Load(ByteReader reader)
        {
            var result = new UserAuthFailure()
            {
                AvailableAuthentications = reader.GetNameList(),
                PartialSuccess = reader.GetBoolean(),
            };
            return result;
        }

        public void InternalGetBytes(ByteWriter writer)
        {
            writer.WriteStringList(AvailableAuthentications);
            writer.WriteBoolean(PartialSuccess);
        }
    }
}
