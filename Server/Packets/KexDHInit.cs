using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    internal class KexDHInit : IPacket<KexDHInit>
    {
        public uint PacketSequence { get; set; }
     public  PacketType Type { get=>PacketType; }

        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_KEXDH_INIT;
            }
        }

        public byte[] ClientValue { get; set; }

        public void InternalGetBytes(ByteWriter writer)
        {
            // Server never sends this
            throw new SSHServerException(DisconnectReason.SSH_DISCONNECT_KEY_EXCHANGE_FAILED, "SSH Server should never send a SSH_MSG_KEXDH_INIT message");
        }

        public static KexDHInit Load(ByteReader reader)
        {
            return new KexDHInit()
            {

                // First, the client sends the following:
                //  byte SSH_MSG_KEXDH_INIT (handled by base class)
                //  mpint e
                ClientValue = reader.GetMPInt()
            };
        }
    }
}
