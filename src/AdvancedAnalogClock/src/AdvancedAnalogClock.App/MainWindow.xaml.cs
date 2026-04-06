using System;
using System.Threading;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using AdvancedAnalogClock.App.Models;
using AdvancedAnalogClock.App.Services;
using AdvancedAnalogClock.App.ViewModels;
using AdvancedAnalogClock.App.Views;

namespace AdvancedAnalogClock.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private enum ClockTheme
    {
        Dark,
        Light,
    }

    private readonly MainWindowViewModel _viewModel;
    private readonly IOutlookCalendarService _outlookCalendarService;
    private CancellationTokenSource? _calendarRequestCts;
    private List<CalendarEventItem> _calendarEvents = new();
    private List<CalendarEventItem> _timedEvents = new();
    private bool _calendarLoaded;
    private bool _calendarLoading;
    private string? _calendarLoadError;
    private ClockTheme _currentTheme = ClockTheme.Light;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        _outlookCalendarService = new OutlookCalendarService();
        DataContext = _viewModel;
        ApplyTheme(ClockTheme.Light);
    }

    protected override void OnClosed(EventArgs e)
    {
        _calendarRequestCts?.Cancel();
        _calendarRequestCts?.Dispose();
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (_calendarLoaded || _calendarLoading)
        {
            return;
        }

        _calendarRequestCts?.Cancel();
        _calendarRequestCts?.Dispose();
        _calendarRequestCts = new CancellationTokenSource();
        _calendarLoading = true;
        RenderCalendarLoading(CalendarMenuItem);

        try
        {
            var events = await _outlookCalendarService.GetTodayEventsAsync(_calendarRequestCts.Token);
            _calendarEvents = events
                .OrderBy(x => x.StartLocal)
                .ToList();
            _timedEvents = events
                .Where(x => !x.IsAllDay)
                .OrderBy(x => x.StartLocal)
                .ToList();

            _calendarLoadError = null;
            _calendarLoaded = true;
            _viewModel.UpdateScheduleRanges(_timedEvents);
            RenderCalendarEvents(CalendarMenuItem, _calendarEvents);
        }
        catch (OperationCanceledException)
        {
            // Ignore shutdown cancellation.
        }
        catch (Exception ex)
        {
            _calendarEvents = new List<CalendarEventItem>();
            _timedEvents = new List<CalendarEventItem>();
            _calendarLoadError = ex.Message;
            _calendarLoaded = true;
            _viewModel.UpdateScheduleRanges(Array.Empty<CalendarEventItem>());
            RenderCalendarError(CalendarMenuItem, ex.Message);
        }
        finally
        {
            _calendarLoading = false;
            _calendarRequestCts?.Dispose();
            _calendarRequestCts = null;
        }
    }

    private void OnWindowDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement sourceElement &&
            sourceElement.DataContext is ClockScheduleRangeMark)
        {
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnExitMenuClick(object sender, RoutedEventArgs e)
    {
        RequestApplicationExit();
    }

    private void OnDarkModeMenuClick(object sender, RoutedEventArgs e)
    {
        ApplyTheme(ClockTheme.Dark);
    }

    private void OnLightModeMenuClick(object sender, RoutedEventArgs e)
    {
        ApplyTheme(ClockTheme.Light);
    }

    private void OnCalendarMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem calendarMenu)
        {
            return;
        }

        if (_calendarLoading)
        {
            RenderCalendarLoading(calendarMenu);
        }
        else if (!_calendarLoaded)
        {
            calendarMenu.Items.Clear();
            calendarMenu.Items.Add(new MenuItem
            {
                Header = "予定を初期化中です",
                IsEnabled = false,
            });
        }
        else if (!string.IsNullOrWhiteSpace(_calendarLoadError))
        {
            RenderCalendarError(calendarMenu, _calendarLoadError);
        }
        else
        {
            RenderCalendarEvents(calendarMenu, _calendarEvents);
        }

        calendarMenu.IsSubmenuOpen = true;
        e.Handled = true;
    }

    private void OnScheduleRangeClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not ClockScheduleRangeMark mark)
        {
            return;
        }

        var detailWindow = new ScheduleDetailWindow(mark.Event)
        {
            Owner = this,
        };

        detailWindow.Show();
        e.Handled = true;
    }

    // Keep exit in one place so tray menu can reuse it later.
    private void RequestApplicationExit()
    {
        Close();
    }

    private static void RenderCalendarLoading(MenuItem menu)
    {
        menu.Items.Clear();
        menu.Items.Add(new MenuItem
        {
            Header = "予定を読み込み中...",
            IsEnabled = false,
        });
    }

    private static void RenderCalendarEvents(MenuItem menu, IReadOnlyList<CalendarEventItem> events)
    {
        menu.Items.Clear();

        if (events.Count == 0)
        {
            menu.Items.Add(new MenuItem
            {
                Header = "今日の予定はありません",
                IsEnabled = false,
            });
            return;
        }

        var now = DateTimeOffset.Now;
        var timedEvents = events
            .Where(x => !x.IsAllDay)
            .OrderBy(x => x.StartLocal)
            .ToList();
        var allDayEvents = events
            .Where(x => x.IsAllDay)
            .OrderBy(x => x.StartLocal)
            .ToList();

        foreach (var calendarEvent in timedEvents)
        {
            menu.Items.Add(CreateCalendarMenuItem(calendarEvent, now));
        }

        if (timedEvents.Count > 0 && allDayEvents.Count > 0)
        {
            menu.Items.Add(new Separator());
        }

        foreach (var calendarEvent in allDayEvents)
        {
            menu.Items.Add(CreateCalendarMenuItem(calendarEvent, now));
        }
    }

    private static MenuItem CreateCalendarMenuItem(CalendarEventItem calendarEvent, DateTimeOffset now)
    {
        var item = new MenuItem
        {
            Header = BuildCalendarLabel(calendarEvent),
            IsEnabled = true,
            IsHitTestVisible = false,
            Focusable = false,
        };

        var isCurrent = calendarEvent.StartLocal <= now && now < calendarEvent.EndLocal;
        var isPast = calendarEvent.EndLocal <= now;

        if (!calendarEvent.IsAllDay && isCurrent)
        {
            item.FontWeight = FontWeights.SemiBold;
            item.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
            item.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534"));
        }
        else if (isPast)
        {
            item.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
            item.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));
            item.Opacity = 1.0d;
        }

        return item;
    }

    private static void RenderCalendarError(MenuItem menu, string errorMessage)
    {
        menu.Items.Clear();
        menu.Items.Add(new MenuItem
        {
            Header = "予定の取得に失敗しました",
            IsEnabled = false,
        });
        menu.Items.Add(new MenuItem
        {
            Header = errorMessage,
            IsEnabled = false,
        });
    }

    private static string BuildCalendarLabel(CalendarEventItem calendarEvent)
    {
        if (calendarEvent.IsAllDay)
        {
            return $"終日 | {calendarEvent.Subject}";
        }

        return $"{calendarEvent.StartLocal:HH:mm}-{calendarEvent.EndLocal:HH:mm} | {calendarEvent.Subject}";
    }

    private void ApplyTheme(ClockTheme theme)
    {
        _currentTheme = theme;

        if (theme == ClockTheme.Dark)
        {
            SetBrush("ClockFaceFillBrush", "#0B1220");
            SetBrush("ClockOuterStrokeBrush", "#93C5FD");
            SetBrush("ClockInnerStrokeBrush", "#1E293B");
            SetBrush("TickBrush", "#94A3B8");
            SetBrush("HourTickBrush", "#F8FAFC");
            SetBrush("NumberBrush", "#E2E8F0");
            SetBrush("HourHandBrush", "#F1F5F9");
            SetBrush("MinuteHandBrush", "#BFDBFE");
            SetBrush("SecondHandBrush", "#FB7185");
            SetBrush("CenterDotFillBrush", "#E2E8F0");
            SetBrush("CenterDotStrokeBrush", "#0F172A");
        }
        else
        {
            SetBrush("ClockFaceFillBrush", "#F8FAFC");
            SetBrush("ClockOuterStrokeBrush", "#2563EB");
            SetBrush("ClockInnerStrokeBrush", "#CBD5E1");
            SetBrush("TickBrush", "#475569");
            SetBrush("HourTickBrush", "#0F172A");
            SetBrush("NumberBrush", "#0F172A");
            SetBrush("HourHandBrush", "#0F172A");
            SetBrush("MinuteHandBrush", "#1E293B");
            SetBrush("SecondHandBrush", "#DC2626");
            SetBrush("CenterDotFillBrush", "#0F172A");
            SetBrush("CenterDotStrokeBrush", "#E2E8F0");
        }

        DarkModeMenuItem.IsChecked = _currentTheme == ClockTheme.Dark;
        LightModeMenuItem.IsChecked = _currentTheme == ClockTheme.Light;
    }

    private void SetBrush(string key, string colorHex)
    {
        Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
    }
}