using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SshGame.Game;

internal class World
{
    private readonly ILogger logger;

    private readonly Channel<(PlayerConsole player, bool added)> playersChannel = System.Threading.Channels.Channel.CreateUnbounded<(PlayerConsole player, bool added)>(new UnboundedChannelOptions { SingleReader = true });

    public World(ILogger logger)
    {
        this.logger = logger;
    }


    public void AddPlayer(PlayerConsole player)
    {
        playersChannel.Writer.WriteAsync((player, true));
    }
    public void RemovePlayer(PlayerConsole player)
    {
        playersChannel.Writer.WriteAsync((player, false));

    }

    public async Task Simulate()
    {
        var targetSimulationSteps = TimeSpan.FromMilliseconds(20);
        var gameStart = DateTime.Now;
        var lastStart = DateTime.Now;
        while (true)
        {
            var start = DateTime.Now;
            var epelepsed = lastStart - start;
            var epelepsedSinceStart = start - gameStart;


            while (playersChannel.Reader.TryRead(out var change))
            {

            }




            var end = DateTime.Now;
            var distance = end - start;
            if (targetSimulationSteps > distance)
            {
                await Task.Delay(targetSimulationSteps - distance);
            }
            else
            {
                logger.LogWarning($"Simulation took longre then the targeted {targetSimulationSteps} ({distance})");
                await Task.Yield();
            }
        }
    }
}