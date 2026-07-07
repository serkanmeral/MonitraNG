using System.Text.Json;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.EditorSessions;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

public sealed class EditorSessionService : IEditorSessionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IWopiSessionStore _sessions;
    private readonly IRequestContext _ctx;
    private readonly IMngDataGatewayClient _dg;
    private readonly MngDocumentSettings _settings;

    public EditorSessionService(
        IWopiSessionStore sessions,
        IRequestContext ctx,
        IMngDataGatewayClient dg,
        IOptions<MngDocumentSettings> settings)
    {
        _sessions = sessions;
        _ctx = ctx;
        _dg = dg;
        _settings = settings.Value;
    }

    public string BeginSession(WopiSession session, TimeSpan ttl)
    {
        var limits = _settings.EditorLimits;
        var idleTimeout = GetIdleTimeout();

        _sessions.PurgeExpired(idleTimeout);

        if (limits.EnforceLimits)
            EnsureWithinLimits(session, limits, idleTimeout);

        return _sessions.CreateSession(session, ttl);
    }

    public void EndSession(string accessToken)
    {
        var token = accessToken?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
            throw DocumentException.Validation("TOKEN_REQUIRED", "Access token is required.", "Oturum anahtarı gerekli.");

        var idleTimeout = GetIdleTimeout();
        _sessions.PurgeExpired(idleTimeout);

        var active = _sessions.ListActive(idleTimeout);
        var match = active.FirstOrDefault(s => string.Equals(s.AccessToken, token, StringComparison.Ordinal));
        if (match is null)
            return;

        EnsureCanManageSession(match.Session);
        _sessions.Revoke(token);
    }

    public async Task<EditorSessionStatsDto> GetStatsAsync(bool includeSessionDetails, CancellationToken ct = default)
    {
        var limits = _settings.EditorLimits;
        var idleTimeout = GetIdleTimeout();
        _sessions.PurgeExpired(idleTimeout);

        var active = _sessions.ListActive(idleTimeout);
        var documentKeys = active
            .Select(s => ResolveDocumentKey(s.Session))
            .Where(k => k is not null)
            .Distinct(StringComparer.Ordinal)
            .Count();

        var byUser = active
            .GroupBy(s => s.Session.UserId, StringComparer.Ordinal)
            .Select(g =>
            {
                var first = g.First().Session;
                return new EditorSessionUserStatsDto
                {
                    UserId = g.Key,
                    DisplayName = first.UserName,
                    ConnectionCount = g.Count()
                };
            })
            .OrderByDescending(u => u.ConnectionCount)
            .ThenBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        IReadOnlyList<EditorSessionItemDto>? sessions = null;
        if (includeSessionDetails)
        {
            var items = active
                .OrderByDescending(s => s.LastSeenAt)
                .Select(s => new EditorSessionItemDto
                {
                    AccessToken = s.AccessToken,
                    TokenPrefix = FormatTokenPrefix(s.AccessToken),
                    ResourceId = NullIfEmpty(s.Session.ResourceId),
                    TemplateId = NullIfEmpty(s.Session.TemplateId),
                    LetterheadId = NullIfEmpty(s.Session.LetterheadId),
                    Kind = ResolveKind(s.Session),
                    UserId = s.Session.UserId,
                    UserName = s.Session.UserName,
                    ReadOnly = s.Session.ReadOnly,
                    CreatedAt = s.Session.CreatedAt,
                    LastSeenAt = s.LastSeenAt
                })
                .ToList();

            await EnrichDisplayNamesAsync(items, ct);
            sessions = items;
        }

        return new EditorSessionStatsDto
        {
            ActiveConnections = active.Count,
            ActiveDocuments = documentKeys,
            Limits = new EditorSessionLimitsDto
            {
                MaxConnections = limits.MaxConcurrentConnections,
                MaxDocuments = limits.MaxConcurrentDocuments,
                MaxSessionsPerUser = limits.MaxSessionsPerUser
            },
            CollaboraHomeMode = new CollaboraHomeModeLimitsDto
            {
                MaxConnections = limits.CollaboraMaxConnections,
                MaxDocuments = limits.CollaboraMaxDocuments
            },
            ByUser = byUser,
            Sessions = sessions
        };
    }

    public DocumentEditorLockStatusDto GetDocumentLockStatus(
        string? resourceId,
        string? templateId,
        string? letterheadId,
        string currentUserId,
        bool isAdminOrManager)
    {
        var lockSettings = _settings.EditorLock;
        var idleTimeout = GetIdleTimeout();
        _sessions.PurgeExpired(idleTimeout);

        var probe = new WopiSession
        {
            ResourceId = resourceId?.Trim() ?? string.Empty,
            TemplateId = templateId?.Trim() ?? string.Empty,
            LetterheadId = letterheadId?.Trim() ?? string.Empty,
            UserId = string.Empty,
            UserName = string.Empty,
            DataGatewayToken = string.Empty,
            Version = "1"
        };
        var documentKey = ResolveDocumentKey(probe);

        var activeEditors = string.IsNullOrWhiteSpace(documentKey)
            ? (IReadOnlyList<DocumentActiveEditorDto>)Array.Empty<DocumentActiveEditorDto>()
            : _sessions.ListActive(idleTimeout)
                .Where(s => string.Equals(ResolveDocumentKey(s.Session), documentKey, StringComparison.Ordinal))
                .Where(s => !s.Session.ReadOnly)
                .OrderByDescending(s => s.LastSeenAt)
                .Select(s => new DocumentActiveEditorDto
                {
                    UserId = s.Session.UserId,
                    UserName = s.Session.UserName,
                    LastSeenAt = s.LastSeenAt,
                    IsCurrentUser = string.Equals(s.Session.UserId, currentUserId, StringComparison.Ordinal)
                })
                .ToList();

        // When BlockSameUserDuplicateSession=false, hide own sessions from lock (legacy behaviour).
        var relevantEditors = lockSettings.BlockSameUserDuplicateSession
            ? activeEditors
            : activeEditors.Where(e => !e.IsCurrentUser).ToList();

        var isLockedBySelf = relevantEditors.Any(e => e.IsCurrentUser);
        var isLockedByOthers = relevantEditors.Any(e => !e.IsCurrentUser);
        var canBypass = lockSettings.AllowManagerBypass && isAdminOrManager && isLockedByOthers && !isLockedBySelf;

        return new DocumentEditorLockStatusDto
        {
            ResourceId = NullIfEmpty(resourceId),
            TemplateId = NullIfEmpty(templateId),
            LetterheadId = NullIfEmpty(letterheadId),
            IsLocked = relevantEditors.Count > 0,
            IsLockedByOthers = isLockedByOthers,
            IsLockedBySelf = isLockedBySelf,
            WarnOnActiveEditor = lockSettings.WarnOnActiveEditor,
            EnforceExclusiveLock = lockSettings.EnforceExclusiveLock,
            CanBypassLock = canBypass,
            ActiveEditors = relevantEditors
        };
    }

    private async Task EnrichDisplayNamesAsync(IList<EditorSessionItemDto> items, CancellationToken ct)
    {
        var token = _ctx.BearerToken;
        if (string.IsNullOrWhiteSpace(token) || items.Count == 0)
            return;

        var resourceIds = items
            .Select(i => i.ResourceId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToList();
        var templateIds = items
            .Select(i => i.TemplateId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToList();
        var letterheadIds = items
            .Select(i => i.LetterheadId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToList();

        // Tekil GetById yerine toplu sorgu — N oturumda N sıralı DG çağrısı yapılıyordu (yavaş yenileme).
        var resourceTask = LoadResourcesByIdsAsync(resourceIds, token, ct);
        var templateTask = LoadTemplatesByIdsAsync(templateIds, token, ct);
        var letterheadTask = LoadLetterheadsByIdsAsync(letterheadIds, token, ct);
        await Task.WhenAll(resourceTask, templateTask, letterheadTask);

        var resourceRows = await resourceTask;
        var templateNames = await templateTask;
        var letterheadNames = await letterheadTask;
        var resourceNames = resourceRows.ToDictionary(
            kv => kv.Key,
            kv => ResolveResourceLabel(kv.Value),
            StringComparer.Ordinal);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            string? displayName = item.Kind switch
            {
                "resource" when item.ResourceId is not null && resourceNames.TryGetValue(item.ResourceId, out var rn) => rn,
                "template" when item.TemplateId is not null && templateNames.TryGetValue(item.TemplateId, out var tn) => tn,
                "letterhead" when item.LetterheadId is not null && letterheadNames.TryGetValue(item.LetterheadId, out var ln) => ln,
                _ => item.ResourceId ?? item.TemplateId ?? item.LetterheadId
            };

            string? officeKind = item.Kind switch
            {
                "resource" when item.ResourceId is not null && resourceRows.TryGetValue(item.ResourceId, out var resource) =>
                    ResolveOfficeKind(resource.extension, resource.mimeType),
                _ => null
            };

            items[i] = new EditorSessionItemDto
            {
                AccessToken = item.AccessToken,
                TokenPrefix = item.TokenPrefix,
                ResourceId = item.ResourceId,
                TemplateId = item.TemplateId,
                LetterheadId = item.LetterheadId,
                Kind = item.Kind,
                OfficeKind = officeKind,
                DisplayName = displayName,
                UserId = item.UserId,
                UserName = item.UserName,
                ReadOnly = item.ReadOnly,
                CreatedAt = item.CreatedAt,
                LastSeenAt = item.LastSeenAt
            };
        }
    }

    private async Task<Dictionary<string, DmResource>> LoadResourcesByIdsAsync(
        IReadOnlyList<string> ids,
        string token,
        CancellationToken ct)
    {
        var map = new Dictionary<string, DmResource>(StringComparer.Ordinal);
        if (ids.Count == 0)
            return map;

        var page = await _dg.QueryPageAsync(
            DmDatasets.Resources,
            new Dictionary<string, object?>
            {
                ["__dataId"] = new Dictionary<string, object?> { ["$in"] = ids.ToArray() }
            },
            $"limit={ids.Count}",
            token,
            ct);

        foreach (var row in page.Items)
        {
            var resource = MapQueryRow<DmResource>(row);
            if (resource?.__dataId is { } dataId && !string.IsNullOrWhiteSpace(dataId))
                map[dataId] = resource;
        }

        return map;
    }

    private async Task<Dictionary<string, string>> LoadTemplatesByIdsAsync(
        IReadOnlyList<string> ids,
        string token,
        CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ids.Count == 0)
            return map;

        var page = await _dg.QueryPageAsync(
            DmDatasets.DocumentTemplates,
            new Dictionary<string, object?>
            {
                ["__dataId"] = new Dictionary<string, object?> { ["$in"] = ids.ToArray() }
            },
            $"limit={ids.Count}",
            token,
            ct);

        foreach (var row in page.Items)
        {
            var template = MapQueryRow<DmDocumentTemplate>(row);
            if (template?.__dataId is not { } dataId || string.IsNullOrWhiteSpace(dataId))
                continue;
            map[dataId] = template.name?.Trim() ?? template.code?.Trim() ?? dataId;
        }

        return map;
    }

    private async Task<Dictionary<string, string>> LoadLetterheadsByIdsAsync(
        IReadOnlyList<string> ids,
        string token,
        CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ids.Count == 0)
            return map;

        var page = await _dg.QueryPageAsync(
            DmDatasets.Letterheads,
            new Dictionary<string, object?>
            {
                ["__dataId"] = new Dictionary<string, object?> { ["$in"] = ids.ToArray() }
            },
            $"limit={ids.Count}",
            token,
            ct);

        foreach (var row in page.Items)
        {
            var letterhead = MapQueryRow<DmLetterhead>(row);
            if (letterhead?.__dataId is not { } dataId || string.IsNullOrWhiteSpace(dataId))
                continue;
            map[dataId] = letterhead.name?.Trim() ?? letterhead.code?.Trim() ?? dataId;
        }

        return map;
    }

    private static T? MapQueryRow<T>(Dictionary<string, object?> row) where T : class
    {
        var json = JsonSerializer.Serialize(row, JsonOptions);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static string? ResolveOfficeKind(string? extension, string? mimeType)
    {
        if (!ManagedOfficeProfiles.TryResolve(extension, mimeType, out var profile))
            return null;

        return profile.Kind switch
        {
            ManagedOfficeKind.Sheet => "sheet",
            ManagedOfficeKind.Presentation => "presentation",
            _ => "document"
        };
    }

    private static string ResolveResourceLabel(DmResource resource)
    {
        var name = resource.name?.Trim();
        if (!string.IsNullOrWhiteSpace(name))
            return name;

        var title = resource.title?.Trim();
        if (!string.IsNullOrWhiteSpace(title))
            return title;

        return resource.__dataId ?? "—";
    }

    private static string ResolveKind(WopiSession session)
    {
        if (!string.IsNullOrWhiteSpace(session.ResourceId))
            return "resource";
        if (!string.IsNullOrWhiteSpace(session.TemplateId))
            return "template";
        if (!string.IsNullOrWhiteSpace(session.LetterheadId))
            return "letterhead";
        return "unknown";
    }

    private void EnsureWithinLimits(WopiSession newSession, EditorLimitsSettings limits, TimeSpan idleTimeout)
    {
        var active = _sessions.ListActive(idleTimeout);
        var connections = active.Count;
        var newDocumentKey = ResolveDocumentKey(newSession);

        if (connections >= limits.MaxConcurrentConnections)
            ThrowCapacityFull(active, limits, connections, CountDistinctDocuments(active));

        if (!string.IsNullOrWhiteSpace(newDocumentKey))
        {
            var hasDocument = active.Any(s => string.Equals(ResolveDocumentKey(s.Session), newDocumentKey, StringComparison.Ordinal));
            if (!hasDocument)
            {
                var documentCount = CountDistinctDocuments(active);
                if (documentCount >= limits.MaxConcurrentDocuments)
                    ThrowCapacityFull(active, limits, connections, documentCount);
            }
        }

        if (limits.MaxSessionsPerUser > 0)
        {
            var userCount = active.Count(s => string.Equals(s.Session.UserId, newSession.UserId, StringComparison.Ordinal));
            if (userCount >= limits.MaxSessionsPerUser)
            {
                throw DocumentException.TooManyRequests(
                    "EDITOR_USER_SESSION_LIMIT",
                    $"User session limit reached ({userCount}/{limits.MaxSessionsPerUser}).",
                    $"Kullanıcı başına editör oturum sınırına ulaşıldı ({userCount}/{limits.MaxSessionsPerUser}).");
            }
        }
    }

    private static void ThrowCapacityFull(
        IReadOnlyList<WopiActiveSession> active,
        EditorLimitsSettings limits,
        int connections,
        int documents)
    {
        throw DocumentException.TooManyRequests(
            "EDITOR_CAPACITY_FULL",
            $"Editor capacity is full ({connections}/{limits.MaxConcurrentConnections} connections, {documents}/{limits.MaxConcurrentDocuments} documents).",
            $"Editör kapasitesi dolu ({connections}/{limits.MaxConcurrentConnections} bağlantı, {documents}/{limits.MaxConcurrentDocuments} döküman).");
    }

    private void EnsureCanManageSession(WopiSession session)
    {
        if (_ctx.IsAdmin || _ctx.IsManager)
            return;

        var callerId = _ctx.UserId ?? _ctx.Username;
        if (string.IsNullOrWhiteSpace(callerId)
            || !string.Equals(callerId, session.UserId, StringComparison.Ordinal))
        {
            throw DocumentException.Forbidden("Bu oturumu sonlandırma yetkiniz yok.");
        }
    }

    private TimeSpan GetIdleTimeout()
    {
        var minutes = Math.Clamp(_settings.EditorLimits.IdleTimeoutMinutes, 5, 1440);
        return TimeSpan.FromMinutes(minutes);
    }

    private static int CountDistinctDocuments(IReadOnlyList<WopiActiveSession> active) =>
        active
            .Select(s => ResolveDocumentKey(s.Session))
            .Where(k => k is not null)
            .Distinct(StringComparer.Ordinal)
            .Count();

    internal static string? ResolveDocumentKey(WopiSession session)
    {
        if (!string.IsNullOrWhiteSpace(session.ResourceId))
            return "r:" + session.ResourceId.Trim();

        if (!string.IsNullOrWhiteSpace(session.TemplateId))
            return "t:" + session.TemplateId.Trim();

        if (!string.IsNullOrWhiteSpace(session.LetterheadId))
            return "l:" + session.LetterheadId.Trim();

        return null;
    }

    private static string FormatTokenPrefix(string token) =>
        token.Length <= 8 ? token : token[..8] + "…";

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
