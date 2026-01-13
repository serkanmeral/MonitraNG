using MongoDB.Driver;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for Template entity
/// </summary>
public class TemplateRepository : MongoRepository<Template>, ITemplateRepository
{
    public TemplateRepository(IMongoDatabase database, ILogger<TemplateRepository> logger) 
        : base(database, "templates", logger)
    {
    }

    public async Task<Template?> GetByNameAsync(string name)
    {
        try
        {
            var filter = Builders<Template>.Filter.Eq(x => x.Name, name);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template by name: {Name}", name);
            return null;
        }
    }

    public async Task<bool> ExistsByNameAsync(string name)
    {
        try
        {
            var filter = Builders<Template>.Filter.Eq(x => x.Name, name);
            return await _collection.Find(filter).AnyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if template exists by name: {Name}", name);
            return false;
        }
    }

    public async Task<IEnumerable<Template>> GetBySourceDomainIdAsync(string domainId)
    {
        try
        {
            var filter = Builders<Template>.Filter.Eq(x => x.SourceDomainId, domainId);
            return await _collection.Find(filter).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting templates by source domain ID: {DomainId}", domainId);
            return Enumerable.Empty<Template>();
        }
    }

    protected override string GetEntityId(Template entity)
    {
        return entity.Id;
    }
}
