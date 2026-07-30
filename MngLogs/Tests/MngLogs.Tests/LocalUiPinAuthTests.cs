using MngLogs.Agent.Configuration;
using MngLogs.Agent.LocalUi;

namespace MngLogs.Tests;

public class LocalUiPinAuthTests
{
    [Fact]
    public void Setup_Unlock_And_Protect_Session()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MngLogs-PinAuth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var auth = new LocalUiPinAuth(new TempConfigStore(dir));

            var status0 = auth.GetStatus(null);
            Assert.False(status0.Configured);
            Assert.False(status0.Unlocked);

            Assert.False(auth.TryValidateSession(null, out _));

            var setup = auth.Setup("1234", "1234");
            Assert.True(setup.Ok);
            Assert.False(string.IsNullOrEmpty(setup.Token));
            Assert.True(auth.TryValidateSession(setup.Token, out _));

            auth.Lock(setup.Token);
            Assert.False(auth.TryValidateSession(setup.Token, out _));

            var bad = auth.Unlock("9999");
            Assert.False(bad.Ok);

            var unlock = auth.Unlock("1234");
            Assert.True(unlock.Ok);
            Assert.True(auth.TryValidateSession(unlock.Token, out _));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Lockout_After_Max_Failures()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MngLogs-PinAuth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var auth = new LocalUiPinAuth(new TempConfigStore(dir));
            Assert.True(auth.Setup("abcd", "abcd").Ok);
            auth.Lock(auth.Unlock("abcd").Token);

            for (var i = 0; i < LocalUiPinAuth.MaxFailedAttempts - 1; i++)
                Assert.False(auth.Unlock("wrong").Ok);

            var locked = auth.Unlock("wrong");
            Assert.False(locked.Ok);
            Assert.NotNull(locked.LockedUntilUtc);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    private sealed class TempConfigStore(string dir) : IAgentConfigStore
    {
        public MngLogsAgentSettings Current { get; } = new()
        {
            System = new SystemConfig { DataDirectory = dir }
        };

        public string ResolveDataDirectory() => dir;
        public string ResolveHostId() => "test";
        public Task SaveSystemAsync(SystemConfig system, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SavePolicyAsync(PolicyConfig policy, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
