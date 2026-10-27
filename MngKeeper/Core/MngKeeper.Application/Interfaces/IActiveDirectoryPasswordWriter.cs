namespace MngKeeper.Application.Interfaces;

public interface IActiveDirectoryPasswordWriter
{
    /// <summary>User changes their own password. Binds as that user over LDAPS.</summary>
    Task ChangeOwnPasswordAsync(
        MngKeeper.Domain.Entities.Domain domain,
        string username,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>Admin reset. Binds as the personnel reset account and replaces unicodePwd.</summary>
    Task ResetPasswordAsync(
        MngKeeper.Domain.Entities.Domain domain,
        string username,
        string newPassword,
        CancellationToken cancellationToken = default);
}
