using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SshGame.Server.Ciphers
{
    public class NoCipher : ICipher<NoCipher>
    {
        string ICipher.Name => Name;

        public static bool IsAdvertised { get => false; }

        public uint BlockSize
        {
            get
            {
                return 8;
            }
        }

        public uint KeySize
        {
            get
            {
                return 0;
            }
        }

        public static string Name
        {
            get
            {
                return "none";
            }
        }

        public byte[] Decrypt(byte[] data)
        {
            return data;
        }

        public byte[] Encrypt(byte[] data)
        {
            return data;
        }

        public void SetKey(byte[] key, byte[] iv)
        {
            // No key for this cipher
        }
    }
}
