using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SshGame.Server.Services;

internal interface IRecycable
{
    void Reset();
    void Init();
}
internal class ConsoleInput : IRecycable
{
    public byte[] buffer;
    public int current;

    public int Length => current;

    public Encoding Encoding = Encoding.ASCII;

    public TerminalColor TerminalColor { get; set; } = TerminalColor.Bit4;

    public ConsoleInput()
    {
    }





    public ConsoleInput Append(ReadOnlySpan<byte> b)
    {
        if (current + b.Length >= buffer.Length)
        {
            Expand();
        }
        b.CopyTo(buffer.AsSpan(current));
        current += b.Length;
        return this;
    }
    public ConsoleInput Append(byte b)
    {
        if (current + 1 >= buffer.Length)
        {
            Expand();
        }
        buffer[current] = b;
        current++;

        return this;
    }
    public ConsoleInput Append(char b)
    {
        if (IsAsciiCompatible(this.Encoding) && b <= 127)
        {
            return Append((byte)b);
        }
        else
        {
            return Append([b]);
        }
    }

    public ConsoleInput Append(String data)
    {
        return Append(this.Encoding.GetBytes(data));
    }
    public ConsoleInput Append(ReadOnlySpan<char> data)
    {
        Span<byte> buffer = stackalloc byte[Math.Max(8
            ,data.Length)];
        var completed = false;
        while (!completed)
        {
            this.Encoding.GetEncoder().Convert(data, buffer, false, out var charsUsed, out var bytesUsed, out completed);
            data = data[charsUsed..];
            Append(buffer[..bytesUsed]);
        }
        return this;
    }



    public ConsoleInput Color(SafeColors color, ColorKind kind = ColorKind.Foreground)
    {
        if (this.TerminalColor == TerminalColor.Bit4)
        {

            if (kind == ColorKind.Foreground)
            {
                if ((byte)color >= 8)
                {
                    Append($"\x1b[{90 + (byte)color}m");
                }
                else
                {
                    Append($"\x1b[{30 + (byte)color}m");
                }
            }
            else
            {
                if ((byte)color >= 8)
                {
                    Append($"\x1b[{100 + (byte)color}m");
                }
                else
                {
                    Append($"\x1b[{40 + (byte)color}m");
                }
            }



        }
        else
        {
            (byte r, byte g, byte b) components = (byte)color switch
            {
                0 => (0, 0, 0),       // Black
                1 => (128, 0, 0),     // Red
                2 => (0, 128, 0),     // Green
                3 => (128, 128, 0),   // Yellow
                4 => (0, 0, 128),     // Blue
                5 => (128, 0, 128),   // Magenta
                6 => (0, 128, 128),   // Cyan
                7 => (192, 192, 192), // White (light gray)
                8 => (128, 128, 128), // Bright black (dark gray)
                9 => (255, 0, 0),     // Bright red
                10 => (0, 255, 0),    // Bright green
                11 => (255, 255, 0),  // Bright yellow
                12 => (0, 0, 255),    // Bright blue
                13 => (255, 0, 255),  // Bright magenta
                14 => (0, 255, 255),  // Bright cyan
                15 => (255, 255, 255),// Bright white
                _ => throw new NotImplementedException()
            };
            Color(components.r, components.g, components.b, kind);
        }
        return this;
    }
    public ConsoleInput Color(byte r, byte g, byte b, ColorKind kind = ColorKind.Foreground)
    {
        if (this.TerminalColor == TerminalColor.Bit4)
        {
            //todo Calculate nearest Safe Color
        }
        else if (this.TerminalColor == TerminalColor.Bit8)
        {
            var rPart = r / 51;
            var gPart = g / 51;
            var bPart = b / 51;

            var color = rPart * 6 * 6 + gPart * 6 + bPart + 16;
            if (kind == ColorKind.Foreground)
            {

                Append($"\x1b[38;5;{color}m");
            }
            else
            {
                Append($"\x1b[48;5;{color}m");
            }

        }
        else if (this.TerminalColor == TerminalColor.Bit24)
        {
            if (kind == ColorKind.Foreground)
            {
                Append($"\x1b[38;2;{r};{g};{b}m");
            }
            else
            {
                Append($"\x1b[48;2;{r};{g};{b}m");
            }
        }
        return this;
    }


