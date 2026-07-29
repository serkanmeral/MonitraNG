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
}
