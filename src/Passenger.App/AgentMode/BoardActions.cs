using Passenger.AgentMode.Issues;

namespace Passenger.App.AgentMode;

/// <summary>What the person changes from the board: a new issue or milestone, a card renamed, moved to another column or a
/// place up or down, or dropped. Each is one change of the folder's files (<see cref="AgentModeFiles"/>), one command Undo
/// takes back, the card it was about chosen again where it went; a change refused is said in the window's words.</summary>
public sealed class BoardActions(AgentModeFiles files, BoardView board, Func<string, string, Func<string, string?>, Task<string?>> askName, Action<string> refuse)
{
    /// <summary>New Issue…: a title asked, the issue in the backlog of the milestone shown, at the end of its list.</summary>
    public async Task NewIssueAsync()
    {
        string? milestone = board.Milestone;
        if (await askName("New Issue", "", title => title.Trim().Length == 0 ? "An issue needs a title" : null) is not { } title)
        {
            return;
        }
        int made = 0;
        IssueChange Planned(IssueBoard read)
        {
            made = read.NextId;
            return IssueChanges.NewIssue(read, title.Trim(), milestone);
        }
        if (await Changed(Planned))
        {
            board.ShowMilestone(milestone);
            board.Choose(made);
        }
    }

    /// <summary>New Milestone…: a title asked that no milestone has, the milestone made and shown.</summary>
    public async Task NewMilestoneAsync()
    {
        string? Refused(string title) => title.Trim().Length == 0 ? "A milestone needs a title"
            : files.Board.MilestoneOf(title.Trim()) is not null || BoardColumns.Milestones(files.Board).Contains(title.Trim()) ? $"Milestone {title.Trim()} is there already"
            : null;
        if (await askName("New Milestone", "", Refused) is { } title && await Changed(b => IssueChanges.NewMilestone(b, title.Trim())))
        {
            board.ShowMilestone(title.Trim());
        }
    }

    /// <summary>Rename…: the card's title asked again, written in its file and its milestone's list.</summary>
    public async Task RenameAsync(BoardCard card)
    {
        if (await askName($"Rename #{card.Id}", card.Title, title => title.Trim().Length == 0 ? "An issue needs a title" : null) is { } title
            && title.Trim() != card.Title)
        {
            await Changed(b => IssueChanges.NewTitle(b, card.Id, title.Trim()), card.Id);
        }
    }

    /// <summary>Move to: the card's issue in the column's own state.</summary>
    public Task MoveToAsync(BoardCard card, BoardColumn to) => Changed(b => IssueChanges.NewState(b, card.Id, BoardColumns.StateOf(to)), card.Id);

    /// <summary>Move Up or Down: the card a place earlier or later in its column, so in its milestone's list.</summary>
    public async Task MoveAsync(BoardCard card, int step)
    {
        if (IssueChanges.MovedInColumn(files.Board, card.Id, step) is null)
        {
            refuse(board.Milestone is null ? "Issues of no milestone keep no order" : step < 0 ? $"#{card.Id} is its column's first card" : $"#{card.Id} is its column's last card");
            return;
        }
        await Changed(b => IssueChanges.MovedInColumn(b, card.Id, step) ?? new IssueChange("Move", []), card.Id);
    }

    /// <summary>A card dragged to a column and a place among its other cards: one change of its state and its milestone's
    /// list; nothing where it lands where it was.</summary>
    public async Task PlaceAsync(BoardCard card, BoardColumn to, int index)
    {
        if (IssueChanges.Placed(files.Board, card.Id, to, index) is not null)
        {
            await Changed(b => IssueChanges.Placed(b, card.Id, to, index) ?? new IssueChange("Move", []), card.Id);
        }
    }

    /// <summary>Drop: the card's issue dropped, out of the board, its file kept for Undo or the person to bring back.</summary>
    public Task DropAsync(BoardCard card) => Changed(b => IssueChanges.NewState(b, card.Id, IssueState.Dropped));

    /// <summary>Writes the change, said where refused; the card of <paramref name="chosen"/> chosen again where it went.</summary>
    private async Task<bool> Changed(Func<IssueBoard, IssueChange> planned, int? chosen = null)
    {
        if (await files.ChangeAsync(planned) is { } why)
        {
            refuse(why);
            return false;
        }
        if (chosen is int id)
        {
            board.Choose(id);
        }
        return true;
    }
}
