using System.Security.Cryptography;

namespace Renci.SshNet.Security
{
    /// <summary>
    /// Base class for "diffie-hellman" SHA-256 group algorithm implementations.
    /// </summary>
    internal abstract class KeyExchangeDiffieHellmanGroupSha256 : KeyExchangeDiffieHellmanGroupShaBase
    {
        /// <summary>
        /// Gets the size, in bits, of the computed hash code.
        /// </summary>
        /// <value>
        /// The size, in bits, of the computed hash code.
        /// </value>
        protected sealed override int HashSize
        {
            get { return 256; }
        }
        protected sealed override HashAlgorithm HashAlgorithm => SHA256.Create();
    }
}
