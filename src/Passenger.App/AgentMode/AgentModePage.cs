using Avalonia.Controls;
using Avalonia.Input;
using Passenger.AgentMode.Issues;
using Passenger.App.ToolWindows;

namespace Passenger.App.AgentMode;

/// <summary>Agent mode's page, over the window's rows in place of the editors and tool windows, which go on beneath it
/// unseen: the folder's board, or while the folder has no issue, where its issues live.</summary>
public sealed class AgentModePage : UserControl
{
    /// <summary>What the page says while the folder has no issue.</summary>
    public const string NoIssues = "No issues yet: each issue is a Markdown file in .passenger/issues";

    public AgentModePage()
    {
        Focusable = true;
        Bind(BackgroundProperty, this.GetResourceObservable(TokenKeys.Editor_Background));
        Empty = new EmptyState(NoIssues);
        Board = new BoardView { IsVisible = false };
        Content = new Panel { Children = { Board, Empty } };
    }

    /// <summary>What the page says in place of the board.</summary>
    public EmptyState Empty { get; }

    public BoardView Board { get; }

    /// <summary>Shows the folder's files as read: the board where there is an issue or a file that could not be read, else
    /// the empty page; the first card given the keys where the page had them.</summary>
    public void Show(IssueBoard board)
    {
        bool none = board.Issues.Count == 0 && board.Unread.Count == 0;
        Empty.IsVisible = none;
        Board.IsVisible = !none;
        Board.Show(board);
        if (IsFocused && !none)
        {
            // The board was hidden until now, so its cards have no rows to take the keys until it is laid out.
            UpdateLayout();
            Board.TakeKeys();
        }
    }

    /// <summary>The keys given the page itself (as it opens, or a click on its empty room) go on to the board's first card.</summary>
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (Board.IsVisible && e.Source == this)
        {
            Board.TakeKeys();
        }
    }
}