    public ReadOnlySpan<byte> GetBytes()
    {
        return this.buffer[..current];
    }
    public byte[] GetBytes(out int offset, out int length)
    {
        offset = 0;
        length = current;
        return this.buffer;
    }

    private void Expand()
    {
        var increase = Math.Max(buffer.Length, 1024);
        var newBuffer = ArrayPool<byte>.Shared.Rent(increase + buffer.Length);
        Array.Copy(buffer, newBuffer, buffer.Length);
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = newBuffer;
    }


    private static readonly Dictionary<int, bool> isAsciiCompatbl = [];

    private static readonly byte[] asciiValues =
        Enumerable.Range(0, 128).Select(b => (byte)b).ToArray();

    private static readonly string asciiChars =
        new string(asciiValues.Select(b => (char)b).ToArray());

    public static bool IsAsciiCompatible(Encoding encoding)
    {
        if (isAsciiCompatbl.TryGetValue(encoding.CodePage, out var isCompatible))
        {
            return isCompatible;
        }
        try
        {
            isCompatible = encoding.GetString(asciiValues).Equals(asciiChars, StringComparison.Ordinal)
                && encoding.GetBytes(asciiChars).SequenceEqual(asciiValues);

        }
        catch (ArgumentException)
        {
            // Encoding.GetString may throw DecoderFallbackException if a fallback occurred 
            // and DecoderFallback is set to DecoderExceptionFallback.
            // Encoding.GetBytes may throw EncoderFallbackException if a fallback occurred 
            // and EncoderFallback is set to EncoderExceptionFallback.
            // Both of these derive from ArgumentException.
            isCompatible = false;
        }

        isAsciiCompatbl.TryAdd(encoding.CodePage, isCompatible);
        return isCompatible;
    }

    void IRecycable.Reset()
    {
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = null!;
    }

    void IRecycable.Init()
    {
        var rented = ArrayPool<byte>.Shared.Rent(10);
        buffer = rented;
        current = 0;
    }

    public enum ColorKind
    {
        Foreground,
        Background,
    }

    public enum SafeColors
    {
        Black = 0,
        Red = 1,
        Green = 2,
        Yellow = 3,
        Blue = 4,
        Magenta = 5,
        Cyan = 6,
        White = 7,
        BrightBlack = 8,
        BrightRed = 9,
        BrightGreen = 10,
        BrightYellow = 11,
        BrightBlue = 12,
        BrightMagenta = 13,
        BrightCyan = 14,
        BrightWhite = 15,
    }
}

internal static class AnsiConsoleInputExtensions
{
    public static ConsoleInput SetColumn(this ConsoleInput sb, int column)
    {
        return sb.Append($"\x1b[{column}G");
    }
     public static ConsoleInput Bell(this ConsoleInput sb)
    {
        return sb.Append($"\a");
    }
    public static ConsoleInput AppendLine(this ConsoleInput sb)
    {
        return sb.Append("\r\n");
    }
    // Reset all attributes
    public static ConsoleInput Reset(this ConsoleInput sb)
    {
        return sb.Append("\x1b[0m");
    }

    // Text formatting
    public static ConsoleInput Bold(this ConsoleInput sb)
    {
        return sb.Append("\x1b[1m");
    }

    public static ConsoleInput Italic(this ConsoleInput sb)
    {
        return sb.Append("\x1b[3m");
    }

    public static ConsoleInput Underline(this ConsoleInput sb)
    {
        return sb.Append("\x1b[4m");
    }

