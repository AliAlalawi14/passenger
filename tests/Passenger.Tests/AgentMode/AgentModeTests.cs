using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Passenger.AgentMode.Issues;
using Passenger.App;
using Passenger.App.AgentMode;
using Passenger.Core;
using Passenger.Editor;
using Passenger.Text;

namespace Passenger.Tests;

/// <summary>Agent mode as a person meets it from the toolbar: its page over the IDE, the IDE beneath it hidden but going
/// on, and the switch back bringing the file, its caret and the keys where they were; closing the folder leaves it. Its
/// files are written as the window writes its own: a new issue in the folder's line breaks, taken back by one Undo.</summary>
[SameOnEverySystem]
public sealed class AgentModeTests : IDisposable
{
    private readonly string folder = TestFolders.Create("agent-mode-");

    public void Dispose() => TestFolders.Delete(folder);

    /// <summary>Agent mode's checks as one flow, each step the check one test made in an instance of its own; a later
    /// phase's check joins it as a step.</summary>
    [AvaloniaFact]
    public void Agent_mode_is_a_page_over_the_IDE_that_the_person_switches_to_and_back()
    {
        Flow.StepApart<AgentModeTests>("agent mode covers the IDE and the switch back finds the file and caret as they were", t => t.Agent_mode_covers_the_IDE_and_the_switch_back_finds_the_file_and_caret_as_they_were());
        Flow.StepApart<AgentModeTests>("a new issue lands in the folders line breaks with its milestone and one undo takes both back", t => t.A_new_issue_lands_in_the_folders_line_breaks_with_its_milestone_and_one_undo_takes_both_back());
        Flow.StepApart<AgentModeTests>("the board shows a milestones cards by state in its lists order follows a file changed on disk and opens a card by Enter", t => t.The_board_shows_a_milestones_cards_by_state_in_its_lists_order_follows_a_file_changed_on_disk_and_opens_a_card_by_Enter());
    }

    private void Agent_mode_covers_the_IDE_and_the_switch_back_finds_the_file_and_caret_as_they_were()
    {
        string notes = Path.Combine(PathNames.Canonical(folder), "notes.txt");
        File.WriteAllText(notes, "first line\nsecond line\n");
        MainWindow window = Ui.OpenFolder(folder, languageServer: false);
        window.OpenFile(notes);
        Ui.Settle();
        TextPosition caret = new(1, 4);
        window.EditorArea.CodeEditor.GoTo(caret);
        ToggleButton agent = window.FindControl<ToggleButton>("AgentModeButton")!;

        Ui.Click(agent);
        AgentModePage page = window.AgentModePage ?? throw new InvalidOperationException("The Agent switch showed no Agent mode page");
        Assert.True(page.IsEffectivelyVisible && page.IsKeyboardFocusWithin, "Agent mode's page is not shown with the keys, so the person's typing goes to the hidden editor");
        Assert.False(window.EditorArea.IsEffectivelyVisible, "The editors still show beside Agent mode's page");
        Assert.True(page.Empty.IsVisible && page.Empty.Text == AgentModePage.NoIssues, "A folder with no issue does not say where its issues live");
        Assert.True(agent.IsChecked == true, "The Agent switch is not on while Agent mode is in force");
        Assert.Equal(notes, window.EditorArea.Active?.Path);

        Ui.Click(agent);
        Assert.Null(window.AgentModePage);
        Assert.True(window.EditorArea.IsEffectivelyVisible, "Leaving Agent mode did not show the editors again");
        Assert.Equal((notes, caret), (window.EditorArea.Active?.Path, window.EditorArea.CodeEditor.CaretPosition));
        Assert.True(window.EditorArea.CodeEditor.IsKeyboardFocusWithin, "Back in the IDE the editor does not have the keys, so typing goes nowhere");
        Assert.True(agent.IsChecked == false, "The Agent switch is still on after leaving Agent mode");

        Ui.Click(agent);
        Assert.Null(window.CloseSolution());
        Ui.Settle();
        Assert.True(window.AgentModePage is null && window.EditorArea.IsEffectivelyVisible, "Closing the folder left Agent mode's page over the Welcome screen");
        window.Close();
    }

