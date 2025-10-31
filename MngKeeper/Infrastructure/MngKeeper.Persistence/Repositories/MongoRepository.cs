using MongoDB.Driver;
using MngKeeper.Application.Interfaces;
using MongoDB.Bson;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Infrastructure.Persistence.Repositories
{
    public abstract class MongoRepository<T> : IRepository<T> where T : class
    {
        protected readonly IMongoCollection<T> _collection;
        protected readonly ILogger<MongoRepository<T>> _logger;

        protected MongoRepository(IMongoDatabase database, string collectionName, ILogger<MongoRepository<T>> logger)
        {
            _collection = database.GetCollection<T>(collectionName);
            _logger = logger;
        }

        public virtual async Task<T?> GetByIdAsync(string id)
        {
            try
            {
                var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
                return await _collection.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entity by id: {Id}", id);
                return null;
            }
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            try
            {
                return await _collection.Find(_ => true).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all entities");
                return Enumerable.Empty<T>();
            }
        }

        public virtual async Task<IEnumerable<T>> GetByFilterAsync(Func<T, bool> filter)
        {
            try
            {
                var allEntities = await GetAllAsync();
                return allEntities.Where(filter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entities by filter");
                return Enumerable.Empty<T>();
            }
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            try
            {
                await _collection.InsertOneAsync(entity);
                _logger.LogDebug("Entity added successfully");
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding entity");
                throw;
            }
        }

        public virtual async Task<T> UpdateAsync(T entity)
        {
            try
            {
                var id = GetEntityId(entity);
                var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
                var options = new ReplaceOptions { IsUpsert = true };
                
                await _collection.ReplaceOneAsync(filter, entity, options);
                _logger.LogDebug("Entity updated successfully: {Id}", id);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating entity");
                throw;
            }
        }

        public virtual async Task<bool> DeleteAsync(string id)
        {
            try
            {
                var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
                var result = await _collection.DeleteOneAsync(filter);
                _logger.LogDebug("Entity deleted successfully: {Id}", id);
                return result.DeletedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting entity: {Id}", id);
                return false;
            }
        }

        public virtual async Task<bool> ExistsAsync(string id)
        {
            try
            {
                var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
                return await _collection.Find(filter).AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking entity existence: {Id}", id);
                return false;
            }
        }

        protected abstract string GetEntityId(T entity);
    }
}
