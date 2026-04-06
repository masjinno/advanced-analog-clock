using AdvancedAnalogClock.App.Models;

namespace AdvancedAnalogClock.App.Services;

public interface IOutlookCalendarService
{
    Task<IReadOnlyList<CalendarEventItem>> GetTodayEventsAsync(CancellationToken cancellationToken);
}
