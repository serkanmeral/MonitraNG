using MngKeeper.Domain.Entities;
using MngKeeper.Application.Common.DTOs;

namespace MngKeeper.Application.Interfaces
{
    public interface IUserRepository
    {
        // IRepository methods with domainId
        Task<User?> GetByIdAsync(string id, string domainId);
        /// <summary><c>cht_messages.authorPersonId</c> veya JWT <c>sub</c> gibi Keycloak kullanıcı id ile arama.</summary>
        Task<User?> GetByKeycloakUserIdAsync(string keycloakUserId, string domainId);
        Task<User> AddAsync(User entity);
        Task<User> UpdateAsync(User entity);
        Task<bool> DeleteAsync(string id, string domainId);
        Task<bool> ExistsAsync(string id, string domainId);
        
        // User-specific methods with domainId
        Task<User?> GetByUsernameAsync(string username, string domainId);
        Task<User?> GetByEmailAsync(string email, string domainId);
        Task<IEnumerable<User>> GetByDomainIdAsync(string domainId);
        
        /// <summary>
        /// Get all users by domain with search, filtering and sorting support (no pagination)
        /// </summary>
        Task<IEnumerable<User>> GetAllByDomainIdAsync(
            string domainId,
            string? searchTerm = null,
            bool? isActive = null,
            string? sortBy = null,
            string? sortOrder = null);
        
        /// <summary>
        /// Get users by domain with pagination, search, filtering and sorting support
        /// </summary>
        Task<QueryResult<User>> GetByDomainIdWithPaginationAsync(
            string domainId,
            int page,
            int pageSize,
            string? searchTerm = null,
            bool? isActive = null,
            string? sortBy = null,
            string? sortOrder = null);
        
        Task<bool> ExistsByUsernameAsync(string username, string domainId);
        Task<bool> ExistsByEmailAsync(string email, string domainId);
        Task<IEnumerable<User>> GetByGroupIdAsync(string groupId, string domainId);
    }
}
