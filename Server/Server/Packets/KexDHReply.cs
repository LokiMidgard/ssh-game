using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    public class KexDHReply : IPacket<KexDHReply>
    {

        public uint PacketSequence { get; set; }
     public  PacketType Type { get=>PacketType; }

        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_KEXDH_REPLY;
            }
        }

        public byte[] ServerHostKey { get; set; }
        public byte[] ServerValue { get; set; }
        public byte[] Signature { get; set; }

        public void InternalGetBytes(ByteWriter writer)
        {
            // string server public host key and certificates(K_S)
            // mpint f
            // string signature of H
            writer.WriteBytes(ServerHostKey);
            writer.WriteMPInt(ServerValue);
            writer.WriteBytes(Signature);
        }

        public static KexDHReply Load(ByteReader reader)
        {
            // Client never sends this!
            throw new SSHServerException(DisconnectReason.SSH_DISCONNECT_KEY_EXCHANGE_FAILED, "SSH Client should never send a SSH_MSG_KEXDH_REPLY message");
        }
    }
}
