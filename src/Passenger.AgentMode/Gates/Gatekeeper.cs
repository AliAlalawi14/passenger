using Passenger.AgentMode.Issues;

namespace Passenger.AgentMode.Gates;

/// <summary>Which move of an issue's state each mover may make, so that nothing reaches main without the person: the
/// person makes any move; an agent only its own work's (planning, building, testing, reporting); Passenger only the
/// lander's. A plan's approval, a change's approval, a drop and a draft taken in are the person's alone, so no path an
/// agent and Passenger take together goes from a plan to building or from a change to landing.</summary>
public static class Gatekeeper
{
    /// <summary>Every move made by one other than the person, as one table.</summary>
    private static readonly (IssueState From, IssueState To, Mover By)[] Moves =
    [
        (IssueState.Backlog, IssueState.Planning, Mover.Agent),
        (IssueState.Planning, IssueState.PlanWaiting, Mover.Agent),
        (IssueState.Building, IssueState.Testing, Mover.Agent),
        (IssueState.Testing, IssueState.Building, Mover.Agent),
        (IssueState.Testing, IssueState.ReviewWaiting, Mover.Agent),
        (IssueState.ChangesRequested, IssueState.Building, Mover.Agent),
        (IssueState.Landing, IssueState.Landed, Mover.Passenger),
        (IssueState.Landing, IssueState.ReviewWaiting, Mover.Passenger),
    ];

    /// <summary>The moves one other than the person may make, as the table has them.</summary>
    public static IEnumerable<(IssueState From, IssueState To)> MovesOf(Mover by) => Moves.Where(m => m.By == by).Select(m => (m.From, m.To));

    /// <summary>Why <paramref name="by"/> may not move an issue from <paramref name="from"/> to <paramref name="to"/>, in the
    /// person's words; null where it may, the person always.</summary>
    public static string? WhyNot(IssueState from, IssueState to, Mover by)
    {
        if (by == Mover.Person || from == to || Array.Exists(Moves, m => m.From == from && m.To == to && m.By == by))
        {
            return null;
        }
        return (from, to) switch
        {
            (IssueState.PlanWaiting, IssueState.Building) => "Only you approve a plan: the agent waits for your OK",
            (IssueState.ReviewWaiting, IssueState.Landing) => "Only you approve a change before it lands",
            (_, IssueState.Dropped) => "Only you drop an issue",
            (IssueState.Draft, _) => "Only you take a draft into the backlog",
            (_, IssueState.Building) when from is IssueState.Backlog or IssueState.Planning => "Every issue's plan waits for your OK before building",
            (_, IssueState.Landing or IssueState.Landed) => "Only Passenger's lander lands a change, once you approved it",
            _ => $"{(by == Mover.Agent ? "An agent" : "Passenger")} may not move an issue from {IssueStates.WordOf(from)} to {IssueStates.WordOf(to)}",
        };
    }
}

/// <summary>A move of an issue's state the Gatekeeper refused, with why in the person's words.</summary>
public sealed class MoveRefused(string why) : InvalidOperationException(why);
