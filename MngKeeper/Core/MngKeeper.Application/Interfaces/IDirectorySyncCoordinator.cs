namespace MngKeeper.Application.Interfaces;

/// <summary>
/// Domain başına tek aktif tam directory sync (409 when busy).
/// </summary>
public interface IDirectorySyncCoordinator
{
    bool TryBeginSync(string domainId);
    void EndSync(string domainId);
}
