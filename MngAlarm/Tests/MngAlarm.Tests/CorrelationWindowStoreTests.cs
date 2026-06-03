using MngAlarm.Infrastructure.State;
using Xunit;

namespace MngAlarm.Tests.State;

public sealed class CorrelationWindowStoreTests
{
    [Fact]
    public void Sliding_window_expires_old_events()
    {
        var store = new InMemoryCorrelationWindowStore();
        var key = "odak:r1:u1";
        var window = TimeSpan.FromMinutes(5);
        var t0 = DateTime.UtcNow;

        store.RecordAndCount(key, t0.AddMinutes(-6), window);
        store.RecordAndCount(key, t0.AddMinutes(-1), window);
        store.RecordAndCount(key, t0, window);

        Assert.Equal(2, store.GetCount(key, t0, window));
    }
}
