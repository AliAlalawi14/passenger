using System.Globalization;
using System.Text;
using Passenger.Core;

namespace Passenger.AgentMode.Issues;

/// <summary>An issue's file, <c>.passenger/issues/15-fix-price-rounding.md</c>: its keys (id, title, state, milestone)
/// between "---" lines, then its sections, What it's for, Done when, Files it may touch and Notes. Read from text and
/// changed as text, each change rewriting only what it changes, so the person's own words and keys stay as written.</summary>
public static class IssueFile
{
    public const string IdKey = "id";
    public const string TitleKey = "title";
    public const string StateKey = "state";
    public const string MilestoneKey = "milestone";

    public const string WhatItsFor = "What it's for";
    public const string DoneWhen = "Done when";
    public const string FilesItMayTouch = "Files it may touch";
    public const string Notes = "Notes";

    /// <summary>The issue <paramref name="text"/> says, or the line that stops it being read and why.</summary>
    public static FileRead<Issue> Read(string text)
    {
        FileRead<FrontMatter> read = FrontMatter.Read(text);
        if (read.Value is not { } keys)
        {
            return FileRead<Issue>.Misread(read.Line, read.Why!);
        }
        if (keys.FieldOf(IdKey) is not { } id)
        {
            return FileRead<Issue>.Misread(keys.EndLine, "The issue has no id: its keys need a line such as \"id: 15\"");
        }
        if (!int.TryParse(id.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number <= 0)
        {
            return FileRead<Issue>.Misread(id.Line, $"The issue's id is \"{id.Value}\", where it is a whole number above 0");
        }
        if (keys.FieldOf(TitleKey) is not { Value.Length: > 0 } title)
        {
            return FileRead<Issue>.Misread(keys.FieldOf(TitleKey)?.Line ?? keys.EndLine, "The issue has no title: its keys need a line such as \"title: Fix price rounding\"");
        }
        if (keys.FieldOf(StateKey) is not { } state)
        {
            return FileRead<Issue>.Misread(keys.EndLine, $"The issue has no state: its keys need a line such as \"state: backlog\" ({IssueStates.Listed})");
        }
        if (IssueStates.Of(state.Value) is not { } stands)
        {
            return FileRead<Issue>.Misread(state.Line, $"The issue's state is \"{state.Value}\", which is none of {IssueStates.Listed}");
        }
        string? milestone = keys[MilestoneKey] is { Length: > 0 } named ? named : null;
        return FileRead<Issue>.Of(new Issue(number, title.Value, stands, milestone, keys));
    }

    /// <summary>A new issue's text, in the backlog with its four sections empty, its lines broken by <paramref name="lineBreak"/>
    /// (the folder's .editorconfig's, which whoever saves it knows).</summary>
    public static Issue New(int id, string title, string? milestone, string lineBreak)
    {
        StringBuilder text = new();
        text.Append(FrontMatter.Fence).Append(lineBreak);
        text.Append(IdKey).Append(": ").Append(id.ToString(CultureInfo.InvariantCulture)).Append(lineBreak);
        text.Append(TitleKey).Append(": ").Append(title.ReplaceLineEndings(" ").Trim()).Append(lineBreak);
        text.Append(StateKey).Append(": ").Append(IssueStates.WordOf(IssueState.Backlog)).Append(lineBreak);
        text.Append(MilestoneKey).Append(": ").Append(milestone ?? "").Append(lineBreak);
        text.Append(FrontMatter.Fence).Append(lineBreak);
        foreach (string heading in (string[])[WhatItsFor, DoneWhen, FilesItMayTouch, Notes])
        {
            text.Append(lineBreak).Append("## ").Append(heading).Append(lineBreak);
        }
        return Read(text.ToString()).Sure;
    }

    public static Issue WithTitle(Issue issue, string title) => Read(issue.Keys.With(TitleKey, title)).Sure;

    public static Issue WithState(Issue issue, IssueState state) => Read(issue.Keys.With(StateKey, IssueStates.WordOf(state))).Sure;

    /// <summary>The issue in <paramref name="milestone"/>, or in none with null.</summary>
    public static Issue WithMilestone(Issue issue, string? milestone) => Read(issue.Keys.With(MilestoneKey, milestone ?? "")).Sure;

    /// <summary>The issue with the words under <paramref name="heading"/> made <paramref name="body"/>.</summary>
    public static Issue WithSection(Issue issue, string heading, string body) =>
        Read(Sections.With(issue.Text, issue.Keys.BodyStart, heading, body, issue.Keys.LineBreak)).Sure;

    /// <summary>The file's name: its id, then its title's letters and digits joined by '-', "15-fix-price-rounding.md".</summary>
    public static string FileName(int id, string title)
    {
        StringBuilder words = new();
        foreach (char c in title.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                words.Append(c);
            }
            else if (words.Length > 0 && words[^1] != '-')
            {
                words.Append('-');
            }
        }
        string slug = words.ToString().Trim('-');
        slug = slug.Length > 50 ? slug[..50].TrimEnd('-') : slug;
        string named = slug.Length == 0 ? id.ToString(CultureInfo.InvariantCulture) : $"{id.ToString(CultureInfo.InvariantCulture)}-{slug}";
        return FileNames.Safe(named + ".md");
    }
}
