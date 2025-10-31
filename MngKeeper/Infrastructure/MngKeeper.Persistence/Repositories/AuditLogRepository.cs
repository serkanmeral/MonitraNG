using MongoDB.Driver;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Infrastructure.Persistence.Repositories
{
    public class AuditLogRepository : MongoRepository<AuditLog>, IAuditLogRepository
    {
        public AuditLogRepository(IMongoDatabase database, ILogger<AuditLogRepository> logger) 
            : base(database, "auditLogs", logger)
        {
        }

        public async Task<IEnumerable<AuditLog>> GetByEntityIdAsync(string entityId)
        {
            try
            {
                var filter = Builders<AuditLog>.Filter.Eq(x => x.ResourceId, entityId);
                return await _collection.Find(filter).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs by entity id: {EntityId}", entityId);
                return Enumerable.Empty<AuditLog>();
            }
        }

        public async Task<IEnumerable<AuditLog>> GetByUserIdAsync(string userId)
        {
            try
            {
                var filter = Builders<AuditLog>.Filter.Eq(x => x.UserId, userId);
                return await _collection.Find(filter).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs by user id: {UserId}", userId);
                return Enumerable.Empty<AuditLog>();
            }
        }

        public async Task<IEnumerable<AuditLog>> GetByDomainIdAsync(string domainId)
        {
            try
            {
                var filter = Builders<AuditLog>.Filter.Eq(x => x.DomainId, domainId);
                return await _collection.Find(filter).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs by domain id: {DomainId}", domainId);
                return Enumerable.Empty<AuditLog>();
            }
        }

        public async Task<IEnumerable<AuditLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var filter = Builders<AuditLog>.Filter.Gte(x => x.Timestamp, startDate) &
                           Builders<AuditLog>.Filter.Lte(x => x.Timestamp, endDate);
                return await _collection.Find(filter).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs by date range: {StartDate} to {EndDate}", startDate, endDate);
                return Enumerable.Empty<AuditLog>();
            }
        }

        public async Task<IEnumerable<AuditLog>> GetByActionAsync(string action)
        {
            try
            {
                var filter = Builders<AuditLog>.Filter.Eq(x => x.Action, action);
                return await _collection.Find(filter).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs by action: {Action}", action);
                return Enumerable.Empty<AuditLog>();
            }
        }

        public async Task<IEnumerable<AuditLog>> GetByDomainIdAndDateRangeAsync(string domainId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var filter = Builders<AuditLog>.Filter.Eq(x => x.DomainId, domainId) &
                           Builders<AuditLog>.Filter.Gte(x => x.Timestamp, startDate) &
                           Builders<AuditLog>.Filter.Lte(x => x.Timestamp, endDate);
                return await _collection.Find(filter).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs by domain id and date range: {DomainId}, {StartDate} to {EndDate}", domainId, startDate, endDate);
                return Enumerable.Empty<AuditLog>();
            }
        }

        protected override string GetEntityId(AuditLog entity)
        {
            return entity.Id;
        }
    }
}
