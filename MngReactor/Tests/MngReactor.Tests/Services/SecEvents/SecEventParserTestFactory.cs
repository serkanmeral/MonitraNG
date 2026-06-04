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
}
