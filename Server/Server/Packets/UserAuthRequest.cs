using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    public abstract class UserAuthRequest : IPacket<UserAuthRequest>
    {
        public uint PacketSequence { get; set; }
        public PacketType Type { get => PacketType; }

        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_USERAUTH_REQUEST;
            }
        }


        public required string MethodName { get; set; }
        public required string UserName { get; set; }
        public required string ServiceName { get; set; }


        public static UserAuthRequest Load(ByteReader reader)
        {

            var UserName = reader.GetString();
            var ServiceName = reader.GetString();
            var MethodName = reader.GetString();
            if (MethodName == "publickey")
            {
                var hasSignatur = reader.GetBoolean();
                var algoName = reader.GetString(Encoding.UTF8);
                var publicKey = reader.GetLengthPrefixedBytes();
                var signature = hasSignatur ? reader.GetLengthPrefixedBytes() : null;

                return new PublicKey()
                {
                    UserName = UserName,
                    ServiceName = ServiceName,
                    MethodName = MethodName,
                    AlgoName = algoName,
                    Key = publicKey,
                    Signature = signature,
                };

            }
            else if (MethodName == "password")
            {
                var setsNewPassowrd = reader.GetBoolean();
                var currentPassword = reader.GetString();
                var newPassword = setsNewPassowrd ? reader.GetString() : null;
                return new Password()
                {
                    UserName = UserName,
                    ServiceName = ServiceName,
                    MethodName = MethodName,
                    CurrentPassword = currentPassword,
                    NewPassword = newPassword,
                };

            }
            else if (MethodName == "hostbased")
            {
                return new Host()
                {
                    UserName = UserName,
                    ServiceName = ServiceName,
                    MethodName = MethodName,
                    PublicKeyAlgoForHost = reader.GetString(),
                    PublicKeyForHost = reader.GetMPInt(),
                    ClientHost = reader.GetString(),
                    UserNameOnClient = reader.GetString(),
                    Signature = reader.GetMPInt(),
                };


            }
            else if (MethodName == "none")
            {
                return new None()
                {
                    UserName = UserName,
                    ServiceName = ServiceName,
                    MethodName = MethodName,
                };

            }
            else
            {
                return new Unsuported()
                {
                    UserName = UserName,
                    ServiceName = ServiceName,
                    MethodName = MethodName,
                };
            }
        }

        public void InternalGetBytes(ByteWriter writer)
        {
            writer.WriteString(ServiceName);
        }

        public class None : UserAuthRequest
        {
        }
        public class Unsuported : UserAuthRequest
        {

        }
        public class PublicKey : UserAuthRequest
        {
            public required string AlgoName { get; set; }
            public required byte[] Key { get; set; }
            public byte[]? Signature { get; set; }
        }
        public class Host : UserAuthRequest
        {
            public required string PublicKeyAlgoForHost { get; set; }
            public required byte[] PublicKeyForHost { get; set; }
            public required string ClientHost { get; set; }
            public required string UserNameOnClient { get; set; }
            public required byte[] Signature { get; set; }
        }
        public class Password : UserAuthRequest
        {
            public required string CurrentPassword { get; set; }
            public string? NewPassword { get; set; }

        }
    }
}
