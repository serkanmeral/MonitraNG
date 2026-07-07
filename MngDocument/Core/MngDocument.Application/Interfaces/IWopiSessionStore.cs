namespace MngDocument.Application.Interfaces;

public sealed class WopiSession
{
    public required string TemplateId { get; init; }
    /// <summary>Dolu ise WOPI oturumu <c>dm_resources</c> dosyası içindir.</summary>
    public string? ResourceId { get; init; }
    /// <summary>Dolu ise WOPI oturumu antet tasarım DOCX içindir.</summary>
    public string? LetterheadId { get; init; }
    public bool ReadOnly { get; init; }
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required string DataGatewayToken { get; init; }
    public string Version { get; set; } = "1";
    /// <summary>Dolu ise WOPI yanıtı bu sürüm anlık görüntüsünden okunur (salt okunur önizleme).</summary>
    public int? PreviewVersionNumber { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    /// <summary>Collabora PostMessage hedefi — tarayıcı <c>window.location.origin</c>.</summary>
    public string? PostMessageOrigin { get; init; }
}

public sealed class WopiActiveSession
{
    public required string AccessToken { get; init; }
    public required WopiSession Session { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime LastSeenAt { get; init; }
}

public interface IWopiSessionStore
{
    string CreateSession(WopiSession session, TimeSpan ttl);
    WopiSession? GetSession(string accessToken);
    void BumpVersion(string accessToken, string newVersion);
    void Touch(string accessToken);
    bool Revoke(string accessToken);
    int RevokeByUser(string userId);
    IReadOnlyList<WopiActiveSession> ListActive(TimeSpan idleTimeout);
    void PurgeExpired(TimeSpan idleTimeout);
}
