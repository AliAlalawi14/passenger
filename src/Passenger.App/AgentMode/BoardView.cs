using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Passenger.AgentMode.Issues;
using Passenger.Core;

namespace Passenger.App.AgentMode;

/// <summary>The board: a milestone's issues as cards in five columns, from the backlog to landed, the milestone chosen above
/// them, and a banner naming each file that could not be read. One card is chosen on the whole board; the arrows move along
/// a column and across to the next, and Enter or a double click asks for the card's file. The columns keep their width and
/// scroll sideways in a narrow window, never squeezed.</summary>
public sealed class BoardView : DockPanel
{
    /// <summary>The least width a column takes.</summary>
    public const double ColumnWidth = 200;

    /// <summary>The milestone choice for the issues of no milestone.</summary>
    public const string NoMilestone = "No milestone";

    private readonly FormDropDown milestones = new() { Name = "Milestone" };
    private readonly TextBlock summary = new() { Classes = { StyleClasses.Caption, StyleClasses.Muted }, VerticalAlignment = VerticalAlignment.Center };
    private readonly Banner unread = Banner.Wrapping(error: true);
    private readonly Dictionary<BoardColumn, (TextBlock Title, ListBox List)> columns = [];
    private IssueBoard? board;
    private IReadOnlyList<string?> choices = [];
    private bool filling;

    public BoardView()
    {
        milestones.SelectionChanged += (_, _) =>
        {
            if (!filling)
            {
                ShowCards();
            }
        };
        unread.Link("Open", () =>
        {
            if (board?.Unread is [var first, ..])
            {
                OpenRequested?.Invoke(this, (first.Path, first.Line - 1));
            }
        });
        Grid across = new() { MinWidth = ColumnWidth * BoardColumns.All.Count };
        foreach ((BoardColumn column, string _) in BoardColumns.All)
        {
            across.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ListBox list = new() { Classes = { StyleClasses.Tree } };
            ReusedRows.Use(list, IssueCard.Row);
            ListKeys.Act(list, new ListActs { Open = () => Open(list), Keys = e => Across(list, e) });
            list.SelectionChanged += (_, _) => OnlyChosen(list);
            TextBlock title = ListRow.Heading();
            DockPanel.SetDock(title, Dock.Top);
            DockPanel stack = new() { Margin = new Thickness(Gaps.Tight, 0), Children = { title, list } };
            Grid.SetColumn(stack, columns.Count);
            across.Children.Add(stack);
            columns[column] = (title, list);
        }
        StackPanel head = new() { Orientation = Orientation.Horizontal, Spacing = Gaps.Wide, Margin = new Thickness(Gaps.Normal), Children = { milestones, summary } };
        DockPanel.SetDock(unread, Dock.Top);
        DockPanel.SetDock(head, Dock.Top);
        Children.Add(unread);
        Children.Add(head);
        ScrollViewer sideways = new()
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = across,
        };
        // A scroller gives its content no bound, so the columns share the room it shows, never less than their least.
        sideways.SizeChanged += (_, e) => across.Width = Math.Max(across.MinWidth, e.NewSize.Width);
        Children.Add(sideways);
    }

    /// <summary>A card's file asked for, or a file that could not be read, with the line to show (from 0).</summary>
    public event EventHandler<(string Path, int Line)>? OpenRequested;

    /// <summary>The milestone shown: its title, or null for the issues of none.</summary>
    public string? Milestone => milestones.SelectedIndex is var at and >= 0 && at < choices.Count ? choices[at] : null;

    /// <summary>The cards a column shows, in order.</summary>
    public IReadOnlyList<BoardCard> CardsIn(BoardColumn column) => columns[column].List.ItemsSource as IReadOnlyList<BoardCard> ?? [];

    /// <summary>The list of a column, which the arrows and Enter act in.</summary>
    public ListBox ListOf(BoardColumn column) => columns[column].List;

    /// <summary>What the banner says of the files that could not be read; null while every file was read.</summary>
    public string? Unread => unread.Said;

    /// <summary>Shows a board read anew, the milestone shown kept where it still has issues or a file, else the first by name.</summary>
    public void Show(IssueBoard read)
    {
        UiThread.Check();
        string? was = Milestone;
        bool chosen = milestones.SelectedIndex >= 0;
        board = read;
        choices = BoardColumns.Milestones(read);
        filling = true;
        milestones.ItemsSource = choices.Select(c => c ?? NoMilestone).ToList();
        milestones.SelectedIndex = chosen && choices.Contains(was) ? choices.ToList().IndexOf(was) : choices.Count > 0 ? 0 : -1;
        filling = false;
        milestones.IsVisible = choices.Count > 0;
        unread.Say(read.Unread.Count == 0 ? null
            : $"{Plural.Of(read.Unread.Count, "file")} could not be read: " + string.Join("; ", read.Unread.Select(u => $"{Path.GetFileName(u.Path)}, line {u.Line}: {u.Why}")));
        ShowCards();
    }

    /// <summary>Gives the keys to the first card on the board, where there is one.</summary>
    public bool TakeKeys() => columns.Values.Select(c => c.List).FirstOrDefault(l => l.ItemCount > 0) is { } first && ListKeys.Hold(first, chooseFirst: true);

    private void ShowCards()
    {
        if (board is null)
        {
            return;
        }
        IReadOnlyDictionary<BoardColumn, IReadOnlyList<BoardCard>> cards = BoardColumns.Cards(board, Milestone);
        foreach ((BoardColumn column, string name) in BoardColumns.All)
        {
            (TextBlock title, ListBox list) = columns[column];
            list.ItemsSource = cards[column];
            title.Text = $"{name} · {cards[column].Count}";
        }
        summary.Text = Plural.Of(cards.Values.Sum(c => c.Count), "issue");
    }

    private void Open(ListBox list)
    {
        if (list.SelectedItem is BoardCard card)
        {
            OpenRequested?.Invoke(this, (card.Path, 0));
        }
    }

    /// <summary>One card chosen on the whole board: choosing one in a column lets go of another's.</summary>
    private void OnlyChosen(ListBox chosen)
    {
        if (chosen.SelectedItem is null)
        {
            return;
        }
        foreach ((TextBlock _, ListBox list) in columns.Values.Where(c => c.List != chosen))
        {
            list.SelectedItem = null;
        }
    }

    /// <summary>Left and Right take the keys to the nearest column that side with a card, its card at the same place or its last.</summary>
    private bool Across(ListBox from, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.Left or Key.Right))
        {
            return false;
        }
        List<ListBox> lists = [.. columns.Values.Select(c => c.List)];
        int step = e.Key == Key.Left ? -1 : 1;
        for (int at = lists.IndexOf(from) + step; at >= 0 && at < lists.Count; at += step)
        {
            if (lists[at].ItemCount > 0)
            {
                lists[at].SelectedIndex = Math.Min(Math.Max(from.SelectedIndex, 0), lists[at].ItemCount - 1);
                ListKeys.Hold(lists[at]);
                return true;
            }
        }
        return true;
    }
}
