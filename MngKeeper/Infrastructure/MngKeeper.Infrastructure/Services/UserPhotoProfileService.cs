using MngKeeper.Application.Directory;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Domain.Enums;
using Microsoft.Extensions.Logging;

using DomainEntity = MngKeeper.Domain.Entities.Domain;

namespace MngKeeper.Infrastructure.Services;

public class UserPhotoProfileService : IUserPhotoProfileService
{
    private static readonly string[] PhotoExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private readonly IMinioService _minioService;
    private readonly IKeycloakService _keycloakService;
    private readonly IUserRepository _userRepository;
    private readonly IDataGatewaySyncService _dataGatewaySyncService;
    private readonly ILogger<UserPhotoProfileService> _logger;

    public UserPhotoProfileService(
        IMinioService minioService,
        IKeycloakService keycloakService,
        IUserRepository userRepository,
        IDataGatewaySyncService dataGatewaySyncService,
        ILogger<UserPhotoProfileService> logger)
    {
        _minioService = minioService;
        _keycloakService = keycloakService;
        _userRepository = userRepository;
        _dataGatewaySyncService = dataGatewaySyncService;
        _logger = logger;
    }

    public string GetBucketName(DomainEntity domain) =>
        domain.DatabaseName.ToLowerInvariant().Replace("_", "-");

    public async Task<bool> PutUserPhotoAsync(
        DomainEntity domain,
        string userId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var bucketName = GetBucketName(domain);
        var extension = ContentTypeToExtension(contentType);
        var objectName = BuildObjectName(userId, extension);

        await DeleteUserPhotoObjectsAsync(domain, userId, cancellationToken);

        return await _minioService.PutObjectAsync(bucketName, objectName, content, contentType, cancellationToken);
    }

    public async Task<bool> DeleteUserPhotoObjectsAsync(
        DomainEntity domain,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var bucketName = GetBucketName(domain);
        var deletedAny = false;

        foreach (var ext in PhotoExtensions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var objectName = BuildObjectName(userId, ext);
            if (await _minioService.RemoveObjectAsync(bucketName, objectName, cancellationToken))
                deletedAny = true;
        }

        return deletedAny;
    }

    public async Task<bool> TryImportDirectoryPhotoAsync(
        User user,
        DomainEntity domain,
        CancellationToken cancellationToken = default)
    {
        if (user.ProvisioningSource != UserProvisioningSource.Directory)
            return false;

        if (user.PhotoSource == UserPhotoSource.Manual)
            return false;

        // Legacy kayıtlar: photoUrl var ama photoSource henüz yazılmamış → manuel kabul et.
        if (user.PhotoSource == UserPhotoSource.None && !string.IsNullOrWhiteSpace(user.PhotoUrl))
            return false;

        if (string.IsNullOrWhiteSpace(user.KeycloakUserId))
            return false;

        var photo = await _keycloakService.GetRealmUserPhotoAsync(
            domain.RealmName,
            user.KeycloakUserId,
            cancellationToken);

        if (photo == null || photo.Bytes.Length == 0)
            return false;

        var hash = UserPhotoProfileHelper.ComputeSha256Hex(photo.Bytes);
        if (user.PhotoSource == UserPhotoSource.Directory
            && string.Equals(user.DirectoryPhotoHash, hash, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        await using var stream = new MemoryStream(photo.Bytes);
        var uploaded = await PutUserPhotoAsync(domain, user.Id, stream, photo.ContentType, cancellationToken);
        if (!uploaded)
        {
            _logger.LogWarning(
                "Directory photo import failed for user {UserId} (Keycloak {KeycloakUserId})",
                user.Id, user.KeycloakUserId);
            return false;
        }

        UserPhotoProfileHelper.ApplyDirectoryPhoto(
            user,
            UserPhotoProfileHelper.BuildPhotoUrl(user.Id),
            hash);

        _logger.LogInformation(
            "Directory photo imported for user {UserId} ({Username})",
            user.Id, user.Username);

        return true;
    }

    public async Task PersistManualUploadAsync(
        User user,
        DomainEntity domain,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var uploaded = await PutUserPhotoAsync(domain, user.Id, content, contentType, cancellationToken);
        if (!uploaded)
            throw new InvalidOperationException("Failed to upload photo to object storage.");

        UserPhotoProfileHelper.ApplyManualPhoto(user, UserPhotoProfileHelper.BuildPhotoUrl(user.Id));
        await _userRepository.UpdateAsync(user);

        try
        {
            await _dataGatewaySyncService.SyncUserToDataGatewayAsync(user, domain.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync user photo to DataGateway: {UserId}", user.Id);
        }
    }

    public async Task PersistPhotoRemovalAsync(
        User user,
        DomainEntity domain,
        CancellationToken cancellationToken = default)
    {
        await DeleteUserPhotoObjectsAsync(domain, user.Id, cancellationToken);
        UserPhotoProfileHelper.ClearPhoto(user);
        await _userRepository.UpdateAsync(user);

        try
        {
            await _dataGatewaySyncService.SyncUserToDataGatewayAsync(user, domain.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync photo removal to DataGateway: {UserId}", user.Id);
        }
    }

    private static string BuildObjectName(string userId, string extension) =>
        $"data/users/{userId}/photo{extension}";

    private static string ContentTypeToExtension(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/jpeg" or "image/jpg" => ".jpg",
            _ => ".jpg",
        };
}
