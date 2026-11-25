namespace Passenger.AgentMode.Issues;

/// <summary>What reading one of agent mode's files came to: its value, or the line it could not read (from 1) and why, in
/// words the person can act on. A file the person may have written by hand is said wrong, never thrown at.</summary>
public sealed record FileRead<T>(T? Value, int Line, string? Why)
    where T : class
{
    public static FileRead<T> Of(T value) => new(value, 0, null);

    public static FileRead<T> Misread(int line, string why) => new(null, line, why);

    /// <summary>The value, where a text Passenger itself wrote must read; else what was wrong, as a fault of Passenger's.</summary>
    public T Sure => Value ?? throw new InvalidOperationException($"Agent mode wrote a file it cannot read: line {Line}: {Why}");
}
