using Passenger.Core;

namespace Passenger.AgentMode.Issues;

/// <summary>Where a folder keeps agent mode's files: <c>.passenger/issues</c> and <c>.passenger/milestones</c>, beside its
/// code, so the person reads them, edits them by hand and keeps them in Git.</summary>
public static class IssueFolder
{
    /// <summary>The folder's own folder of agent mode's files.</summary>
    public const string Kept = ".passenger";

    public static string IssuesOf(string folder) => Path.Combine(folder, Kept, "issues");

    public static string MilestonesOf(string folder) => Path.Combine(folder, Kept, "milestones");

    /// <summary>Where a new issue's file goes, named by its id and title; one read keeps the path it was read from.</summary>
    public static string NewIssuePath(string folder, Issue issue) => Path.Combine(IssuesOf(folder), IssueFile.FileName(issue.Id, issue.Title));

    /// <summary>Where a new milestone's file goes, named by its title ("0.5.md").</summary>
    public static string NewMilestonePath(string folder, string title) => Path.Combine(MilestonesOf(folder), FileNames.Safe(title.Trim() + ".md"));
}
