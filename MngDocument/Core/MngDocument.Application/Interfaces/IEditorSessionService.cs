namespace MngDocument.Application.Interfaces;

using MngDocument.Application.Contracts.EditorSessions;

public interface IEditorSessionService
{
    string BeginSession(WopiSession session, TimeSpan ttl);

    void EndSession(string accessToken);

    Task<EditorSessionStatsDto> GetStatsAsync(bool includeSessionDetails, CancellationToken ct = default);

    DocumentEditorLockStatusDto GetDocumentLockStatus(
        string? resourceId,
        string? templateId,
        string? letterheadId,
        string currentUserId,
        bool isAdminOrManager);
}
