using MngNotifier.Application.Models;

namespace MngNotifier.Application.Services;

public interface ITelegramUpdateProcessor
{
    Task ProcessUpdateAsync(TelegramUpdate update, CancellationToken cancellationToken = default);
}
