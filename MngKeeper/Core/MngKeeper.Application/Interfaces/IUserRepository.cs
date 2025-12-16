using MngKeeper.Domain.Entities;
using MngKeeper.Application.Common.DTOs;

namespace MngKeeper.Application.Interfaces
{
    public interface IUserRepository
    {
        // IRepository methods with domainId
        Task<User?> GetByIdAsync(string id, string domainId);
        Task<User> AddAsync(User entity);
        Task<User> UpdateAsync(User entity);
        Task<bool> DeleteAsync(string id, string domainId);
        Task<bool> ExistsAsync(string id, string domainId);
        
        // User-specific methods with domainId
        Task<User?> GetByUsernameAsync(string username, string domainId);
        Task<User?> GetByEmailAsync(string email, string domainId);
        Task<IEnumerable<User>> GetByDomainIdAsync(string domainId);
        
        /// <summary>
        /// Get users by domain with pagination, search and filtering support
        /// </summary>
        Task<QueryResult<User>> GetByDomainIdWithPaginationAsync(
            string domainId,
            int page,
            int pageSize,
            string? searchTerm = null,
            bool? isActive = null);
        
        Task<bool> ExistsByUsernameAsync(string username, string domainId);
        Task<bool> ExistsByEmailAsync(string email, string domainId);
        Task<IEnumerable<User>> GetByGroupIdAsync(string groupId, string domainId);
    }
}
