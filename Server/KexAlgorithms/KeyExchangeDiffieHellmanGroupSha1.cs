using System.Security.Cryptography;

namespace Renci.SshNet.Security
{
    /// <summary>
    /// Represents "diffie-hellman-group1-sha1" algorithm implementation.
    /// </summary>
    internal abstract class KeyExchangeDiffieHellmanGroupSha1 : KeyExchangeDiffieHellmanGroupShaBase
    {
        /// <summary>
        /// Gets the size, in bits, of the computed hash code.
        /// </summary>
        /// <value>
        /// The size, in bits, of the computed hash code.
        /// </value>
        protected sealed override int HashSize
        {
            get { return 160; }
        }

        protected override HashAlgorithm HashAlgorithm => SHA1.Create();


    }
}
