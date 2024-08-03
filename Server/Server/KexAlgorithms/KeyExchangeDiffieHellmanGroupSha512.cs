using System.Security.Cryptography;

namespace Renci.SshNet.Security
{
    /// <summary>
    /// Base class for "diffie-hellman" SHA-512 group algorithm implementations.
    /// </summary>
    internal abstract class KeyExchangeDiffieHellmanGroupSha512 : KeyExchangeDiffieHellmanGroupShaBase
    {
        /// <summary>
        /// Gets the size, in bits, of the computed hash code.
        /// </summary>
        /// <value>
        /// The size, in bits, of the computed hash code.
        /// </value>
        protected override int HashSize
        {
            get { return 512; }
        }

        protected override HashAlgorithm HashAlgorithm => System.Security.Cryptography.SHA512.Create();



    }
}
