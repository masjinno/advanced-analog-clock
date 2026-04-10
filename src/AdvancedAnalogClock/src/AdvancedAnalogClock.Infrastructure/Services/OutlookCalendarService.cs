using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AdvancedAnalogClock.Domain.Models;
using AdvancedAnalogClock.Domain.Services;

namespace AdvancedAnalogClock.Infrastructure.Services;

public sealed class OutlookCalendarService : IOutlookCalendarService
{
    private static readonly Regex UrlRegex = new(
        "https?://[^\\s<>\"']+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] MeetingDomains =
    [
        "teams.microsoft.com",
        "zoom.us",
        "meet.google.com",
        "webex.com",
    ];

    public async Task<IReadOnlyList<CalendarEventItem>> GetTodayEventsAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();

        cancellationToken.ThrowIfCancellationRequested();
        return ReadTodayEventsFromDesktopOutlook(cancellationToken);
    }

    private static IReadOnlyList<CalendarEventItem> ReadTodayEventsFromDesktopOutlook(CancellationToken cancellationToken)
    {
        object? outlookApplication = null;
        object? mapiNamespace = null;
        object? calendarFolder = null;
        object? calendarItems = null;
        object? restrictedItems = null;

        try
        {
            var outlookType = Type.GetTypeFromProgID("Outlook.Application");
            if (outlookType is null)
            {
                throw new InvalidOperationException("Outlook デスクトップが見つかりません。");
            }

            outlookApplication = Activator.CreateInstance(outlookType)
                ?? throw new InvalidOperationException("Outlook の起動に失敗しました。");

            var app = (dynamic)outlookApplication;
            mapiNamespace = app.GetNamespace("MAPI");
            dynamic ns = mapiNamespace;

            // 9 = olFolderCalendar
            calendarFolder = ns.GetDefaultFolder(9);
            dynamic folder = calendarFolder;
            calendarItems = folder.Items;
            dynamic items = calendarItems;

            items.IncludeRecurrences = true;
            items.Sort("[Start]");

            var todayLocal = DateTime.Today;
            var tomorrowLocal = todayLocal.AddDays(1);
            var restriction = BuildOutlookRestriction(todayLocal, tomorrowLocal);
            restrictedItems = items.Restrict(restriction);
            dynamic filtered = restrictedItems;

            var result = new List<CalendarEventItem>();
            foreach (var entry in filtered)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var start = (DateTime)entry.Start;
                    var end = (DateTime)entry.End;
                    var subject = (string?)entry.Subject;
                    var isAllDay = (bool)entry.AllDayEvent;
                    var location = SafeRead(() => (string?)entry.Location);
                    var bodyText = SafeRead(() => (string?)entry.Body);

                    if (end <= todayLocal || start >= tomorrowLocal)
                    {
                        continue;
                    }

                    result.Add(new CalendarEventItem
                    {
                        Subject = string.IsNullOrWhiteSpace(subject) ? "(件名なし)" : subject,
                        StartLocal = new DateTimeOffset(start),
                        EndLocal = new DateTimeOffset(end),
                        IsAllDay = isAllDay,
                        Location = Normalize(location),
                        BodyPreview = BuildBodyPreview(bodyText),
                        JoinUrl = TryFindFirstUrl(bodyText),
                    });
                }
                finally
                {
                    if (entry is not null && Marshal.IsComObject(entry))
                    {
                        Marshal.FinalReleaseComObject(entry);
                    }
                }
            }

            return result
                .OrderBy(x => x.StartLocal)
                .ToList();
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException("Outlook 予定表へのアクセスに失敗しました。Outlook が起動済みか確認してください。", ex);
        }
        finally
        {
            ReleaseComObject(restrictedItems);
            ReleaseComObject(calendarItems);
            ReleaseComObject(calendarFolder);
            ReleaseComObject(mapiNamespace);
            ReleaseComObject(outlookApplication);
        }
    }

    private static string BuildOutlookRestriction(DateTime start, DateTime end)
    {
        var format = "g";
        var culture = CultureInfo.CurrentCulture;
        return $"[Start] < '{end.ToString(format, culture)}' AND [End] > '{start.ToString(format, culture)}'";
    }

    private static void ReleaseComObject(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.FinalReleaseComObject(comObject);
        }
    }

    private static string? BuildBodyPreview(string? body)
    {
        return Normalize(body);
    }

    private static string? TryFindFirstUrl(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var matches = UrlRegex.Matches(body);
        if (matches.Count == 0)
        {
            return null;
        }

        var urls = matches
            .Select(x => x.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var domain in MeetingDomains)
        {
            var preferred = urls.FirstOrDefault(url => IsDomainMatch(url, domain));
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                return preferred;
            }
        }

        return null;
    }

    private static bool IsDomainMatch(string url, string expectedDomain)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host;
        return host.Equals(expectedDomain, StringComparison.OrdinalIgnoreCase)
               || host.EndsWith($".{expectedDomain}", StringComparison.OrdinalIgnoreCase);
    }

    private static T? SafeRead<T>(Func<T?> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return default;
        }
    }

    private static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Trim();
    }
}