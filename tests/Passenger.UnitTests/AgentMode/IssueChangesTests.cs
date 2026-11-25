using Passenger.AgentMode.Issues;

namespace Passenger.UnitTests.AgentMode;

/// <summary>Each change of agent mode's files as the files it writes, worked out from a board read from texts: what it
/// writes and nothing more, an issue's order kept in its milestone's list alone, and ids never given twice.</summary>
public class IssueChangesTests
{
    private const string Folder = "/work";

    private static readonly string Fifteen = Path.Combine(IssueFolder.IssuesOf(Folder), "15-fix-price-rounding.md");
    private static readonly string Sixteen = Path.Combine(IssueFolder.IssuesOf(Folder), "16-show-tax.md");
    private static readonly string Broken = Path.Combine(IssueFolder.IssuesOf(Folder), "21-half-written.md");
    private static readonly string Five = Path.Combine(IssueFolder.MilestonesOf(Folder), "0.5.md");

    /// <summary>Two issues of milestone 0.5, and a file of the person's they broke, whose name still holds its id.</summary>
    private static IssueBoard Board() => IssueBoard.Of(Folder,
        [
            (Fifteen, "---\nid: 15\ntitle: Fix price rounding\nstate: backlog\nmilestone: 0.5\n---\n\n## Notes\nMine.\n"),
            (Sixteen, "---\nid: 16\ntitle: Show tax\nstate: building\nmilestone: 0.5\n---\n"),
            (Broken, "---\nid: 21\n"),
        ],
        [(Five, "---\ntitle: 0.5\n---\n\n## Goal\nRight totals.\n\n## Issues\n1. #15 Fix price rounding\n2. #16 Show tax\n")]);

    [Fact]
    public void Each_change_writes_the_files_it_changes_and_no_others() => Cases.All(
        ("a board says each file it could not read and never gives a file's id again", () =>
        {
            IssueBoard board = Board();
            Assert.Equal<int>([15, 16], board.Issues.Select(i => i.Value.Id));
            Assert.Equal((Broken, 2), (board.Unread.Single().Path, board.Unread.Single().Line));
            Assert.Equal(22, board.NextId);
        }),
        ("a new issue is a new file in the backlog at the end of its milestone's list", () =>
        {
            IssueChange change = IssueChanges.NewIssue(Board(), "Round in one place", "0.5");
            Assert.Equal<(string, bool)>([(Path.Combine(IssueFolder.IssuesOf(Folder), "22-round-in-one-place.md"), true), (Five, false)], change.Writes.Select(w => (w.Path, w.New)));
            Assert.Equal((22, IssueState.Backlog, "0.5"), (IssueFile.Read(change.Writes[0].Text).Sure.Id, IssueFile.Read(change.Writes[0].Text).Sure.State, IssueFile.Read(change.Writes[0].Text).Sure.Milestone!));
            Assert.Contains("1. #15 Fix price rounding\n2. #16 Show tax\n3. #22 Round in one place\n", change.Writes[1].Text, StringComparison.Ordinal);
        }),
        ("a new issue of a milestone the folder lacks makes the milestone too", () =>
        {
            IssueChange change = IssueChanges.NewIssue(Board(), "Later", "0.6");
            Assert.Equal((Path.Combine(IssueFolder.MilestonesOf(Folder), "0.6.md"), true), (change.Writes[1].Path, change.Writes[1].New));
            Assert.Equal<int>([22], MilestoneFile.Read(change.Writes[1].Text).Sure.Order);
        }),
        ("a new state writes the issue's state alone", () =>
        {
            IssueChange change = IssueChanges.NewState(Board(), 15, IssueState.Planning);
            Assert.Equal(Fifteen, change.Writes.Single().Path);
            Assert.Equal(Board().IssueOf(15)!.Value.Text.Replace("state: backlog", "state: planning", StringComparison.Ordinal), change.Writes.Single().Text);
        }),
        ("a new title renames the issue and its line in its milestone's list", () =>
        {
            IssueChange change = IssueChanges.NewTitle(Board(), 16, "Show tax per line");
            Assert.Equal<string>([Sixteen, Five], change.Writes.Select(w => w.Path));
            Assert.Contains("2. #16 Show tax per line\n", change.Writes[1].Text, StringComparison.Ordinal);
        }),
        ("an issue moved to another milestone leaves its old list and ends its new one", () =>
        {
            IssueChange change = IssueChanges.NewMilestone(Board(), 15, "0.6");
            Assert.Equal<string>([Fifteen, Five, Path.Combine(IssueFolder.MilestonesOf(Folder), "0.6.md")], change.Writes.Select(w => w.Path));
            Assert.Equal<int>([16], MilestoneFile.Read(change.Writes[1].Text).Sure.Order);
            Assert.Equal<int>([15], MilestoneFile.Read(change.Writes[2].Text).Sure.Order);
        }),
        ("a new order writes the milestone's list alone, its goal kept", () =>
        {
            IssueChange change = IssueChanges.NewOrder(Board(), "0.5", [16, 15]);
            Assert.Equal(Five, change.Writes.Single().Path);
            Assert.Equal("---\ntitle: 0.5\n---\n\n## Goal\nRight totals.\n\n## Issues\n1. #16 Show tax\n2. #15 Fix price rounding\n", change.Writes.Single().Text);
        }));
}
