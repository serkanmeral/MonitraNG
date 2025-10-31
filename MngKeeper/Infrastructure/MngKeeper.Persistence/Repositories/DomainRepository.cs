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
                var domainDatabase = _database.Client.GetDatabase(databaseName);
                
                // Create initial collections for the domain using MongoDB commands
                var command = new MongoDB.Bson.BsonDocument
                {
                    { "create", "users" }
                };
                await domainDatabase.RunCommandAsync<MongoDB.Bson.BsonDocument>(command);
                
                command = new MongoDB.Bson.BsonDocument
                {
                    { "create", "groups" }
                };
                await domainDatabase.RunCommandAsync<MongoDB.Bson.BsonDocument>(command);
                
                command = new MongoDB.Bson.BsonDocument
                {
                    { "create", "audit_logs" }
                };
                await domainDatabase.RunCommandAsync<MongoDB.Bson.BsonDocument>(command);
                
                command = new MongoDB.Bson.BsonDocument
                {
                    { "create", "assets" }
                };
                await domainDatabase.RunCommandAsync<MongoDB.Bson.BsonDocument>(command);
                
                _logger.LogInformation("Domain database created successfully: {DatabaseName}", databaseName);
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
