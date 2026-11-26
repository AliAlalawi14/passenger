using Passenger.AgentMode.Issues;

namespace Passenger.UnitTests.AgentMode;

/// <summary>The board's columns: each state in its column with the tag it needs, the two that wait for the person marked,
/// a dropped issue left out, and a milestone's cards in its list's order before those it does not list.</summary>
public class BoardColumnsTests
{
    [Fact]
    public void Each_state_stands_in_its_column_with_the_tag_it_needs() => Cases.Each<(IssueState State, BoardColumn? Column, string? Tag, bool Waits)>(
    [
        (IssueState.Draft, BoardColumn.Backlog, "draft", false),
        (IssueState.Backlog, BoardColumn.Backlog, null, false),
        (IssueState.Planning, BoardColumn.Planning, null, false),
        (IssueState.PlanWaiting, BoardColumn.Planning, "plan waiting", true),
        (IssueState.Building, BoardColumn.Building, null, false),
        (IssueState.Testing, BoardColumn.Building, "testing", false),
        (IssueState.ChangesRequested, BoardColumn.Building, "changes requested", false),
        (IssueState.ReviewWaiting, BoardColumn.Review, "review waiting", true),
        (IssueState.Landing, BoardColumn.Review, "landing", false),
        (IssueState.Landed, BoardColumn.Landed, null, false),
        (IssueState.Dropped, null, null, false),
    ], row => Assert.Equal((row.Column, row.Tag, row.Waits), (BoardColumns.Of(row.State), BoardColumns.TagOf(row.State), BoardColumns.WaitsForPerson(row.State))));

    [Fact]
    public void A_milestones_cards_follow_its_list_then_the_issues_it_does_not_list() => Cases.All(
        ("the cards of a column come in the list's order, then by number, a dropped issue left out", () =>
        {
            IReadOnlyDictionary<BoardColumn, IReadOnlyList<BoardCard>> cards = BoardColumns.Cards(Board(), "0.5");
            Assert.Equal<int>([18, 15, 11], cards[BoardColumn.Backlog].Select(c => c.Id));
            Assert.Equal<int>([16], cards[BoardColumn.Planning].Select(c => c.Id));
            Assert.Empty(cards[BoardColumn.Landed]);
        }),
        ("the milestones are each one named, by name, then the issues of none", () =>
        {
            Assert.Equal<string?>(["0.4", "0.5", null], BoardColumns.Milestones(Board()));
            Assert.Equal<int>([7], BoardColumns.Cards(Board(), null)[BoardColumn.Backlog].Select(c => c.Id));
        }));

    /// <summary>Milestone 0.5 listing 18 then 15, with 11 it does not list, 16 waiting on its plan and 20 dropped; 9 named
    /// milestone 0.4, which has no file; 7 in none.</summary>
    private static IssueBoard Board() => IssueBoard.Of("/work",
        [
            ("/work/a.md", Issue(15, "backlog", "0.5")),
            ("/work/b.md", Issue(18, "backlog", "0.5")),
            ("/work/c.md", Issue(11, "backlog", "0.5")),
            ("/work/d.md", Issue(16, "plan-waiting", "0.5")),
            ("/work/e.md", Issue(20, "dropped", "0.5")),
            ("/work/f.md", Issue(9, "building", "0.4")),
            ("/work/g.md", Issue(7, "backlog", "")),
        ],
        [("/work/0.5.md", "---\ntitle: 0.5\n---\n## Issues\n1. #18\n2. #15\n3. #16\n")]);

    private static string Issue(int id, string state, string milestone) => $"---\nid: {id}\ntitle: Issue {id}\nstate: {state}\nmilestone: {milestone}\n---\n";
}
