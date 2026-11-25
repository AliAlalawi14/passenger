namespace Passenger.AgentMode.Issues;

/// <summary>An issue as its file says it: its number, title, state and milestone from its keys, its parts from its
/// sections, and the file's whole text, which a change rewrites in place (<see cref="IssueFile"/>).</summary>
public sealed record Issue(int Id, string Title, IssueState State, string? Milestone, FrontMatter Keys)
{
    /// <summary>The file's whole text.</summary>
    public string Text => Keys.Text;

    /// <summary>The words under a section's heading (<see cref="IssueFile.DoneWhen"/>), or null where it has none.</summary>
    public string? Section(string heading) => Sections.Text(Keys.Text, Keys.BodyStart, heading);
}
