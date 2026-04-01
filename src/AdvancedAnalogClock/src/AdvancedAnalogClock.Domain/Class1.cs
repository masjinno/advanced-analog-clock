namespace AdvancedAnalogClock.Domain;

public readonly record struct ClockAngles(double HourAngle, double MinuteAngle, double SecondAngle);

public static class ClockMath
{
	public static ClockAngles CalculateAngles(DateTime now, bool tickSecondHand = true)
	{
		var second = tickSecondHand ? now.Second : now.Second + (now.Millisecond / 1000.0d);
		var minute = now.Minute + (second / 60.0d);
		var hour = (now.Hour % 12) + (minute / 60.0d);

		return new ClockAngles(
			HourAngle: hour * 30.0d,
			MinuteAngle: minute * 6.0d,
			SecondAngle: second * 6.0d);
	}
}
