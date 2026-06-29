namespace MngDocument.Application.Interfaces;

public sealed class WopiSession
{
    public required string TemplateId { get; init; }
    /// <summary>Dolu ise WOPI oturumu <c>dm_resources</c> dosyası içindir (<see cref="TemplateId"/> boş olmalı).</summary>
    public string? ResourceId { get; init; }
    public bool ReadOnly { get; init; }
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required string DataGatewayToken { get; init; }
    public string Version { get; set; } = "1";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public interface IWopiSessionStore
{
    string CreateSession(WopiSession session, TimeSpan ttl);
    WopiSession? GetSession(string accessToken);
    void BumpVersion(string accessToken, string newVersion);
}
