using Passenger.AgentMode.Gates;
using Passenger.AgentMode.Issues;

namespace Passenger.UnitTests.AgentMode;

/// <summary>The Gatekeeper: each move an agent or Passenger may make is allowed, every other refused with why; the person
/// makes any; and no path agents and Passenger take on their own passes either of the person's two approvals.</summary>
public class GatekeeperTests
{
    [Fact]
    public void An_agent_and_Passenger_make_their_own_moves_alone_and_are_told_why_of_any_other() => Cases.All(
        ("each move of the table is allowed to its mover and to no other but the person", () =>
        {
            foreach (Mover by in (Mover[])[Mover.Agent, Mover.Passenger])
            {
                HashSet<(IssueState, IssueState)> own = [.. Gatekeeper.MovesOf(by)];
                foreach (IssueState from in Enum.GetValues<IssueState>())
                {
                    foreach (IssueState to in Enum.GetValues<IssueState>().Where(s => s != from))
                    {
                        string? why = Gatekeeper.WhyNot(from, to, by);
                        Assert.True(own.Contains((from, to)) == (why is null), $"{by} moving {from} to {to}: {why ?? "allowed"}");
                    }
                }
            }
        }),
        ("the person makes every move", () => Assert.All(
            Enum.GetValues<IssueState>().SelectMany(from => Enum.GetValues<IssueState>().Select(to => (from, to))),
            move => Assert.Null(Gatekeeper.WhyNot(move.from, move.to, Mover.Person)))),
        ("a refusal says why in the person's words", () =>
        {
            Assert.Equal("Only you approve a plan: the agent waits for your OK", Gatekeeper.WhyNot(IssueState.PlanWaiting, IssueState.Building, Mover.Agent));
            Assert.Equal("Only you approve a change before it lands", Gatekeeper.WhyNot(IssueState.ReviewWaiting, IssueState.Landing, Mover.Agent));
            Assert.Equal("Every issue's plan waits for your OK before building", Gatekeeper.WhyNot(IssueState.Backlog, IssueState.Building, Mover.Agent));
            Assert.Equal("Only you drop an issue", Gatekeeper.WhyNot(IssueState.Building, IssueState.Dropped, Mover.Passenger));
        }));

    [Fact]
    public void No_path_of_agents_and_Passenger_alone_passes_a_plan_or_a_change_the_person_has_not_approved() => Cases.Each(
        Enum.GetValues<IssueState>().Where(s => s is not (IssueState.Building or IssueState.Testing or IssueState.ChangesRequested or IssueState.Landing or IssueState.Landed)),
        start =>
        {
            // Building comes only past an approved plan and Landing past an approved change, so neither is reached alone.
            HashSet<IssueState> reached = Reached(start);
            Assert.False(reached.Contains(IssueState.Landing) || reached.Contains(IssueState.Landed), $"From {start} agents and Passenger alone reach [{string.Join(", ", reached)}]");
            if (start is not IssueState.ReviewWaiting)
            {
                Assert.False(reached.Contains(IssueState.Building), $"From {start} agents and Passenger alone reach building, with no plan approved: [{string.Join(", ", reached)}]");
            }
        });

    [Fact]
    public void A_state_change_asked_by_an_agent_that_the_Gatekeeper_refuses_writes_nothing()
    {
        IssueBoard board = IssueBoard.Of("/work", [("/work/15.md", "---\nid: 15\ntitle: A\nstate: plan-waiting\nmilestone:\n---\n")], []);
        MoveRefused refused = Assert.Throws<MoveRefused>(() => IssueChanges.NewState(board, 15, IssueState.Building, Mover.Agent));
        Assert.Equal("Only you approve a plan: the agent waits for your OK", refused.Message);
        Assert.Single(IssueChanges.NewState(board, 15, IssueState.Building, Mover.Person).Writes);
    }

    /// <summary>Every state the moves of agents and Passenger reach from <paramref name="start"/>, it among them.</summary>
    private static HashSet<IssueState> Reached(IssueState start)
    {
        (IssueState From, IssueState To)[] moves = [.. Gatekeeper.MovesOf(Mover.Agent), .. Gatekeeper.MovesOf(Mover.Passenger)];
        HashSet<IssueState> reached = [start];
        Queue<IssueState> next = new([start]);
        while (next.TryDequeue(out IssueState at))
        {
            foreach ((IssueState _, IssueState to) in moves.Where(m => m.From == at))
            {
                if (reached.Add(to))
                {
                    next.Enqueue(to);
                }
            }
        }
        return reached;
    }
}
