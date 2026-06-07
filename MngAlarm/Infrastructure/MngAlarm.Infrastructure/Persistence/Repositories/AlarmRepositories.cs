using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using MngAlarm.Application.Contracts;
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

public sealed class AlarmNotificationPolicyRepository(IAlarmMongoContext context) : IAlarmNotificationPolicyRepository
{
    public async Task InsertAsync(AlarmNotificationPolicyDocument policy, CancellationToken cancellationToken = default)
    {
        await Collection(policy.DomainName).InsertOneAsync(policy, cancellationToken: cancellationToken);
    }

    public async Task<AlarmNotificationPolicyDocument?> GetByIdAsync(
        string domainName,
        string policyId,
        CancellationToken cancellationToken = default)
    {
        return await Collection(domainName)
            .Find(x => x.Id == policyId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpdateAsync(AlarmNotificationPolicyDocument policy, CancellationToken cancellationToken = default)
    {
        await Collection(policy.DomainName)
            .ReplaceOneAsync(x => x.Id == policy.Id, policy, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string domainName, string policyId, CancellationToken cancellationToken = default)
    {
        await Collection(domainName).DeleteOneAsync(x => x.Id == policyId, cancellationToken);
    }

    public async Task<IReadOnlyList<AlarmNotificationPolicyDocument>> ListAsync(
        string domainName,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var filter = isActive.HasValue
            ? Builders<AlarmNotificationPolicyDocument>.Filter.Eq(x => x.IsActive, isActive.Value)
            : FilterDefinition<AlarmNotificationPolicyDocument>.Empty;

        return await Collection(domainName)
            .Find(filter)
            .SortByDescending(x => x.Priority)
            .ThenByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    private IMongoCollection<AlarmNotificationPolicyDocument> Collection(string domainName) =>
        context.GetDatabase(domainName).GetCollection<AlarmNotificationPolicyDocument>(AlarmCollectionNames.NotificationPolicies);
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
        string? ruleId = null,
        string? search = null,
        DateTime? from = null,
        DateTime? to = null,
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

        if (!string.IsNullOrWhiteSpace(ruleId))
            filter &= Builders<AlarmDocument>.Filter.Eq(x => x.RuleId, ruleId.Trim());

        if (from.HasValue)
        {
            var utcFrom = from.Value.Kind == DateTimeKind.Utc ? from.Value : from.Value.ToUniversalTime();
            filter &= Builders<AlarmDocument>.Filter.Gte(x => x.LastSeenAt, utcFrom);
        }

        if (to.HasValue)
        {
            var utcTo = to.Value.Kind == DateTimeKind.Utc ? to.Value : to.Value.ToUniversalTime();
            filter &= Builders<AlarmDocument>.Filter.Lte(x => x.LastSeenAt, utcTo);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var escaped = Regex.Escape(search.Trim());
            var regex = new BsonRegularExpression(escaped, "i");
            filter &= Builders<AlarmDocument>.Filter.Or(
                Builders<AlarmDocument>.Filter.Regex(x => x.DedupKey, regex),
                Builders<AlarmDocument>.Filter.Regex(x => x.CorrelationId, regex),
                Builders<AlarmDocument>.Filter.Regex("context.key", regex),
                Builders<AlarmDocument>.Filter.Regex("context.userId", regex),
                Builders<AlarmDocument>.Filter.Regex("context.srcIp", regex),
                Builders<AlarmDocument>.Filter.Regex("context.dstIp", regex));
        }

        var total = await col.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await col
            .Find(filter)
            .SortByDescending(x => x.LastSeenAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<AlarmScenarioRollupDto>> GetScenarioRollupAsync(
        string domainName,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        await indexInitializer.EnsureAsync(domainName, cancellationToken);
        var col = Collection(domainName);

        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("lastSeenAt", new BsonDocument
            {
                { "$gte", from },
                { "$lte", to },
            })),
            new BsonDocument("$match", new BsonDocument("context.key", new BsonDocument("$type", "string"))),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$context.key" },
                { "totalInRange", new BsonDocument("$sum", 1) },
                {
                    "openCount", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$in", new BsonArray { "$status", new BsonArray { "Active", "Acknowledged" } }),
                        1,
                        0,
                    }))
                },
                { "maxSeverity", new BsonDocument("$max", "$severity") },
                { "lastSeenAt", new BsonDocument("$max", "$lastSeenAt") },
            }),
        };

        var docs = await col.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        return docs.Select(doc => new AlarmScenarioRollupDto
        {
            MatchKey = doc.GetValue("_id", "").AsString,
            TotalInRange = doc.GetValue("totalInRange", 0).ToInt32(),
            OpenCount = doc.GetValue("openCount", 0).ToInt32(),
            MaxSeverity = doc.GetValue("maxSeverity", 0).ToInt32(),
            LastSeenAt = doc.GetValue("lastSeenAt", DateTime.UtcNow).ToUniversalTime(),
        }).ToList();
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
