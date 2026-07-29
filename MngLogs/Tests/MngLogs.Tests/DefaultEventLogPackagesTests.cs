using MngLogs.Agent.Configuration;
using MngLogs.Agent.EventLog;

namespace MngLogs.Tests;

public class DefaultEventLogPackagesTests
{
    [Fact]
    public void Resolve_uses_defaults_when_packages_empty()
    {
        var packages = DefaultEventLogPackages.Resolve(new EventLogPolicy { Packages = [] });
        Assert.Equal(2, packages.Count);
        Assert.Contains(packages, p => p.Name == "security-auth" && p.Channel == "Security");
        Assert.Contains(packages, p => p.Name == "system-lifecycle");
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
}
