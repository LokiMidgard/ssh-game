using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Org.BouncyCastle.Asn1.Crmf;
using Org.BouncyCastle.Asn1.Ocsp;

namespace SshGame.Game.Screens;
internal abstract class Screen
{
    private int width;
    private int height;
    private PlayerConsole player;


    protected int Width => width;
    protected int Height => height;
    protected PlayerConsole Player => player;

    public virtual Task Enter(int width, int height, PlayerConsole player, ConsoleInput console)
    {
        this.width = width;
        this.height = height;
        this.player = player;

        return Task.CompletedTask;
    }

    public virtual Task Leave()
    {
        return Task.CompletedTask;
    }


    public virtual Task WorldUpdate(ConsoleInput console, TimeSpan epelepsedTime, TimeSpan sinceStart) { return Task.CompletedTask; }
    public virtual Task HandleInput(ConsoleInput console, byte[] input) { return Task.CompletedTask; }

    public virtual Task ChangeSize(int width, int height)
    {
        this.width = width;
        this.height = height;
        return Task.CompletedTask;
    }
}

