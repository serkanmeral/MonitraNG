using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MngAlarm.Application.Configuration;

namespace MngAlarm.Infrastructure.Persistence;

public interface IAlarmMongoContext
{
    IMongoDatabase GetDatabase(string domainName);
}

public sealed class AlarmMongoContext : IAlarmMongoContext
{
    private readonly IMongoClient _client;
    private readonly string _prefix;

    public AlarmMongoContext(IOptions<MngAlarmSettings> settings)
    {
        _client = new MongoClient(settings.Value.MongoDb.ConnectionString);
        _prefix = settings.Value.MongoDb.DatabasePrefix;
    }

    public IMongoDatabase GetDatabase(string domainName) =>
        _client.GetDatabase($"{_prefix}{domainName}");
}

public sealed class AlarmIndexInitializer
{
    private readonly IAlarmMongoContext _context;
    private readonly HashSet<string> _initialized = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    public AlarmIndexInitializer(IAlarmMongoContext context) => _context = context;

    public async Task EnsureAsync(string domainName, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_initialized.Add(domainName))
                return;
        }

        var db = _context.GetDatabase(domainName);
        var alarms = db.GetCollection<Domain.Entities.AlarmDocument>(Domain.Constants.AlarmCollectionNames.Alarms);
        await alarms.Indexes.CreateManyAsync([
            new CreateIndexModel<Domain.Entities.AlarmDocument>(
                Builders<Domain.Entities.AlarmDocument>.IndexKeys
                    .Ascending(x => x.DedupKey)
                    .Ascending(x => x.Status),
                new CreateIndexOptions { Name = "idx_dedup_status" })
        ], cancellationToken);
    }
}
