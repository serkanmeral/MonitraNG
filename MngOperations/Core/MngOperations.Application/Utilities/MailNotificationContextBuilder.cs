using System.Text.Json;
using MngOperations.Application.Interfaces;

namespace MngOperations.Application.Utilities;

public static class MailNotificationContextBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<JsonElement> BuildAsync(
        NotificationDispatchRequest request,
        IMetadataCache metadataCache,
        IKeeperDirectoryClient keeper,
        CancellationToken cancellationToken = default)
    {
        var actor = await ResolveActorAsync(request, keeper, cancellationToken);
        var fromState = await ResolveStateNameAsync(request.FromStateId, metadataCache, request.Token, cancellationToken);
        var toState = await ResolveStateNameAsync(request.ToStateId, metadataCache, request.Token, cancellationToken);
        var domain = await ResolveDomainAsync(request, keeper, cancellationToken);

        var context = new Dictionary<string, object?>
        {
            ["actor"] = actor,
            ["workItem"] = new Dictionary<string, object?>
            {
                ["key"] = request.WorkItemKey,
                ["title"] = WorkItemDataHelper.GetString(request.WorkItem, "title") ?? string.Empty
            },
            ["transition"] = new Dictionary<string, object?>
            {
                ["key"] = request.TransitionKey ?? string.Empty,
                ["fromState"] = fromState,
                ["toState"] = toState
            },
            ["domain"] = domain,
            ["event"] = new Dictionary<string, object?>
            {
                ["type"] = request.EventType,
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            }
        };

        return JsonSerializer.SerializeToElement(context, JsonOptions);
    }

    private static async Task<Dictionary<string, object?>> ResolveActorAsync(
        NotificationDispatchRequest request,
        IKeeperDirectoryClient keeper,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Actor))
        {
            return new Dictionary<string, object?>
            {
                ["displayName"] = string.Empty
            };
        }

        var users = await keeper.GetUsersAsync(new[] { request.Actor }, request.Token, cancellationToken);
        if (users.TryGetValue(request.Actor, out var person))
        {
            return new Dictionary<string, object?>
            {
                ["displayName"] = person.Name ?? request.Actor,
                ["email"] = person.Email
            };
        }

        return new Dictionary<string, object?>
        {
            ["displayName"] = request.Actor
        };
    }

    private static async Task<string> ResolveStateNameAsync(
        string? stateId,
        IMetadataCache metadataCache,
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stateId))
            return string.Empty;

        try
        {
            var state = await metadataCache.GetStateAsync(stateId, token, cancellationToken);
            return string.IsNullOrWhiteSpace(state.Name) ? stateId : state.Name;
        }
        catch
        {
            return stateId;
        }
    }

    private static async Task<Dictionary<string, object?>> ResolveDomainAsync(
        NotificationDispatchRequest request,
        IKeeperDirectoryClient keeper,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DomainName))
        {
            return new Dictionary<string, object?>
            {
                ["name"] = string.Empty,
                ["displayName"] = string.Empty
            };
        }

        var branding = await keeper.GetDomainByNameAsync(request.DomainName, request.Token, cancellationToken);
        if (branding == null)
        {
            return new Dictionary<string, object?>
            {
                ["name"] = request.DomainName
            };
        }

        return new Dictionary<string, object?>
        {
            ["name"] = branding.Name ?? request.DomainName,
            ["displayName"] = branding.DisplayName,
            ["logoUrl"] = branding.LogoUrl
        };
    }
}
