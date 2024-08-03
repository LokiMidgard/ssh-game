using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SshGame.Server.Compressions
{
    public interface ICompression
    {
        byte[] Compress(byte[] data);
        byte[] Decompress(byte[] data);
    }

    public interface ICompression<T> : IDiscovrable, ICompression
where T : ICompression<T>
    {

    }

}
