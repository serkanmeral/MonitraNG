using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Bson;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 4.5: Initialize DataGateway collections in the domain database
/// Creates @users and @groups collections for DataGateway sync
/// </summary>
public class InitializeDataGatewayCollectionsStep : IPipelineStep<DomainCreationContext>
{
    private readonly IMongoClient _mongoClient;
    private readonly ILogger<InitializeDataGatewayCollectionsStep> _logger;
    
    public string StepName => "InitializeDataGatewayCollections";
    
    public InitializeDataGatewayCollectionsStep(
        IMongoClient mongoClient,
        ILogger<InitializeDataGatewayCollectionsStep> logger)
    {
        _mongoClient = mongoClient;
        _logger = logger;
    }
    
    public async Task<StepResult> ExecuteAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing DataGateway collections in database: {DatabaseName}", context.DatabaseName);
            
            var database = _mongoClient.GetDatabase(context.DatabaseName);
            
            // Create @users collection
            await database.CreateCollectionAsync("@users", cancellationToken: cancellationToken);
            _logger.LogInformation("Created collection: @users");
            
            // Create @groups collection
            await database.CreateCollectionAsync("@groups", cancellationToken: cancellationToken);
            _logger.LogInformation("Created collection: @groups");
            
            // Create indexes for @users collection
            var usersCollection = database.GetCollection<BsonDocument>("@users");
            
            // __dataId index (unique) - MngKeeper User._id
            await usersCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("__dataId"),
                    new CreateIndexOptions { Unique = true }
                ),
                cancellationToken: cancellationToken
            );
            _logger.LogInformation("Created unique index on @users.__dataId");
            
            // keycloakUserId index
            await usersCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("keycloakUserId")
                ),
                cancellationToken: cancellationToken
            );
            _logger.LogInformation("Created index on @users.keycloakUserId");
            
            // username index (unique)
            await usersCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("username"),
                    new CreateIndexOptions { Unique = true }
                ),
                cancellationToken: cancellationToken
            );
            _logger.LogInformation("Created unique index on @users.username");
            
            // email unique yalnızca dolu değerlerde (LDAP'ta e-postasız kullanıcılar olabilir)
            await usersCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("email"),
                    new CreateIndexOptions<BsonDocument>
                    {
                        Unique = true,
                        Name = "email_1",
                        PartialFilterExpression = Builders<BsonDocument>.Filter.And(
                            Builders<BsonDocument>.Filter.Exists("email"),
                            Builders<BsonDocument>.Filter.Type("email", BsonType.String),
                            Builders<BsonDocument>.Filter.Ne("email", BsonNull.Value),
                            Builders<BsonDocument>.Filter.Ne("email", ""))
                    }),
                cancellationToken: cancellationToken
            );
            _logger.LogInformation("Created partial unique index on @users.email (non-empty only)");
            
            // domainId index
            await usersCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("domainId")
                ),
                cancellationToken: cancellationToken
            );
            _logger.LogInformation("Created index on @users.domainId");
            
            // __isDeleted index (for soft delete queries)
            await usersCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("__isDeleted")
                ),
                cancellationToken: cancellationToken
            );
            _logger.LogInformation("Created index on @users.__isDeleted");
            
            // Create indexes for @groups collection
            var groupsCollection = database.GetCollection<BsonDocument>("@groups");
            
            // __dataId index (unique) - MngKeeper Group._id
            await groupsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("__dataId"),
                    new CreateIndexOptions { Unique = true }
                ),
                cancellationToken: cancellationToken
            );
            _logger.LogInformation("Created unique index on @groups.__dataId");
            
            // name index (unique)
            await groupsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("name"),
                    new CreateIndexOptions { Unique = true }
                ),
                cancellationToken: cancellationToken
            );
            _logger.LogInformation("Created unique index on @groups.name");
            
            // domainId index
            await groupsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("domainId")
                ),
                cancellationToken: cancellationToken
            );
            _logger.LogInformation("Created index on @groups.domainId");
            
            // __isDeleted index (for soft delete queries)
            await groupsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("__isDeleted")
                ),
                cancellationToken: cancellationToken
            );
            _logger.LogInformation("Created index on @groups.__isDeleted");
            
            _logger.LogInformation("DataGateway collections initialized successfully in: {DatabaseName}", context.DatabaseName);
            
            return StepResult.Success(new Dictionary<string, object>
            {
                ["collectionsCreated"] = new[] { "@users", "@groups" },
                ["indexesCreated"] = new[] { 
                    "@users: __dataId, keycloakUserId, username, email, domainId, __isDeleted",
                    "@groups: __dataId, name, domainId, __isDeleted"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize DataGateway collections in: {DatabaseName}", context.DatabaseName);
            return StepResult.Failure($"Failed to initialize DataGateway collections: {ex.Message}", ex);
        }
    }
    
    public async Task RollbackAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Rollback: Dropping DataGateway collections in {DatabaseName}", context.DatabaseName);
        
        try
        {
            var database = _mongoClient.GetDatabase(context.DatabaseName);
            
            await database.DropCollectionAsync("@users", cancellationToken);
            await database.DropCollectionAsync("@groups", cancellationToken);
            
            _logger.LogInformation("DataGateway collections dropped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to drop DataGateway collections during rollback");
        }
    }
}

