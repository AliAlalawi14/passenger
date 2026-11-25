namespace Passenger.AgentMode.Issues;

/// <summary>A milestone as its file says it: its title from its keys, the issues it holds in the order they are taken
/// from its Issues section, and the file's whole text, which a change rewrites in place (<see cref="MilestoneFile"/>).</summary>
public sealed record Milestone(string Title, IReadOnlyList<int> Order, FrontMatter Keys)
{
    /// <summary>The file's whole text.</summary>
    public string Text => Keys.Text;

    /// <summary>The words under a section's heading (<see cref="MilestoneFile.Goal"/>), or null where it has none.</summary>
    public string? Section(string heading) => Sections.Text(Keys.Text, Keys.BodyStart, heading);
}
