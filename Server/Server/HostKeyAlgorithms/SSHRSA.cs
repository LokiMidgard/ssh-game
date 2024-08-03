using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Configuration;

namespace SshGame.Server.HostKeyAlgorithms
{
    public class SSHRSA : IHostKeyAlgorithm<SSHRSA>
    {
        private readonly RSA m_RSA = RSA.Create();

        public static string Name
        {
            get
            {
                return "ssh-rsa";
            }
        }

        public void ImportKey(IDictionary<string, string> keyXml) => m_RSA.ImportFromPem(keyXml["rsa"]);


        public byte[] CreateKeyAndCertificatesData()
        {
            // The "ssh-rsa" key format has the following specific encoding:
            //      string    "ssh-rsa"
            //      mpint e
            //      mpint n
            RSAParameters parameters = m_RSA.ExportParameters(false);

            using (ByteWriter writer = new ByteWriter())
            {
                writer.WriteString(Name);
                writer.WriteMPInt(parameters.Exponent);
                writer.WriteMPInt(parameters.Modulus);
                return writer.ToByteArray();
            }
        }

        public byte[] CreateSignatureData(byte[] value)
        {
            // Signing and verifying using this key format is performed according to
            // the RSASSA-PKCS1-v1_5 scheme in [RFC3447] using the SHA-1 hash.
            // The resulting signature is encoded as follows:
            //      string    "ssh-rsa"
            //      string    rsa_signature_blob
            using (ByteWriter writer = new ByteWriter())
            {
                writer.WriteString(Name);
                writer.WriteBytes(m_RSA.SignData(value, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1));
                return writer.ToByteArray();
            }
        }
    }

    public class SSHRSA256 : IHostKeyAlgorithm<SSHRSA>
    {
        private readonly RSA m_RSA = RSA.Create();

        public static string Name
        {
            get
            {
                return "rsa-sha2-256";
            }
        }

        public void ImportKey(IDictionary<string, string> keyXml) => m_RSA.ImportFromPem(keyXml["rsa"]);


        public byte[] CreateKeyAndCertificatesData()
        {
            // The "ssh-rsa" key format has the following specific encoding:
            //      string    "ssh-rsa"
            //      mpint e
            //      mpint n
            RSAParameters parameters = m_RSA.ExportParameters(false);

            using (ByteWriter writer = new ByteWriter())
            {
                writer.WriteString(Name);
                writer.WriteMPInt(parameters.Exponent);
                writer.WriteMPInt(parameters.Modulus);
                return writer.ToByteArray();
            }
        }

        public byte[] CreateSignatureData(byte[] value)
        {
            // Signing and verifying using this key format is performed according to
            // the RSASSA-PKCS1-v1_5 scheme in [RFC3447] using the SHA-1 hash.
            // The resulting signature is encoded as follows:
            //      string    "ssh-rsa"
            //      string    rsa_signature_blob
            using (ByteWriter writer = new ByteWriter())
            {
                writer.WriteString(Name);
                writer.WriteBytes(m_RSA.SignData(value, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
                return writer.ToByteArray();
            }
        }
    }

    public class SSHRSA512 : IHostKeyAlgorithm<SSHRSA>
    {
        private readonly RSA m_RSA = RSA.Create();

        public static string Name
        {
            get
            {
                return "rsa-sha2-512";
            }
        }

        public void ImportKey(IDictionary<string, string> keyXml) => m_RSA.ImportFromPem(keyXml["rsa"]);

        public byte[] CreateKeyAndCertificatesData()
        {
            // The "ssh-rsa" key format has the following specific encoding:
            //      string    "ssh-rsa"
            //      mpint e
            //      mpint n
            RSAParameters parameters = m_RSA.ExportParameters(false);

            using (ByteWriter writer = new ByteWriter())
            {
                writer.WriteString(Name);
                writer.WriteMPInt(parameters.Exponent);
                writer.WriteMPInt(parameters.Modulus);
                return writer.ToByteArray();
            }
        }

        public byte[] CreateSignatureData(byte[] value)
        {
            // Signing and verifying using this key format is performed according to
            // the RSASSA-PKCS1-v1_5 scheme in [RFC3447] using the SHA-1 hash.
            // The resulting signature is encoded as follows:
            //      string    "ssh-rsa"
            //      string    rsa_signature_blob
            using (ByteWriter writer = new ByteWriter())
            {
                writer.WriteString(Name);
                writer.WriteBytes(m_RSA.SignData(value, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1));
                return writer.ToByteArray();
            }
        }
    }
}
