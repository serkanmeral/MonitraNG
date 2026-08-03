using MngLogs.Agent.Configuration;
using MngLogs.Agent.EventLog;

namespace MngLogs.Tests;

public class EventLogPackageMergerTests
{
    [Fact]
    public void Merge_keeps_server_when_no_overrides()
    {
        var effective = EventLogPackageMerger.Merge(DefaultEventLogPackages.Defaults, null, null);
        Assert.Equal(4, effective.Count);
        Assert.DoesNotContain(effective, p => p.Name == "security-auth");
    }

    [Fact]
    public void Merge_disables_server_package()
    {
        var effective = EventLogPackageMerger.Merge(
            DefaultEventLogPackages.Defaults,
            null,
            ["rdp-session"]);
        Assert.Equal(3, effective.Count);
        Assert.DoesNotContain(effective, p => p.Name == "rdp-session");
    }

    [Fact]
    public void Merge_override_replaces_and_adds()
    {
        var overrides = new List<EventLogPackage>
        {
            new()
            {
                Name = "system-lifecycle",
                Channel = "System",
                EventIds = [7036]
            },
            new()
            {
                Name = "custom-app",
                Channel = "Application",
                EventIds = [1000]
            }
        };

        var effective = EventLogPackageMerger.Merge(DefaultEventLogPackages.Defaults, overrides, null);
        var system = Assert.Single(effective, p => p.Name == "system-lifecycle");
        Assert.Equal(new[] { 7036 }, system.EventIds);
        Assert.Contains(effective, p => p.Name == "custom-app");
        Assert.Contains(effective, p => p.Name == "rdp-session");
    }

    [Fact]
    public void Resolve_legacy_packages_still_full_replace()
    {
        var policy = new EventLogPolicy
        {
            Packages =
            [
                new EventLogPackage { Name = "only", Channel = "System", EventIds = [6005] }
            ]
        };
        var resolved = DefaultEventLogPackages.Resolve(policy, DefaultEventLogPackages.Defaults);
        Assert.Single(resolved);
        Assert.Equal("only", resolved[0].Name);
    }

    [Fact]
    public void IsValid_allows_all_channel_without_event_ids()
    {
        var p = new EventLogPackage
        {
            Name = "system-all",
            Channel = "System",
            SelectionMode = "all",
            EventIds = []
        };
        Assert.True(EventLogPackageMerger.IsValid(p));
        var clone = EventLogPackageMerger.Clone(p);
        Assert.Equal("all", clone.SelectionMode);
        Assert.Empty(clone.EventIds);
    }

    [Fact]
    public void Resolve_prefers_override_model_over_legacy_packages()
    {
        var policy = new EventLogPolicy
        {
            Packages =
            [
                new EventLogPackage { Name = "legacy", Channel = "System", EventIds = [1] }
            ],
            AgentOverrides =
            [
                new EventLogPackage { Name = "custom-app", Channel = "Application", EventIds = [1000] }
            ],
            DisabledServerPackages = ["rdp-session"]
        };

        var resolved = DefaultEventLogPackages.Resolve(policy, DefaultEventLogPackages.Defaults);
        Assert.DoesNotContain(resolved, p => p.Name == "legacy");
        Assert.DoesNotContain(resolved, p => p.Name == "rdp-session");
        Assert.Contains(resolved, p => p.Name == "custom-app");
        Assert.Contains(resolved, p => p.Name == "system-lifecycle");
    }
}
