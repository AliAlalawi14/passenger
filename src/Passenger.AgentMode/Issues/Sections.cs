using Passenger.Text;

namespace Passenger.AgentMode.Issues;

/// <summary>A Markdown body's "## " sections, as an issue or a milestone keeps its parts: each heading's words and the
/// text under it up to the next such heading. A "## " line inside a fenced code block is code, never a heading.</summary>
public static class Sections
{
    /// <summary>One section: its heading's words, where its heading's line starts, and where the text under it starts and
    /// ends (the next heading's line, or the text's end).</summary>
    public readonly record struct Section(string Heading, int HeadingStart, int Start, int End);

    private const string Mark = "## ";

    /// <summary>The sections of <paramref name="text"/> from <paramref name="from"/> on, in the order written.</summary>
    public static IReadOnlyList<Section> Of(string text, int from)
    {
        List<(string Heading, int HeadingStart, int Start)> headings = [];
        bool inCode = false;
        for (int start = from; start < text.Length; start = TextNavigation.NextLineStart(text, start))
        {
            string line = text[start..TextNavigation.LineEnd(text, start)];
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                inCode = !inCode;
            }
            else if (!inCode && line.StartsWith(Mark, StringComparison.Ordinal))
            {
                headings.Add((line[Mark.Length..].Trim(), start, TextNavigation.NextLineStart(text, start)));
            }
        }
        List<Section> sections = [];
        for (int i = 0; i < headings.Count; i++)
        {
            int end = i + 1 < headings.Count ? headings[i + 1].HeadingStart : text.Length;
            sections.Add(new Section(headings[i].Heading, headings[i].HeadingStart, headings[i].Start, end));
        }
        return sections;
    }

    /// <summary>The section under <paramref name="heading"/>, matched as written, or null where there is none.</summary>
    public static Section? Find(string text, int from, string heading)
    {
        foreach (Section section in Of(text, from))
        {
            if (section.Heading == heading)
            {
                return section;
            }
        }
        return null;
    }

    /// <summary>The words under <paramref name="heading"/>, blank lines round them left out; null where there is no such
    /// section.</summary>
    public static string? Text(string text, int from, string heading) =>
        Find(text, from, heading) is { } section ? text[section.Start..section.End].Trim('\r', '\n') : null;

    /// <summary>The text with the words under <paramref name="heading"/> made <paramref name="body"/>, in the text's own
    /// line breaks: in place where the section is written, else a section of its own added at the end.</summary>
    public static string With(string text, int from, string heading, string body, string lineBreak)
    {
        string words = body.Trim('\r', '\n').ReplaceLineEndings(lineBreak);
        string under = words.Length == 0 ? "" : words + lineBreak;
        if (Find(text, from, heading) is { } section)
        {
            string gap = section.End < text.Length ? lineBreak : "";
            return text[..section.Start] + under + gap + text[section.End..];
        }
        string ended = text.Length == 0 || text.EndsWith('\n') ? text : text + lineBreak;
        return ended + lineBreak + Mark + heading + lineBreak + under;
    }
}
