using System;
using System.Numerics;
using System.Security.Cryptography;
using SshGame.Server.KexAlgorithms;

namespace Renci.SshNet.Security
{
    /// <summary>
    /// Represents base class for Diffie Hellman key exchange algorithm.
    /// </summary>
    internal abstract class KeyExchangeDiffieHellman : IKexAlgorithm
    {
#pragma warning disable SA1401 // Fields should be private
        /// <summary>
        /// Specifies key exchange group number.
        /// </summary>
        protected abstract BigInteger _group { get; }

        /// <summary>
        /// Specifies key exchange prime number.
        /// </summary>
        protected abstract BigInteger _prime { get; }



        /// <summary>
        /// Specifies random generated number.
        /// </summary>
        protected BigInteger _privateExponent;

        protected abstract HashAlgorithm HashAlgorithm { get; }


#pragma warning restore SA1401 // Fields should be private

        /// <summary>
        /// Gets the size, in bits, of the computed hash code.
        /// </summary>
        /// <value>
        /// The size, in bits, of the computed hash code.
        /// </value>
        protected abstract int HashSize { get; }

        public byte[] ComputeHash(byte[] value)
        {
            using (var hashAlgorithm = HashAlgorithm)
                return hashAlgorithm.ComputeHash(value);
        }

        public byte[] CreateKeyExchange()
        {
            //  and computes: f = g ^ y mod p.
            BigInteger keyExchange = BigInteger.ModPow(_group, _privateExponent, _prime);
            byte[] key = keyExchange.ToByteArray();
            if (BitConverter.IsLittleEndian)
                key = key.Reverse().ToArray();

            if ((key.Length > 1) && (key[0] == 0x00))
            {
                key = key.Skip(1).ToArray();
            }

            return key;
        }

        public byte[] DecryptKeyExchange(byte[] keyEx)
        {
            // https://tools.ietf.org/html/rfc4253#section-8
            // 1. C generates a random number x (1 < x < q) and computes
            //    e = g ^ x mod p.  C sends e to S.

            // S receives e.  It computes K = e^y mod p,
            if (BitConverter.IsLittleEndian)
                keyEx = keyEx.Reverse().ToArray();

            BigInteger e = new BigInteger(keyEx.Concat(new byte[] { 0 }).ToArray());
            byte[] decrypted = BigInteger.ModPow(e, _privateExponent, _prime).ToByteArray();
            if (BitConverter.IsLittleEndian)
                decrypted = decrypted.Reverse().ToArray();

            if ((decrypted.Length > 1) && (decrypted[0] == 0x00))
            {
                decrypted = decrypted.Skip(1).ToArray();
            }

            return decrypted;
        }


        internal KeyExchangeDiffieHellman()
        {

            if (_group.IsZero)
            {
                throw new ArgumentNullException("_group");
            }

            if (_prime.IsZero)
            {
                throw new ArgumentNullException("_prime");
            }

            // generate private exponent that is twice the hash size (RFC 4419) with a minimum
            // of 1024 bits (whatever is less)
            var privateExponentSize = Math.Max(HashSize * 2, 1024);

            BigInteger clientExchangeValue;

            do
            {
                var bytes = new byte[privateExponentSize]; // 80 * 8 = 640 bits
                RandomNumberGenerator.Create().GetBytes(bytes);
                // Create private component
                _privateExponent = BigInteger.Abs(new BigInteger(bytes));

                // Generate public component
                clientExchangeValue = BigInteger.ModPow(_group, _privateExponent, _prime);
            }
            while (clientExchangeValue < 1 || clientExchangeValue > (_prime - 1));





        }




    }
}
