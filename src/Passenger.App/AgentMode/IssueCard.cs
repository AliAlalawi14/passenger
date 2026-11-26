using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Passenger.AgentMode.Issues;

namespace Passenger.App.AgentMode;

/// <summary>An issue's card, a row of its column's list: its number muted in the code font, its title, and at its end the
/// tag its state needs, in the warning's colour where that state waits for the person (a plan or a change to look at).</summary>
public static class IssueCard
{
    /// <summary>A card's row, made once and handed each card its list shows.</summary>
    public static Control Row()
    {
        TextBlock number = new() { Classes = { StyleClasses.Code, StyleClasses.Muted }, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, Gaps.Small, 0) };
        TextBlock title = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Border tag = Tags.Of(null);
        Border waits = Tags.Of(null, TokenKeys.Warning);
        DockPanel.SetDock(number, Dock.Left);
        DockPanel.SetDock(tag, Dock.Right);
        DockPanel.SetDock(waits, Dock.Right);
        DockPanel row = new() { Height = ListRow.Height, Margin = new Thickness(Gaps.Normal, 0), Children = { number, tag, waits, title } };
        return row.Showing<BoardCard>(card =>
        {
            number.Text = $"#{card.Id}";
            title.Text = card.Title;
            Tags.WordsOf(card.WaitsForPerson ? waits : tag).Text = card.Tag;
            tag.IsVisible = card.Tag is not null && !card.WaitsForPerson;
            waits.IsVisible = card.Tag is not null && card.WaitsForPerson;
        });
    }
}
