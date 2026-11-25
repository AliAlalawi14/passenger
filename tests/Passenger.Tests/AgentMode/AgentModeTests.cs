using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
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
        Assert.Equal(AgentModePage.Coming, page.Empty.Text);
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
    }}
