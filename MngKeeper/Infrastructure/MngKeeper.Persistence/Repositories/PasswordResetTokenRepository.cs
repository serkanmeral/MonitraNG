using MongoDB.Driver;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Microsoft.Extensions.Configuration;

namespace MngKeeper.Infrastructure.Persistence.Repositories
{
    public class PasswordResetTokenRepository : IPasswordResetTokenRepository
    {
        private readonly IMongoClient _mongoClient;
        private readonly ILogger<PasswordResetTokenRepository> _logger;
        private readonly string _databaseName;

        public PasswordResetTokenRepository(
            IMongoClient mongoClient,
            IConfiguration configuration,
            ILogger<PasswordResetTokenRepository> logger)
        {
            _mongoClient = mongoClient;
            _logger = logger;
            _databaseName = configuration["MngKeeperSettings:MongoDB:DatabaseName"] ?? "mngkeeper";
        }

        private IMongoCollection<PasswordResetToken> GetCollection()
        {
            var database = _mongoClient.GetDatabase(_databaseName);
            return database.GetCollection<PasswordResetToken>("password_reset_tokens");
        }

        public async Task<PasswordResetToken?> GetByTokenAsync(string token)
        {
            try
            {
                var collection = GetCollection();
                var filter = Builders<PasswordResetToken>.Filter.Eq(t => t.Token, token);
                return await collection.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting password reset token by token: {Token}", token);
                return null;
            }
        }

        public async Task<PasswordResetToken> AddAsync(PasswordResetToken entity)
        {
            try
            {
                var collection = GetCollection();
                await collection.InsertOneAsync(entity);
                _logger.LogDebug("Password reset token added successfully: {TokenId}", entity.Id);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding password reset token: {TokenId}", entity.Id);
                throw;
            }
        }

        public async Task<PasswordResetToken> UpdateAsync(PasswordResetToken entity)
        {
            try
            {
                var collection = GetCollection();
                var filter = Builders<PasswordResetToken>.Filter.Eq(t => t.Id, entity.Id);
                await collection.ReplaceOneAsync(filter, entity);
                _logger.LogDebug("Password reset token updated successfully: {TokenId}", entity.Id);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating password reset token: {TokenId}", entity.Id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string id)
        {
            try
            {
                var collection = GetCollection();
                var filter = Builders<PasswordResetToken>.Filter.Eq("_id", ObjectId.Parse(id));
                var result = await collection.DeleteOneAsync(filter);
                _logger.LogDebug("Password reset token deleted successfully: {TokenId}", id);
                return result.DeletedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting password reset token: {TokenId}", id);
                return false;
            }
        }

        public async Task<bool> ExistsAsync(string id)
        {
            try
            {
                var collection = GetCollection();
                var filter = Builders<PasswordResetToken>.Filter.Eq("_id", ObjectId.Parse(id));
                var count = await collection.CountDocumentsAsync(filter);
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if password reset token exists: {TokenId}", id);
                return false;
            }
        }

        public async Task<bool> IsTokenValidAsync(string token)
        {
            try
            {
                var tokenEntity = await GetByTokenAsync(token);
                if (tokenEntity == null)
                {
                    return false;
                }

                // Check if token is used
                if (tokenEntity.IsUsed)
                {
                    return false;
                }

                // Check if token is expired
                if (tokenEntity.ExpiresAt < DateTime.UtcNow)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating password reset token: {Token}", token);
                return false;
            }
        }

        public async Task<bool> MarkTokenAsUsedAsync(string token)
        {
            try
            {
                var collection = GetCollection();
                var filter = Builders<PasswordResetToken>.Filter.Eq(t => t.Token, token);
                var update = Builders<PasswordResetToken>.Update
                    .Set(t => t.IsUsed, true)
                    .Set(t => t.UsedAt, DateTime.UtcNow);
                
                var result = await collection.UpdateOneAsync(filter, update);
                _logger.LogDebug("Password reset token marked as used: {Token}", token);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking password reset token as used: {Token}", token);
                return false;
            }
        }

        public async Task DeleteExpiredTokensAsync()
        {
            try
            {
                var collection = GetCollection();
                var filter = Builders<PasswordResetToken>.Filter.Or(
                    Builders<PasswordResetToken>.Filter.Lt(t => t.ExpiresAt, DateTime.UtcNow),
                    Builders<PasswordResetToken>.Filter.Eq(t => t.IsUsed, true)
                );
                
                var result = await collection.DeleteManyAsync(filter);
                _logger.LogInformation("Deleted {Count} expired/used password reset tokens", result.DeletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting expired password reset tokens");
            }
        }
    }
}

