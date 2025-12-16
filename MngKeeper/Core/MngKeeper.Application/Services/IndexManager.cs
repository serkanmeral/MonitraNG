using MongoDB.Driver;
using MngKeeper.Domain.Entities;
using Microsoft.Extensions.Logging;
using MngKeeper.Application.Common.Constants;

namespace MngKeeper.Application.Services
{
    /// <summary>
    /// Manages MongoDB indexes for domain-specific collections
    /// </summary>
    public class IndexManager
    {
        private readonly IMongoClient _mongoClient;
        private readonly ILogger<IndexManager> _logger;

        public IndexManager(IMongoClient mongoClient, ILogger<IndexManager> logger)
        {
            _mongoClient = mongoClient;
            _logger = logger;
        }

        /// <summary>
        /// Creates indexes for users collection in a domain-specific database
        /// </summary>
        public async Task CreateUserIndexesAsync(string databaseName, CancellationToken cancellationToken = default)
        {
            try
            {
                var database = _mongoClient.GetDatabase(databaseName);
                var collection = database.GetCollection<User>(SystemConstants.Collections.Users);

                // DomainId index (most common query filter)
                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<User>(
                        Builders<User>.IndexKeys.Ascending(u => u.DomainId),
                        new CreateIndexOptions { Name = "idx_users_domainId" }
                    ),
                    cancellationToken: cancellationToken
                );
                _logger.LogDebug("Created index: idx_users_domainId");

                // Username index (unique per domain)
                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<User>(
                        Builders<User>.IndexKeys.Ascending(u => u.Username),
                        new CreateIndexOptions 
                        { 
                            Name = "idx_users_username",
                            Unique = true 
                        }
                    ),
                    cancellationToken: cancellationToken
                );
                _logger.LogDebug("Created index: idx_users_username");

                // Email index (unique per domain)
                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<User>(
                        Builders<User>.IndexKeys.Ascending(u => u.Email),
                        new CreateIndexOptions 
                        { 
                            Name = "idx_users_email",
                            Unique = true 
                        }
                    ),
                    cancellationToken: cancellationToken
                );
                _logger.LogDebug("Created index: idx_users_email");

                // Compound index: DomainId + IsActive (common query pattern)
                var compoundIndexDefinition = Builders<User>.IndexKeys
                    .Ascending(u => u.DomainId)
                    .Ascending(u => u.IsActive);
                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<User>(
                        compoundIndexDefinition,
                        new CreateIndexOptions { Name = "idx_users_domainId_isActive" }
                    ),
                    cancellationToken: cancellationToken
                );
                _logger.LogDebug("Created index: idx_users_domainId_isActive");

                // KeycloakUserId index (for Keycloak lookups)
                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<User>(
                        Builders<User>.IndexKeys.Ascending(u => u.KeycloakUserId),
                        new CreateIndexOptions { Name = "idx_users_keycloakUserId" }
                    ),
                    cancellationToken: cancellationToken
                );
                _logger.LogDebug("Created index: idx_users_keycloakUserId");

                _logger.LogInformation("User indexes created successfully for database: {DatabaseName}", databaseName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user indexes for database: {DatabaseName}", databaseName);
                throw;
            }
        }

        /// <summary>
        /// Creates indexes for groups collection in a domain-specific database
        /// </summary>
        public async Task CreateGroupIndexesAsync(string databaseName, CancellationToken cancellationToken = default)
        {
            try
            {
                var database = _mongoClient.GetDatabase(databaseName);
                var collection = database.GetCollection<Group>(SystemConstants.Collections.Groups);

                // DomainId index (most common query filter)
                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<Group>(
                        Builders<Group>.IndexKeys.Ascending(g => g.DomainId),
                        new CreateIndexOptions { Name = "idx_groups_domainId" }
                    ),
                    cancellationToken: cancellationToken
                );
                _logger.LogDebug("Created index: idx_groups_domainId");

                // Name index (unique per domain)
                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<Group>(
                        Builders<Group>.IndexKeys.Ascending(g => g.Name),
                        new CreateIndexOptions 
                        { 
                            Name = "idx_groups_name",
                            Unique = true 
                        }
                    ),
                    cancellationToken: cancellationToken
                );
                _logger.LogDebug("Created index: idx_groups_name");

                // Compound index: DomainId + IsActive (common query pattern)
                var compoundIndexDefinition = Builders<Group>.IndexKeys
                    .Ascending(g => g.DomainId)
                    .Ascending(g => g.IsActive);
                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<Group>(
                        compoundIndexDefinition,
                        new CreateIndexOptions { Name = "idx_groups_domainId_isActive" }
                    ),
                    cancellationToken: cancellationToken
                );
                _logger.LogDebug("Created index: idx_groups_domainId_isActive");

                // KeycloakGroupId index (for Keycloak lookups)
                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<Group>(
                        Builders<Group>.IndexKeys.Ascending(g => g.KeycloakGroupId),
                        new CreateIndexOptions { Name = "idx_groups_keycloakGroupId" }
                    ),
                    cancellationToken: cancellationToken
                );
                _logger.LogDebug("Created index: idx_groups_keycloakGroupId");

                _logger.LogInformation("Group indexes created successfully for database: {DatabaseName}", databaseName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group indexes for database: {DatabaseName}", databaseName);
                throw;
            }
        }

        /// <summary>
        /// Creates all indexes for a domain-specific database
        /// </summary>
        public async Task CreateAllIndexesAsync(string databaseName, CancellationToken cancellationToken = default)
        {
            await CreateUserIndexesAsync(databaseName, cancellationToken);
            await CreateGroupIndexesAsync(databaseName, cancellationToken);
        }
    }
}

