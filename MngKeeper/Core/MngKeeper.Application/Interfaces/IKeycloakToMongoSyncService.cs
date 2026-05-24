using MngKeeper.Application.Directory;

namespace MngKeeper.Application.Interfaces;

public interface IKeycloakToMongoSyncService
{
    Task<DirectorySyncResult> SyncDomainAsync(string domainId, DirectorySyncTrigger trigger, CancellationToken cancellationToken = default);

    /// <summary>
    /// Login sonrası oturum açan kullanıcı için KC→Mongo sync (tam sync ile aynı domain kilidi).
    /// </summary>
    Task<DirectorySyncResult> SyncUserOnLoginAsync(
        string domainId,
        string username,
        CancellationToken cancellationToken = default);
}
