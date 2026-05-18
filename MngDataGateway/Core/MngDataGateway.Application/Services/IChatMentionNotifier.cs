using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MngDataGateway.Application.Services;

/// <summary>
/// <c>cht_messages</c> oluşturulduğunda mention hedeflerini MngNotifier'a iletir (HTTP MVP).
/// </summary>
public interface IChatMentionNotifier
{
    /// <summary>
    /// <paramref name="authorFromToken"/> JWT kullanıcı id'si; gövdedeki <c>authorPersonId</c> ile uyumludur.
    /// </summary>
    Task NotifyChatMentionsAsync(
        string domainName,
        Dictionary<string, object> createdMessageRow,
        string authorFromToken,
        CancellationToken cancellationToken = default);
}
