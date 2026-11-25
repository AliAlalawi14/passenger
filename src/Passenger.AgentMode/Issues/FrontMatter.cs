using Passenger.Text;

namespace Passenger.AgentMode.Issues;

/// <summary>The "key: value" lines between a file's opening "---" line and the next, as an issue or a milestone file
/// begins. Each key is read where it stands, so a change rewrites its value alone and every other character (the
/// person's own keys, comments, spacing and line breaks) stays as written.</summary>
public sealed class FrontMatter
{
    /// <summary>The line that opens and closes the keys.</summary>
    public const string Fence = "---";

    private FrontMatter(string text, IReadOnlyList<Field> fields, int end, int endLine)
    {
        Text = text;
        Fields = fields;
        End = end;
        EndLine = endLine;
        BodyStart = TextNavigation.NextLineStart(text, end);
        LineBreak = LineBreakOf(text);
    }

    /// <summary>One key's line: its key, its value trimmed, where that value stands in the text, and its line from 1.</summary>
    public readonly record struct Field(string Key, string Value, int ValueStart, int ValueEnd, int Line);

    /// <summary>The whole file's text.</summary>
    public string Text { get; }

    public IReadOnlyList<Field> Fields { get; }

    /// <summary>Where the closing "---" line starts, before which a key not yet written goes.</summary>
    public int End { get; }

    /// <summary>The closing "---" line's number, from 1, where a key missing is said to be missed.</summary>
    public int EndLine { get; }

    /// <summary>Where what follows the keys begins.</summary>
    public int BodyStart { get; }

    /// <summary>The file's own line break, which what is written into it takes.</summary>
    public string LineBreak { get; }

    /// <summary>A key's value, or null where the file does not write it.</summary>
    public string? this[string key] => FieldOf(key)?.Value;

    public Field? FieldOf(string key)
    {
        foreach (Field field in Fields)
        {
            if (field.Key == key)
            {
                return field;
            }
        }
        return null;
    }

    /// <summary>The keys of <paramref name="text"/>, or the line that stops them being read and why.</summary>
    public static FileRead<FrontMatter> Read(string text)
    {
        if (text[..TextNavigation.LineEnd(text, 0)].TrimEnd() != Fence)
        {
            return FileRead<FrontMatter>.Misread(1, "The file does not begin with a --- line, which opens its keys");
        }
        List<Field> fields = [];
        int line = 2;
        for (int start = TextNavigation.NextLineStart(text, 0); start < text.Length; start = TextNavigation.NextLineStart(text, start), line++)
        {
            int end = TextNavigation.LineEnd(text, start);
            string written = text[start..end];
            if (written.TrimEnd() == Fence)
            {
                return FileRead<FrontMatter>.Of(new FrontMatter(text, fields, start, line));
            }
            if (written.Trim().Length == 0 || written.TrimStart().StartsWith('#'))
            {
                continue;
            }
            int colon = written.IndexOf(':');
            if (colon <= 0 || written[..colon].Trim().Length == 0)
            {
                return FileRead<FrontMatter>.Misread(line, $"Line {line} is not a \"key: value\" line: {written.Trim()}");
            }
            string key = written[..colon].Trim();
            if (fields.FindIndex(f => f.Key == key) is var earlier and >= 0)
            {
                return FileRead<FrontMatter>.Misread(line, $"The key \"{key}\" is written twice, on lines {fields[earlier].Line} and {line}");
            }
            int valueStart = start + colon + 1;
            while (valueStart < end && text[valueStart] is ' ' or '\t')
            {
                valueStart++;
            }
            int valueEnd = end;
            while (valueEnd > valueStart && text[valueEnd - 1] is ' ' or '\t')
            {
                valueEnd--;
            }
            fields.Add(new Field(key, text[valueStart..valueEnd], valueStart, valueEnd, line));
        }
        return FileRead<FrontMatter>.Misread(line - 1, "The keys the --- line on line 1 opens are never closed by another --- line");
    }

    /// <summary>The text with <paramref name="key"/> set to <paramref name="value"/>, made one line: its value rewritten
    /// where it is written, else a line of its own added before the closing "---".</summary>
    public string With(string key, string value)
    {
        string one = value.ReplaceLineEndings(" ").Trim();
        if (FieldOf(key) is not { } field)
        {
            return Text[..End] + key + ": " + one + LineBreak + Text[End..];
        }
        bool spaced = field.ValueStart > 0 && Text[field.ValueStart - 1] != ':';
        return Text[..field.ValueStart] + (spaced || one.Length == 0 ? one : " " + one) + Text[field.ValueEnd..];
    }

    /// <summary>The line break a text's first line ends with, "\n" for a text of one line.</summary>
    public static string LineBreakOf(string text)
    {
        int end = text.IndexOf('\n');
        return end > 0 && text[end - 1] == '\r' ? "\r\n" : "\n";
    }
}
