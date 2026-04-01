namespace AdvancedAnalogClock.App.ViewModels;

public sealed class ClockTickMark
{
    public required double X1 { get; init; }

    public required double Y1 { get; init; }

    public required double X2 { get; init; }

    public required double Y2 { get; init; }

    public required bool IsHourMark { get; init; }
}
