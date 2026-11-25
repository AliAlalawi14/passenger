using System.Globalization;
using Passenger.Text;

namespace Passenger.AgentMode.Issues;

/// <summary>An issue's or a milestone's file as read: where it is and what it says.</summary>
public sealed record Placed<T>(string Path, T Value);

/// <summary>A file agent mode could not read: where it is, its line from 1, and why, said to the person, never dropped.</summary>
public sealed record UnreadFile(string Path, int Line, string Why);

/// <summary>What a folder's agent mode files say at once: its issues by id, its milestones by title, and each file that
/// could not be read with its line and why. Read whole each time, from the disk (<see cref="Read"/>) or from texts.</summary>
public sealed class IssueBoard
{
    private IssueBoard(string folder, IReadOnlyList<Placed<Issue>> issues, IReadOnlyList<Placed<Milestone>> milestones, IReadOnlyList<UnreadFile> unread, int highest)
    {
        Folder = folder;
        Issues = issues;
        Milestones = milestones;
        Unread = unread;
        NextId = highest + 1;
    }

    /// <summary>The folder whose files these are.</summary>
    public string Folder { get; }

    /// <summary>The issues, by their files' names.</summary>
    public IReadOnlyList<Placed<Issue>> Issues { get; }

    /// <summary>The milestones, by their files' names.</summary>
    public IReadOnlyList<Placed<Milestone>> Milestones { get; }

    public IReadOnlyList<UnreadFile> Unread { get; }

    /// <summary>The id a new issue takes: one past the highest any issue's file holds or begins its name with, a file that
    /// could not be read among them, so no id is given twice.</summary>
    public int NextId { get; }

    public Placed<Issue>? IssueOf(int id) => Issues.FirstOrDefault(i => i.Value.Id == id);

    public Placed<Milestone>? MilestoneOf(string title) => Milestones.FirstOrDefault(m => m.Value.Title == title);

    /// <summary>The folder's files as they are on disk, each read as Passenger reads any text file (its encoding kept); a
    /// folder with none is a board with nothing on it. Blocks on the disk: the window reads it off its own thread.</summary>
    public static IssueBoard Read(string folder) => Of(folder, Texts(IssueFolder.IssuesOf(folder)), Texts(IssueFolder.MilestonesOf(folder)));

    /// <summary>What the texts given say, each with the path it was read from: two issues of one id leave the later file
    /// unread, as do two milestones of one title.</summary>
    public static IssueBoard Of(string folder, IEnumerable<(string Path, string? Text)> issueTexts, IEnumerable<(string Path, string? Text)> milestoneTexts)
    {
        List<Placed<Issue>> issues = [];
        List<Placed<Milestone>> milestones = [];
        List<UnreadFile> unread = [];
        int highest = 0;
        foreach ((string path, string? text) in issueTexts)
        {
            highest = Math.Max(highest, IdNamed(path));
            FileRead<Issue> read = text is null ? FileRead<Issue>.Misread(1, "The file is not text") : IssueFile.Read(text);
            if (read.Value is not { } issue)
            {
                unread.Add(new UnreadFile(path, read.Line, read.Why!));
            }
            else if (issues.FirstOrDefault(i => i.Value.Id == issue.Id) is { } first)
            {
                unread.Add(new UnreadFile(path, issue.Keys.FieldOf(IssueFile.IdKey)!.Value.Line, $"Issue #{issue.Id} is {System.IO.Path.GetFileName(first.Path)}'s id too"));
            }
            else
            {
                issues.Add(new Placed<Issue>(path, issue));
                highest = Math.Max(highest, issue.Id);
            }
        }
        foreach ((string path, string? text) in milestoneTexts)
        {
            FileRead<Milestone> read = text is null ? FileRead<Milestone>.Misread(1, "The file is not text") : MilestoneFile.Read(text);
            if (read.Value is not { } milestone)
            {
                unread.Add(new UnreadFile(path, read.Line, read.Why!));
            }
            else if (milestones.FirstOrDefault(m => m.Value.Title == milestone.Title) is { } first)
            {
                unread.Add(new UnreadFile(path, milestone.Keys.FieldOf(MilestoneFile.TitleKey)!.Value.Line, $"Milestone {milestone.Title} is {System.IO.Path.GetFileName(first.Path)}'s title too"));
            }
            else
            {
                milestones.Add(new Placed<Milestone>(path, milestone));
            }
        }
        return new IssueBoard(folder, issues, milestones, unread, highest);
    }

    /// <summary>Each Markdown file of a folder, in its names' order, with its text, null for one that is not text.</summary>
    private static IEnumerable<(string Path, string? Text)> Texts(string folder) => Directory.Exists(folder)
        ? Directory.EnumerateFiles(folder, "*.md").Order(StringComparer.Ordinal).Select(path => (path, FileText.Load(path) is { IsBinary: false } read ? read.Text : null)).ToList()
        : [];

    /// <summary>The id an issue's file name begins with ("15-fix….md"), 0 for one that begins with none.</summary>
    private static int IdNamed(string path)
    {
        string name = System.IO.Path.GetFileName(path);
        int digits = 0;
        while (digits < name.Length && char.IsAsciiDigit(name[digits]))
        {
            digits++;
        }
        return digits > 0 && int.TryParse(name.AsSpan(0, digits), NumberStyles.None, CultureInfo.InvariantCulture, out int id) ? id : 0;
    }
}
