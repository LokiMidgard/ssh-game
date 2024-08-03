using System.Numerics;


namespace Renci.SshNet.Security
{
    internal abstract class KeyExchangeDiffieHellmanGroupShaBase : KeyExchangeDiffieHellman
    {
        /// <summary>
        /// Gets the group prime.
        /// </summary>
        /// <value>
        /// The group prime.
        /// </value>

        protected sealed override BigInteger _group { get; } = new BigInteger(new byte[] { 2 });


    }
}
