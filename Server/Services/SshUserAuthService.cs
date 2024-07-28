using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Unicode;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Bcpg;
using SshGame.Server;
using SshGame.Server.KexAlgorithms;
using SshGame.Server.Packets;

namespace SshGame.Server.Services;


class SshUserAuthService : Service
{

    public class Factiory : IServiceFactory, IDiscovrable
    {
        public static string Name => "ssh-userauth";

        Service IServiceFactory.Create(Client client, ILogger logger)
        {
            return new SshUserAuthService(client, logger);
        }
    }
    public SshUserAuthService(Client client, ILogger logger) : base(client, logger)
    {
        m_PendingExchangeContext = new ExchangeContext();
    }
    protected override bool HandlePacketInternal(IPacket packet)
    {
        if (packet is UserAuthRequest.None userAuthRequestnone)
        {
            // this.client.User = userAuthRequestnone.UserName;
            // client.Send(new UserAuthSuccess());

            logger.LogDebug($"user wants to use no authentication");
            client.Send(new UserAuthFailure()
            {
                AvailableAuthentications = ["publickey"],
                PartialSuccess = false
            });

        }
        else if (packet is UserAuthRequest.PublicKey publicKey)
        {
            logger.LogDebug($"User uses {publicKey.AlgoName} {(publicKey.Key is null ? "without" : "with")} Signature");


            if (publicKey.AlgoName is "ssh-rsa" or "rsa-sha2-512" or "rsa-sha2-256")
            {

                if (publicKey.Signature != null)
                {



                    byte[] SpecialSSHBigIntegerFormat(byte[] data)
                    {

                        Span<byte> reversed = [.. data];
                        reversed.Reverse();

                        return new BigInteger(reversed, isUnsigned: true).ToByteArray(isUnsigned: true, isBigEndian: true);

                    }





                    ByteReader publickKeySSHBlob = new(publicKey.Key);
                    var keyAlgo = publickKeySSHBlob.GetString();// this is always ssh-rsa

                    var exponent = SpecialSSHBigIntegerFormat(publickKeySSHBlob.GetLengthPrefixedBytes());
                    var modulus = SpecialSSHBigIntegerFormat(publickKeySSHBlob.GetLengthPrefixedBytes());



                    RSAParameters rsaParameters = new RSAParameters
                    {
                        Modulus = modulus,
                        Exponent = exponent
                    };

                    using var rsa = System.Security.Cryptography.RSA.Create();
                    rsa.ImportParameters(rsaParameters);



                    ByteWriter written = new ByteWriter();

                    written.WriteBytes(client.SessionId.ToArray());
                    written.WriteByte((byte)50);
                    written.WriteString(publicKey.UserName);
                    written.WriteString(publicKey.ServiceName);
                    written.WriteString(publicKey.MethodName);
                    written.WriteBoolean(true);
                    written.WriteString(publicKey.AlgoName);
                    written.WriteBytes(publicKey.Key);

                    var signedData = written.ToByteArray();


                    ByteReader signatureReader = new ByteReader(publicKey.Signature);
                    var signatureAlgo = signatureReader.GetString();
                    var sig = signatureReader.GetLengthPrefixedBytes();


                    var verification = rsa.VerifyData(signedData, sig, signatureAlgo switch
                    {
                        "rsa-sha2-512" => HashAlgorithmName.SHA512,
                        "rsa-sha2-256" => HashAlgorithmName.SHA256,
                        _ => HashAlgorithmName.SHA1
                    }, RSASignaturePadding.Pkcs1);
                    if (verification)
                    {
                        this.client.User = publicKey.UserName;
                        this.client.UserIdentity = publicKey.Key;
                        client.Send(new UserAuthSuccess());
                    }
                    else
                    {


                        throw new SSHServerException(DisconnectReason.SSH_DISCONNECT_PROTOCOL_ERROR, $"Result was {String.Join(", ", verification)}");
                    }

                }
                else
                {
                    client.Send(new UserAuthPkOk()
                    {
                        AlgoName = publicKey.AlgoName,
                        Key = publicKey.Key,

                    });
                }

            }
            else
            {
                client.Send(new UserAuthFailure()
                {
                    AvailableAuthentications = ["publickey"],
                    PartialSuccess = false
                });
            }


        }
        else if (packet is ChannelOpen channelOpen)
        {
            logger.LogInformation($"Channel Type {channelOpen.ChannelType}");
            if (channelOpen.ChannelType == "session")
            {
                client.SwitchService(new ChannelService(this.client, this.logger, 1, channelOpen.SenderChannel));
                client.Send(new ChannelOpenConfirmation()
                {
                    RecipientChannel = channelOpen.SenderChannel
                ,
                    SenderChannel = 1,
                    InitialWindowSize = channelOpen.InitialWindowSize,
                    MaxPacketSize = IPacket.MaxPacketSize

                });
            }
            else
            {
                //TODO: reject
            }
        }
        else
        {
            return false;
        }
        return true;
    }



}