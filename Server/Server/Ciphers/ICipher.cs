using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SshGame.Server.Ciphers
{
    public interface ICipher 
    {
        string Name { get; }
        uint BlockSize { get; }
        uint KeySize { get; }
        byte[] Encrypt(byte[] data);
        byte[] Decrypt(byte[] data);
        void SetKey(byte[] key, byte[] iv);
    }
    public interface ICipher<T> : IDiscovrable, ICipher
where T : ICipher<T>
    {

    }
}
