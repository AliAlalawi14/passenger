using Avalonia.Controls;
using Passenger.App.ToolWindows;

namespace Passenger.App.AgentMode;

/// <summary>Agent mode's page, over the window's rows in place of the editors and tool windows, which go on beneath it
/// unseen. It says only what is to come until the folder's issues and their agents have a board of their own.</summary>
public sealed class AgentModePage : UserControl
{
    /// <summary>What the page says while it has nothing to show.</summary>
    public const string Coming = "Agent mode: the board of this folder's issues comes here";

    public AgentModePage()
    {
        Focusable = true;
        Bind(BackgroundProperty, this.GetResourceObservable(TokenKeys.Editor_Background));
        Empty = new EmptyState(Coming);
        Content = Empty;
    }

    /// <summary>What the page says in place of the board.</summary>
    public EmptyState Empty { get; }
}
