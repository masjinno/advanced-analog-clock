using System.Collections.ObjectModel;
using System.Windows.Threading;
using AdvancedAnalogClock.Domain;

namespace AdvancedAnalogClock.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;
    private double _hourAngle;
    private double _minuteAngle;
    private double _secondAngle;

    public MainWindowViewModel()
    {
        TickMarks = new ReadOnlyCollection<ClockTickMark>(CreateTickMarks());
        NumberMarks = new ReadOnlyCollection<ClockNumberMark>(CreateNumberMarks());

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
}