    private void A_new_issue_lands_in_the_folders_line_breaks_with_its_milestone_and_one_undo_takes_both_back()
    {
        string root = PathNames.Canonical(folder);
        File.WriteAllText(Path.Combine(root, ".editorconfig"), "root = true\n\n[*]\nend_of_line = crlf\n");
        MainWindow window = Ui.OpenFolder(folder, languageServer: false);
        ToggleButton agent = window.FindControl<ToggleButton>("AgentModeButton")!;
        Ui.Click(agent);
        AgentModeFiles files = window.AgentModeFiles ?? throw new InvalidOperationException("Agent mode opened with no files of the folder's");

        string? refused = Ui.Await(files.ChangeAsync(board => IssueChanges.NewIssue(board, "Fix price rounding", "0.5")), "the new issue's write");
        Assert.Null(refused);
        string issue = Path.Combine(IssueFolder.IssuesOf(root), "1-fix-price-rounding.md");
        string milestone = Path.Combine(IssueFolder.MilestonesOf(root), "0.5.md");
        Assert.True(File.Exists(issue) && File.Exists(milestone), "A new issue wrote no issue file, or no milestone listing it");
        Assert.True(File.ReadAllText(issue).Contains("state: backlog\r\n", StringComparison.Ordinal), "A new issue's file is not in the folder's .editorconfig line breaks");
        Assert.True(File.ReadAllText(milestone).Contains("1. #1 Fix price rounding\r\n", StringComparison.Ordinal), "The milestone does not list the new issue");
        Assert.Equal<int>([1], files.Board.Issues.Select(i => i.Value.Id));

        // Undo moves what the command made to the Trash, which it asks the person first: agreed to here, as a person would.
        List<string> asked = [];
        window.Confirm = (title, _, _) =>
        {
            asked.Add(title);
            return Task.FromResult(true);
        };
        Ui.Click(agent);
        Ui.Run(window, AppActions.FocusExplorer);
        Ui.Run(window, EditorActions.Undo);
        Ui.WaitFor(() => !File.Exists(issue) && !File.Exists(milestone), "one Undo to take back the new issue and the milestone it made");
        Assert.Equal<string>(["Undo"], asked);
        window.Close();
    }

    private void The_board_shows_a_milestones_cards_by_state_in_its_lists_order_follows_a_file_changed_on_disk_and_opens_a_card_by_Enter()
    {
        string root = PathNames.Canonical(folder);
        string issues = IssueFolder.IssuesOf(root);
        Directory.CreateDirectory(issues);
        Directory.CreateDirectory(IssueFolder.MilestonesOf(root));
        string Issue(int id, string state, string milestone) => $"---\nid: {id}\ntitle: Issue {id}\nstate: {state}\nmilestone: {milestone}\n---\n";
        File.WriteAllText(Path.Combine(issues, "15-a.md"), Issue(15, "building", "0.5"));
        File.WriteAllText(Path.Combine(issues, "16-b.md"), Issue(16, "plan-waiting", "0.5"));
        File.WriteAllText(Path.Combine(issues, "18-c.md"), Issue(18, "backlog", "0.5"));
        File.WriteAllText(Path.Combine(issues, "12-d.md"), Issue(12, "landed", "0.5"));
        File.WriteAllText(Path.Combine(issues, "20-e.md"), Issue(20, "dropped", "0.5"));
        File.WriteAllText(Path.Combine(issues, "7-f.md"), Issue(7, "backlog", ""));
        File.WriteAllText(Path.Combine(issues, "21-half-written.md"), "---\nid: 21\n");
        File.WriteAllText(Path.Combine(IssueFolder.MilestonesOf(root), "0.5.md"), "---\ntitle: 0.5\n---\n\n## Issues\n1. #18 Issue 18\n2. #16 Issue 16\n3. #15 Issue 15\n4. #12 Issue 12\n");
        MainWindow window = Ui.OpenFolder(folder, languageServer: false);
        Ui.Click(window.FindControl<ToggleButton>("AgentModeButton")!);
        BoardView board = window.AgentModePage!.Board;
        Ui.WaitFor(() => board.IsEffectivelyVisible && board.CardsIn(BoardColumn.Landed).Count == 1, "the board to show the folder's issues");

        Assert.Equal("0.5", board.Milestone);
        Assert.Equal<int>([18], board.CardsIn(BoardColumn.Backlog).Select(c => c.Id));
        Assert.True(board.CardsIn(BoardColumn.Planning) is [{ Id: 16, Tag: "plan waiting", WaitsForPerson: true }], "Issue 16's plan waits for the person, and its card does not say so");
        Assert.Equal<int>([15], board.CardsIn(BoardColumn.Building).Select(c => c.Id));
        Assert.Empty(board.CardsIn(BoardColumn.Review));
        Assert.True(board.Unread?.Contains("21-half-written.md, line 2", StringComparison.Ordinal) == true, $"The file broken by hand is not named with its line: {board.Unread}");
        Assert.True(board.ListOf(BoardColumn.Backlog).IsKeyboardFocusWithin, $"The board's first card does not have the keys, so the arrows move nothing: {window.FocusManager?.GetFocusedElement()?.GetType().Name} has them");

        Ui.Press(window, "RIGHT");
        Assert.True(board.ListOf(BoardColumn.Planning) is { IsKeyboardFocusWithin: true, SelectedItem: BoardCard { Id: 16 } }, "Right did not take the keys to the Planning column's card");

        File.WriteAllText(Path.Combine(issues, "16-b.md"), Issue(16, "review-waiting", "0.5"));
        Ui.WaitFor(() => board.CardsIn(BoardColumn.Review) is [{ Id: 16 }], "the card of a file changed on disk to move to Review");

        Assert.True(ListKeys.Hold(board.ListOf(BoardColumn.Review), chooseFirst: true), "Review's card could not take the keys");
        Ui.Press(window, "ENTER");
        Assert.True(window.AgentModePage is null && window.EditorArea.Active?.Path == Path.Combine(issues, "16-b.md"), "Enter on a card did not open its issue's file in the editor");
        window.Close();
    }
}
