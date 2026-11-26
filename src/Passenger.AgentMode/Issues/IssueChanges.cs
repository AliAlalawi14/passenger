namespace Passenger.AgentMode.Issues;

/// <summary>One file a change writes: its path, its whole new text, and whether it is made new (written only where no file
/// is there yet, in the folder's own line breaks) rather than changed in place.</summary>
public sealed record FileWrite(string Path, string Text, bool New);

/// <summary>A change of agent mode's files, named as Undo names it, and every file it writes, written as one.</summary>
public sealed record IssueChange(string Title, IReadOnlyList<FileWrite> Writes);

/// <summary>Each change the board makes, worked out from what the board read as the files it writes, nothing written yet:
/// so a change and its Undo are one, and what it writes is held by unit tests. An issue's order lives in its milestone's
/// list alone, so a change of milestone or of order writes lists, a change of state the issue's file alone.</summary>
public static class IssueChanges
{
    /// <summary>A new issue in the backlog, with the next id, at the end of <paramref name="milestone"/>'s list (made too
    /// where the folder has none of that title); in no milestone with null.</summary>
    public static IssueChange NewIssue(IssueBoard board, string title, string? milestone)
    {
        Issue issue = IssueFile.New(board.NextId, title, milestone, "\n");
        List<FileWrite> writes = [new FileWrite(IssueFolder.NewIssuePath(board.Folder, issue), issue.Text, New: true)];
        if (milestone is not null)
        {
            writes.Add(Listed(board, milestone, [.. Order(board, milestone), issue.Id], issue));
        }
        return new IssueChange($"New Issue #{issue.Id}", writes);
    }

    public static IssueChange NewState(IssueBoard board, int id, IssueState state)
    {
        Placed<Issue> issue = Sure(board, id);
        return new IssueChange($"Move #{id} to {IssueStates.WordOf(state)}", [Rewritten(issue, IssueFile.WithState(issue.Value, state))]);
    }

    /// <summary>The issue's new title, its milestone's list naming it so too.</summary>
    public static IssueChange NewTitle(IssueBoard board, int id, string title)
    {
        Placed<Issue> issue = Sure(board, id);
        Issue named = IssueFile.WithTitle(issue.Value, title);
        List<FileWrite> writes = [Rewritten(issue, named)];
        if (named.Milestone is { } milestone && board.MilestoneOf(milestone) is not null)
        {
            writes.Add(Listed(board, milestone, Order(board, milestone), named));
        }
        return new IssueChange($"Rename #{id}", writes);
    }

    /// <summary>The issue in another milestone, or none with null: out of its old list, at the end of its new one.</summary>
    public static IssueChange NewMilestone(IssueBoard board, int id, string? milestone)
    {
        Placed<Issue> issue = Sure(board, id);
        Issue moved = IssueFile.WithMilestone(issue.Value, milestone);
        List<FileWrite> writes = [Rewritten(issue, moved)];
        if (issue.Value.Milestone is { } left && left != milestone && board.MilestoneOf(left) is not null)
        {
            writes.Add(Listed(board, left, [.. Order(board, left).Where(i => i != id)], null));
        }
        if (milestone is not null && milestone != issue.Value.Milestone)
        {
            writes.Add(Listed(board, milestone, [.. Order(board, milestone).Where(i => i != id), id], moved));
        }
        return new IssueChange(milestone is null ? $"Take #{id} out of its milestone" : $"Move #{id} to {milestone}", writes);
    }

    /// <summary>A milestone's issues in a new order, its list alone written again.</summary>
    public static IssueChange NewOrder(IssueBoard board, string milestone, IReadOnlyList<int> order) =>
        new($"Reorder {milestone}", [Listed(board, milestone, order, null)]);

    /// <summary>A new milestone with no issue, its Goal and Issues empty.</summary>
    public static IssueChange NewMilestone(IssueBoard board, string title) =>
        new($"New Milestone {title.Trim()}", [new FileWrite(IssueFolder.NewMilestonePath(board.Folder, title), MilestoneFile.New(title, "\n").Text, New: true)]);

    /// <summary>The issue a place earlier (<paramref name="step"/> -1) or later (1) among its column's cards: swapped in its
    /// milestone's list with the card beside it there, those the list leaves out taken in after it by number. Null at the
    /// column's end, or for an issue of no milestone, which has no order of its own.</summary>
    public static IssueChange? MovedInColumn(IssueBoard board, int id, int step)
    {
        if (Sure(board, id).Value.Milestone is not { } milestone)
        {
            return null;
        }
        IReadOnlyList<BoardCard> column = BoardColumns.Cards(board, milestone).Values.First(c => c.Any(card => card.Id == id));
        int at = column.ToList().FindIndex(c => c.Id == id);
        if (at + step < 0 || at + step >= column.Count)
        {
            return null;
        }
        int beside = column[at + step].Id;
        List<int> order = [.. Order(board, milestone)];
        order.AddRange(board.Issues.Where(i => i.Value.Milestone == milestone && !order.Contains(i.Value.Id)).Select(i => i.Value.Id).Order());
        int mine = order.IndexOf(id);
        int theirs = order.IndexOf(beside);
        (order[mine], order[theirs]) = (order[theirs], order[mine]);
        return new IssueChange(step < 0 ? $"Move #{id} Up" : $"Move #{id} Down", [Listed(board, milestone, order, null)]);
    }

    private static Placed<Issue> Sure(IssueBoard board, int id) =>
        board.IssueOf(id) ?? throw new ArgumentException($"The board has no issue #{id}", nameof(id));

    private static FileWrite Rewritten(Placed<Issue> issue, Issue now) => new(issue.Path, now.Text, New: false);

    private static IReadOnlyList<int> Order(IssueBoard board, string milestone) => board.MilestoneOf(milestone)?.Value.Order ?? [];

    /// <summary>A milestone's list written again in <paramref name="order"/>, each line titled by the board's issue, else by
    /// <paramref name="named"/>, the issue the change makes or renames; the milestone made new where the folder has none.</summary>
    private static FileWrite Listed(IssueBoard board, string milestone, IReadOnlyList<int> order, Issue? named)
    {
        (int Id, string Title)[] lines = [.. order.Select(id => (id, named?.Id == id ? named.Title : board.IssueOf(id)?.Value.Title ?? ""))];
        if (board.MilestoneOf(milestone) is { } had)
        {
            return new FileWrite(had.Path, MilestoneFile.WithOrder(had.Value, lines).Text, New: false);
        }
        return new FileWrite(IssueFolder.NewMilestonePath(board.Folder, milestone), MilestoneFile.WithOrder(MilestoneFile.New(milestone, "\n"), lines).Text, New: true);
    }
}
