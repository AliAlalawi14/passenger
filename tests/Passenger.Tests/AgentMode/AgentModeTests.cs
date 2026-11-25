using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Passenger.App;
using Passenger.App.AgentMode;
using Passenger.Core;
using Passenger.Text;

namespace Passenger.Tests;

/// <summary>Agent mode as a person meets it from the toolbar: its page over the IDE, the IDE beneath it hidden but going
/// on, and the switch back bringing the file, its caret and the keys where they were; closing the folder leaves it.</summary>
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
}
