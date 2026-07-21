using MngNotifier.Application.DTOs;

namespace MngNotifier.Application.Services;

public interface ITelegramMessageSender
{
    Task<SendMessageTargetResult> SendTextAsync(
        string chatId,
        string text,
        string? parseMode,
        bool disableWebPagePreview,
        CancellationToken cancellationToken = default);
}
