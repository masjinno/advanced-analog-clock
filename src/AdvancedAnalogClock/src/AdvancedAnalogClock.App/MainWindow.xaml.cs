using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AdvancedAnalogClock.App.ViewModels;

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
    private ClockTheme _currentTheme = ClockTheme.Dark;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        ApplyTheme(ClockTheme.Dark);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    private void OnWindowDragMove(object sender, MouseButtonEventArgs e)
    {
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

    // Keep exit in one place so tray menu can reuse it later.
    private void RequestApplicationExit()
    {
        Close();
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
            SetBrush("MenuBackgroundBrush", "#111827");
            SetBrush("MenuForegroundBrush", "#F8FAFC");
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
            SetBrush("MenuBackgroundBrush", "#FFFFFF");
            SetBrush("MenuForegroundBrush", "#0F172A");
        }

        DarkModeMenuItem.IsChecked = _currentTheme == ClockTheme.Dark;
        LightModeMenuItem.IsChecked = _currentTheme == ClockTheme.Light;
    }

    private void SetBrush(string key, string colorHex)
    {
        Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
    }
}