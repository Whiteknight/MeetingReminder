namespace MeetingReminder.Domain.Calendars;

public readonly record struct CalendarName(string Name) : IEquatable<CalendarName>, IEquatable<string>
{
    public static implicit operator CalendarName(string name) => new(name);

    public bool IsValid => !string.IsNullOrWhiteSpace(Name);

    public bool Equals(string? other) => Name.Equals(other);

    public bool Equals(string? other, StringComparison comparison) => Name.Equals(other, comparison);
}
