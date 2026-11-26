using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Passenger.AgentMode.Issues;
using Passenger.App.Docking;
using Passenger.App.ToolWindows;
using Passenger.Core;

namespace Passenger.App.AgentMode;

/// <summary>The board: a milestone's issues as cards in five columns, from the backlog to landed, the milestone chosen above
/// them, and a banner naming each file that could not be read. One card is chosen on the whole board; the arrows move along
/// a column and across to the next, and Enter or a double click asks for the card's file. A card dragged lands before the
/// card it is let go over, in its column or another, the place lit up meanwhile; Escape gives up a drag. The columns keep
/// their width and scroll sideways in a narrow window, never squeezed.</summary>
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
    private readonly Panel surface = new();
    private readonly DropOverlay landing = new();
    private readonly DragGesture<BoardCard> drag;
    private bool dragging;

    public BoardView()
    {
        drag = new DragGesture<BoardCard>(surface, landing, (_, at) => PlaceAt(at) is { } place ? new DropZone(place.Line) : null, Dropped);
        drag.DraggingChanged += (_, change) => dragging = change.Dragging;
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (dragging && e.Key == Key.Escape)
            {
                drag.Cancel();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
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
            ListKeys.Act(list, new ListActs
            {
                Open = () => Open(list),
                Edit = () => Act(list, card => RenameRequested?.Invoke(this, card)),
                Remove = () => Act(list, card => DropRequested?.Invoke(this, card)),
                Keys = e => Across(list, e) || Moved(list, e),
            });
            ListMenus.Attach(list, () => list.SelectedItem is BoardCard card ? MenuOf(list, card) : []);
            list.SelectionChanged += (_, _) => OnlyChosen(list);
            // A list takes a press for its own choice, so its drag hears the pointer ahead of it, handled or not.
            list.AddHandler(PointerPressedEvent, (_, e) => Pressed(list, e), RoutingStrategies.Tunnel, handledEventsToo: true);
            list.AddHandler(PointerMovedEvent, (_, e) => Moving(list, e), RoutingStrategies.Tunnel, handledEventsToo: true);
            list.AddHandler(PointerReleasedEvent, (_, e) => Released(list, e), RoutingStrategies.Tunnel, handledEventsToo: true);
            TextBlock title = ListRow.Heading();
            DockPanel.SetDock(title, Dock.Top);
            DockPanel stack = new() { Margin = new Thickness(Gaps.Tight, 0), Children = { title, list } };
            Grid.SetColumn(stack, columns.Count);
            across.Children.Add(stack);
            columns[column] = (title, list);
        }
        StackPanel head = new() { Orientation = Orientation.Horizontal, Spacing = Gaps.Wide, Margin = new Thickness(Gaps.Normal), Children = { milestones, summary, NewIssueButton, NewMilestoneButton } };
        DockPanel.SetDock(unread, Dock.Top);
        DockPanel.SetDock(head, Dock.Top);
        Children.Add(unread);
        Children.Add(head);
        ScrollViewer sideways = new()
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = surface,
        };
        surface.Children.Add(across);
        surface.Children.Add(landing);
        // A scroller gives its content no bound, so the columns share the room it shows, never less than their least.
        sideways.SizeChanged += (_, e) => across.Width = Math.Max(across.MinWidth, e.NewSize.Width);
        Children.Add(sideways);
    }

    /// <summary>New Issue…, over the board, which the window binds to its action.</summary>
    public Button NewIssueButton { get; } = new() { Name = "NewIssue", VerticalAlignment = VerticalAlignment.Center };

    /// <summary>New Milestone…, over the board, which the window binds to its action.</summary>
    public Button NewMilestoneButton { get; } = new() { Name = "NewMilestone", VerticalAlignment = VerticalAlignment.Center };

    /// <summary>What the window's keymap runs for a key (its Move Line Up and Down move a card), null for none.</summary>
    public Func<KeyEventArgs, string?> KeyAction { get; set; } = _ => null;

    /// <summary>The card chosen on the board, null for none.</summary>
    public BoardCard? Chosen => columns.Values.Select(c => c.List.SelectedItem).OfType<BoardCard>().FirstOrDefault();

    /// <summary>A card's title to change (F2, its menu's Rename…).</summary>
    public event EventHandler<BoardCard>? RenameRequested;

    /// <summary>A card to drop (Delete, its menu's Drop): its state dropped, its file kept.</summary>
    public event EventHandler<BoardCard>? DropRequested;

    /// <summary>A card to move to another column (its menu's Move to).</summary>
    public event EventHandler<(BoardCard Card, BoardColumn To)>? MoveToRequested;

    /// <summary>A card to move a place up (-1) or down (1) in its column, so in its milestone's list.</summary>
    public event EventHandler<(BoardCard Card, int Step)>? MoveRequested;

    /// <summary>A card's file asked for, or a file that could not be read, with the line to show (from 0).</summary>
    public event EventHandler<(string Path, int Line)>? OpenRequested;

    /// <summary>The milestone shown: its title, or null for the issues of none.</summary>
    public string? Milestone => milestones.SelectedIndex is var at and >= 0 && at < choices.Count ? choices[at] : null;

    /// <summary>The cards a column shows, in order.</summary>
    public IReadOnlyList<BoardCard> CardsIn(BoardColumn column) => CardsInList(columns[column].List);

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
        int? had = Chosen?.Id;
        bool keys = IsKeyboardFocusWithin;
        IReadOnlyDictionary<BoardColumn, IReadOnlyList<BoardCard>> cards = BoardColumns.Cards(board, Milestone);
        foreach ((BoardColumn column, string name) in BoardColumns.All)
        {
            (TextBlock title, ListBox list) = columns[column];
            list.ItemsSource = cards[column];
            title.Text = $"{name} · {cards[column].Count}";
        }
        summary.Text = Plural.Of(cards.Values.Sum(c => c.Count), "issue");
        if (had is int id)
        {
            Choose(id, keys);
        }
    }

    /// <summary>Shows a milestone, where the board has it.</summary>
    public void ShowMilestone(string? title)
    {
        if (choices.ToList().IndexOf(title) is var at and >= 0)
        {
            milestones.SelectedIndex = at;
        }
    }

    /// <summary>Chooses an issue's card wherever it stands now, giving it the keys where <paramref name="keys"/>; false
    /// where the board shows no card of it.</summary>
    public bool Choose(int id, bool keys = true)
    {
        foreach ((TextBlock _, ListBox list) in columns.Values)
        {
            if (CardsInList(list).ToList().FindIndex(c => c.Id == id) is var at and >= 0)
            {
                list.SelectedIndex = at;
                if (keys)
                {
                    // A card's column may have been empty until now, with no row laid out to take the keys.
                    UpdateLayout();
                    ListKeys.Hold(list);
                }
                return true;
            }
        }
        return false;
    }

    private static IReadOnlyList<BoardCard> CardsInList(ListBox list) => list.ItemsSource as IReadOnlyList<BoardCard> ?? [];

    private static void Act(ListBox list, Action<BoardCard> act)
    {
        if (list.SelectedItem is BoardCard card)
        {
            act(card);
        }
    }

    /// <summary>A card's menu: open it, rename it, move it to another column or a place up or down, drop it.</summary>
    private IReadOnlyList<Control> MenuOf(ListBox list, BoardCard card)
    {
        IReadOnlyList<BoardCard> column = CardsInList(list);
        int at = column.ToList().FindIndex(c => c.Id == card.Id);
        string? unordered = Milestone is null ? "Issues of no milestone keep no order" : null;
        return
        [
            ListMenus.Open("Open", () => Open(list)),
            ListMenus.Edit("Rename…", () => RenameRequested?.Invoke(this, card)),
            ListMenus.Sub("Move to", BoardColumns.All.Where(c => c.Column != card.Column)
                .Select(c => ListMenus.Item(c.Title, () => MoveToRequested?.Invoke(this, (card, c.Column))))),
            ListMenus.Item("Move Up", () => MoveRequested?.Invoke(this, (card, -1)), unordered ?? (at <= 0 ? "It is the column's first card" : null)),
            ListMenus.Item("Move Down", () => MoveRequested?.Invoke(this, (card, 1)), unordered ?? (at >= column.Count - 1 ? "It is the column's last card" : null)),
            ListMenus.Remove("Drop", () => DropRequested?.Invoke(this, card)),
        ];
    }

    /// <summary>A card dragged and let go: its column, and its place there among the column's other cards.</summary>
    public event EventHandler<(BoardCard Card, BoardColumn To, int Index)>? PlaceRequested;

    private void Pressed(ListBox list, PointerPressedEventArgs e)
    {
        if ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { } row && list.ItemFromContainer(row) is BoardCard card)
        {
            drag.Begin(card, e);
        }
    }

    private void Moving(ListBox list, PointerEventArgs e)
    {
        if (!e.GetCurrentPoint(list).Properties.IsLeftButtonPressed)
        {
            // Let go outside the list before it was a drag, which the list never heard.
            drag.Cancel();
            return;
        }
        // Once a drag, the pointer is the list's until the release, wherever it goes.
        if (drag.Continue(e) && e.Pointer.Captured != list)
        {
            e.Pointer.Capture(list);
        }
    }

    private void Released(ListBox list, PointerReleasedEventArgs e)
    {
        if (drag.End(e))
        {
            e.Handled = true;
        }
        if (e.Pointer.Captured == list)
        {
            e.Pointer.Capture(null);
        }
    }

    /// <summary>A card let go over a column: its place there counted among the column's other cards, as it will stand.</summary>
    private void Dropped(BoardCard card, Point at)
    {
        if (PlaceAt(at) is not { } place)
        {
            return;
        }
        int was = card.Column == place.Column ? CardsIn(place.Column).ToList().FindIndex(c => c.Id == card.Id) : -1;
        PlaceRequested?.Invoke(this, (card, place.Column, was >= 0 && was < place.Index ? place.Index - 1 : place.Index));
    }

    /// <summary>The column under a point of the board's surface, the place a card let go there takes (before the first card
    /// whose middle is below it, else after the last) and the line that shows it; null outside the columns.</summary>
    private (BoardColumn Column, int Index, Rect Line)? PlaceAt(Point at)
    {
        foreach (KeyValuePair<BoardColumn, (TextBlock Title, ListBox List)> pair in columns)
        {
            ListBox list = pair.Value.List;
            if (list.TranslatePoint(default, surface) is not { } corner || at.X < corner.X || at.X >= corner.X + list.Bounds.Width)
            {
                continue;
            }
            Rect area = new(corner, list.Bounds.Size);
            double below = area.Top;
            for (int i = 0; i < list.ItemCount; i++)
            {
                if (list.ContainerFromIndex(i) is not { } row || row.TranslatePoint(default, surface) is not { } top)
                {
                    continue;
                }
                if (at.Y < top.Y + (row.Bounds.Height / 2))
                {
                    return (pair.Key, i, LineAt(area, top.Y));
                }
                below = top.Y + row.Bounds.Height;
            }
            return (pair.Key, list.ItemCount, LineAt(area, below));
        }
        return null;
    }

    private static Rect LineAt(Rect column, double y) => new(column.Left, Math.Max(column.Top, y - 1), column.Width, 2);

    /// <summary>The keymap's Move Line Up and Down move the chosen card a place in its column.</summary>
    private bool Moved(ListBox list, KeyEventArgs e)
    {
        int step = KeyAction(e) switch
        {
            Passenger.Editor.EditorActions.MoveLineUp => -1,
            Passenger.Editor.EditorActions.MoveLineDown => 1,
            _ => 0,
        };
        if (step == 0 || list.SelectedItem is not BoardCard card)
        {
            return false;
        }
        MoveRequested?.Invoke(this, (card, step));
        return true;
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
