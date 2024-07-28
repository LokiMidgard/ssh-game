using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SshGame.Server.Compressions
{
    public class NoCompression : ICompression<NoCompression>
    {
        public static string Name
        {
            get
            {
                return "none";
            }
        }

        public byte[] Compress(byte[] data)
        {
            return data;
        }

        public byte[] Decompress(byte[] data)
        {
            return data;
        }
    }
}
