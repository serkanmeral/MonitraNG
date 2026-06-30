using MngKeeper.Domain.Entities;
using DomainEntity = MngKeeper.Domain.Entities.Domain;

namespace MngKeeper.Application.Interfaces;

public interface IUserPhotoProfileService
{
    string GetBucketName(DomainEntity domain);

    Task<bool> PutUserPhotoAsync(
        DomainEntity domain,
        string userId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteUserPhotoObjectsAsync(
        DomainEntity domain,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Directory kullanıcı için Keycloak/AD fotoğrafını MinIO'ya yazar.
    /// Manual kaynaklı fotoğraflara dokunmaz. Değişiklik olduysa true döner.
    /// </summary>
    Task<bool> TryImportDirectoryPhotoAsync(
        User user,
        DomainEntity domain,
        CancellationToken cancellationToken = default);

    Task PersistManualUploadAsync(
        User user,
        DomainEntity domain,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task PersistPhotoRemovalAsync(
        User user,
        DomainEntity domain,
        CancellationToken cancellationToken = default);
}
