using System.Security.Cryptography;
using MngKeeper.Domain.Entities;
using MngKeeper.Domain.Enums;

namespace MngKeeper.Application.Directory;

public static class UserPhotoProfileHelper
{
    public static string BuildPhotoUrl(string userId) => $"/keeper/api/user/{userId}/photo";

    public static string ComputeSha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    public static void ApplyManualPhoto(User user, string photoUrl)
    {
        user.PhotoUrl = photoUrl;
        user.PhotoSource = UserPhotoSource.Manual;
        user.DirectoryPhotoHash = null;
    }

    public static void ApplyDirectoryPhoto(User user, string photoUrl, string hash)
    {
        user.PhotoUrl = photoUrl;
        user.PhotoSource = UserPhotoSource.Directory;
        user.DirectoryPhotoHash = hash;
    }

    public static void ClearPhoto(User user)
    {
        user.PhotoUrl = null;
        user.PhotoSource = UserPhotoSource.None;
        user.DirectoryPhotoHash = null;
    }

    /// <summary>Profil güncellemesinde photoUrl değiştiyse Manual/None uygular; aynıysa kaynağı korur.</summary>
    public static void ApplyPhotoUrlFromRequest(User user, string? requestPhotoUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(requestPhotoUrl) ? null : requestPhotoUrl.Trim();
        if (string.Equals(user.PhotoUrl, normalized, StringComparison.OrdinalIgnoreCase))
            return;

        if (normalized == null)
            ClearPhoto(user);
        else
            ApplyManualPhoto(user, normalized);
    }
}
