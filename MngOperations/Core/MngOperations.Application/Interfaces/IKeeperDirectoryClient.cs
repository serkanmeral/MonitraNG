using MngOperations.Application.Contracts.Runtime;

namespace MngOperations.Application.Interfaces;

/// <summary>
/// MngKeeper kullanıcı dizini — tekil id ile kullanıcı çözümleme (Bearer forward).
/// Keeper'da toplu (by-ids) endpoint olmadığından id başına çağrılır; cache <see cref="IPersonDirectory"/>'de.
/// </summary>
public interface IKeeperDirectoryClient
{
    /// <summary>Kullanıcıyı id ile çözer; bulunamazsa veya yapılandırılmamışsa null döner.</summary>
    Task<PersonDisplayDto?> GetUserAsync(
        string userId,
        string token,
        CancellationToken cancellationToken = default);
}
