using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SshGame.Server.MACAlgorithms
{
    public interface IMACAlgorithm
    {
        uint KeySize { get; }
        uint DigestLength { get; }
        void SetKey(byte[] key);
        byte[] ComputeHash(uint packetNumber, byte[] data);
    }
    public interface IMACAlgorithm<T> : IDiscovrable, IMACAlgorithm
where T : IMACAlgorithm<T>
    {

    }
}
