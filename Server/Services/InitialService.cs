using Microsoft.Extensions.Logging;
using SshGame.Server;
using SshGame.Server.KexAlgorithms;
using SshGame.Server.Packets;

namespace SshGame.Server.Services;


class InitalService : Service
{

    public class Factiory : IServiceFactory
    {
        public static string Name => "";

        Service IServiceFactory.Create(Client client, ILogger logger)
        {
            return new InitalService(client, logger);
        }
    }
    public InitalService(Client client, ILogger logger) : base(client, logger)
    {
        m_PendingExchangeContext = new ExchangeContext();
    }
    protected override bool HandlePacketInternal(IPacket packet)
    {
        return false;
    }



}