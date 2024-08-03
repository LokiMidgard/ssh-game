using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Security.Principal;
using System.Text;
using System.Threading.Channels;
using Microlithix.Text.Ansi;
using Microlithix.Text.Ansi.Element;
using Org.BouncyCastle.Math.EC.Rfc7748;
using SshGame.Game.Screens;
using SshGame.Server;
using SshGame.Server.Services;

namespace SshGame.Game;
internal class PlayerConsole
{

    public delegate void WriteToClient(byte[] buffer, int offset, int length);

    private readonly Channel<Func<ConsoleInput, Task>> Channel = System.Threading.Channels.Channel.CreateUnbounded<Func<ConsoleInput, Task>>(new UnboundedChannelOptions { SingleReader = true });

    private readonly Client client;
    private int width;
    private int height;
    private int lastSentence = 0;

    private Screen? screen;

    public PlayerConsole(Client client, byte[] userId, int width, int height)
    {
        this.client = client;
        this.width = width;
        this.height = height;
        var shell = new ShellScreen();
        shell.EnetrFilesystem(new VirtualFileSystem()
        {
            InitalLocation = (VirtualFileSystem.IPath.AbsolutePath)VirtualFileSystem.IPath.Create("/user"),
            Root = new()
            {
                Files ={
                    {"user", new VirtualFileSystem.ShellFile.Folder(){
                    Files={
                        {"change-directory", new VirtualFileSystem.ShellFile.Extension(){
                            Command=new ShellScreen.Command("change-directory", new(1,0), "Changes the current directory", [new ("dir", "The directory to change to", ShellScreen.ParameterType.File)], async (console, location, parameters)=>{
                                if(parameters.Length==0){
                                    console.Color(ConsoleInput.SafeColors.Red).Append("No target supplied");
                                    return;
                                }
                                var changeToPath = VirtualFileSystem.IPath.Create(parameters[0].value);
                                var changeTo = location.Filesystem.GetFile(location.Path, changeToPath);
                                if(changeTo is null){
                                    console.Color(ConsoleInput.SafeColors.Red).Append("Path not found");
                                    return;
                                }
                                if(changeTo is not VirtualFileSystem.ShellFile.Folder folder){
                                    console.Color(ConsoleInput.SafeColors.Red).Append("Path not a folder");
                                    return;
                                }

                            })
                        }}
                    }}
                }
                }

            }
        });
        RequestSwitchScreen(shell);
    }

    public async Task RequestSwitchScreen(Screen screen)
    {
        await this.Channel.Writer.WriteAsync(async (console) =>
                {
                    if (this.screen is not null)
                    {
                        await this.screen.Leave();
                    }
                    this.screen = screen;
                    await this.screen.Enter(this.width, this.height, this, console);
                });
    }

    public async Task Loop(WriteToClient writeToClient)
    {
        ConsoleInput consoleInput = new()
        {
            TerminalColor = TerminalColor.Bit24,
            Encoding = client.SupportUTF8 ? Encoding.UTF8 : Encoding.ASCII,
        };
        ((IRecycable)consoleInput).Init();

        await foreach (var item in Channel.Reader.ReadAllAsync())
        {
            try
            {

                await item(consoleInput);
                if (consoleInput.Length > 0)
                {
                    consoleInput.Reset();
                    var buffer = consoleInput.GetBytes(out var offset, out var length);
                    writeToClient(buffer, offset, length);
                    ((IRecycable)consoleInput).Reset();
                    ((IRecycable)consoleInput).Init();
                }
            }
            catch
            {

            }
        }
    }

    public static string GetSentence(int index, string user)
    {
        string[] sentences = {
            "I'm sorry " + user + ", I can't do that.",
            "Unfortunately " + user + ", that's not possible.",
            "Apologies " + user + ", this action is not allowed.",
            "I'm afraid I can't comply with that request, " + user + ".",
            "My apologies " + user + ", but I must refuse that action.",
            "I regret to inform you " + user + ", that isn't something I can do.",
            "Sadly " + user + ", I cannot perform that task.",
            "I wish I could help, but I can't do that, " + user + ".",
            "It's beyond my capabilities to do that, " + user + ".",
            "I'm unable to fulfill that request, " + user + ".",
            "That request is out of my reach, " + user + ".",
            "Unfortunately, I can't do that for you, " + user + ".",
            "I'm sorry, but that's not within my power, " + user + ".",
            "I must decline your request, " + user + ".",
            "That action is not permitted, " + user + ".",
            "I can't execute that request, " + user + ".",
            "I'm not able to perform that action, " + user + ".",
            "I cannot complete that task for you, " + user + ".",
            "That is not something I can do, " + user + ".",
            "I regret that I can't help with that, " + user + "."
        };

        if (index < 0 || index >= sentences.Length)
        {
            return "Invalid index. Please provide a number between 0 and " + (sentences.Length - 1) + ".";
        }

        return sentences[index];
    }

    public void HandleInput(byte[] data)
    {
        Channel.Writer.WriteAsync(async console =>
        {
            await (screen?.HandleInput(console, data) ?? Task.CompletedTask);
        });


    }

    public void HandleUpdate(TimeSpan epelepsedTime, TimeSpan sinceStart)
    {
        Channel.Writer.WriteAsync(async console =>
        {
            await (screen?.WorldUpdate(console, epelepsedTime, sinceStart) ?? Task.CompletedTask);
        });


    }

    internal async Task Disconect()
    {
        await this.Channel.Writer.WriteAsync(async (console) =>
             {
                 if (this.screen is not null)
                 {
                     await this.screen.Leave();
                 }
                 //  Thread.Sleep(1000);
                 this.client.Disconnect(
                  SshGame.Server.Packets.DisconnectReason.SSH_DISCONNECT_BY_APPLICATION, "");
             });
    }

    internal void WindowChange(int width, int height)
    {
        this.width = width;
        this.height = height;
        this.screen?.ChangeSize(width, height);
    }
}