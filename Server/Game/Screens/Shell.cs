


using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microlithix.Text.Ansi;
using Org.BouncyCastle.Math.EC.Rfc7748;
using Org.BouncyCastle.Utilities;
using Spectre.Console;

namespace SshGame.Game.Screens;

internal record Location(VirtualFileSystem Filesystem, VirtualFileSystem.IPath.AbsolutePath Path) { public VirtualFileSystem.IPath.AbsolutePath Path { get; set; } = Path; };
class ShellScreen : Screen
{
    private RollingBuffer virtualBuffer = new RollingBuffer(400, 120);

    private string currentInput = "";

    private int cursorPosition = 0;
    // the horizontal scroll position of the current line, used when the line is to long
    private int currentInputOffset = 0;


    private readonly Stack<Location> FileSystemStack = [];

    public VirtualFileSystem CurrentFilesystem => FileSystemStack.Peek().Filesystem;
    public VirtualFileSystem.IPath.AbsolutePath CurrentDirectory
    {
        get => FileSystemStack.Peek().Path;
        set => FileSystemStack.Peek().Path = value;
    }
    public void EnetrFilesystem(VirtualFileSystem fileSystem)
    {
        FileSystemStack.Push(new(fileSystem, fileSystem.InitalLocation));
    }


    public ShellScreen()
    {
        this.LoadedCommands = new Dictionary<string, Command>{
        {"help", new Command("help",new(1,0), "show help for a command or programm", [],async (console,location,parameters)=>{

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
        {"dir", new Command("dir", new(1,0),"list filse in the current directory", [],async(console,location,parameters)=>{
            var folder =location.Filesystem.GetFile(CurrentDirectory, CurrentDirectory) as VirtualFileSystem.ShellFile.Folder;
            if(folder is null){
                console.Color(ConsoleInput.SafeColors.Red).Append("Folder Not Found").AppendLine();
            }
            if(folder is not null){
                foreach (var item in folder.Files.Keys)
                {
                    console.Append("  ").Append(item).AppendLine();
                }
            }
        } )},
        {"load", new Command("load", new(1,0) ,"loads a program from disc", [new("program", "the file to load",ParameterType.File)],async(console,location,parameters)=>{
            var extension =location.Filesystem.GetFile(CurrentDirectory, VirtualFileSystem.IPath.Create(parameters[0].value)) as VirtualFileSystem.ShellFile.Extension;
            if(extension is null){
                console.Color(ConsoleInput.SafeColors.Red).Append("File Not Found").AppendLine();
                return;
            }
            if(!this.LoadedCommands.TryGetValue(extension.Command.Name, out var command) || command.Version< extension.Command.Version){
                LoadedCommands[extension.Command.Name] = extension.Command;
                console.Append($"loaded {extension.Command.Name} in Version {extension.Command.Version}").AppendLine();
            }
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

    private void ExecuteCommand(ConsoleInput console, ReadOnlySpan<char> command)
    {
        List<Range> parts = [];
        int lastIndex = 0;


        bool isInEsqape = false;
        int bufferUsed = 0;
        int bufferCurrent = 0;
        Span<char> buffer = stackalloc char[command.Length];
        for (int i = 0; i < command.Length + 1; i++)
        {
            char currentChar = i < command.Length ? command[i] : ' ';
            if (i == command.Length)
            {
                isInEsqape = false;
            }
            else if (currentChar == '\\' && !isInEsqape)
            {
                isInEsqape = true;
            }
            else if (currentChar == ' ' && !isInEsqape)
            {
                var length = (i - 1) - lastIndex;
                if (length > 0)
                {
                    parts.Add(new Range(bufferUsed, bufferUsed + length));
                    bufferUsed += length;
                }
                lastIndex = i + 1;
            }
            else
            {
                buffer[bufferCurrent] = command[i];
                bufferCurrent++;
                isInEsqape = false;
            }
        }

        var actualCommand = parts.Count > 0 ? buffer[parts[0]] : Span<char>.Empty;

        if (actualCommand.Length > 0)
        {
            if (LoadedCommands.TryGetValue(actualCommand.ToString(), out var executable))
            {
                var parameterLookup = executable.Parameters.ToDictionary(p => "--" + p.Name).GetAlternateLookup<string, CommandParameter, ReadOnlySpan<char>>();

                // .TryGetAlternateLookup(Dictionary<string, CommandParameter>.AlternateLookup.);
                int currentPositionalParameter = 0;
                for (int i = 1; i < parts.Count; i++)
                {
                    var current = buffer[parts[i]];
                    if (parameterLookup.TryGetValue(current, out var parameter))
                    {
                        if (parameter.Type.HasFlag(ParameterType.File))
                        {
                            if (i + 1 < parts.Count - 1)
                            {
                                var nextValue = buffer[parts[i + 1]];
                                var file = CurrentFilesystem.GetFile(CurrentDirectory, VirtualFileSystem.IPath.Create(nextValue));
                                if (file is null)
                                {
                                    // file not found

                                }
                            }

                        }
                    }
                }
                executable.execute(console, FileSystemStack.Peek(), []);

            }
            else
            {
                console.Color(ConsoleInput.SafeColors.Red).Append("Invalid Command")
                .Reset().AppendLine();
            }
        }
    }

    public Dictionary<string, Command> LoadedCommands { get; init; } = [];

    public enum ParameterType
    {
        String = 1 << 0,
        Integer = 1 << 1,
        File = 1 << 2,
        Folder = 1 << 3,
    }
    public record Command(string Name, Version Version, string Description, ICommandParameter<object>[] Parameters, Func<ConsoleInput, Location, (CommandParameter parameter, String value)[], Task> execute);


    public abstract record CommandParameter<T>(string Name, string Description, ParameterType Type, bool isMultiple = false)
    {
        public abstract bool TryParseParameter(ReadOnlySpan<char> bufer, IList<Range> parts, out T result);
    }

    public interface ICommandParameter<out T>
    {
        string Name { get; }
        string Description { get; }
        ParameterType Type { get; }
        bool IsMultiple { get; }
        T? TryParseParameter(ReadOnlySpan<char> bufer, IList<Range> parts);
    }





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


internal class VirtualFileSystem
{

    public required VirtualFileSystem.IPath.AbsolutePath InitalLocation { get; init; }
    // public Dictionary<string, ShellFile> Files { get; init; } = [];

    public ShellFile.Folder Root { get; init; } = new ShellFile.Folder();

    public ShellFile? GetFile<T>(VirtualFileSystem.IPath.AbsolutePath currentDirectory, T path)
    where T : VirtualFileSystem.IPath
    {
        var absolute = path.ToAbsolute(currentDirectory);
        ShellFile cuurrent = Root;
        foreach (var part in absolute.Parts)
        {
            if (cuurrent is ShellFile.Folder folder && folder.Files.TryGetValue(absolute.Normalized[part], out var file))
            {
                cuurrent = file;
            }
            else
            {
                return null;
            }
        }

        return cuurrent;
    }

    public interface IPath
    {

        public string Normalized { get; }
        public bool IsAbsolute { get; }

        public readonly struct AbsolutePath : IPath
        {
            public ImmutableArray<Range> Parts { get; }

            public string Normalized { get; }

            public bool IsAbsolute => true;

            public AbsolutePath(string path)
            {
                Normalized = path;
            }

            public AbsolutePath(string path, ImmutableArray<Range> parts) : this(path)
            {
                this.Parts = parts;
            }
        }


        public readonly struct RelativePath : IPath
        {
            public ImmutableArray<Range> Parts { get; }
            public int MoveUp { get; }
            public string Normalized { get; }

            public bool IsAbsolute => true;


            public RelativePath(string path, ImmutableArray<Range> parts, int outside)
            {
                this.Parts = parts;
                MoveUp = outside;
                Normalized = path;

            }
        }


        public static IPath Create(ReadOnlySpan<char> path)
        {
            var isAbsolute = path[0] == '/';
            string Normalized;
            ImmutableArray<Range> parts;

            Span<Range> ranges = stackalloc Range[path.Length / 2];
            ranges = ranges[..path.Split(ranges, '/', StringSplitOptions.RemoveEmptyEntries)];
            Stack<Range> r = [];
            var outside = 0;
            foreach (var part in ranges)
            {
                if (path[part] == ".")
                {
                    continue;
                }
                if (path[part] == "..")
                {
                    if (r.Count == 0)
                    {
                        outside++;
                    }
                    else
                    {
                        r.Pop();
                    }
                }
                else
                {
                    r.Push(part);
                }
            }
            if (outside > 0 && isAbsolute)
            {
                // Error?
            }
            var length = path.Length;
            Span<char> buffer = stackalloc char[r.Sum(x => x.GetOffsetAndLength(length).Length) + r.Count + outside * 3 + 1];
            var index = 0;
            if (!isAbsolute)
            {
                buffer[index] = '.';
                index++;
            }
            for (int i = 0; i < outside; i++)
            {
                "/..".CopyTo(buffer[index..]);
            }
            var partsBuilder = ImmutableArray.CreateBuilder<Range>(r.Count);
            foreach (var item in r)
            {
                buffer[index] = '/';
                index += 1;
                var current = path[item];
                partsBuilder.Add(new Range(index, index + current.Length));
                current.CopyTo(buffer[index..]);
                index += current.Length;
            }
            parts = partsBuilder.ToImmutable();
            Normalized = buffer[..index].ToString();
            return isAbsolute
            ? new AbsolutePath(Normalized, parts)
            : new RelativePath(Normalized, parts, outside);
        }



        public static IPath Combine(params ReadOnlySpan<IPath> combine)
        {
            int from = 0;
            for (int i = combine.Length - 1; i >= 0; i--)
            {
                if (combine[i].IsAbsolute)
                {
                    from = i;
                    break;
                }
            }
            combine = combine[from..];
            var totalLength = combine.Length - 1;
            for (int i = 0; i < combine.Length; i++)
            {
                totalLength += combine[i].Normalized.Length;
            }
            Span<char> buffer = stackalloc char[totalLength];
            var index = 0;
            combine[0].Normalized.CopyTo(buffer);
            index += combine[0].Normalized.Length;
            for (int i = 1; i < combine.Length; i++)
            {
                buffer[index] = '/';
                index++;
                combine[i].Normalized.CopyTo(buffer[index..]);
                index += combine[i].Normalized.Length;
            }
            return IPath.Create(buffer);
        }



    }

    public abstract class ShellFile
    {
        public class Folder : ShellFile
        {
            public Dictionary<string, ShellFile> Files { get; } = [];
        }
        public class Extension : ShellFile
        {
            public required ShellScreen.Command Command { get; init; }
        }

    }
}

internal static class PathExtensions
{
    public static VirtualFileSystem.IPath.AbsolutePath ToAbsolute<TPath>(this TPath self, VirtualFileSystem.IPath.AbsolutePath currentDirectory)
      where TPath : VirtualFileSystem.IPath
    {
        if (self is VirtualFileSystem.IPath.AbsolutePath absolutePath)
        {
            return absolutePath;
        }
        Span<char> buffer = stackalloc char[self.Normalized.Length + currentDirectory.Normalized.Length];
        return (VirtualFileSystem.IPath.AbsolutePath)VirtualFileSystem.IPath.Create(buffer);
    }
    public static VirtualFileSystem.IPath.AbsolutePath Absolute(this VirtualFileSystem.IPath.AbsolutePath self, VirtualFileSystem.IPath.AbsolutePath currentDirectory)
    {
        return self;
    }
}