using MongoDB.Driver;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;
using MngAlarm.Domain.Enums;
using MngAlarm.Infrastructure.Persistence;

namespace MngAlarm.Infrastructure.Persistence.Repositories;

public sealed class AlarmRuleRepository(IAlarmMongoContext context) : IAlarmRuleRepository
{
    public async Task InsertAsync(AlarmRuleDocument rule, CancellationToken cancellationToken = default)
    {
        var col = Collection(rule.DomainName);
        await col.InsertOneAsync(rule, cancellationToken: cancellationToken);
    }

    public async Task<AlarmRuleDocument?> GetByIdAsync(string domainName, string ruleId, CancellationToken cancellationToken = default)
    {
        return await Collection(domainName)
            .Find(x => x.Id == ruleId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpdateAsync(AlarmRuleDocument rule, CancellationToken cancellationToken = default)
    {
        await Collection(rule.DomainName).ReplaceOneAsync(x => x.Id == rule.Id, rule, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string domainName, string ruleId, CancellationToken cancellationToken = default)
    {
        await Collection(domainName).DeleteOneAsync(x => x.Id == ruleId, cancellationToken);
    }

    public async Task<IReadOnlyList<AlarmRuleDocument>> ListEnabledByKeyAsync(
        string domainName,
        string matchKey,
        CancellationToken cancellationToken = default)
    {
        return await Collection(domainName)
            .Find(x => x.Enabled && x.MatchKey == matchKey)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlarmRuleDocument>> ListEnabledByTypeAsync(
        string domainName,
        string type,
        CancellationToken cancellationToken = default)
    {
        return await Collection(domainName)
            .Find(x => x.Enabled && x.Type == type)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlarmRuleDocument>> ListAllAsync(string domainName, CancellationToken cancellationToken = default)
    {
        return await Collection(domainName)
            .Find(FilterDefinition<AlarmRuleDocument>.Empty)
            .SortByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    private IMongoCollection<AlarmRuleDocument> Collection(string domainName) =>
        context.GetDatabase(domainName).GetCollection<AlarmRuleDocument>(AlarmCollectionNames.Rules);
}

public sealed class AlarmRepository(IAlarmMongoContext context, AlarmIndexInitializer indexInitializer) : IAlarmRepository
{
    public async Task<AlarmDocument?> GetActiveByDedupKeyAsync(string domainName, string dedupKey, CancellationToken cancellationToken = default)
    {
        await indexInitializer.EnsureAsync(domainName, cancellationToken);
        return await Collection(domainName)
            .Find(x => x.DedupKey == dedupKey && x.Status == AlarmStatus.Active)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AlarmDocument?> GetByIdAsync(string domainName, string alarmId, CancellationToken cancellationToken = default)
    {
        await indexInitializer.EnsureAsync(domainName, cancellationToken);
        return await Collection(domainName)
            .Find(x => x.Id == alarmId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlarmDocument>> ListActiveByRuleIdAsync(
        string domainName,
        string ruleId,
        CancellationToken cancellationToken = default)
    {
        await indexInitializer.EnsureAsync(domainName, cancellationToken);
        return await Collection(domainName)
            .Find(x => x.RuleId == ruleId && x.Status == AlarmStatus.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AlarmDocument> Items, long Total)> ListAsync(
        string domainName,
        AlarmStatus? status,
        int? minSeverity,
        bool openOnly,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await indexInitializer.EnsureAsync(domainName, cancellationToken);
        var col = Collection(domainName);

        var filter = Builders<AlarmDocument>.Filter.Empty;
        if (status.HasValue)
            filter &= Builders<AlarmDocument>.Filter.Eq(x => x.Status, status.Value);
        else if (openOnly)
            filter &= Builders<AlarmDocument>.Filter.In(x => x.Status, [AlarmStatus.Active, AlarmStatus.Acknowledged]);

        if (minSeverity.HasValue)
            filter &= Builders<AlarmDocument>.Filter.Gte(x => x.Severity, minSeverity.Value);

        var total = await col.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await col
            .Find(filter)
            .SortByDescending(x => x.LastSeenAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task InsertAsync(AlarmDocument alarm, CancellationToken cancellationToken = default)
    {
        await indexInitializer.EnsureAsync(alarm.DomainName, cancellationToken);
        await Collection(alarm.DomainName).InsertOneAsync(alarm, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(AlarmDocument alarm, CancellationToken cancellationToken = default)
    {
        await Collection(alarm.DomainName).ReplaceOneAsync(x => x.Id == alarm.Id, alarm, cancellationToken: cancellationToken);
    }

    private IMongoCollection<AlarmDocument> Collection(string domainName) =>
        context.GetDatabase(domainName).GetCollection<AlarmDocument>(AlarmCollectionNames.Alarms);
}
