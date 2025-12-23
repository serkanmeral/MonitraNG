using MongoDB.Driver;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Application.Common.DTOs;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace MngKeeper.Infrastructure.Persistence.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IMongoClient _mongoClient;
        private readonly IDomainRepository _domainRepository;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(
            IMongoClient mongoClient,
            IDomainRepository domainRepository,
            ILogger<UserRepository> logger)
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
            return database.GetCollection<BsonDocument>("@users");
        }

        private User? MapBsonDocumentToUser(BsonDocument? doc)
        {
            if (doc == null) return null;
            
            // Get ID from __dataId if exists, otherwise from _id
            var idValue = doc.Contains("__dataId") ? doc["__dataId"] : doc["_id"];
            var id = idValue.IsObjectId ? idValue.AsObjectId.ToString() : idValue.ToString();
            
            return new User
            {
                Id = id,
                DomainId = doc.GetValue("domainId", "").AsString,
                KeycloakUserId = doc.GetValue("keycloakUserId", "").AsString,
                Username = doc.GetValue("username", "").AsString,
                Email = doc.GetValue("email", "").AsString,
                FirstName = doc.GetValue("firstName", "").AsString,
                LastName = doc.GetValue("lastName", "").AsString,
                IsActive = doc.GetValue("isActive", true).AsBoolean,
                Groups = doc.GetValue("groups", new BsonArray()).AsBsonArray.Select(x => x.AsString).ToList(),
                Roles = doc.GetValue("roles", new BsonArray()).AsBsonArray.Select(x => x.AsString).ToList(),
                CreatedAt = doc.GetValue("createdAt", DateTime.UtcNow).ToUniversalTime(),
                LastLoginAt = doc.GetValue("lastLoginAt", BsonNull.Value).IsBsonNull ? null : doc.GetValue("lastLoginAt").ToUniversalTime(),
                CreatedBy = doc.GetValue("createdBy", "").AsString,
                UpdatedAt = doc.GetValue("updatedAt", BsonNull.Value).IsBsonNull ? null : doc.GetValue("updatedAt").ToUniversalTime(),
                UpdatedBy = doc.GetValue("updatedBy", BsonNull.Value).IsBsonNull ? null : doc.GetValue("updatedBy").AsString
            };
        }

        public async Task<User?> GetByIdAsync(string id)
        {
            // This method requires domainId, but we don't have it here
            // This should not be used directly - use GetByIdAsync(string id, string domainId) instead
            throw new NotImplementedException("GetByIdAsync(string id) requires domainId. Use GetByIdAsync(string id, string domainId) instead.");
        }

        public async Task<User?> GetByIdAsync(string id, string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<BsonDocument>.Filter.Eq("__dataId", id);
                var doc = await collection.Find(filter).FirstOrDefaultAsync();
                return MapBsonDocumentToUser(doc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by id: {Id}, domainId: {DomainId}", id, domainId);
                return null;
            }
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            // This method requires domainId - should not be used
            throw new NotImplementedException("GetAllAsync() requires domainId. Use GetByDomainIdAsync(string domainId) instead.");
        }

        public async Task<IEnumerable<User>> GetByFilterAsync(Func<User, bool> filter)
        {
            // This method requires domainId - should not be used
            throw new NotImplementedException("GetByFilterAsync() requires domainId.");
        }

        public async Task<User> AddAsync(User entity)
        {
            try
            {
                var collection = await GetCollectionAsync(entity.DomainId);
                
                // Convert User entity to BsonDocument and add __dataId field for DataGateway compatibility
                var document = new BsonDocument
                {
                    ["_id"] = MongoDB.Bson.ObjectId.Parse(entity.Id),
                    ["__dataId"] = entity.Id, // Required for DataGateway @users collection
                    ["domainId"] = entity.DomainId,
                    ["keycloakUserId"] = entity.KeycloakUserId,
                    ["username"] = entity.Username,
                    ["email"] = entity.Email,
                    ["firstName"] = entity.FirstName,
                    ["lastName"] = entity.LastName,
                    ["isActive"] = entity.IsActive,
                    ["groups"] = new BsonArray(entity.Groups ?? new List<string>()),
                    ["roles"] = new BsonArray(entity.Roles ?? new List<string>()),
                    ["createdAt"] = entity.CreatedAt,
                    ["lastLoginAt"] = entity.LastLoginAt.HasValue ? entity.LastLoginAt.Value : BsonNull.Value,
                    ["createdBy"] = entity.CreatedBy,
                    ["updatedAt"] = entity.UpdatedAt.HasValue ? entity.UpdatedAt.Value : BsonNull.Value,
                    ["updatedBy"] = string.IsNullOrEmpty(entity.UpdatedBy) ? BsonNull.Value : entity.UpdatedBy
                };
                
                await collection.InsertOneAsync(document);
                _logger.LogDebug("User added successfully to domain database: {UserId}, DomainId: {DomainId}", entity.Id, entity.DomainId);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user: {UserId}, DomainId: {DomainId}", entity.Id, entity.DomainId);
                throw;
            }
        }

        public async Task<User> UpdateAsync(User entity)
        {
            try
            {
                var collection = await GetCollectionAsync(entity.DomainId);
                var filter = Builders<BsonDocument>.Filter.Eq("__dataId", entity.Id);
                
                var document = new BsonDocument
                {
                    ["_id"] = MongoDB.Bson.ObjectId.Parse(entity.Id),
                    ["__dataId"] = entity.Id,
                    ["domainId"] = entity.DomainId,
                    ["keycloakUserId"] = entity.KeycloakUserId,
                    ["username"] = entity.Username,
                    ["email"] = entity.Email,
                    ["firstName"] = entity.FirstName,
                    ["lastName"] = entity.LastName,
                    ["isActive"] = entity.IsActive,
                    ["groups"] = new BsonArray(entity.Groups ?? new List<string>()),
                    ["roles"] = new BsonArray(entity.Roles ?? new List<string>()),
                    ["createdAt"] = entity.CreatedAt,
                    ["lastLoginAt"] = entity.LastLoginAt.HasValue ? entity.LastLoginAt.Value : BsonNull.Value,
                    ["createdBy"] = entity.CreatedBy,
                    ["updatedAt"] = entity.UpdatedAt.HasValue ? entity.UpdatedAt.Value : BsonNull.Value,
                    ["updatedBy"] = string.IsNullOrEmpty(entity.UpdatedBy) ? BsonNull.Value : entity.UpdatedBy
                };
                
                var options = new ReplaceOptions { IsUpsert = true };
                await collection.ReplaceOneAsync(filter, document, options);
                _logger.LogDebug("User updated successfully: {Id}, DomainId: {DomainId}", entity.Id, entity.DomainId);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {Id}, DomainId: {DomainId}", entity.Id, entity.DomainId);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string id)
        {
            // This method requires domainId - use DeleteAsync(string id, string domainId) instead
            throw new NotImplementedException("DeleteAsync(string id) requires domainId. Use DeleteAsync(string id, string domainId) instead.");
        }

        public async Task<bool> DeleteAsync(string id, string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<BsonDocument>.Filter.Eq("__dataId", id);
                var result = await collection.DeleteOneAsync(filter);
                _logger.LogDebug("User deleted successfully: {Id}, DomainId: {DomainId}", id, domainId);
                return result.DeletedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {Id}, DomainId: {DomainId}", id, domainId);
                return false;
            }
        }

        public async Task<bool> ExistsAsync(string id)
        {
            // This method requires domainId - use ExistsAsync(string id, string domainId) instead
            throw new NotImplementedException("ExistsAsync(string id) requires domainId. Use ExistsAsync(string id, string domainId) instead.");
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
                _logger.LogError(ex, "Error checking user existence: {Id}, DomainId: {DomainId}", id, domainId);
                return false;
            }
        }

        public async Task<User?> GetByEmailAsync(string email, string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<BsonDocument>.Filter.Eq("email", email);
                var doc = await collection.Find(filter).FirstOrDefaultAsync();
                return MapBsonDocumentToUser(doc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email: {Email}, DomainId: {DomainId}", email, domainId);
                return null;
            }
        }

        public async Task<User?> GetByUsernameAsync(string username, string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<BsonDocument>.Filter.Eq("username", username);
                var doc = await collection.Find(filter).FirstOrDefaultAsync();
                return MapBsonDocumentToUser(doc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by username: {Username}, DomainId: {DomainId}", username, domainId);
                return null;
            }
        }

        public async Task<IEnumerable<User>> GetByGroupIdAsync(string groupId, string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<BsonDocument>.Filter.AnyEq("groups", groupId);
                var docs = await collection.Find(filter).ToListAsync();
                return docs.Select(doc => MapBsonDocumentToUser(doc)).Where(u => u != null).Cast<User>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by group id: {GroupId}, DomainId: {DomainId}", groupId, domainId);
                return Enumerable.Empty<User>();
            }
        }

        public async Task<bool> ExistsByEmailAsync(string email, string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<BsonDocument>.Filter.Eq("email", email);
                return await collection.Find(filter).AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user existence by email: {Email}, DomainId: {DomainId}", email, domainId);
                return false;
            }
        }

        public async Task<bool> ExistsByUsernameAsync(string username, string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<BsonDocument>.Filter.Eq("username", username);
                return await collection.Find(filter).AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user existence by username: {Username}, DomainId: {DomainId}", username, domainId);
                return false;
            }
        }

        public async Task<IEnumerable<User>> GetByDomainIdAsync(string domainId)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filter = Builders<BsonDocument>.Filter.Eq("domainId", domainId);
                var docs = await collection.Find(filter).ToListAsync();
                return docs.Select(doc => MapBsonDocumentToUser(doc)).Where(u => u != null).Cast<User>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by domain id: {DomainId}", domainId);
                return Enumerable.Empty<User>();
            }
        }

        public async Task<QueryResult<User>> GetByDomainIdWithPaginationAsync(
            string domainId,
            int page,
            int pageSize,
            string? searchTerm = null,
            bool? isActive = null)
        {
            try
            {
                var collection = await GetCollectionAsync(domainId);
                var filterBuilder = Builders<BsonDocument>.Filter;
                var filter = filterBuilder.Eq("domainId", domainId);

                // Apply search filter (case-insensitive regex)
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var searchFilter = filterBuilder.Or(
                        filterBuilder.Regex("username", new BsonRegularExpression(searchTerm, "i")),
                        filterBuilder.Regex("email", new BsonRegularExpression(searchTerm, "i")),
                        filterBuilder.Regex("firstName", new BsonRegularExpression(searchTerm, "i")),
                        filterBuilder.Regex("lastName", new BsonRegularExpression(searchTerm, "i"))
                    );
                    filter &= searchFilter;
                }

                // Apply active filter
                if (isActive.HasValue)
                {
                    filter &= filterBuilder.Eq("isActive", isActive.Value);
                }

                // Get total count
                var totalCount = await collection.CountDocumentsAsync(filter);

                // Apply pagination
                var skip = (page - 1) * pageSize;
                var docs = await collection
                    .Find(filter)
                    .Skip(skip)
                    .Limit(pageSize)
                    .ToListAsync();

                var users = docs.Select(doc => MapBsonDocumentToUser(doc)).Where(u => u != null).Cast<User>();

                return new QueryResult<User>
                {
                    Items = users,
                    TotalCount = (int)totalCount,
                    Page = page,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by domain id with pagination: {DomainId}", domainId);
                return new QueryResult<User>
                {
                    Items = Enumerable.Empty<User>(),
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize
                };
            }
        }
    }
}
