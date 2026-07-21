namespace MngKeeper.Application.Common;

/// <summary>Normalize Telegram profile fields for storage.</summary>
public static class TelegramUserProfileHelper
{
    public static string? NormalizeUsername(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();
        if (s.StartsWith('@'))
            s = s[1..].Trim();

        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    public static string? NormalizeChatId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    /// <summary>
    /// Apply username/chatId from request; set or clear LinkedAt when chatId changes.
    /// </summary>
    public static void ApplyFromRequest(
        Domain.Entities.User user,
        string? telegramUsername,
        string? telegramChatId)
    {
        user.TelegramUsername = NormalizeUsername(telegramUsername);

        var newChatId = NormalizeChatId(telegramChatId);
        var previous = NormalizeChatId(user.TelegramChatId);

        if (string.IsNullOrEmpty(newChatId))
        {
            user.TelegramChatId = null;
            user.TelegramLinkedAt = null;
            return;
        }

        user.TelegramChatId = newChatId;
        if (!string.Equals(previous, newChatId, StringComparison.Ordinal))
            user.TelegramLinkedAt = DateTime.UtcNow;
    }
}
