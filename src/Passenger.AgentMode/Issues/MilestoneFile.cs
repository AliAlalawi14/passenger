using System.Globalization;
using System.Text;
using Passenger.Text;

namespace Passenger.AgentMode.Issues;

/// <summary>A milestone's file, <c>.passenger/milestones/0.5.md</c>: its title between "---" lines, then its Goal and its
/// Issues, a numbered list ("1. #15 Fix price rounding") that is the one place the issues' order is kept, so a card
/// dragged on the board rewrites this list and no issue's file.</summary>
public static class MilestoneFile
{
    public const string TitleKey = "title";

    public const string Goal = "Goal";
    public const string Issues = "Issues";

    /// <summary>The milestone <paramref name="text"/> says, or the line that stops it being read and why.</summary>
    public static FileRead<Milestone> Read(string text)
    {
        FileRead<FrontMatter> read = FrontMatter.Read(text);
        if (read.Value is not { } keys)
        {
            return FileRead<Milestone>.Misread(read.Line, read.Why!);
        }
        if (keys.FieldOf(TitleKey) is not { Value.Length: > 0 } title)
        {
            return FileRead<Milestone>.Misread(keys.FieldOf(TitleKey)?.Line ?? keys.EndLine, "The milestone has no title: its keys need a line such as \"title: 0.5\"");
        }
        List<int> order = [];
        if (Sections.Find(text, keys.BodyStart, Issues) is { } listed)
        {
            for (int start = listed.Start; start < listed.End; start = TextNavigation.NextLineStart(text, start))
            {
                string line = text[start..TextNavigation.LineEnd(text, start)].Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                int number = LineOf(text, start);
                if (IdOf(line) is not { } id)
                {
                    return FileRead<Milestone>.Misread(number, $"Line {number} of Issues is not an issue such as \"1. #15 Fix price rounding\": {line}");
                }
                if (order.Contains(id))
                {
                    return FileRead<Milestone>.Misread(number, $"Issue #{id} is listed twice in Issues");
                }
                order.Add(id);
            }
        }
        return FileRead<Milestone>.Of(new Milestone(title.Value, order, keys));
    }

    /// <summary>A new milestone's text, with its Goal and Issues empty, its lines broken by <paramref name="lineBreak"/>.</summary>
    public static Milestone New(string title, string lineBreak)
    {
        StringBuilder text = new();
        text.Append(FrontMatter.Fence).Append(lineBreak);
        text.Append(TitleKey).Append(": ").Append(title.ReplaceLineEndings(" ").Trim()).Append(lineBreak);
        text.Append(FrontMatter.Fence).Append(lineBreak);
        text.Append(lineBreak).Append("## ").Append(Goal).Append(lineBreak);
        text.Append(lineBreak).Append("## ").Append(Issues).Append(lineBreak);
        return Read(text.ToString()).Sure;
    }

    public static Milestone WithTitle(Milestone milestone, string title) => Read(milestone.Keys.With(TitleKey, title)).Sure;

    /// <summary>The milestone holding <paramref name="issues"/> in this order, its Issues list written again from them, each
    /// line its place, its id and its title.</summary>
    public static Milestone WithOrder(Milestone milestone, IReadOnlyList<(int Id, string Title)> issues)
    {
        string lineBreak = milestone.Keys.LineBreak;
        StringBuilder list = new();
        for (int i = 0; i < issues.Count; i++)
        {
            string title = issues[i].Title.ReplaceLineEndings(" ").Trim();
            list.Append(CultureInfo.InvariantCulture, $"{i + 1}. #{issues[i].Id}{(title.Length > 0 ? " " + title : "")}").Append(lineBreak);
        }
        return Read(Sections.With(milestone.Text, milestone.Keys.BodyStart, Issues, list.ToString(), lineBreak)).Sure;
    }

    /// <summary>The milestone with the words under <paramref name="heading"/> made <paramref name="body"/>.</summary>
    public static Milestone WithSection(Milestone milestone, string heading, string body) =>
        Read(Sections.With(milestone.Text, milestone.Keys.BodyStart, heading, body, milestone.Keys.LineBreak)).Sure;

    /// <summary>The issue a list's line names: "1. #15 …" or "- #15 …", the id after its '#'; null for any other line.</summary>
    private static int? IdOf(string line)
    {
        int at = 0;
        if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
        {
            at = 2;
        }
        else
        {
            while (at < line.Length && char.IsAsciiDigit(line[at]))
            {
                at++;
            }
            if (at == 0 || at + 1 >= line.Length || line[at] != '.' || line[at + 1] != ' ')
            {
                return null;
            }
            at += 2;
        }
        while (at < line.Length && line[at] == ' ')
        {
            at++;
        }
        if (at >= line.Length || line[at] != '#')
        {
            return null;
        }
        int digits = at + 1;
        int end = digits;
        while (end < line.Length && char.IsAsciiDigit(line[end]))
        {
            end++;
        }
        bool ended = end == line.Length || line[end] == ' ';
        return end > digits && ended && int.TryParse(line.AsSpan(digits, end - digits), NumberStyles.None, CultureInfo.InvariantCulture, out int id) && id > 0
            ? id : null;
    }

    /// <summary>The line, from 1, that <paramref name="offset"/> is on.</summary>
    private static int LineOf(string text, int offset) => text.AsSpan(0, offset).Count('\n') + 1;
}
