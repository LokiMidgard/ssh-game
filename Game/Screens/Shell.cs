


using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microlithix.Text.Ansi;
using Org.BouncyCastle.Utilities;

namespace SshGame.Game.Screens;

class ShellScreen : Screen
{
    private RollingBuffer virtualBuffer = new RollingBuffer(400, 120);

    private string currentInput = "";

    private int cursorPosition = 0;
    // the horizontal scroll position of the current line, used when the line is to long
    private int currentInputOffset = 0;


    public ShellScreen()
    {
        this.LoadedCommands = new Dictionary<string, Command>{
        {"help", new Command("help", "show help for a command or programm", [],async (console,parameters)=>{

            console.Append("List of commands").AppendLine();

            foreach (var item in LoadedCommands)
            {
                console.Append("    ").Color(ConsoleInput.SafeColors.BrightWhite).Append(item.Key);

foreach (var parameter in item.Value.Parameters)
{
console.Append(" ");
if(!parameter.Required){
    console.Color(ConsoleInput.SafeColors.White).Append("[");
}
console.Color(ConsoleInput.SafeColors.Cyan).Append(parameter.Name);
if(!parameter.Required){
    console.Color(ConsoleInput.SafeColors.White).Append("]");
}
}

                console.Color(ConsoleInput.SafeColors.White).Append(" - ")
                .Color(ConsoleInput.SafeColors.BrightWhite).Append(item.Value.Description)
                .AppendLine();
foreach (var parameter in item.Value.Parameters)
{
    console.Append("      ").Color(ConsoleInput.SafeColors.Cyan).Append(parameter.Name)
    .Color(ConsoleInput.SafeColors.White).Append(" - ")
.Color(ConsoleInput.SafeColors.White).Append(parameter.Description)
.AppendLine();
}
            }
            console.Reset();

        })},
        {"dir", new Command("dir", "list filse in the current directory", [],async(console,parameters)=>{} )},
        {"load", new Command("load", "loads a program from disc", [new("program", "the file to load",ParameterType.File)],async(console,parameters)=>{

        } )},
            };
    }

    public override async Task Enter(int width, int height, PlayerConsole player, ConsoleInput console)
    {
        await base.Enter(width, height, player, console);
        console.ClearScreen();
        console.ShowCursor();
        console.SetCursorPosition(4, 1);
    }

