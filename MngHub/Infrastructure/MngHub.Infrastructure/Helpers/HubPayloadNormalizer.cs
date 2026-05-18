using System.Text.Json;
using System.Text.Json.Nodes;

namespace MngHub.Infrastructure.Helpers;

/// <summary>
/// RabbitMQ tüketicisinde <see cref="JsonSerializer.Deserialize{T}(string, JsonSerializerOptions?)"/> ile
/// <c>object</c> hedefi <see cref="JsonElement"/> üretir; SignalR istemcisine giderken yapı bazen düz JSON nesnesi
/// olarak açılmaz. DG <c>dataset.*</c> zarfını istemciye her zaman okunabilir nesne olarak iletmek için
/// kök <see cref="JsonElement"/> ağacını <see cref="JsonNode"/> ile yeniden ayrıştırır.
/// </summary>
public static class HubPayloadNormalizer
{
    public static object NormalizeForClient(object? message)
    {
        if (message is null)
            return new JsonObject();

        if (message is JsonElement je)
        {
            try
            {
                return JsonNode.Parse(je.GetRawText()) ?? new JsonObject();
            }
            catch
            {
                return message;
            }
        }

        return message;
    }
}
