namespace MngDocument.Application.Contracts.EditorSessions;

public sealed class EditorSessionLimitsDto
{
    public int MaxConnections { get; init; }
    public int MaxDocuments { get; init; }
    public int MaxSessionsPerUser { get; init; }
}

public sealed class CollaboraHomeModeLimitsDto
{
    public int MaxConnections { get; init; }
    public int MaxDocuments { get; init; }
}

public sealed class EditorSessionUserStatsDto
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int ConnectionCount { get; init; }
}

public sealed class EditorSessionItemDto
{
    /// <summary>Yalnızca manager/admin oturum listesinde — zorla kapatma için.</summary>
    public string? AccessToken { get; init; }
    public string TokenPrefix { get; init; } = string.Empty;
    public string? ResourceId { get; init; }
    public string? TemplateId { get; init; }
    public string? LetterheadId { get; init; }
    /// <summary>resource | template | letterhead</summary>
    public string Kind { get; init; } = string.Empty;
    /// <summary>Office içerik profili: document | sheet | presentation (yalnızca editörde açılan dosyalar).</summary>
    public string? OfficeKind { get; init; }
    public string? DisplayName { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public bool ReadOnly { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastSeenAt { get; init; }
}

public sealed class EditorSessionStatsDto
{
    public int ActiveConnections { get; init; }
    public int ActiveDocuments { get; init; }
    public EditorSessionLimitsDto Limits { get; init; } = new();
    public CollaboraHomeModeLimitsDto CollaboraHomeMode { get; init; } = new();
    public IReadOnlyList<EditorSessionUserStatsDto> ByUser { get; init; } = Array.Empty<EditorSessionUserStatsDto>();
    public IReadOnlyList<EditorSessionItemDto>? Sessions { get; init; }
}
