namespace AdvancedAnalogClock.Domain.Tests;

public class ClockMathTests
{
    [Fact]
    public void CalculateAngles_UsesTickSecond_WhenEnabled()
    {
        var now = new DateTime(2026, 4, 1, 10, 5, 12, 750, DateTimeKind.Local);

        var result = ClockMath.CalculateAngles(now, tickSecondHand: true);

        Assert.Equal(302.6d, result.HourAngle, precision: 6);
        Assert.Equal(31.2d, result.MinuteAngle, precision: 6);
        Assert.Equal(72.0d, result.SecondAngle, precision: 6);
    }

    [Fact]
    public void CalculateAngles_UsesFractionalSecond_WhenDisabled()
    {
        var now = new DateTime(2026, 4, 1, 10, 5, 12, 500, DateTimeKind.Local);

        var result = ClockMath.CalculateAngles(now, tickSecondHand: false);

        Assert.Equal(302.6041666667d, result.HourAngle, precision: 6);
        Assert.Equal(31.25d, result.MinuteAngle, precision: 6);
        Assert.Equal(75.0d, result.SecondAngle, precision: 6);
    }

    [Fact]
    public void CalculateAngles_MapsMidnightToZeroAngles()
    {
        var now = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Local);

        var result = ClockMath.CalculateAngles(now, tickSecondHand: true);

        Assert.Equal(0.0d, result.HourAngle, precision: 6);
        Assert.Equal(0.0d, result.MinuteAngle, precision: 6);
        Assert.Equal(0.0d, result.SecondAngle, precision: 6);
    }
}