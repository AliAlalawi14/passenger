namespace Passenger.AgentMode.Issues;

/// <summary>A board's five columns, left to right as work moves.</summary>
public enum BoardColumn
{
    Backlog,
    Planning,
    Building,
    Review,
    Landed,
}

/// <summary>One issue as its card shows it: its number, title and file, the column it stands in, the tag its state needs
/// beside its title (null for none), and whether that state waits for the person.</summary>
public sealed record BoardCard(int Id, string Title, string Path, BoardColumn Column, string? Tag, bool WaitsForPerson);

/// <summary>Which column each state stands in and how a milestone's cards are laid in them: by the milestone's list, then
/// the issues it does not list by number. A dropped issue stands in none.</summary>
public static class BoardColumns
{
    /// <summary>Each column with its title, in order.</summary>
    public static IReadOnlyList<(BoardColumn Column, string Title)> All { get; } =
    [
        (BoardColumn.Backlog, "Backlog"),
        (BoardColumn.Planning, "Planning"),
        (BoardColumn.Building, "Building"),
        (BoardColumn.Review, "Review"),
        (BoardColumn.Landed, "Landed"),
    ];

    /// <summary>The column a state stands in; null for a dropped issue, which the board leaves out.</summary>
    public static BoardColumn? Of(IssueState state) => state switch
    {
        IssueState.Draft or IssueState.Backlog => BoardColumn.Backlog,
        IssueState.Planning or IssueState.PlanWaiting => BoardColumn.Planning,
        IssueState.Building or IssueState.Testing or IssueState.ChangesRequested => BoardColumn.Building,
        IssueState.ReviewWaiting or IssueState.Landing => BoardColumn.Review,
        IssueState.Landed => BoardColumn.Landed,
        _ => null,
    };

    /// <summary>The state an issue moved to a column takes: the column's own, a review waiting for the person.</summary>
    public static IssueState StateOf(BoardColumn column) => column switch
    {
        BoardColumn.Backlog => IssueState.Backlog,
        BoardColumn.Planning => IssueState.Planning,
        BoardColumn.Building => IssueState.Building,
        BoardColumn.Review => IssueState.ReviewWaiting,
        _ => IssueState.Landed,
    };

    /// <summary>The words a card shows beside its title for a state its column alone does not say; null for one it does.</summary>
    public static string? TagOf(IssueState state) => state switch
    {
        IssueState.Draft => "draft",
        IssueState.PlanWaiting => "plan waiting",
        IssueState.Testing => "testing",
        IssueState.ChangesRequested => "changes requested",
        IssueState.ReviewWaiting => "review waiting",
        IssueState.Landing => "landing",
        _ => null,
    };

    /// <summary>Whether a state waits for the person: a plan to approve, or a change to review.</summary>
    public static bool WaitsForPerson(IssueState state) => state is IssueState.PlanWaiting or IssueState.ReviewWaiting;

    /// <summary>The milestones a board can show, by name: each milestone's file and each an issue names, then null for the
    /// issues of none where there are any.</summary>
    public static IReadOnlyList<string?> Milestones(IssueBoard board)
    {
        List<string?> named = [.. board.Milestones.Select(m => m.Value.Title).Concat(board.Issues.Select(i => i.Value.Milestone).OfType<string>())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        if (board.Issues.Any(i => i.Value.Milestone is null))
        {
            named.Add(null);
        }
        return named;
    }

    /// <summary>A milestone's cards (the issues of none for null), each column's in the milestone's list order, then those
    /// it does not list by number.</summary>
    public static IReadOnlyDictionary<BoardColumn, IReadOnlyList<BoardCard>> Cards(IssueBoard board, string? milestone)
    {
        List<int> listed = milestone is not null && board.MilestoneOf(milestone) is { } file ? [.. file.Value.Order] : [];
        int PlaceOf(int id) => listed.IndexOf(id) is var at and >= 0 ? at : int.MaxValue;
        List<BoardCard> cards = [.. board.Issues
            .Where(i => i.Value.Milestone == milestone && Of(i.Value.State) is not null)
            .OrderBy(i => PlaceOf(i.Value.Id)).ThenBy(i => i.Value.Id)
            .Select(i => new BoardCard(i.Value.Id, i.Value.Title, i.Path, Of(i.Value.State)!.Value, TagOf(i.Value.State), WaitsForPerson(i.Value.State)))];
        return All.ToDictionary(c => c.Column, c => (IReadOnlyList<BoardCard>)[.. cards.Where(card => card.Column == c.Column)]);
    }
}
