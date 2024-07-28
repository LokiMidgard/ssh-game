using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace SshGame.Server
{
    public interface IDiscovrable
    {
        static abstract string Name { get; }
        static virtual bool IsAdvertised { get => true; }

    }

    internal static class AlgorithmExtensions
    {
        public static string Name<T>(this T also)
        where T : IDiscovrable
        {
            return T.Name;
        }
    }

}
