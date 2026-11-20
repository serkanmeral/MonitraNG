using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MngExtensions.Application.Pdks;

namespace MngExtensions.Infrastructure.Pdks;

/// <summary>
/// Removes door frames ninety days after the passage. The passage row stays.
/// </summary>
public sealed class PassagePhotoRetentionService : BackgroundService
{
    private readonly IKeeperTokenSource _tokens;
    private readonly IPdksDataGateway _data;
    private readonly ILogger<PassagePhotoRetentionService> _logger;

    public PassagePhotoRetentionService(
        IKeeperTokenSource tokens,
        IPdksDataGateway data,
        ILogger<PassagePhotoRetentionService> logger)
    {
        _tokens = tokens;
        _data = data;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Pause(TimeSpan.FromMinutes(2), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var token = await _tokens.GetAccessTokenAsync(stoppingToken);
                var cleared = await _data.ExpirePhotosAsync(token, stoppingToken);
                if (cleared > 0)
                    _logger.LogInformation("Removed {Count} expired passage frames", cleared);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Passage frame retention failed");
            }

            await Pause(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private static async Task Pause(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
