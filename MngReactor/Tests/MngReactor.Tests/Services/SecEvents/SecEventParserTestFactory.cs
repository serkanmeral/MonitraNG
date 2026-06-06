using Microsoft.Extensions.Options;
using MngReactor.Application.Configuration;
using MngReactor.Persistence.Services.SecEvents.Parsers;

namespace MngReactor.Tests.Services.SecEvents;

internal static class SecEventParserTestFactory
{
    public static WindowsSecurityParser CreateWindowsParser(
        SecEventMaintenanceWindowSettings? maintenanceWindow = null) =>
        new(new SecEventMaintenanceWindowEvaluator(Options.Create(new MngReactorSettings
        {
            SecEventMaintenanceWindow = maintenanceWindow ?? new SecEventMaintenanceWindowSettings()
        })));

    public static WindowsNxlogJsonParser CreateNxlogParser(
        SecEventMaintenanceWindowSettings? maintenanceWindow = null) =>
        new(new SecEventMaintenanceWindowEvaluator(Options.Create(new MngReactorSettings
        {
            SecEventMaintenanceWindow = maintenanceWindow ?? new SecEventMaintenanceWindowSettings()
        })));

    public static SecEventParserRegistry CreateRegistry(
        SecEventMaintenanceWindowSettings? maintenanceWindow = null) =>
        new(
            new WindowsSecurityExtendedParser(),
            CreateNxlogParser(maintenanceWindow),
            CreateWindowsParser(maintenanceWindow),
            new BastionGenericSyslogParser(),
            new LinuxAuthSyslogParser(),
            new FirewallVendorParser(),
            new FirewallGenericSyslogParser(),
            new UnknownSecEventFallback());
}
