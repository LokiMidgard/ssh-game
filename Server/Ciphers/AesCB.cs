using SshGame.Server.Packets;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SshGame.Server.Ciphers
{
    internal abstract class AesBase
    {
        private Aes aes = Aes.Create();
        private ICryptoTransform m_Decryptor;
        private ICryptoTransform m_Encryptor;

        public uint BlockSize
        {
            get
            {
                // According to https://msdn.microsoft.com/en-us/library/system.security.cryptography.symmetricalgorithm.blocksize(v=vs.110).aspx
                // TripleDES.BlockSize is the size of the block in bits, so we need to divide by 8
                // to convert from bits to bytes.
                return (uint)(aes.BlockSize / 8);
            }
        }

        public uint KeySize
        {
            get
            {
                // https://msdn.microsoft.com/en-us/library/system.security.cryptography.symmetricalgorithm.keysize(v=vs.110).aspx
                // TripleDES.KeySize is the size of the key in bits, so we need to divide by 8
                // to convert from bits to bytes.
                return (uint)(aes.KeySize / 8);
            }
        }


        public AesBase(int keySize, PaddingMode padding, CipherMode cipherMode)
        {
            aes.KeySize = keySize;
            aes.Padding = padding;
            aes.Mode = cipherMode;
        }

        public byte[] Decrypt(byte[] data)
        {
            return PerformTransform(m_Decryptor, data);
        }

        public byte[] Encrypt(byte[] data)
        {
            return PerformTransform(m_Encryptor, data);
        }

        public void SetKey(byte[] key, byte[] iv)
        {
            aes.Key = key;
            aes.IV = iv;

            m_Decryptor = aes.CreateDecryptor(key, iv);
            m_Encryptor = aes.CreateEncryptor(key, iv);
        }

        private byte[] PerformTransform(ICryptoTransform transform, byte[] data)
        {
            if (transform == null)
                throw new SSHServerException(DisconnectReason.SSH_DISCONNECT_KEY_EXCHANGE_FAILED, "SetKey must be called before attempting to encrypt or decrypt data.");

            // I found a problem with using the CryptoStream here, but this works...
            var output = new byte[data.Length];
            transform.TransformBlock(data, 0, data.Length, output, 0);
            return output;
        }
    }
    internal class AesCtr256 : AesCtrImpl, ICipher<AesCtr256>
    {
        string ICipher.Name => Name;
        public static string Name => "aes256-ctr";
        public AesCtr256() : base(256)
        {
        }
    }
    internal class AesCtr192 : AesCtrImpl, ICipher<AesCtr192>
    {
        string ICipher.Name => Name;
        public static string Name => "aes192-ctr";
        public AesCtr192() : base(192)
        {
        }
    }
    internal class AesCtr128 : AesCtrImpl, ICipher<AesCtr128>
    {
        string ICipher.Name => Name;
        public static string Name => "aes128-ctr";
        public AesCtr128() : base(128)
        {
        }
    }
    internal class AesCbc256 : AesBase, ICipher<AesCbc256>
    {
        string ICipher.Name => Name;
        public static string Name => "aes256-cbc";
        public AesCbc256() : base(256, PaddingMode.None, CipherMode.CBC)
        {
        }
    }
    internal class AesCbc192 : AesBase, ICipher<AesCbc192>
    {
        string ICipher.Name => Name;
        public static string Name => "aes192-cbc";
        public AesCbc192() : base(192, PaddingMode.None, CipherMode.CBC)
        {
        }
    }
    internal class AesCbc128 : AesBase, ICipher<AesCbc128>
    {
        string ICipher.Name => Name;
        public static string Name => "aes128-cbc";
        public AesCbc128() : base(128, PaddingMode.None, CipherMode.CBC)
        {
        }
    }


    abstract class AesCtrImpl : IDisposable
    {
        private readonly Aes aes;

        private ICryptoTransform _encryptor;

        private ulong _ivUpper; // The upper 64 bits of the IV
        private ulong _ivLower; // The lower 64 bits of the IV


        public uint BlockSize
        {
            get
            {
                // According to https://msdn.microsoft.com/en-us/library/system.security.cryptography.symmetricalgorithm.blocksize(v=vs.110).aspx
                // TripleDES.BlockSize is the size of the block in bits, so we need to divide by 8
                // to convert from bits to bytes.
                return (uint)(aes.BlockSize / 8);
            }
        }

        public uint KeySize
        {
            get
            {
                // https://msdn.microsoft.com/en-us/library/system.security.cryptography.symmetricalgorithm.keysize(v=vs.110).aspx
                // TripleDES.KeySize is the size of the key in bits, so we need to divide by 8
                // to convert from bits to bytes.
                return (uint)(aes.KeySize / 8);
            }
        }

        public AesCtrImpl(int keySize)
        {
            var aes = Aes.Create();
            aes.KeySize = keySize;
            aes.Mode = System.Security.Cryptography.CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            this.aes = aes;




        }

        public byte[] Encrypt(byte[] data)
        {
            return CTREncryptDecrypt(data, 0, data.Length);

        }

        public byte[] Decrypt(byte[] data)
        {
            return CTREncryptDecrypt(data, 0, data.Length);
        }

        public void SetKey(byte[] key, byte[] iv)
        {
            aes.Key = key;
            _ivLower = BinaryPrimitives.ReadUInt64BigEndian(iv.AsSpan(8));
            _ivUpper = BinaryPrimitives.ReadUInt64BigEndian(iv);
            _encryptor = aes.CreateEncryptor();
        }




        private byte[] CTREncryptDecrypt(byte[] data, int offset, int length)
        {
            var count = length / BlockSize;
            if (length % BlockSize != 0)
            {
                count++;
            }

            var buffer = new byte[count * BlockSize];
            CTRCreateCounterArray(buffer);
            _ = _encryptor.TransformBlock(buffer, 0, buffer.Length, buffer, 0);
            ArrayXOR(buffer, data, offset, length);

            // adjust output for non-blocksized lengths
            if (buffer.Length > length)
            {
                Array.Resize(ref buffer, length);
            }

            return buffer;
        }

        // creates the Counter array filled with incrementing copies of IV
        private void CTRCreateCounterArray(byte[] buffer)
        {
            for (var i = 0; i < buffer.Length; i += 16)
            {
                BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(i + 8), _ivLower);
                BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(i), _ivUpper);

                _ivLower += 1;
                _ivUpper += (_ivLower == 0) ? 1UL : 0UL;
            }
        }

        // XOR 2 arrays using Vector<byte>
        private static void ArrayXOR(byte[] buffer, byte[] data, int offset, int length)
        {
            var i = 0;

            var oneVectorFromEnd = length - Vector<byte>.Count;
            for (; i <= oneVectorFromEnd; i += Vector<byte>.Count)
            {
                var v = new Vector<byte>(buffer, i) ^ new Vector<byte>(data, offset + i);
                v.CopyTo(buffer, i);
            }

            for (; i < length; i++)
            {
                buffer[i] ^= data[offset + i];
            }
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                aes.Dispose();
                _encryptor.Dispose();
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }


    }
}
