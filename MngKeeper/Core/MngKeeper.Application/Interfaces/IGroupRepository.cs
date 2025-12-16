using MngKeeper.Domain.Entities;
using MngKeeper.Application.Common.DTOs;

namespace MngKeeper.Application.Interfaces
{
    public interface IGroupRepository
    {
        // IRepository methods with domainId
        Task<Group?> GetByIdAsync(string id, string domainId);
        Task<Group> AddAsync(Group entity);
        Task<Group> UpdateAsync(Group entity);
        Task<bool> DeleteAsync(string id, string domainId);
        Task<bool> ExistsAsync(string id, string domainId);
        
        // Group-specific methods with domainId
        Task<Group?> GetByNameAsync(string name, string domainId);
        Task<IEnumerable<Group>> GetByDomainIdAsync(string domainId);
        
        /// <summary>
        /// Get groups by domain with pagination, search and filtering support
        /// </summary>
        Task<QueryResult<Group>> GetByDomainIdWithPaginationAsync(
            string domainId,
            int page,
            int pageSize,
            string? searchTerm = null,
            bool? isActive = null);
        
        Task<bool> ExistsByNameAsync(string name, string domainId);
    }
}
