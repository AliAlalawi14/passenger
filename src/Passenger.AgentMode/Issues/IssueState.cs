namespace Passenger.AgentMode.Issues;

/// <summary>Where an issue stands, from a draft the person has not yet taken to landed on main. Which move between them
/// is allowed, and which waits for the person, is the Gatekeeper's to say, never the file's.</summary>
public enum IssueState
{
    Draft,
    Backlog,
    Planning,
    PlanWaiting,
    Building,
    Testing,
    ReviewWaiting,
    Landing,
    Landed,
    ChangesRequested,
    Dropped,
}

/// <summary>Each state as its file writes it ("plan-waiting"), the one place those words are kept.</summary>
public static class IssueStates
{
    private static readonly (IssueState State, string Word)[] Words =
    [
        (IssueState.Draft, "draft"),
        (IssueState.Backlog, "backlog"),
        (IssueState.Planning, "planning"),
        (IssueState.PlanWaiting, "plan-waiting"),
        (IssueState.Building, "building"),
        (IssueState.Testing, "testing"),
        (IssueState.ReviewWaiting, "review-waiting"),
        (IssueState.Landing, "landing"),
        (IssueState.Landed, "landed"),
        (IssueState.ChangesRequested, "changes-requested"),
        (IssueState.Dropped, "dropped"),
    ];

    /// <summary>The word a file writes for <paramref name="state"/>.</summary>
    public static string WordOf(IssueState state) => Array.Find(Words, w => w.State == state).Word;

    /// <summary>The state a file's word names, as written, or null for a word that names none.</summary>
    public static IssueState? Of(string word) => Array.FindIndex(Words, w => w.Word == word) is var at and >= 0 ? Words[at].State : null;

    /// <summary>Every state's word, as a file that names none is told them.</summary>
    public static string Listed => string.Join(", ", Words.Select(w => w.Word));
}
