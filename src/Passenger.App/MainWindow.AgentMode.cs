using Avalonia.Controls;
using Passenger.App.AgentMode;
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
        AgentModeFiles = new AgentModeFiles(Folder!, (title, paths, write) => ChangeFileAsync(title, paths, write));
        IdeErrors.Observe(AgentModeFiles.ReadAsync());
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
