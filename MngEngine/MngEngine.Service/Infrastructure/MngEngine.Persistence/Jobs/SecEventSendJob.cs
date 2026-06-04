using MngEngine.Application.Interfaces;
using Quartz;
using Serilog;

namespace MngEngine.Persistence.Jobs;

public sealed class SecEventSendJob : IJob
{
    private readonly ILogger _logger;
    private readonly ISecEventSendProcessing _sendProcessing;

    public SecEventSendJob(ILogger logger, ISecEventSendProcessing sendProcessing)
    {
        _logger = logger;
        _sendProcessing = sendProcessing;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.Debug("SecEventSendJob başladı");

        var result = await _sendProcessing.FlushAsync(context.CancellationToken);
        if (!result.Success && result.ErrorMessage != null)
            _logger.Warning("SecEventSendJob: {Error}", result.ErrorMessage);
    }
}
