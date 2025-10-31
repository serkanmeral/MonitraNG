using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Interfaces
{
    public interface IDomainRepository : IRepository<MngKeeper.Domain.Entities.Domain>
    {
        Task<MngKeeper.Domain.Entities.Domain?> GetByNameAsync(string name);
        Task<MngKeeper.Domain.Entities.Domain?> GetByRealmNameAsync(string realmName);
        Task<IEnumerable<MngKeeper.Domain.Entities.Domain>> GetByStatusAsync(DomainStatus status);
        Task<bool> ExistsByNameAsync(string name);
        Task<bool> CreateDatabaseAsync(string databaseName);
        Task<bool> DeleteDatabaseAsync(string databaseName);
    }
}
