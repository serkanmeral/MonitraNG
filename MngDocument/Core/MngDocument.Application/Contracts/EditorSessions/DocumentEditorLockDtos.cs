namespace MngDocument.Application.Contracts.EditorSessions;

public sealed class DocumentActiveEditorDto
{
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public DateTime LastSeenAt { get; init; }
    public bool IsCurrentUser { get; init; }
}

public sealed class DocumentEditorLockStatusDto
{
    public string? ResourceId { get; init; }
    public string? TemplateId { get; init; }
    public string? LetterheadId { get; init; }
    /// <summary>Herhangi bir aktif düzenleme oturumu var (kendisi dahil).</summary>
    public bool IsLocked { get; init; }
    public bool IsLockedByOthers { get; init; }
    public bool IsLockedBySelf { get; init; }
    public bool WarnOnActiveEditor { get; init; }
    public bool EnforceExclusiveLock { get; init; }
    public bool CanBypassLock { get; init; }
    public IReadOnlyList<DocumentActiveEditorDto> ActiveEditors { get; init; } = Array.Empty<DocumentActiveEditorDto>();
}
