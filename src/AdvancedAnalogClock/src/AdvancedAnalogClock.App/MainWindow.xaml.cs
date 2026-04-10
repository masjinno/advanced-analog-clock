using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using AdvancedAnalogClock.App.ViewModels;
using AdvancedAnalogClock.App.Views;
using AdvancedAnalogClock.Domain.Models;
using AdvancedAnalogClock.Domain.Services;
using AdvancedAnalogClock.Infrastructure.Services;

namespace AdvancedAnalogClock.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int WmSizing = 0x0214;
    private const int WmszLeft = 1;
    private const int WmszRight = 2;
    private const int WmszTop = 3;
    private const int WmszTopLeft = 4;
    private const int WmszTopRight = 5;
    private const int WmszBottom = 6;
    private const int WmszBottomLeft = 7;
    private const int WmszBottomRight = 8;

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
    private HwndSource? _hwndSource;
    private ClockTheme _currentTheme = ClockTheme.Light;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        _outlookCalendarService = new OutlookCalendarService();
        DataContext = _viewModel;
        ApplyTheme(ClockTheme.Light);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwndSource = (HwndSource?)PresentationSource.FromVisual(this);
        _hwndSource?.AddHook(WindowProc);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WindowProc);
        }

        _calendarRequestCts?.Cancel();
        _calendarRequestCts?.Dispose();
        _viewModel.Dispose();
        base.OnClosed(e);
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

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmSizing)
        {
            return IntPtr.Zero;
        }

        if (lParam == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var edge = wParam.ToInt32();
        var rect = Marshal.PtrToStructure<RectStruct>(lParam);
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var minSize = (int)Math.Ceiling(Math.Max(MinWidth, MinHeight));

        var targetSize = edge switch
        {
            WmszLeft or WmszRight => Math.Max(width, minSize),
            WmszTop or WmszBottom => Math.Max(height, minSize),
            _ => Math.Max(Math.Max(width, height), minSize),
        };

        switch (edge)
        {
            case WmszLeft:
                rect.Left = rect.Right - targetSize;
                rect.Bottom = rect.Top + targetSize;
                break;
            case WmszRight:
                rect.Right = rect.Left + targetSize;
                rect.Bottom = rect.Top + targetSize;
                break;
            case WmszTop:
                rect.Top = rect.Bottom - targetSize;
                rect.Right = rect.Left + targetSize;
                break;
            case WmszBottom:
                rect.Bottom = rect.Top + targetSize;
                rect.Right = rect.Left + targetSize;
                break;
            case WmszTopLeft:
                rect.Left = rect.Right - targetSize;
                rect.Top = rect.Bottom - targetSize;
                break;
            case WmszTopRight:
                rect.Right = rect.Left + targetSize;
                rect.Top = rect.Bottom - targetSize;
                break;
            case WmszBottomLeft:
                rect.Left = rect.Right - targetSize;
                rect.Bottom = rect.Top + targetSize;
                break;
            case WmszBottomRight:
                rect.Right = rect.Left + targetSize;
                rect.Bottom = rect.Top + targetSize;
                break;
        }

        Marshal.StructureToPtr(rect, lParam, fDeleteOld: true);
        handled = true;
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectStruct
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
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

    private async void OnRefreshCalendarMenuClick(object sender, RoutedEventArgs e)
    {
        await FetchCalendarAsync();
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

    private async Task FetchCalendarAsync()
    {
        if (_calendarLoading)
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