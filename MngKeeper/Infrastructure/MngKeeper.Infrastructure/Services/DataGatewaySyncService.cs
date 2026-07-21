using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MngKeeper.Application.Common;
using MngKeeper.Application.Configuration;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using System.Text.Json;

namespace MngKeeper.Infrastructure.Services
{
    /// <summary>
    /// DataGateway sync service - MngKeeper'dan MngDataGateway MongoDB'ye direkt sync
    /// </summary>
    public class DataGatewaySyncService : IDataGatewaySyncService
    {
        private readonly ILogger<DataGatewaySyncService> _logger;
        private readonly IMongoClient _mongoClient;
        private readonly IDomainRepository _domainRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly MngKeeperSettings _settings;

        public DataGatewaySyncService(
            ILogger<DataGatewaySyncService> logger,
            IMongoClient mongoClient,
            IDomainRepository domainRepository,
            IUserRepository userRepository,
            IGroupRepository groupRepository,
            IOptions<MngKeeperSettings> settings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
            _domainRepository = domainRepository ?? throw new ArgumentNullException(nameof(domainRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _groupRepository = groupRepository ?? throw new ArgumentNullException(nameof(groupRepository));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task SyncUserToDataGatewayAsync(
            User user, 
            string domainId,
            Dictionary<string, object>? customData = null)
        {
            try
            {
                // Get domain to get database name
                var domain = await _domainRepository.GetByIdAsync(domainId);
                if (domain == null)
                {
                    _logger.LogError("Domain not found: DomainId={DomainId}", domainId);
                    throw new InvalidOperationException($"Domain not found: {domainId}");
                }

                var databaseName = domain.DatabaseName; // mng_{domainName}
                var database = _mongoClient.GetDatabase(databaseName);
                var collection = database.GetCollection<BsonDocument>("@users");

                // Check if user already exists
                var existingFilter = Builders<BsonDocument>.Filter.Eq("__dataId", user.Id);
                var existingUser = await collection.Find(existingFilter).FirstOrDefaultAsync();

                var userDocument = new BsonDocument
                {
                    ["__dataId"] = user.Id, // MngKeeper User._id
                    ["keycloakUserId"] = user.KeycloakUserId,
                    ["username"] = user.Username,
                    ["email"] = UserEmailHelper.NormalizeForStorage(user.Email) is { } email ? email : BsonNull.Value,
                    ["firstName"] = user.FirstName,
                    ["lastName"] = user.LastName,
                    ["isActive"] = user.IsActive,
                    ["includeInApplication"] = user.IncludeInApplication,
                    ["domainId"] = user.DomainId,
                    ["groups"] = new BsonArray(user.Groups ?? new List<string>()),
                    ["__syncInfo"] = new BsonDocument
                    {
                        ["lastSyncedAt"] = DateTime.UtcNow,
                        ["syncSource"] = "mngkeeper",
                        ["syncVersion"] = existingUser != null && existingUser.Contains("__syncInfo")
                            ? (existingUser["__syncInfo"].AsBsonDocument?.Contains("syncVersion") == true
                                ? existingUser["__syncInfo"].AsBsonDocument["syncVersion"].AsInt32 + 1
                                : 1)
                            : 1
                    },
                    ["__createInfo"] = existingUser != null && existingUser.Contains("__createInfo")
                        ? existingUser["__createInfo"].AsBsonDocument
                        : new BsonDocument
                        {
                            ["createdAt"] = user.CreatedAt,
                            ["userInfo"] = new BsonDocument
                            {
                                ["uid"] = "system",
                                ["userName"] = "system",
                                ["domain"] = domain.Name
                            }
                        },
                    ["__lastUpdateInfo"] = new BsonDocument
                    {
                        ["updatedAt"] = DateTime.UtcNow,
                        ["userInfo"] = new BsonDocument
                        {
                            ["uid"] = "system",
                            ["userName"] = "system",
                            ["domain"] = domain.Name
                        }
                    },
                    ["__isDeleted"] = false
                };

                // Add optional fields if they have values
                if (!string.IsNullOrEmpty(user.Title))
                {
                    userDocument["title"] = user.Title;
                }
                if (!string.IsNullOrEmpty(user.Department))
                {
                    userDocument["department"] = user.Department;
                }
                userDocument["gender"] = (int)user.Gender;
                if (!string.IsNullOrEmpty(user.PhoneNumber))
                {
                    userDocument["phoneNumber"] = user.PhoneNumber;
                }
                if (!string.IsNullOrEmpty(user.TelegramUsername))
                {
                    userDocument["telegramUsername"] = user.TelegramUsername;
                }
                if (!string.IsNullOrEmpty(user.TelegramChatId))
                {
                    userDocument["telegramChatId"] = user.TelegramChatId;
                }
                if (user.TelegramLinkedAt.HasValue)
                {
                    userDocument["telegramLinkedAt"] = user.TelegramLinkedAt.Value;
                }
                if (!string.IsNullOrEmpty(user.PhotoUrl))
                {
                    userDocument["photoUrl"] = user.PhotoUrl;
                }

                // Add custom data if provided
                if (customData != null && customData.Any())
                {
                    foreach (var kvp in customData)
                    {
                        userDocument[kvp.Key] = ConvertToBsonValue(kvp.Value);
                    }
                }

                if (existingUser == null)
                {
                    // Insert new user
                    await collection.InsertOneAsync(userDocument);
                    _logger.LogInformation("User synced to DataGateway MongoDB: UserId={UserId}, Username={Username}, Database={Database}", 
                        user.Id, user.Username, databaseName);
                }
                else
                {
                    // Update existing user
                    await collection.ReplaceOneAsync(existingFilter, userDocument);
                    _logger.LogInformation("User updated in DataGateway MongoDB: UserId={UserId}, Username={Username}, Database={Database}", 
                        user.Id, user.Username, databaseName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing user to DataGateway MongoDB: UserId={UserId}, DomainId={DomainId}", user.Id, domainId);
                throw;
            }
        }

        public async Task SyncGroupToDataGatewayAsync(
            Group group, 
            string domainId,
            Dictionary<string, object>? customData = null)
        {
            try
            {
                // Get domain to get database name
                var domain = await _domainRepository.GetByIdAsync(domainId);
                if (domain == null)
                {
                    _logger.LogError("Domain not found: DomainId={DomainId}", domainId);
                    throw new InvalidOperationException($"Domain not found: {domainId}");
                }

                var databaseName = domain.DatabaseName; // mng_{domainName}
                var database = _mongoClient.GetDatabase(databaseName);
                var collection = database.GetCollection<BsonDocument>("@groups");

                // Check if group already exists
                var existingFilter = Builders<BsonDocument>.Filter.Eq("__dataId", group.Id);
                var existingGroup = await collection.Find(existingFilter).FirstOrDefaultAsync();

                var keycloakGroupId = group.KeycloakGroupId?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(keycloakGroupId) && existingGroup != null && existingGroup.Contains("keycloakGroupId") &&
                    !existingGroup["keycloakGroupId"].IsBsonNull)
                    keycloakGroupId = existingGroup["keycloakGroupId"].AsString;

                var groupDocument = new BsonDocument
                {
                    ["__dataId"] = group.Id, // MngKeeper Group._id
                    ["name"] = group.Name,
                    ["description"] = string.IsNullOrEmpty(group.Description) ? BsonNull.Value : group.Description,
                    ["permissions"] = new BsonArray(group.Permissions ?? new List<string>()),
                    ["domainId"] = group.DomainId,
                    ["keycloakGroupId"] = keycloakGroupId,
                    ["isActive"] = group.IsActive,
                    ["includeInApplication"] = group.IncludeInApplication,
                    ["__syncInfo"] = new BsonDocument
                    {
                        ["lastSyncedAt"] = DateTime.UtcNow,
                        ["syncSource"] = "mngkeeper",
                        ["syncVersion"] = existingGroup != null && existingGroup.Contains("__syncInfo")
                            ? (existingGroup["__syncInfo"].AsBsonDocument?.Contains("syncVersion") == true
                                ? existingGroup["__syncInfo"].AsBsonDocument["syncVersion"].AsInt32 + 1
                                : 1)
                            : 1
                    },
                    ["__createInfo"] = existingGroup != null && existingGroup.Contains("__createInfo")
                        ? existingGroup["__createInfo"].AsBsonDocument
                        : new BsonDocument
                        {
                            ["createdAt"] = DateTime.UtcNow,
                            ["userInfo"] = new BsonDocument
                            {
                                ["uid"] = "system",
                                ["userName"] = "system",
                                ["domain"] = domain.Name
                            }
                        },
                    ["__lastUpdateInfo"] = new BsonDocument
                    {
                        ["updatedAt"] = DateTime.UtcNow,
                        ["userInfo"] = new BsonDocument
                        {
                            ["uid"] = "system",
                            ["userName"] = "system",
                            ["domain"] = domain.Name
                        }
                    },
                    ["__isDeleted"] = false
                };

                // Add custom data if provided
                if (customData != null && customData.Any())
                {
                    foreach (var kvp in customData)
                    {
                        groupDocument[kvp.Key] = ConvertToBsonValue(kvp.Value);
                    }
                }

                if (existingGroup == null)
                {
                    // Insert new group
                    await collection.InsertOneAsync(groupDocument);
                    _logger.LogInformation("Group synced to DataGateway MongoDB: GroupId={GroupId}, Name={Name}, Database={Database}", 
                        group.Id, group.Name, databaseName);
                }
                else
                {
                    // Update existing group
                    await collection.ReplaceOneAsync(existingFilter, groupDocument);
                    _logger.LogInformation("Group updated in DataGateway MongoDB: GroupId={GroupId}, Name={Name}, Database={Database}", 
                        group.Id, group.Name, databaseName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing group to DataGateway MongoDB: GroupId={GroupId}, DomainId={DomainId}", group.Id, domainId);
                throw;
            }
        }

        public async Task<DataGatewaySyncResult> SyncAllUsersAsync(string domainId)
        {
            var result = new DataGatewaySyncResult();

            try
            {
                var domain = await _domainRepository.GetByIdAsync(domainId);
                if (domain == null)
                {
                    result.Errors.Add($"Domain not found: {domainId}");
                    result.Message = "Sync failed: Domain not found";
                    return result;
                }

                // @users koleksiyonu BsonDocument + __dataId kullanır; IUserRepository ile oku
                var users = (await _userRepository.GetByDomainIdAsync(domainId)).ToList();
                result.TotalCount = users.Count;

                foreach (var user in users)
                {
                    try
                    {
                        var existingFilter = Builders<BsonDocument>.Filter.Eq("__dataId", user.Id);
                        var database = _mongoClient.GetDatabase(domain.DatabaseName);
                        var collection = database.GetCollection<BsonDocument>("@users");
                        var exists = await collection.Find(existingFilter).AnyAsync();

                        await SyncUserToDataGatewayAsync(user, domainId, null);

                        if (exists)
                            result.UpdatedCount++;
                        else
                            result.CreatedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing user: {UserId}", user.Id);
                        result.Errors.Add($"User {user.Id}: {ex.Message}");
                        result.ErrorCount++;
                    }
                }

                result.Message = $"User sync completed: {result.CreatedCount} created, {result.UpdatedCount} updated, {result.ErrorCount} errors";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user sync");
                result.Errors.Add($"Sync failed: {ex.Message}");
                result.Message = "User sync failed";
            }

            return result;
        }

        public async Task<DataGatewaySyncResult> SyncAllGroupsAsync(string domainId)
        {
            var result = new DataGatewaySyncResult();

            try
            {
                var domain = await _domainRepository.GetByIdAsync(domainId);
                if (domain == null)
                {
                    result.Errors.Add($"Domain not found: {domainId}");
                    result.Message = "Sync failed: Domain not found";
                    return result;
                }

                var groups = (await _groupRepository.GetByDomainIdAsync(domainId)).ToList();
                result.TotalCount = groups.Count;

                foreach (var group in groups)
                {
                    try
                    {
                        var existingFilter = Builders<BsonDocument>.Filter.Eq("__dataId", group.Id);
                        var database = _mongoClient.GetDatabase(domain.DatabaseName);
                        var collection = database.GetCollection<BsonDocument>("@groups");
                        var exists = await collection.Find(existingFilter).AnyAsync();

                        await SyncGroupToDataGatewayAsync(group, domainId, null);

                        if (exists)
                            result.UpdatedCount++;
                        else
                            result.CreatedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing group: {GroupId}", group.Id);
                        result.Errors.Add($"Group {group.Id}: {ex.Message}");
                        result.ErrorCount++;
                    }
                }

                result.Message = $"Group sync completed: {result.CreatedCount} created, {result.UpdatedCount} updated, {result.ErrorCount} errors";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during group sync");
                result.Errors.Add($"Sync failed: {ex.Message}");
                result.Message = "Group sync failed";
            }

            return result;
        }

        public async Task<DataGatewaySyncResult> SyncAllAsync(string domainId)
        {
            var result = new DataGatewaySyncResult();

            // Sync users
            var usersResult = await SyncAllUsersAsync(domainId);
            result.TotalCount += usersResult.TotalCount;
            result.CreatedCount += usersResult.CreatedCount;
            result.UpdatedCount += usersResult.UpdatedCount;
            result.ErrorCount += usersResult.ErrorCount;
            result.Errors.AddRange(usersResult.Errors);

            // Sync groups
            var groupsResult = await SyncAllGroupsAsync(domainId);
            result.TotalCount += groupsResult.TotalCount;
            result.CreatedCount += groupsResult.CreatedCount;
            result.UpdatedCount += groupsResult.UpdatedCount;
            result.ErrorCount += groupsResult.ErrorCount;
            result.Errors.AddRange(groupsResult.Errors);

            result.Message = $"Full sync completed: Users ({usersResult.CreatedCount + usersResult.UpdatedCount}), Groups ({groupsResult.CreatedCount + groupsResult.UpdatedCount}), Errors ({result.ErrorCount})";

            return result;
        }

        /// <summary>
        /// Convert object to BsonValue, handling JsonElement and nested objects
        /// </summary>
        private BsonValue ConvertToBsonValue(object? value)
        {
            if (value == null)
                return BsonNull.Value;

            // Handle JsonElement (from System.Text.Json)
            if (value is JsonElement jsonElement)
            {
                return ConvertJsonElementToBsonValue(jsonElement);
            }

            // Handle Dictionary<string, object> (nested objects)
            if (value is Dictionary<string, object> dict)
            {
                var bsonDoc = new BsonDocument();
                foreach (var kvp in dict)
                {
                    bsonDoc[kvp.Key] = ConvertToBsonValue(kvp.Value);
                }
                return bsonDoc;
            }

            // Handle arrays/lists
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var bsonArray = new BsonArray();
                foreach (var item in enumerable)
                {
                    bsonArray.Add(ConvertToBsonValue(item));
                }
                return bsonArray;
            }

            // Try direct conversion for primitive types
            try
            {
                return BsonValue.Create(value);
            }
            catch (ArgumentException)
            {
                // If direct conversion fails, serialize to JSON and parse as BsonDocument
                try
                {
                    var json = JsonSerializer.Serialize(value);
                    return BsonDocument.Parse(json);
                }
                catch
                {
                    // Last resort: convert to string
                    var stringValue = value?.ToString();
                    return string.IsNullOrEmpty(stringValue) ? BsonNull.Value : (BsonValue)stringValue;
                }
            }
        }

        /// <summary>
        /// Convert JsonElement to BsonValue recursively
        /// </summary>
        private BsonValue ConvertJsonElementToBsonValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => ConvertJsonObjectToBsonDocument(element),
                JsonValueKind.Array => ConvertJsonArrayToBsonArray(element),
                JsonValueKind.String => GetStringValue(element),
                JsonValueKind.Number => element.TryGetInt64(out var longVal) 
                    ? longVal 
                    : (BsonValue)element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => BsonNull.Value,
                _ => element.GetRawText()
            };
        }

        /// <summary>
        /// Get string value from JsonElement, returning BsonNull if empty
        /// </summary>
        private BsonValue GetStringValue(JsonElement element)
        {
            var str = element.GetString();
            return string.IsNullOrEmpty(str) ? BsonNull.Value : (BsonValue)str;
        }

        /// <summary>
        /// Convert JsonElement object to BsonDocument
        /// </summary>
        private BsonDocument ConvertJsonObjectToBsonDocument(JsonElement element)
        {
            var doc = new BsonDocument();
            foreach (var prop in element.EnumerateObject())
            {
                doc[prop.Name] = ConvertJsonElementToBsonValue(prop.Value);
            }
            return doc;
        }

        /// <summary>
        /// Convert JsonElement array to BsonArray
        /// </summary>
        private BsonArray ConvertJsonArrayToBsonArray(JsonElement element)
        {
            var array = new BsonArray();
            foreach (var item in element.EnumerateArray())
            {
                array.Add(ConvertJsonElementToBsonValue(item));
            }
            return array;
        }
    }
}

