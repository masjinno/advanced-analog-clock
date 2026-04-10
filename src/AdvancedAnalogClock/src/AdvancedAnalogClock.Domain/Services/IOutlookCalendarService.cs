using AdvancedAnalogClock.Domain.Models;

namespace AdvancedAnalogClock.Domain.Services;

public interface IOutlookCalendarService
{
    Task<IReadOnlyList<CalendarEventItem>> GetTodayEventsAsync(CancellationToken cancellationToken);
}