    public static ConsoleInput Inverse(this ConsoleInput sb)
    {
        return sb.Append("\x1b[7m");
    }

    public static ConsoleInput Strikethrough(this ConsoleInput sb)
    {
        return sb.Append("\x1b[9m");
    }

    // Foreground colors
    public static ConsoleInput ForegroundColor(this ConsoleInput sb, int color)
    {
        return sb.Append($"\x1b[{30 + color}m");
    }

    public static ConsoleInput ForegroundBrightColor(this ConsoleInput sb, int color)
    {
        return sb.Append($"\x1b[{90 + color}m");
    }

    // Background colors
    public static ConsoleInput BackgroundColor(this ConsoleInput sb, int color)
    {
        return sb.Append($"\x1b[{40 + color}m");
    }

    public static ConsoleInput BackgroundBrightColor(this ConsoleInput sb, int color)
    {
        return sb.Append($"\x1b[{100 + color}m");
    }

    // Cursor movement
    public static ConsoleInput MoveCursorUp(this ConsoleInput sb, int n = 1)
    {
        return sb.Append($"\x1b[{n}A");
    }

    public static ConsoleInput MoveCursorDown(this ConsoleInput sb, int n = 1)
    {
        return sb.Append($"\x1b[{n}B");
    }

    public static ConsoleInput MoveCursorRight(this ConsoleInput sb, int n = 1)
    {
        return sb.Append($"\x1b[{n}C");
    }

    public static ConsoleInput MoveCursorLeft(this ConsoleInput sb, int n = 1)
    {
        return sb.Append($"\x1b[{n}D");
    }

    public static ConsoleInput SetCursorPosition(this ConsoleInput sb, int row, int col)
    {
        return sb.Append($"\x1b[{row};{col}H");
    }

    // Screen control
    public static ConsoleInput ClearToEndOfScreen(this ConsoleInput sb)
    {
        return sb.Append("\x1b[J");
    }

    public static ConsoleInput ClearToEndOfLine(this ConsoleInput sb)
    {
        return sb.Append("\x1b[K");
    }

    public static ConsoleInput ClearScreen(this ConsoleInput sb)
    {
        return sb.Append("\x1b[2J");
    }

    public static ConsoleInput ClearLine(this ConsoleInput sb)
    {
        return sb.Append("\x1b[2K");
    }

    public static ConsoleInput ShowCursor(this ConsoleInput sb)
    {
        return sb.Append("\x1b[?25h");
    }

    public static ConsoleInput HideCursor(this ConsoleInput sb)
    {
        return sb.Append("\x1b[?25l");
    }

    // Scrolling
    public static ConsoleInput ScrollUp(this ConsoleInput sb, int n = 1)
    {
        return sb.Append($"\x1b[{n}S");
    }

    public static ConsoleInput ScrollDown(this ConsoleInput sb, int n = 1)
    {
        return sb.Append($"\x1b[{n}T");
    }

    public static ConsoleInput SaveCurser(this ConsoleInput sb)
    {
        return sb.Append($"\x1b[s");
    }

    public static ConsoleInput RestoreCursor(this ConsoleInput sb)
    {
        return sb.Append($"\x1b[u");
    }


    public static ConsoleInput Foreground256Color(this ConsoleInput sb, int color)
    {
        return sb.Append($"\x1b[38;5;{color}m");
    }

    public static ConsoleInput Background256Color(this ConsoleInput sb, int color)
    {
        return sb.Append($"\x1b[48;5;{color}m");
    }

    // True color (24-bit)
    public static ConsoleInput ForegroundTrueColor(this ConsoleInput sb, int r, int g, int b)
    {
        return sb.Append($"\x1b[38;2;{r};{g};{b}m");
    }

    public static ConsoleInput BackgroundTrueColor(this ConsoleInput sb, int r, int g, int b)
    {
        return sb.Append($"\x1b[48;2;{r};{g};{b}m");
    }




}

