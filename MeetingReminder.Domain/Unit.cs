namespace MeetingReminder.Domain;

/// <summary>
/// Represents a void/unit type for use in Result types where no value is returned.
/// Used when an operation succeeds but has no meaningful return value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// The singleton Unit value.
    /// </summary>
    public static Unit Value => default;

    public bool Equals(Unit other) => true;

    public override bool Equals(object? obj) => obj is Unit;

    public override int GetHashCode() => 0;

    public override string ToString() => "()";

    public static bool operator ==(Unit left, Unit right) => true;

    public static bool operator !=(Unit left, Unit right) => false;
}
