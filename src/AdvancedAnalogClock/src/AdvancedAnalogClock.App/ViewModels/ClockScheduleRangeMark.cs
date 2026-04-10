using System.Windows.Media;
using AdvancedAnalogClock.Domain.Models;

namespace AdvancedAnalogClock.App.ViewModels;

public sealed class ClockScheduleRangeMark
{
    public required Geometry Geometry { get; init; }

    public required Brush Fill { get; init; }

    public required Brush Stroke { get; init; }

    public required string ToolTip { get; init; }

    public required CalendarEventItem Event { get; init; }
}
