using MngNotifier.Application.Configuration;

namespace MngNotifier.Application.Services;

/// <summary>
/// Domain SMTP when configured; otherwise the process-level Mail settings.
/// </summary>
public interface IDomainMailSettingsResolver
{
    Task<MailSettings> ResolveAsync(string? domainName, CancellationToken cancellationToken = default);
}
