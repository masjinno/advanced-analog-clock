namespace AdvancedAnalogClock.Domain.Models;

public sealed class CalendarEventItem
{
    public required string Subject { get; init; }

    public required DateTimeOffset StartLocal { get; init; }

    public required DateTimeOffset EndLocal { get; init; }

    public bool IsAllDay { get; init; }

    public string? Location { get; init; }

    public string? BodyPreview { get; init; }

    public string? JoinUrl { get; init; }
}