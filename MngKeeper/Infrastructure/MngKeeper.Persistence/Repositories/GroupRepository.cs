using MongoDB.Driver;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Application.Common.DTOs;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace MngKeeper.Infrastructure.Persistence.Repositories
{
    public class GroupRepository : IGroupRepository
    {
        private readonly IMongoClient _mongoClient;
        private readonly IDomainRepository _domainRepository;
        private readonly ILogger<GroupRepository> _logger;

        public GroupRepository(
            IMongoClient mongoClient,
            IDomainRepository domainRepository,
            ILogger<GroupRepository> logger)
        {
            _mongoClient = mongoClient;
            _domainRepository = domainRepository;
            _logger = logger;
        }

        private async Task<IMongoCollection<Group>> GetCollectionAsync(string domainId)
        {
            var domain = await _domainRepository.GetByIdAsync(domainId);
            if (domain == null)
            {
                throw new InvalidOperationException($"Domain not found: {domainId}");
            }
            var database = _mongoClient.GetDatabase(domain.DatabaseName);
            return database.GetCollection<Group>("groups");
        }

        public async Task<Group?> GetByIdAsync(string id, string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<Group>.Filter.Eq("_id", ObjectId.Parse(id));
                return await collection.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group by id: {Id}, domainId: {DomainId}", id, domainId);
                return null;
            }
        }

        public async Task<Group> AddAsync(Group entity)
        {
            try
            {
                var collection = await GetCollectionAsync(entity.DomainId);
                await collection.InsertOneAsync(entity);
                _logger.LogDebug("Group added successfully to domain database: {GroupId}, DomainId: {DomainId}", entity.Id, entity.DomainId);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding group: {GroupId}, DomainId: {DomainId}", entity.Id, entity.DomainId);
                throw;
            }
        }

        public async Task<Group> UpdateAsync(Group entity)
        {
            try
            {
                var collection = await GetCollectionAsync(entity.DomainId);
                var filter = Builders<Group>.Filter.Eq("_id", ObjectId.Parse(entity.Id));
                var options = new ReplaceOptions { IsUpsert = true };
                await collection.ReplaceOneAsync(filter, entity, options);
                _logger.LogDebug("Group updated successfully: {Id}, DomainId: {DomainId}", entity.Id, entity.DomainId);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group: {Id}, DomainId: {DomainId}", entity.Id, entity.DomainId);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string id, string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<Group>.Filter.Eq("_id", ObjectId.Parse(id));
                var result = await collection.DeleteOneAsync(filter);
                _logger.LogDebug("Group deleted successfully: {Id}, DomainId: {DomainId}", id, domainId);
                return result.DeletedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group: {Id}, DomainId: {DomainId}", id, domainId);
                return false;
            }
        }

        public async Task<bool> ExistsAsync(string id, string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<Group>.Filter.Eq("_id", ObjectId.Parse(id));
                return await collection.Find(filter).AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking group existence: {Id}, DomainId: {DomainId}", id, domainId);
                return false;
            }
        }

        public async Task<Group?> GetByNameAsync(string name, string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<Group>.Filter.Eq(x => x.Name, name);
                return await collection.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group by name: {Name}, DomainId: {DomainId}", name, domainId);
                return null;
            }
        }

        public async Task<IEnumerable<Group>> GetByDomainIdAsync(string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<Group>.Filter.Eq(x => x.DomainId, domainId);
                return await collection.Find(filter).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting groups by domain id: {DomainId}", domainId);
                return Enumerable.Empty<Group>();
            }
        }

        public async Task<QueryResult<Group>> GetByDomainIdWithPaginationAsync(
            string domainId,
            int page,
            int pageSize,
            string? searchTerm = null,
            bool? isActive = null)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filterBuilder = Builders<Group>.Filter;
                var filter = filterBuilder.Eq(x => x.DomainId, domainId);

                // Apply search filter (case-insensitive regex)
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var searchFilter = filterBuilder.Or(
                        filterBuilder.Regex(x => x.Name, new BsonRegularExpression(searchTerm, "i")),
                        filterBuilder.Regex(x => x.Description, new BsonRegularExpression(searchTerm, "i"))
                    );
                    filter &= searchFilter;
                }

                // Apply active filter
                if (isActive.HasValue)
                {
                    filter &= filterBuilder.Eq(x => x.IsActive, isActive.Value);
                }

                // Get total count
                var totalCount = await collection.CountDocumentsAsync(filter);

                // Apply pagination
                var skip = (page - 1) * pageSize;
                var groups = await collection
                    .Find(filter)
                    .Skip(skip)
                    .Limit(pageSize)
                    .ToListAsync();

                return new QueryResult<Group>
                {
                    Items = groups,
                    TotalCount = (int)totalCount,
                    Page = page,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting groups by domain id with pagination: {DomainId}", domainId);
                return new QueryResult<Group>
                {
                    Items = Enumerable.Empty<Group>(),
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize
                };
            }
        }

        public async Task<bool> ExistsByNameAsync(string name, string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<Group>.Filter.Eq(x => x.Name, name);
                return await collection.Find(filter).AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking group existence by name: {Name}, DomainId: {DomainId}", name, domainId);
                return false;
            }
        }
    }
}
