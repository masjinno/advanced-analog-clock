using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using AdvancedAnalogClock.Domain;
using AdvancedAnalogClock.Domain.Models;

namespace AdvancedAnalogClock.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private const int MaxScheduleLanes = 4;
    private readonly DispatcherTimer _timer;
    private static readonly Color[] ScheduleColors =
    [
        (Color)ColorConverter.ConvertFromString("#66EF4444"),
        (Color)ColorConverter.ConvertFromString("#66F59E0B"),
        (Color)ColorConverter.ConvertFromString("#6622C55E"),
        (Color)ColorConverter.ConvertFromString("#663B82F6"),
        (Color)ColorConverter.ConvertFromString("#66A855F7"),
    ];

    // Use a contrast-first sequence so neighboring schedules avoid similar hues.
    private static readonly int[] ScheduleColorOrder = [0, 2, 4, 1, 3];

    private double _hourAngle;
    private double _minuteAngle;
    private double _secondAngle;

    public MainWindowViewModel()
    {
        TickMarks = new ReadOnlyCollection<ClockTickMark>(CreateTickMarks());
        NumberMarks = new ReadOnlyCollection<ClockNumberMark>(CreateNumberMarks());
        ScheduleRanges = new ObservableCollection<ClockScheduleRangeMark>();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _timer.Tick += OnTick;
        UpdateClock();
        _timer.Start();
    }

    public IReadOnlyList<ClockTickMark> TickMarks { get; }

    public IReadOnlyList<ClockNumberMark> NumberMarks { get; }

    public ObservableCollection<ClockScheduleRangeMark> ScheduleRanges { get; }

    public double HourAngle
    {
        get => _hourAngle;
        private set => SetProperty(ref _hourAngle, value);
    }

    public double MinuteAngle
    {
        get => _minuteAngle;
        private set => SetProperty(ref _minuteAngle, value);
    }

    public double SecondAngle
    {
        get => _secondAngle;
        private set => SetProperty(ref _secondAngle, value);
    }

    // Keep this for a future digital assist display without changing VM shape.
    public string CurrentTimeText => DateTime.Now.ToString("HH:mm:ss");

    public void Dispose()
    {
        _timer.Tick -= OnTick;
        _timer.Stop();
    }

    public void UpdateScheduleRanges(IReadOnlyList<CalendarEventItem> events)
    {
        ScheduleRanges.Clear();

        var dayStart = new DateTimeOffset(DateTime.Today, TimeZoneInfo.Local.GetUtcOffset(DateTime.Today));
        var dayEnd = dayStart.AddDays(1);

        var timedEvents = events
            .Where(x => !x.IsAllDay)
            .Select(x =>
            {
                var start = x.StartLocal < dayStart ? dayStart : x.StartLocal;
                var end = x.EndLocal > dayEnd ? dayEnd : x.EndLocal;
                return new RenderScheduleItem(x, start, end);
            })
            .Where(x => x.End > x.Start)
            .OrderBy(x => x.Start)
            .ThenBy(x => x.End)
            .ToList();

        var laneAssignments = AssignLanes(timedEvents);

        for (var i = 0; i < timedEvents.Count; i++)
        {
            var renderItem = timedEvents[i];
            if (!laneAssignments.TryGetValue(renderItem.Source, out var lane))
            {
                continue;
            }

            foreach (var segment in BuildSegments(renderItem.Start, renderItem.End, lane))
            {
                var color = ScheduleColors[ScheduleColorOrder[i % ScheduleColorOrder.Length]];
                var fillBrush = new SolidColorBrush(color);
                var strokeBrush = new SolidColorBrush(Color.Multiply(color, 1.15f));
                fillBrush.Freeze();
                strokeBrush.Freeze();

                ScheduleRanges.Add(new ClockScheduleRangeMark
                {
                    Geometry = segment,
                    Fill = fillBrush,
                    Stroke = strokeBrush,
                    ToolTip = BuildToolTip(renderItem.Source),
                    Event = renderItem.Source,
                });
            }
        }
    }

    private static List<ClockTickMark> CreateTickMarks()
    {
        var marks = new List<ClockTickMark>(capacity: 60);

        for (var i = 0; i < 60; i++)
        {
            var isHour = i % 5 == 0;
            var angle = (Math.PI / 180.0d) * ((i * 6.0d) - 90.0d);
            var outerRadius = 140.0d;
            var innerRadius = isHour ? 118.0d : 128.0d;

            marks.Add(new ClockTickMark
            {
                X1 = 150.0d + (innerRadius * Math.Cos(angle)),
                Y1 = 150.0d + (innerRadius * Math.Sin(angle)),
                X2 = 150.0d + (outerRadius * Math.Cos(angle)),
                Y2 = 150.0d + (outerRadius * Math.Sin(angle)),
                IsHourMark = isHour
            });
        }

        return marks;
    }

    private static List<ClockNumberMark> CreateNumberMarks()
    {
        var numbers = new List<ClockNumberMark>(capacity: 12);

        for (var hour = 1; hour <= 12; hour++)
        {
            var angle = (Math.PI / 180.0d) * ((hour * 30.0d) - 90.0d);
            var radius = 98.0d;

            numbers.Add(new ClockNumberMark
            {
                Text = hour.ToString(),
                X = 150.0d + (radius * Math.Cos(angle)),
                Y = 150.0d + (radius * Math.Sin(angle))
            });
        }

        return numbers;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        UpdateClock();
    }

    private void UpdateClock()
    {
        var angles = ClockMath.CalculateAngles(DateTime.Now, tickSecondHand: true);

        HourAngle = angles.HourAngle;
        MinuteAngle = angles.MinuteAngle;
        SecondAngle = angles.SecondAngle;

        // Notify in case a digital display is added and bound later.
        OnCurrentTimeTextChanged();
    }

    private void OnCurrentTimeTextChanged()
    {
        OnPropertyChanged(nameof(CurrentTimeText));
    }

    private static Dictionary<CalendarEventItem, int> AssignLanes(IReadOnlyList<RenderScheduleItem> items)
    {
        var assignments = new Dictionary<CalendarEventItem, int>();
        var active = new List<ActiveLaneItem>();

        var groupedByStart = items
            .GroupBy(x => x.Start)
            .OrderBy(x => x.Key);

        foreach (var startGroup in groupedByStart)
        {
            var start = startGroup.Key;

            active.RemoveAll(x => x.End <= start);
            var usedLanes = active
                .Select(x => x.Lane)
                .ToHashSet();

            var orderedStarts = startGroup
                .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.Source.JoinUrl))
                .ThenBy(x => x.End)
                .ToList();

            foreach (var item in orderedStarts)
            {
                int? lane = null;
                for (var i = 0; i < MaxScheduleLanes; i++)
                {
                    if (!usedLanes.Contains(i))
                    {
                        lane = i;
                        break;
                    }
                }

                if (lane is null)
                {
                    continue;
                }

                assignments[item.Source] = lane.Value;
                usedLanes.Add(lane.Value);
                active.Add(new ActiveLaneItem(lane.Value, item.End));
            }
        }

        return assignments;
    }

    private static IEnumerable<PathGeometry> BuildSegments(DateTimeOffset start, DateTimeOffset end, int lane)
    {
        const double minutesPerDial = 12.0d * 60.0d;
        const double laneStep = 16.0d;
        const double laneThickness = 12.0d;

        var outerRadius = 136.0d - (lane * laneStep);
        var innerRadius = outerRadius - laneThickness;

        var startMinute = ToDialMinute(start);
        var endMinute = ToDialMinute(end);

        while (endMinute <= startMinute)
        {
            endMinute += minutesPerDial;
        }

        if (endMinute - startMinute > minutesPerDial)
        {
            endMinute = startMinute + minutesPerDial;
        }

        var cursor = startMinute;
        while (cursor < endMinute)
        {
            var nextBoundary = Math.Floor(cursor / minutesPerDial + 1.0d) * minutesPerDial;
            var segmentEnd = Math.Min(endMinute, nextBoundary);

            var startOnDial = cursor % minutesPerDial;
            var endOnDial = segmentEnd % minutesPerDial;
            if (endOnDial <= startOnDial)
            {
                endOnDial += minutesPerDial;
            }

            if (Math.Abs(endOnDial - startOnDial) < 0.001d)
            {
                cursor = segmentEnd;
                continue;
            }

            var geometry = CreateRingSector(startOnDial, endOnDial, outerRadius, innerRadius);
            if (geometry is not null)
            {
                yield return geometry;
            }

            cursor = segmentEnd;
        }
    }

    private static PathGeometry? CreateRingSector(double startMinute, double endMinute, double outerRadius, double innerRadius)
    {
        const double centerX = 150.0d;
        const double centerY = 150.0d;

        var sweepMinutes = endMinute - startMinute;
        if (sweepMinutes <= 0.25d)
        {
            return null;
        }

        if (sweepMinutes >= 719.75d)
        {
            sweepMinutes = 719.75d;
            endMinute = startMinute + sweepMinutes;
        }

        var startAngle = (startMinute * 0.5d) - 90.0d;
        var endAngle = (endMinute * 0.5d) - 90.0d;
        var sweepAngle = endAngle - startAngle;

        var outerStart = ToPoint(centerX, centerY, outerRadius, startAngle);
        var outerEnd = ToPoint(centerX, centerY, outerRadius, endAngle);
        var innerEnd = ToPoint(centerX, centerY, innerRadius, endAngle);
        var innerStart = ToPoint(centerX, centerY, innerRadius, startAngle);
        var isLargeArc = sweepAngle > 180.0d;

        var figure = new PathFigure
        {
            StartPoint = outerStart,
            IsClosed = true,
            IsFilled = true,
        };

        figure.Segments.Add(new ArcSegment
        {
            Point = outerEnd,
            Size = new Size(outerRadius, outerRadius),
            IsLargeArc = isLargeArc,
            SweepDirection = SweepDirection.Clockwise,
        });
        figure.Segments.Add(new LineSegment
        {
            Point = innerEnd,
            IsStroked = true,
        });
        figure.Segments.Add(new ArcSegment
        {
            Point = innerStart,
            Size = new Size(innerRadius, innerRadius),
            IsLargeArc = isLargeArc,
            SweepDirection = SweepDirection.Counterclockwise,
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();

        return geometry;
    }

    private static Point ToPoint(double centerX, double centerY, double radius, double angleDegrees)
    {
        var radians = Math.PI * angleDegrees / 180.0d;
        return new Point(
            centerX + (radius * Math.Cos(radians)),
            centerY + (radius * Math.Sin(radians)));
    }

    private static double ToDialMinute(DateTimeOffset value)
    {
        var local = value.LocalDateTime;
        return ((local.Hour % 12) * 60.0d) + local.Minute + (local.Second / 60.0d);
    }

    private static string BuildToolTip(CalendarEventItem calendarEvent)
    {
        return calendarEvent.Subject;
    }

    private sealed record ActiveLaneItem(int Lane, DateTimeOffset End);

    private sealed record RenderScheduleItem(CalendarEventItem Source, DateTimeOffset Start, DateTimeOffset End);
}
