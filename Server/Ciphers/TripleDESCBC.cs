﻿using SshGame.Server.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SshGame.Server.Ciphers
{
    public class TripleDESCBC : ICipher<TripleDESCBC>
    {
        string ICipher.Name => Name;
        private TripleDES m_3DES = TripleDES.Create();
        private ICryptoTransform m_Decryptor;
        private ICryptoTransform m_Encryptor;

        public uint BlockSize
        {
            get
            {
                // According to https://msdn.microsoft.com/en-us/library/system.security.cryptography.symmetricalgorithm.blocksize(v=vs.110).aspx
                // TripleDES.BlockSize is the size of the block in bits, so we need to divide by 8
                // to convert from bits to bytes.
                return (uint)(m_3DES.BlockSize / 8);
            }
        }

        public uint KeySize
        {
            get
            {
                // https://msdn.microsoft.com/en-us/library/system.security.cryptography.symmetricalgorithm.keysize(v=vs.110).aspx
                // TripleDES.KeySize is the size of the key in bits, so we need to divide by 8
                // to convert from bits to bytes.
                return (uint)(m_3DES.KeySize / 8);
            }
        }

        public static string Name
        {
            get
            {
                return "3des-cbc";
            }
        }

        public TripleDESCBC()
        {
            m_3DES.KeySize = 192;
            m_3DES.Padding = PaddingMode.None;
            m_3DES.Mode = CipherMode.CBC;
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
            m_3DES.Key = key;
            m_3DES.IV = iv;

            m_Decryptor = m_3DES.CreateDecryptor(key, iv);
            m_Encryptor = m_3DES.CreateEncryptor(key, iv);
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
}
