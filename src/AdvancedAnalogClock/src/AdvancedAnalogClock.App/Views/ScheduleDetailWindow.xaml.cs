using System.Diagnostics;
using System.Windows;
using AdvancedAnalogClock.App.Models;

namespace AdvancedAnalogClock.App.Views;

public partial class ScheduleDetailWindow : Window
{
    private readonly string? _joinUrl;

    public ScheduleDetailWindow(CalendarEventItem calendarEvent)
    {
        InitializeComponent();

        _joinUrl = calendarEvent.JoinUrl;

        SubjectTextBlock.Text = calendarEvent.Subject;
        TimeTextBlock.Text = calendarEvent.IsAllDay
            ? "終日の予定"
            : $"{calendarEvent.StartLocal:yyyy/MM/dd HH:mm} - {calendarEvent.EndLocal:yyyy/MM/dd HH:mm}";

        LocationTextBlock.Text = string.IsNullOrWhiteSpace(calendarEvent.Location)
            ? "場所: (未設定)"
            : $"場所: {calendarEvent.Location}";

        BodyTextBlock.Text = string.IsNullOrWhiteSpace(calendarEvent.BodyPreview)
            ? "本文情報はありません。"
            : calendarEvent.BodyPreview;

        OpenJoinLinkButton.IsEnabled = !string.IsNullOrWhiteSpace(_joinUrl);
    }

    private void OnOpenJoinLinkClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_joinUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _joinUrl,
                UseShellExecute = true,
            });

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"リンクを開けませんでした。\n{ex.Message}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
