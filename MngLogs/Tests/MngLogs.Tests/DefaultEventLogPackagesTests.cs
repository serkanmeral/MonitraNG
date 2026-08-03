using MngLogs.Agent.Configuration;
using MngLogs.Agent.EventLog;

namespace MngLogs.Tests;

public class DefaultEventLogPackagesTests
{
    [Fact]
    public void Resolve_uses_defaults_when_packages_empty()
    {
        var packages = DefaultEventLogPackages.Resolve(new EventLogPolicy { Packages = [] });
        Assert.Equal(4, packages.Count);
        Assert.Contains(packages, p => p.Name == "system-lifecycle" && p.Channel == "System");
        Assert.Contains(packages, p => p.Name == "application-signals" && p.Channel == "Application");
        Assert.Contains(packages, p => p.Name == "powershell-engine" && p.Channel == "Windows PowerShell");
        Assert.Contains(packages, p => p.Name == "rdp-session");
        Assert.DoesNotContain(packages, p => p.Channel == "Security");
    }

    [Fact]
    public void Defaults_system_includes_service_and_boot_ids()
    {
        var system = Assert.Single(DefaultEventLogPackages.Defaults, p => p.Name == "system-lifecycle");
        Assert.Contains(6005, system.EventIds);
        Assert.Contains(7031, system.EventIds);
        Assert.Contains(7034, system.EventIds);
        Assert.Contains(7036, system.EventIds);
        Assert.Contains(7040, system.EventIds);
        Assert.Contains(7045, system.EventIds);
    }

    [Fact]
    public void SecurityAuth_remains_available_as_optional()
    {
        Assert.Equal("Security", DefaultEventLogPackages.SecurityAuth.Channel);
        Assert.Contains(4624, DefaultEventLogPackages.SecurityAuth.EventIds);
        Assert.Contains(DefaultEventLogPackages.AllKnown, p => p.Name == "security-auth");
    }

    [Fact]
    public void BuildQuery_with_record_bound()
    {
        var package = new EventLogPackage
        {
            Name = "security-auth",
            Channel = "Security",
            EventIds = [4624, 4625]
        };

        var q = DefaultEventLogPackages.BuildQuery(package, 100);
        Assert.Contains("EventID=4624", q);
        Assert.Contains("EventID=4625", q);
        Assert.Contains("EventRecordID > 100", q);
    }

    [Fact]
    public void BuildQuery_without_bound()
    {
        var package = new EventLogPackage
        {
            Name = "x",
            Channel = "System",
            EventIds = [6005]
        };
        var q = DefaultEventLogPackages.BuildQuery(package, null);
        Assert.DoesNotContain("EventRecordID", q);
        Assert.Contains("EventID=6005", q);
    }

    [Fact]
    public void BuildQuery_all_channel_with_excludes_and_bound()
    {
        var package = new EventLogPackage
        {
            Name = "system-all",
            Channel = "System",
            SelectionMode = "all",
            ExcludedEventIds = [7036, 1]
        };

        var q = DefaultEventLogPackages.BuildQuery(package, 50);
        Assert.Contains("EventID!=1", q);
        Assert.Contains("EventID!=7036", q);
        Assert.Contains("EventRecordID > 50", q);
        Assert.DoesNotContain("EventID=", q.Replace("EventID!=", ""));
    }

    [Fact]
    public void BuildQuery_all_channel_no_filter()
    {
        var package = new EventLogPackage
        {
            Name = "app-all",
            Channel = "Application",
            SelectionMode = "all"
        };

        var q = DefaultEventLogPackages.BuildQuery(package, null);
        Assert.Equal("*", q);
    }

    [Fact]
    public void BuildHistoryWindowQuery_includes_timediff_and_event_filter()
    {
        var package = new EventLogPackage
        {
            Name = "x",
            Channel = "Application",
            EventIds = [1000]
        };
        var q = DefaultEventLogPackages.BuildHistoryWindowQuery(package, 24);
        Assert.Contains("EventID=1000", q);
        Assert.Contains("timediff(@SystemTime) <= 86400000", q);
    }
}
