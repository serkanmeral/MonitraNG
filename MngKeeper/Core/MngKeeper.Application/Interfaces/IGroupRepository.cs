using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Interfaces
{
    public interface IGroupRepository : IRepository<Group>
    {
        Task<Group?> GetByNameAsync(string name);
        Task<IEnumerable<Group>> GetByDomainIdAsync(string domainId);
        Task<bool> ExistsByNameAsync(string name);
    }
}