    public override async Task HandleInput(ConsoleInput console, byte[] input)
    {
        await base.HandleInput(console, input);

        foreach (var item in AnsiSequenceParser.ParseAnsiSequences(input, console.Encoding))
        {
            if (item is AnsiSequence ansiSequence)
            {
                if (ansiSequence.Type == AnsiSequenceType.CursorForward)
                {
                    var distance = ansiSequence.Parameters.FirstOrDefault();
                    if (distance == 0)
                    {
                        distance = 1;
                    }
                    cursorPosition += distance;
                }
                else if (ansiSequence.Type == AnsiSequenceType.CursorBackward)
                {
                    var distance = ansiSequence.Parameters.FirstOrDefault();
                    if (distance == 0)
                    {
                        distance = 1;
                    }
                    cursorPosition -= distance;
                }
                else if (ansiSequence.Type == AnsiSequenceType.CursorPreviousLine && ansiSequence.Parameters.Length == 0)
                {
                    // for some reason this is End
                    cursorPosition = currentInput.Length;
                }
                else if (ansiSequence.Type == AnsiSequenceType.CursorPosition && ansiSequence.Parameters.Length == 0)
                {
                    // and this pos1 which make a little more sense
                    cursorPosition = 0;
                }
            }
            else if (item is TextSequence textSequence)
            {
                currentInput = currentInput.Insert(cursorPosition, textSequence.Text);
                cursorPosition += textSequence.Text.Length;
                if (currentInput.Length > virtualBuffer.Width)
                {
                    currentInput = currentInput.Substring(0, virtualBuffer.Width);
                    console.Bell();
                }
            }
            else if (item is LineSequence lineSequence)
            {
                if (lineSequence.Type == LineSequenceType.LineBreak)
                {
                    currentInput.CopyTo(virtualBuffer[0]);
                    var command = currentInput;
                    currentInput = string.Empty;
                    virtualBuffer.Shift();
                    console.MoveCursorRight(Width);
                    console.Append('\n').SetColumn(0);
                    currentInputOffset = 0;
                    ExecuteCommand(console, command);
                }
            }

            cursorPosition = Math.Clamp(cursorPosition, 0, currentInput.Length);
        }
        var prompt = ">";
        string visibleInput;
        var availableSize = Width - prompt.Length - 1;
        bool showEpelepseBegin;
        bool showEpelepseEnd;
        if (currentInput.Length > availableSize)
        {
            if (cursorPosition - currentInputOffset + 3 > availableSize)
            {
                // move offest by that value 
                var move = (cursorPosition - currentInputOffset + 3) - availableSize;
                currentInputOffset += move;
            }
            else if (currentInputOffset > cursorPosition - 3)
            {
                currentInputOffset = cursorPosition - 3;
            }

            currentInputOffset = Math.Clamp(currentInputOffset, 0, currentInput.Length - availableSize);
            if (currentInputOffset > 0)
            {
                visibleInput = currentInput.Substring(currentInputOffset + 1, availableSize - 1);
                showEpelepseBegin = true;
            }
            else
            {

                visibleInput = currentInput.Substring(currentInputOffset, availableSize);
                showEpelepseBegin = false;
            }

            if (currentInputOffset + availableSize < currentInput.Length)
            {
                // show … at end
                visibleInput = visibleInput.Substring(0, visibleInput.Length - 1);
                showEpelepseEnd = true;
            }
            else
            {
                showEpelepseEnd = false;
            }

        }
        else
        {
            visibleInput = currentInput;
            showEpelepseEnd = false;
            showEpelepseBegin = false;
        }
        console.ClearLine();
        console.SetColumn(0);
        console.Append(prompt);
        if (showEpelepseBegin)
        {
            console.Append('…');
        }
        console.Append(visibleInput);
        if (showEpelepseEnd)
        {
            console.Append('…');
        }
        console.SetColumn(cursorPosition - currentInputOffset + 1 + prompt.Length);
        // console.MoveCursorRight(cursorPosition + 1);


    }

    private void ExecuteCommand(ConsoleInput console, string command)
    {
        command = command.Trim();
        if (LoadedCommands.TryGetValue(command, out var executable))
        {
            executable.execute(console, []);
        }
        else
        {
            console.Color(ConsoleInput.SafeColors.Red).Append("Invalid Command")
            .Reset().AppendLine();
        }
    }

    public Dictionary<string, Command> LoadedCommands;

    public enum ParameterType
    {
        String,
        Integer,
        File
    }
    public record CommandParameter(string Name, string Description, ParameterType Type, bool Required = false);
    public record Command(string Name, string Description, CommandParameter[] Parameters, Func<ConsoleInput, (CommandParameter parameter, String value)[], Task> execute);







    private class RollingBuffer
    {
        public int Width { get; }
        public int Height { get; }

        public int Length => Math.Min(offset + 1, Height);

        private int Transform(int row) => (offset - row) % Height;

        public Span<char> this[int row]
        {
            get
            {
                ref var reference = ref MemoryMarshal.GetArrayDataReference(buffer);
                Span<char> span = MemoryMarshal.CreateSpan(ref Unsafe.As<byte, char>(ref reference), buffer.Length);
                var lineInBuffer = Transform(row);
                return span[(lineInBuffer * Width)..][..Width];
            }
        }



        private int offset = 0;
        private char[,] buffer;

        public RollingBuffer(int width, int height)
        {
            Width = width;
            Height = height;
            buffer = new char[width, height];
            ref var reference = ref MemoryMarshal.GetArrayDataReference(buffer);
            Span<char> span = MemoryMarshal.CreateSpan(ref Unsafe.As<byte, char>(ref reference), buffer.Length);
            span.Fill(' ');
        }

        public char this[int width, int height]
        {
            get
            {
                return buffer[width, Transform(height)];
            }
            set
            {
                buffer[width, Transform(height)] = value;
            }
        }

        public void Shift()
        {
            offset += 1;
            // clear new line
            var row = Transform(0);
            for (int i = 0; i < Width; i++)
            {
                buffer[i, row] = ' ';
            }
        }

    }
}