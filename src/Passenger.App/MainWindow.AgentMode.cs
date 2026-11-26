using Avalonia.Controls;
using Passenger.AgentMode.Issues;
using Passenger.App.AgentMode;
using Passenger.App.Workspace;
using Passenger.Core;
using Passenger.Editor;
using Passenger.Text;
using A = Passenger.App.AppActions;

namespace Passenger.App;

/// <summary>Agent mode and Human mode, the IDE as it is. Agent mode's page lies over the window's rows below the toolbar,
/// whose switch, a segment on while the page shows, goes back; what it covers is hidden, never ended, so the editors, the
/// terminal and what runs go on, and the editor takes the keys again where it had them. Only the person switches, as it
/// changes what they look at; closing the folder goes back to Human mode, as the page is the folder's.</summary>
public partial class MainWindow
{
    private AgentModePage? agentPage;

    /// <summary>Agent mode's page while it shows; null in Human mode.</summary>
    public AgentModePage? AgentModePage => agentPage;

    /// <summary>The folder's agent mode files, read again each time Agent mode opens; null until it first opens.</summary>
    public AgentModeFiles? AgentModeFiles { get; private set; }

    private BoardActions? boardActions;

    private void AddAgentMode()
    {
        actions!.Add(new IdeAction(A.AgentMode, "Agent Mode", "IDE", _ => SwitchMode())
        {
            WhyNot = c => AskedByAgent(c) ? Refusals.ModeIsThePersons : agentPage is null ? NeedsFolder(c) : null,
            IsChecked = _ => agentPage is not null,
            Menus = [new("View", 0, 0)],
        });
        Segments.Made(AgentModeButton, "Agent");
        ActionButtons.Bind(AgentModeButton, A.AgentMode, actions, id => Request(id));
        // The toggle turns itself as it is clicked; it shows the mode in force, a refused switch included.
        AgentModeButton.Click += (_, _) => AgentModeButton.IsChecked = agentPage is not null;
        string? BoardWhyNot(ActionContext c) => AskedByAgent(c) ? Refusals.IssuesAreThePersons : agentPage is null ? Refusals.AgentModeClosed : null;
        actions.Add(new IdeAction(A.NewIssue, "New Issue…", "Agent Mode", _ => OnBoard(b => b.NewIssueAsync())) { WhyNot = BoardWhyNot });
        actions.Add(new IdeAction(A.NewMilestone, "New Milestone…", "Agent Mode", _ => OnBoard(b => b.NewMilestoneAsync())) { WhyNot = BoardWhyNot });
    }

    /// <summary>A change the person makes from the board, refused to an agent's input into the window.</summary>
    private void OnBoard(Func<BoardActions, Task> act)
    {
        if (!RefusedToAgentsInput(Refusals.IssuesAreThePersons) && boardActions is { } board)
        {
            IdeErrors.Observe(act(board));
        }
    }

    private void SwitchMode()
    {
        UiThread.Check();
        if (RefusedToAgentsInput(Refusals.ModeIsThePersons))
        {
            return;
        }
        if (agentPage is null)
        {
            ShowAgentMode();
        }
        else
        {
            LeaveAgentMode();
        }
    }

    private void ShowAgentMode()
    {
        AgentModePage page = new();
        Grid.SetRow(page, 1);
        foreach (Control under in Rows.Children.OfType<Control>().Where(c => Grid.GetRow(c) == 1))
        {
            under.IsVisible = false;
        }
        Rows.Children.Add(page);
        agentPage = page;
        AgentModeButton.IsChecked = true;
        page.Focus();
        page.Board.OpenRequested += (_, place) =>
        {
            LeaveAgentMode();
            NavigateTo(place.Path, new TextPosition(Math.Max(0, place.Line), 0));
        };
        AgentModeFiles = new AgentModeFiles(Folder!, (title, paths, write) => ChangeFileAsync(title, paths, write));
        AgentModeFiles.BoardRead += (_, board) => agentPage?.Show(board);
        boardActions = new BoardActions(AgentModeFiles, page.Board, (title, initial, refused) => AskNameAsync(title, initial, refused), why => Refuse(why));
        page.Board.KeyAction = e => keys.Keymap.ActionFor(new Shortcut(Keystroke.From(e)));
        page.Board.RenameRequested += (_, card) => OnBoard(b => b.RenameAsync(card));
        page.Board.DropRequested += (_, card) => OnBoard(b => b.DropAsync(card));
        page.Board.MoveToRequested += (_, asked) => OnBoard(b => b.MoveToAsync(asked.Card, asked.To));
        page.Board.MoveRequested += (_, asked) => OnBoard(b => b.MoveAsync(asked.Card, asked.Step));
        page.Board.PlaceRequested += (_, asked) => OnBoard(b => b.PlaceAsync(asked.Card, asked.To, asked.Index));
        foreach ((Button button, string id) in (ReadOnlySpan<(Button, string)>)[(page.Board.NewIssueButton, A.NewIssue), (page.Board.NewMilestoneButton, A.NewMilestone)])
        {
            button.Content = actions![id]!.Title;
            ActionButtons.Bind(button, id, actions, action => Request(action));
        }
        IdeErrors.Observe(AgentModeFiles.ReadAsync());
    }

    /// <summary>The board read again while it shows, as the folder's watcher hears a change of its files (another editor's,
    /// a git pull's, Passenger's own), or hears it lost changes, which may have been any.</summary>
    private void AgentModeFolderChanged(FolderChanges changes)
    {
        if (agentPage is null || AgentModeFiles is not { } files || Folder is not { } folder)
        {
            return;
        }
        string kept = Path.Combine(folder, IssueFolder.Kept);
        if (changes.Lost || changes.Paths.Any(path => PathNames.Within(path, kept)))
        {
            IdeErrors.Observe(files.ReadAsync());
        }
    }

    /// <summary>Human mode again: the page gone, what it covered shown as it was, the editor given the keys it had.</summary>
    private void LeaveAgentMode()
    {
        if (agentPage is not { } page)
        {
            return;
        }
        agentPage = null;
        Rows.Children.Remove(page);
        foreach (Control under in Rows.Children.OfType<Control>().Where(c => Grid.GetRow(c) == 1))
        {
            under.IsVisible = true;
        }
        AgentModeButton.IsChecked = false;
        if (Editors.Active is not null)
        {
            FocusEditor();
        }
    }
}
