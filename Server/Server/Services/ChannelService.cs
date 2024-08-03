using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Unicode;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Bcpg;
using Spectre.Console;
using SshGame.Game;
using SshGame.Server;
using SshGame.Server.KexAlgorithms;
using SshGame.Server.Packets;

namespace SshGame.Server.Services;


class ChannelService : Service
{
    private uint width;
    private uint height;

    public ChannelService(Client client, ILogger logger, UInt32 ownChannelId, UInt32 otherChannelId) : base(client, logger)
    {
        OwnChannelId = ownChannelId;
        OtherChannelId = otherChannelId;
    }

    public uint OwnChannelId { get; }
    public uint OtherChannelId { get; }

    RemoteConsoleOutput output;
    IAnsiConsole console;
    private PlayerConsole player;


    protected override bool HandlePacketInternal(IPacket packet)
    {
        if (packet is ChannelSuccess)
        {
            // we currently only send one Chenel request so this is always for the encoding
        }
        else if (packet is ChannelFailur)
        {
            // we currently only send one Chenel request so this is always for the encoding
            // we do not support utf8
        }
        else if (packet is ChannelRequest channelRequest)
        {
            logger.LogInformation($"chenelrequest {channelRequest.RequestType}");
            if (channelRequest is ChannelRequest.PtyRuquest ptyRequet)
            {
                this.width = ptyRequet.TerminalCharacterWidth;
                this.height = ptyRequet.TerminalCharacterHeight;
                this.output = new RemoteConsoleOutput(this);
                logger.LogInformation(ptyRequet.TermEnviroment);
                if (ptyRequet.TermEnviroment.Contains("256color"))
                {
                    client.Colors = TerminalColor.Bit8;
                }
                console = Spectre.Console.AnsiConsole.Create(new Spectre.Console.AnsiConsoleSettings()
                {
                    Out = output,
                    Interactive = InteractionSupport.No,
                });
                // console.Clear(true);
                // console.WriteLine("Do you want to play a game?");
                if (ptyRequet.WantReply)
                {
                    client.Send(new ChannelSuccess() { RecipientChannel = this.OtherChannelId });
                }
                client.Send(new ChannelRequest.EnvironmentRuquest
                {
                    Name = "LANG",
                    Value = "C.UTF-8",
                    WantReply = true,
                    RecipientChannel = this.OtherChannelId,
                });




                client.SupportUTF8 = true;

                this.player = new PlayerConsole(client, client.UserIdentity, (int)this.width, (int)this.height);
                Task.Run(() => this.player.Loop((buffer, offset, length) =>
                  {
                      client.Send(new ChannelData() { RecipientChannel = OtherChannelId, Data = buffer, Offset = offset, Length = length });
                  }));

                // builder.ClearScreen().SetCursorPosition(1, 1).Strikethrough().Append("Hello").Reset().Append(" ").ForegroundColor(3).Append("Wolrld").HideCursor().Reset();
                // client.Send(new ChannelData() { RecipientChannel = OtherChannelId, Data = Encoding.ASCII.GetBytes(builder.ToString()) });

            }
            else if (channelRequest is ChannelRequest.ShellRuquest shellRequest)
            {
                if (shellRequest.WantReply)
                {
                    client.Send(new ChannelSuccess() { RecipientChannel = this.OtherChannelId });
                }
            }
            else if (channelRequest is ChannelRequest.WindowChange windowChange)
            {
                this.width = windowChange.TerminalCharacterWidth;
                this.height = windowChange.TerminalCharacterHeight;
                if (this.player is not null)
                {
                    this.player.WindowChange((int)this.width, (int)this.height);
                }
                if (windowChange.WantReply)
                {
                    client.Send(new ChannelSuccess() { RecipientChannel = this.OtherChannelId });
                }
            }
            else if (channelRequest is ChannelRequest.EnvironmentRuquest enviromentRequest)
            {
                if (enviromentRequest.RequestType == "LANG")
                {
                    client.SupportUTF8 = enviromentRequest.Value.Contains("UTF-8");
                }
                else if (channelRequest.WantReply)
                {
                    client.Send(new ChannelFailur() { RecipientChannel = this.OtherChannelId });
                }
            }
            else
            {
                if (channelRequest.WantReply)
                {
                    client.Send(new ChannelFailur() { RecipientChannel = this.OtherChannelId });
                }
            }
        }
        else if (packet is ChannelData channelData)
        {
            // if (channelData.Data[0] == 3)
            // {
            //     client.Disconnect(DisconnectReason.SSH_DISCONNECT_BY_APPLICATION, "User iniziated");
            //     return true;
            // }

            player.HandleInput(channelData.Data);
            // console.Write("\x1b[2J");
            // console.WriteLine("Foo");

        }
        else
        {
            return false;
        }
        return true;
    }

    class RemoteConsoleOutput : IAnsiConsoleOutput
    {
        private ChannelService channelService;

        public RemoteConsoleOutput(ChannelService channelService)
        {
            this.channelService = channelService;
            Writer = new ChannelWriter(channelService);
        }

        public TextWriter Writer { get; }

        public bool IsTerminal => false;

        public int Width => (int)channelService.width;

        public int Height => (int)channelService.height;

        public void SetEncoding(Encoding encoding)
        {
            throw new NotImplementedException();
        }

        private class ChannelWriter : TextWriter
        {
            private ChannelService channelService;

            public ChannelWriter(ChannelService channelService)
            {
                this.channelService = channelService;

            }

            public override Encoding Encoding => Encoding.ASCII;

            public override void Write(ReadOnlySpan<char> buffer)
            {
                var b = new byte[buffer.Length];
                for (int i = 0; i < buffer.Length; i++)
                {
                    b[i] = (byte)buffer[i];
                }
                channelService.client.Send(new ChannelData() { RecipientChannel = channelService.OtherChannelId, Data = b });
            }

        }
    }

}

internal enum TerminalColor
{
    Bit4,
    Bit8,
    Bit24,
}