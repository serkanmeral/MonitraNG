using MngLogs.Agent.Configuration;
using MngLogs.Agent.Contracts;

namespace MngLogs.Agent.EventLog;

public sealed class NoOpWindowsEventLogReader : IWindowsEventLogReader
{
    public IReadOnlyList<IngestEventItem> ReadNew(
        EventLogPackage package,
        ChannelBookmark? bookmark,
        int maxEvents,
        out ChannelBookmark? updatedBookmark)
    {
        updatedBookmark = bookmark;
        return [];
    }

    public ChannelBookmark SeedFromNow(EventLogPackage package) =>
        new(0, DateTime.UtcNow, CatchUpFromNow: false, CursorMode: "now");

    public ChannelBookmark SeedFromLastHours(EventLogPackage package, int hours) =>
        new(
            0,
            DateTime.UtcNow,
            CatchUpFromNow: false,
            CursorMode: "hours",
            HistoryHours: hours,
            HistoryFromUtc: DateTime.UtcNow.AddHours(-Math.Clamp(hours, 1, 168)));
}
