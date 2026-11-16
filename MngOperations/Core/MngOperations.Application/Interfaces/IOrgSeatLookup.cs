namespace MngOperations.Application.Interfaces;

/// <summary>
/// org_mevkiler satırından karar anındaki oturan kişiyi okur.
/// </summary>
public interface IOrgSeatLookup
{
    Task<string?> ResolveOccupantAsync(string? mevkiId, CancellationToken cancellationToken = default);
}
