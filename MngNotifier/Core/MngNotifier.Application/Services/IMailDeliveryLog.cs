using MngNotifier.Application.DTOs;

namespace MngNotifier.Application.Services;

public interface IMailDeliveryLog
{
    Task RecordAsync(MailDeliveryLogEntry entry, CancellationToken cancellationToken = default);

    Task<MailDeliveryLogPage> SearchAsync(string domainName, MailDeliveryLogQuery query, CancellationToken cancellationToken = default);
}
