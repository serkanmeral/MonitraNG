namespace MngKeeper.Domain.Enums;

/// <summary>
/// Profil fotoğrafının kaynağı. Directory sync yalnızca Manual olmayan kayıtları günceller.
/// </summary>
public enum UserPhotoSource
{
    None = 0,
    Directory = 1,
    Manual = 2,
}
