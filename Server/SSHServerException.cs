using SshGame.Server.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SshGame.Server
{
    public class SSHServerException : Exception
    {
        public DisconnectReason Reason { get; set; }

        public SSHServerException(DisconnectReason reason, string message) : base(message)
        {
            Reason = reason;
        }
    }
}
