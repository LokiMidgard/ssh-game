using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SshGame.Server.KexAlgorithms
{
    public interface IKexAlgorithm
    {
              byte[] CreateKeyExchange();
        byte[] DecryptKeyExchange(byte[] keyEx);
        byte[] ComputeHash(byte[] value);
    }
    public interface IKexAlgorithm<T> : IDiscovrable,IKexAlgorithm
        where T : IKexAlgorithm<T>
    {
    }
}
