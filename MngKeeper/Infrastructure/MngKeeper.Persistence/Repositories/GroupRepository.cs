using MongoDB.Driver;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Infrastructure.Persistence.Repositories
{
    public class GroupRepository : MongoRepository<Group>, IGroupRepository
    {
        public GroupRepository(IMongoDatabase database, ILogger<GroupRepository> logger) 
            : base(database, "groups", logger)
        {
        }

        public async Task<Group?> GetByNameAsync(string name)
        {
            try
            {
                var filter = Builders<Group>.Filter.Eq(x => x.Name, name);
                return await _collection.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group by name: {Name}", name);
                return null;
            }
        }

        public async Task<IEnumerable<Group>> GetByDomainIdAsync(string domainId)
        {
            try
            {
                var filter = Builders<Group>.Filter.Eq(x => x.DomainId, domainId);
                return await _collection.Find(filter).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting groups by domain id: {DomainId}", domainId);
                return Enumerable.Empty<Group>();
            }
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            try
            {
                var filter = Builders<Group>.Filter.Eq(x => x.Name, name);
                return await _collection.Find(filter).AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking group existence by name: {Name}", name);
                return false;
            }
        }

        protected override string GetEntityId(Group entity)
        {
            return entity.Id;
        }
    }
}
