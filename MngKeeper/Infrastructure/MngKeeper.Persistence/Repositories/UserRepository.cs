using MongoDB.Driver;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Infrastructure.Persistence.Repositories
{
    public class UserRepository : MongoRepository<User>, IUserRepository
    {
        public UserRepository(IMongoDatabase database, ILogger<UserRepository> logger) 
            : base(database, "users", logger)
        {
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(x => x.Email, email);
                return await _collection.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email: {Email}", email);
                return null;
            }
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(x => x.Username, username);
                return await _collection.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by username: {Username}", username);
                return null;
            }
        }

        public async Task<IEnumerable<User>> GetByGroupIdAsync(string groupId)
        {
            try
            {
                var filter = Builders<User>.Filter.AnyEq(x => x.Groups, groupId);
                return await _collection.Find(filter).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by group id: {GroupId}", groupId);
                return Enumerable.Empty<User>();
            }
        }

        public async Task<bool> ExistsByEmailAsync(string email)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(x => x.Email, email);
                return await _collection.Find(filter).AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user existence by email: {Email}", email);
                return false;
            }
        }

        public async Task<bool> ExistsByUsernameAsync(string username)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(x => x.Username, username);
                return await _collection.Find(filter).AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user existence by username: {Username}", username);
                return false;
            }
        }

        public async Task<IEnumerable<User>> GetByDomainIdAsync(string domainId)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(x => x.DomainId, domainId);
                return await _collection.Find(filter).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by domain id: {DomainId}", domainId);
                return Enumerable.Empty<User>();
            }
        }

        protected override string GetEntityId(User entity)
        {
            return entity.Id;
        }
    }
}
