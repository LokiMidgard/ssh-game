using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace SshGame.Game.Screens;

public static partial class AnsiSequenceParser
{
    [GeneratedRegex(@"^\x1b\[(?<parameters>[0-9;]*)(?<finalChar>[A-Za-z])", RegexOptions.Compiled)]
    private static partial Regex AnsiRegex();
    public static IEnumerable<Sequence> ParseAnsiSequences(ReadOnlySpan<byte> input, Encoding encoding)
    {
        return ParseAnsiSequences(encoding.GetString(input));
    }
    public static IEnumerable<Sequence> ParseAnsiSequences(string input)
    {
        for (int i = 0; i < input.Length; i++)
        {
            var match = AnsiRegex().Match(input, i);
            if (match.Success)
            {
                var parameters = ImmutableArray.CreateBuilder<int>();
                if (match.Groups[1].Value.Length > 0)
                {
                    foreach (var param in match.Groups["parameters"].Value.Split(';'))
                    {
                        if (int.TryParse(param, out int value))
                        {
                            parameters.Add(value);
                        }
                    }
                }

                yield return new AnsiSequence
                {
                    Sequence = match.Value,
                    Type = GetAnsiSequenceType(match.Groups["finalChar"].Value[0]),
                    Parameters = parameters.ToImmutableArray(),
                };
                i += match.Length - 1;// one will be added
                continue;
            }
            else if (input[i] == '\t')
            {
                yield return new LineSequence { Type = LineSequenceType.Tab };
            }
            else if (input[i] is '\n' or '\r')
            {
                if (i + 1 < input.Length && input[i + 1] is '\n' or '\r' && input[i] != input[i + 1])
                    i++;
                yield return new LineSequence { Type = LineSequenceType.LineBreak };
            }
            else if (char.IsControl(input[i]))
            {
                continue;
            }
            else
            {
                // no known sequence
                var text = string.Concat(input[i..].TakeWhile(x => !(char.IsControl(x) || x is '\t' or '\n' or '\r')));
                i += text.Length - 1;// one will be added
                yield return new TextSequence { Text = text };
            }
        }
    }

    private static AnsiSequenceType GetAnsiSequenceType(char finalChar)
    {
        return finalChar switch
        {
            'm' => AnsiSequenceType.TextFormat,
            'A' => AnsiSequenceType.CursorUp,
            'B' => AnsiSequenceType.CursorDown,
            'C' => AnsiSequenceType.CursorForward,
            'D' => AnsiSequenceType.CursorBackward,
            'H' or 'f' => AnsiSequenceType.CursorPosition,
            'J' => AnsiSequenceType.EraseInDisplay,
            'K' => AnsiSequenceType.EraseInLine,
            'S' => AnsiSequenceType.ScrollUp,
            'T' => AnsiSequenceType.ScrollDown,
            'E' => AnsiSequenceType.CursorNextLine,
            'F' => AnsiSequenceType.CursorPreviousLine,
            'G' => AnsiSequenceType.CursorHorizontalAbsolute,
            _ => AnsiSequenceType.Unknown
        };
    }


}
public enum LineSequenceType
{
    LineBreak,
    Tab,
}
public enum AnsiSequenceType
{
    Unknown,
    TextFormat,
    ForegroundColor,
    BackgroundColor,
    CursorUp,
    CursorDown,
    CursorForward,
    CursorBackward,
    CursorPosition,
    EraseInDisplay,
    EraseInLine,
    ScrollUp,
    ScrollDown,
    CursorNextLine,
    CursorPreviousLine,
    CursorHorizontalAbsolute,
}

public abstract class Sequence
{

}
public class TextSequence : Sequence
{
    public required string Text { get; init; }
}
public class LineSequence : Sequence
{
    public LineSequenceType Type { get; init; }
}
public class AnsiSequence : Sequence
{
    public AnsiSequenceType Type { get; init; }
    public required string Sequence { get; init; }
    public required ImmutableArray<int> Parameters { get; set; }

}
