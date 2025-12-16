using MongoDB.Driver;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Infrastructure.Persistence.Repositories
{
    public class DomainRepository : MongoRepository<MngKeeper.Domain.Entities.Domain>, IDomainRepository
    {
        private readonly IMongoDatabase _database;

        public DomainRepository(IMongoDatabase database, ILogger<DomainRepository> logger) 
            : base(database, "domains", logger)
        {
            _database = database;
        }

        public async Task<MngKeeper.Domain.Entities.Domain?> GetByNameAsync(string name)
        {
            try
            {
                var filter = Builders<MngKeeper.Domain.Entities.Domain>.Filter.Eq(x => x.Name, name);
                return await _collection.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting domain by name: {Name}", name);
                return null;
            }
        }

        public async Task<MngKeeper.Domain.Entities.Domain?> GetByRealmNameAsync(string realmName)
        {
            try
            {
                var filter = Builders<MngKeeper.Domain.Entities.Domain>.Filter.Eq(x => x.RealmName, realmName);
                return await _collection.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting domain by realm name: {RealmName}", realmName);
                return null;
            }
        }

        public async Task<IEnumerable<MngKeeper.Domain.Entities.Domain>> GetByStatusAsync(DomainStatus status)
        {
            try
            {
                var filter = Builders<MngKeeper.Domain.Entities.Domain>.Filter.Eq(x => x.Status, status);
                return await _collection.Find(filter).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting domains by status: {Status}", status);
                return Enumerable.Empty<MngKeeper.Domain.Entities.Domain>();
            }
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            try
            {
                var filter = Builders<MngKeeper.Domain.Entities.Domain>.Filter.Eq(x => x.Name, name);
                return await _collection.Find(filter).AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking domain existence by name: {Name}", name);
                return false;
            }
        }

        public async Task<bool> CreateDatabaseAsync(string databaseName)
        {
            try
            {
                // Create a new database with the domain-specific name
                // Note: MongoDB creates database automatically on first write
                // We don't need to create empty collections here because:
                // - @users and @groups are created by InitializeDataGatewayCollectionsStep
                // - @datasets and @dataset_categories are created by InitializeDatabaseCollectionsStep
                // - Other collections will be created on-demand when needed
                
                var domainDatabase = _database.Client.GetDatabase(databaseName);
                
                // Verify database exists by creating a test collection and dropping it
                // This ensures the database is created in MongoDB
                var testCollection = domainDatabase.GetCollection<MongoDB.Bson.BsonDocument>("__test");
                await testCollection.InsertOneAsync(new MongoDB.Bson.BsonDocument { { "_id", MongoDB.Bson.ObjectId.GenerateNewId() } });
                await domainDatabase.DropCollectionAsync("__test");
                
                _logger.LogInformation("Domain database created successfully: {DatabaseName}", databaseName);
                _logger.LogInformation("Note: Collections will be created by pipeline steps (@users, @groups, @datasets, etc.)");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating domain database: {DatabaseName}", databaseName);
                return false;
            }
        }

        public async Task<bool> DeleteDatabaseAsync(string databaseName)
        {
            try
            {
                await _database.Client.DropDatabaseAsync(databaseName);
                _logger.LogInformation("Domain database deleted successfully: {DatabaseName}", databaseName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting domain database: {DatabaseName}", databaseName);
                return false;
            }
        }

        protected override string GetEntityId(MngKeeper.Domain.Entities.Domain entity)
        {
            return entity.Id;
        }
    }
}
