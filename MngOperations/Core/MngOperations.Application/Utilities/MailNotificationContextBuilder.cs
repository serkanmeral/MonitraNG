using System.Text.Json;
using MngOperations.Application.Interfaces;

namespace MngOperations.Application.Utilities;

public static class MailNotificationContextBuilder
{
    // Template validation treats blank text as missing. Required fields fall back to this.
    private const string MissingLabel = "—";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<JsonElement> BuildAsync(
        NotificationDispatchRequest request,
        IMetadataCache metadataCache,
        IKeeperDirectoryClient keeper,
        CancellationToken cancellationToken = default,
        string? recipientDisplayName = null)
    {
        var actor = await ResolveActorAsync(request, keeper, cancellationToken);
        var fromState = await ResolveStateNameAsync(request.FromStateId, metadataCache, request.Token, cancellationToken);
        var toState = await ResolveStateNameAsync(request.ToStateId, metadataCache, request.Token, cancellationToken);
        var domain = await ResolveDomainAsync(request, keeper, cancellationToken);
        var workspaceName = await ResolveWorkspaceNameAsync(request, metadataCache, cancellationToken);
        var boardName = await ResolveBoardNameAsync(request, metadataCache, cancellationToken);
        var title = WorkItemDataHelper.GetString(request.WorkItem, "title");

        var context = new Dictionary<string, object?>
        {
            ["recipient"] = new Dictionary<string, object?>
            {
                ["displayName"] = Label(recipientDisplayName)
            },
            ["actor"] = actor,
            ["workItem"] = new Dictionary<string, object?>
            {
                ["key"] = Label(request.WorkItemKey),
                ["title"] = Label(string.IsNullOrWhiteSpace(title) ? request.WorkItemKey : title)
            },
            ["workspace"] = new Dictionary<string, object?>
            {
                ["name"] = workspaceName
            },
            ["board"] = new Dictionary<string, object?>
            {
                ["name"] = boardName
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
                ["type"] = Label(request.EventType),
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            }
        };

        return JsonSerializer.SerializeToElement(context, JsonOptions);
    }

    private static string Label(string? value) =>
        string.IsNullOrWhiteSpace(value) ? MissingLabel : value.Trim();

    private static async Task<Dictionary<string, object?>> ResolveActorAsync(
        NotificationDispatchRequest request,
        IKeeperDirectoryClient keeper,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Actor))
        {
            return new Dictionary<string, object?>
            {
                ["displayName"] = MissingLabel
            };
        }

        var users = await keeper.GetUsersAsync(new[] { request.Actor }, request.Token, cancellationToken);
        if (users.TryGetValue(request.Actor, out var person))
        {
            return new Dictionary<string, object?>
            {
                ["displayName"] = Label(string.IsNullOrWhiteSpace(person.Name) ? request.Actor : person.Name),
                ["email"] = person.Email
            };
        }

        return new Dictionary<string, object?>
        {
            ["displayName"] = Label(request.Actor)
        };
    }

    private static async Task<string> ResolveWorkspaceNameAsync(
        NotificationDispatchRequest request,
        IMetadataCache metadataCache,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WorkspaceId))
            return MissingLabel;

        try
        {
            var workspace = await metadataCache.GetWorkspaceAsync(request.WorkspaceId, request.Token, cancellationToken);
            return Label(workspace.Name);
        }
        catch
        {
            return MissingLabel;
        }
    }

    private static async Task<string> ResolveBoardNameAsync(
        NotificationDispatchRequest request,
        IMetadataCache metadataCache,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BoardId))
            return MissingLabel;

        try
        {
            var board = await metadataCache.GetBoardAsync(request.BoardId, request.Token, cancellationToken);
            return Label(board.Name);
        }
        catch
        {
            return MissingLabel;
        }
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
