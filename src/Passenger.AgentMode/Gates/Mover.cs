namespace Passenger.AgentMode.Gates;

/// <summary>Who asks for an issue to move: the person, an agent doing its own work, or Passenger itself (its lander).</summary>
public enum Mover
{
    Person,
    Agent,
    Passenger,
}
