using MongoDB.Driver;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Application.Common.DTOs;
using MngKeeper.Application.Directory;
using MngKeeper.Domain.Enums;
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

        private async Task<IMongoCollection<BsonDocument>> GetCollectionAsync(string domainId)
        {
            var domain = await _domainRepository.GetByIdAsync(domainId);
            if (domain == null)
            {
                throw new InvalidOperationException($"Domain not found: {domainId}");
            }
            var database = _mongoClient.GetDatabase(domain.DatabaseName);
            return database.GetCollection<BsonDocument>("@groups");
        }

        private Group? MapBsonDocumentToGroup(BsonDocument? doc)
        {
            if (doc == null) return null;
            
            // Get ID from __dataId if exists, otherwise from _id
            var idValue = doc.Contains("__dataId") ? doc["__dataId"] : doc["_id"];
            var id = idValue.IsObjectId ? idValue.AsObjectId.ToString() : idValue.ToString();
            
            return new Group
            {
                Id = id,
                Name = doc.GetValue("name", "").AsString,
                Description = doc.GetValue("description", BsonNull.Value).IsBsonNull ? null : doc.GetValue("description").AsString,
                Permissions = doc.GetValue("permissions", new BsonArray()).AsBsonArray.Select(x => x.AsString).ToList(),
                DomainId = doc.GetValue("domainId", "").AsString,
                KeycloakGroupId =
                    doc.Contains("keycloakGroupId") && !doc["keycloakGroupId"].IsBsonNull
                        ? doc["keycloakGroupId"].AsString
                        : string.Empty,
                IsActive = doc.GetValue("isActive", true).AsBoolean,
                IncludeInApplication = ApplicationScopeDefaults.ResolveFromDocument(doc),
                CreatedAt = doc.GetValue("createdAt", DateTime.UtcNow).ToUniversalTime(),
                CreatedBy = doc.GetValue("createdBy", "").AsString,
                UpdatedAt = doc.GetValue("updatedAt", BsonNull.Value).IsBsonNull ? null : doc.GetValue("updatedAt").ToUniversalTime(),
                UpdatedBy = doc.GetValue("updatedBy", BsonNull.Value).IsBsonNull ? null : doc.GetValue("updatedBy").AsString,
                ProvisioningSource = doc.Contains("provisioningSource") && !doc["provisioningSource"].IsBsonNull
                    ? (UserProvisioningSource)doc["provisioningSource"].AsInt32
                    : UserProvisioningSource.Local,
                DirectorySyncedAt = doc.Contains("directorySyncedAt") && !doc["directorySyncedAt"].IsBsonNull
                    ? doc["directorySyncedAt"].ToUniversalTime()
                    : null,
            };
        }

        public async Task<Group?> GetByIdAsync(string id, string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<BsonDocument>.Filter.Eq("__dataId", id);
                var doc = await collection.Find(filter).FirstOrDefaultAsync();
                return MapBsonDocumentToGroup(doc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group by id: {Id}, domainId: {DomainId}", id, domainId);
                return null;
            }
        }

        public async Task<IEnumerable<Group>> GetByIdsAsync(IEnumerable<string> ids, string domainId)
        {
            try
            {
                var list = (ids ?? Enumerable.Empty<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (list.Count == 0)
                    return Enumerable.Empty<Group>();

                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<BsonDocument>.Filter.In("__dataId", list);
                var docs = await collection.Find(filter).ToListAsync();
                return docs.Select(doc => MapBsonDocumentToGroup(doc)).Where(g => g != null).Cast<Group>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting groups by ids, domainId: {DomainId}", domainId);
                return Enumerable.Empty<Group>();
            }
        }

        public async Task<Group> AddAsync(Group entity)
        {
            try
            {
                var collection = await GetCollectionAsync(entity.DomainId);
                
                // Convert Group entity to BsonDocument and add __dataId field for DataGateway compatibility
                var document = new BsonDocument
                {
                    ["_id"] = MongoDB.Bson.ObjectId.Parse(entity.Id),
                    ["__dataId"] = entity.Id, // Required for DataGateway @groups collection
                    ["name"] = entity.Name,
                    ["description"] = string.IsNullOrEmpty(entity.Description) ? BsonNull.Value : entity.Description,
                    ["permissions"] = new BsonArray(entity.Permissions ?? new List<string>()),
                    ["domainId"] = entity.DomainId,
                    ["keycloakGroupId"] = string.IsNullOrWhiteSpace(entity.KeycloakGroupId) ? string.Empty : entity.KeycloakGroupId,
                    ["isActive"] = entity.IsActive,
                    ["includeInApplication"] = entity.IncludeInApplication,
                    ["createdAt"] = entity.CreatedAt,
                    ["createdBy"] = entity.CreatedBy,
                    ["updatedAt"] = entity.UpdatedAt.HasValue ? entity.UpdatedAt.Value : BsonNull.Value,
                    ["updatedBy"] = string.IsNullOrEmpty(entity.UpdatedBy) ? BsonNull.Value : entity.UpdatedBy,
                    ["provisioningSource"] = (int)entity.ProvisioningSource,
                    ["directorySyncedAt"] = entity.DirectorySyncedAt.HasValue ? entity.DirectorySyncedAt.Value : BsonNull.Value,
                };
                
                await collection.InsertOneAsync(document);
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
                var filter = Builders<BsonDocument>.Filter.Eq("__dataId", entity.Id);
                
                // Use UpdateOneAsync instead of ReplaceOneAsync to avoid _id immutable field error
                var updateBuilder = Builders<BsonDocument>.Update;
                var updateDefinition = updateBuilder
                    .Set("name", (BsonValue)entity.Name)
                    .Set("permissions", new BsonArray(entity.Permissions ?? new List<string>()))
                    .Set("isActive", (BsonValue)entity.IsActive)
                    .Set("includeInApplication", entity.IncludeInApplication)
                    .Set("updatedAt", entity.UpdatedAt.HasValue ? (BsonValue)entity.UpdatedAt.Value : BsonNull.Value);
                
                // Handle description (can be null or empty)
                if (string.IsNullOrEmpty(entity.Description))
                {
                    updateDefinition = updateDefinition.Set("description", BsonNull.Value);
                }
                else
                {
                    updateDefinition = updateDefinition.Set("description", (BsonValue)entity.Description);
                }
                
                // Handle updatedBy (can be null or empty)
                if (string.IsNullOrEmpty(entity.UpdatedBy))
                {
                    updateDefinition = updateDefinition.Set("updatedBy", BsonNull.Value);
                }
                else
                {
                    updateDefinition = updateDefinition.Set("updatedBy", (BsonValue)entity.UpdatedBy);
                }
                
                updateDefinition = updateDefinition
                    .Set("provisioningSource", (int)entity.ProvisioningSource)
                    .Set("directorySyncedAt", entity.DirectorySyncedAt.HasValue ? (BsonValue)entity.DirectorySyncedAt.Value : BsonNull.Value);

                // Note: We don't update __dataId, domainId, createdAt, createdBy as these should not change
                
                var result = await collection.UpdateOneAsync(filter, updateDefinition);
                
                if (result.MatchedCount == 0)
                {
                    throw new InvalidOperationException($"Group with id {entity.Id} not found in domain {entity.DomainId}");
                }
                
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
                var filter = Builders<BsonDocument>.Filter.Eq("__dataId", id);
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
                var filter = Builders<BsonDocument>.Filter.Eq("__dataId", id);
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
                var filter = Builders<BsonDocument>.Filter.Eq("name", name);
                var doc = await collection.Find(filter).FirstOrDefaultAsync();
                return MapBsonDocumentToGroup(doc);
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
                var filter = Builders<BsonDocument>.Filter.Eq("domainId", domainId);
                var docs = await collection.Find(filter).ToListAsync();
                return docs.Select(doc => MapBsonDocumentToGroup(doc)).Where(g => g != null).Cast<Group>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting groups by domain id: {DomainId}", domainId);
                return Enumerable.Empty<Group>();
            }
        }

        public async Task<IEnumerable<Group>> GetAllByDomainIdAsync(
            string domainId,
            string? searchTerm = null,
            bool? isActive = null,
            bool? includeInApplication = null,
            UserProvisioningSource? provisioningSource = null)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = BuildGroupListFilter(
                    domainId,
                    searchTerm,
                    isActive,
                    includeInApplication,
                    provisioningSource);

                var docs = await collection.Find(filter).ToListAsync();
                return docs.Select(doc => MapBsonDocumentToGroup(doc)).Where(g => g != null).Cast<Group>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all groups by domain id: {DomainId}", domainId);
                return Enumerable.Empty<Group>();
            }
        }

        public async Task<QueryResult<Group>> GetByDomainIdWithPaginationAsync(
            string domainId,
            int page,
            int pageSize,
            string? searchTerm = null,
            bool? isActive = null,
            bool? includeInApplication = null,
            string? sortBy = null,
            string? sortOrder = null,
            UserProvisioningSource? provisioningSource = null)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = BuildGroupListFilter(
                    domainId,
                    searchTerm,
                    isActive,
                    includeInApplication,
                    provisioningSource);

                var totalCount = await collection.CountDocumentsAsync(filter);

                var sortBuilder = Builders<BsonDocument>.Sort;
                SortDefinition<BsonDocument>? sortDefinition = null;

                if (!string.IsNullOrWhiteSpace(sortBy))
                {
                    var isAscending = string.IsNullOrWhiteSpace(sortOrder) ||
                                     sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);

                    sortDefinition = isAscending
                        ? sortBuilder.Ascending(sortBy)
                        : sortBuilder.Descending(sortBy);
                }

                var skip = (page - 1) * pageSize;
                var findQuery = collection.Find(filter);

                if (sortDefinition != null)
                {
                    findQuery = findQuery.Sort(sortDefinition);
                }

                var docs = await findQuery
                    .Skip(skip)
                    .Limit(pageSize)
                    .ToListAsync();

                var groups = docs.Select(doc => MapBsonDocumentToGroup(doc)).Where(g => g != null).Cast<Group>();

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

        private static FilterDefinition<BsonDocument> BuildGroupListFilter(
            string domainId,
            string? searchTerm,
            bool? isActive,
            bool? includeInApplication,
            UserProvisioningSource? provisioningSource)
        {
            var filterBuilder = Builders<BsonDocument>.Filter;
            var filter = filterBuilder.Eq("domainId", domainId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var searchFilter = filterBuilder.Or(
                    filterBuilder.Regex("name", new BsonRegularExpression(searchTerm, "i")),
                    filterBuilder.Regex("description", new BsonRegularExpression(searchTerm, "i"))
                );
                filter &= searchFilter;
            }

            if (isActive.HasValue)
            {
                filter &= filterBuilder.Eq("isActive", isActive.Value);
            }

            if (includeInApplication.HasValue)
            {
                filter &= ApplicationScopeMongoFilters.IncludeInApplicationEquals(includeInApplication.Value);
            }

            if (provisioningSource.HasValue)
            {
                filter &= ProvisioningSourceMongoFilters.Equals(filterBuilder, provisioningSource.Value);
            }

            return filter;
        }

        public async Task<bool> ExistsByNameAsync(string name, string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<BsonDocument>.Filter.Eq("name", name);
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
