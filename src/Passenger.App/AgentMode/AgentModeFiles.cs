using Passenger.AgentMode.Gates;
using Passenger.AgentMode.Issues;
using Passenger.Core;
using Passenger.Text;

namespace Passenger.App.AgentMode;

/// <summary>A window's agent mode files: the folder's board read off the window's thread, and each change written as the
/// window writes any file it changes itself (<paramref name="change"/>, MainWindow's): one command Undo takes back whole,
/// an open file taking the text written. A change is worked out from the board read again just before, never from one
/// read earlier, so a file changed meanwhile is changed as it is now.</summary>
public sealed class AgentModeFiles(string folder, Func<string, IReadOnlyCollection<string>, Func<string?>, Task<string?>> change)
{
    /// <summary>The board as last read; empty until read.</summary>
    public IssueBoard Board { get; private set; } = IssueBoard.Of(folder, [], []);

    /// <summary>The board read anew, on the window's thread.</summary>
    public event EventHandler<IssueBoard>? BoardRead;

    /// <summary>Reads the folder's files again.</summary>
    public async Task<IssueBoard> ReadAsync()
    {
        UiThread.Check();
        Board = await BackgroundWork.Run(() => IssueBoard.Read(folder));
        BoardRead?.Invoke(this, Board);
        return Board;
    }

    /// <summary>Writes the change <paramref name="planned"/> works out from the board as it is now, as one command, then reads
    /// the board again. Null once written, else why not, nothing of it left written.</summary>
    public async Task<string?> ChangeAsync(Func<IssueBoard, IssueChange> planned)
    {
        IssueChange made;
        try
        {
            made = planned(await ReadAsync());
        }
        catch (MoveRefused refused)
        {
            return refused.Message;
        }
        string? refused = await change(made.Title, [.. made.Writes.Select(w => w.Path)], () => Write(made.Writes));
        await ReadAsync();
        return refused;
    }

    /// <summary>Each file of a change: a new one only where none is there yet, in the folder's own line breaks; another in
    /// its own encoding. Null once all are written, else why the first that was not.</summary>
    private static string? Write(IReadOnlyList<FileWrite> writes)
    {
        foreach (FileWrite write in writes)
        {
            if (write.New)
            {
                if (FileText.SaveNewOnly(write.Path, write.Text) is null)
                {
                    return $"{Path.GetFileName(write.Path)} is there already";
                }
            }
            else if (FileText.Load(write.Path) is { } had)
            {
                FileText.SaveOver(write.Path, had, write.Text);
            }
            else
            {
                return $"{Path.GetFileName(write.Path)} is not text, so it was left as it is";
            }
        }
        return null;
    }
}
