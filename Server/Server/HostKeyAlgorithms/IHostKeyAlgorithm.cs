using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SshGame.Server.HostKeyAlgorithms
{
    public interface IHostKeyAlgorithm 
    {
        void ImportKey(IDictionary<string,string> keyXml);
        byte[] CreateKeyAndCertificatesData();
        byte[] CreateSignatureData(byte[] hash);
    }
    public interface IHostKeyAlgorithm<T> : IDiscovrable,IHostKeyAlgorithm
where T : IHostKeyAlgorithm<T>
    {

    }
}
