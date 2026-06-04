using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Configuration;

namespace MngReactor.Persistence.Services.SecEvents.Parsers;

public sealed class SecEventMaintenanceWindowEvaluator : ISecEventMaintenanceWindowEvaluator
{
    private readonly IOptions<MngReactorSettings> _options;

    public SecEventMaintenanceWindowEvaluator(IOptions<MngReactorSettings> options)
    {
        _options = options;
    }

    public bool IsOutsideAllowedWindow(DateTime utcTimestamp)
    {
        var settings = _options.Value.SecEventMaintenanceWindow;
        if (!settings.Enabled)
            return false;

        var utc = utcTimestamp.Kind == DateTimeKind.Utc
            ? utcTimestamp
            : utcTimestamp.ToUniversalTime();

        var hour = utc.Hour;
        return hour < settings.AllowedStartHourUtc || hour >= settings.AllowedEndHourUtc;
    }
}
