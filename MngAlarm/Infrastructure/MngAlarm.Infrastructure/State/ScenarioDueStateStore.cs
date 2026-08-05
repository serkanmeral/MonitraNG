using System.Collections.Concurrent;
using MongoDB.Driver;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Persistence;

namespace MngAlarm.Infrastructure.State;

public interface IScenarioDueStateStore
{
    Task UpsertAsync(ScenarioDueStateDocument state, CancellationToken cancellationToken = default);
    Task CancelAsync(
        string domainName,
        string ruleId,
        string nodeId,
        string groupKey,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScenarioDueStateDocument>> ClaimDueAsync(
        DateTime now,
        TimeSpan lease,
        int limit,
        CancellationToken cancellationToken = default);
    Task<bool> IsClaimValidAsync(string id, string claimToken, CancellationToken cancellationToken = default);
    Task<bool> CompleteAsync(string id, string claimToken, CancellationToken cancellationToken = default);
    Task ReleaseAsync(
        string id,
        string claimToken,
        DateTime retryAt,
        CancellationToken cancellationToken = default);
}

public static class ScenarioDueStateKeys
{
    public static string Create(string domainName, string ruleId, string nodeId, string groupKey) =>
        $"{domainName}:{ruleId}:{nodeId}:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(groupKey)))[..16]}";
}

public sealed class MongoScenarioDueStateStore(
    IAlarmMongoContext context,
    TimeProvider timeProvider) : IScenarioDueStateStore
{
    private const string RuntimeDatabase = "_alarm_runtime";
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesReady;

    public async Task UpsertAsync(ScenarioDueStateDocument state, CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        state.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            await Collection.ReplaceOneAsync(
                x => x.Id == state.Id && (x.ClaimedUntil == null || x.ClaimedUntil < state.UpdatedAt),
                state,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // A scanner currently owns this deterministic state id. Its claim wins.
        }
    }

    public async Task CancelAsync(
        string domainName,
        string ruleId,
        string nodeId,
        string groupKey,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        await Collection.DeleteOneAsync(
            x => x.Id == ScenarioDueStateKeys.Create(domainName, ruleId, nodeId, groupKey),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ScenarioDueStateDocument>> ClaimDueAsync(
        DateTime now,
        TimeSpan lease,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        var claimed = new List<ScenarioDueStateDocument>();
        for (var i = 0; i < Math.Clamp(limit, 1, 1000); i++)
        {
            var token = Guid.NewGuid().ToString("N");
            var filter = Builders<ScenarioDueStateDocument>.Filter.And(
                Builders<ScenarioDueStateDocument>.Filter.Lte(x => x.NextEvaluationAt, now),
                Builders<ScenarioDueStateDocument>.Filter.Or(
                    Builders<ScenarioDueStateDocument>.Filter.Eq(x => x.ClaimedUntil, null),
                    Builders<ScenarioDueStateDocument>.Filter.Lt(x => x.ClaimedUntil, now)));
            var update = Builders<ScenarioDueStateDocument>.Update
                .Set(x => x.ClaimToken, token)
                .Set(x => x.ClaimedUntil, now.Add(lease))
                .Inc(x => x.Attempts, 1);
            var item = await Collection.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<ScenarioDueStateDocument>
                {
                    Sort = Builders<ScenarioDueStateDocument>.Sort.Ascending(x => x.NextEvaluationAt),
                    ReturnDocument = ReturnDocument.After
                },
                cancellationToken);
            if (item == null) break;
            claimed.Add(item);
        }
        return claimed;
    }

    public async Task<bool> CompleteAsync(
        string id,
        string claimToken,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        return (await Collection.DeleteOneAsync(
            x => x.Id == id && x.ClaimToken == claimToken,
            cancellationToken)).DeletedCount == 1;
    }

    public async Task<bool> IsClaimValidAsync(
        string id,
        string claimToken,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        return await Collection.Find(x => x.Id == id && x.ClaimToken == claimToken)
            .AnyAsync(cancellationToken);
    }

    public async Task ReleaseAsync(
        string id,
        string claimToken,
        DateTime retryAt,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        await Collection.UpdateOneAsync(
            x => x.Id == id && x.ClaimToken == claimToken,
            Builders<ScenarioDueStateDocument>.Update
                .Set(x => x.NextEvaluationAt, retryAt)
                .Set(x => x.ClaimToken, null)
                .Set(x => x.ClaimedUntil, null),
            cancellationToken: cancellationToken);
    }

    private IMongoCollection<ScenarioDueStateDocument> Collection =>
        context.GetDatabase(RuntimeDatabase)
            .GetCollection<ScenarioDueStateDocument>(AlarmCollectionNames.ScenarioDueState);

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (_indexesReady) return;
        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            if (_indexesReady) return;
            await Collection.Indexes.CreateOneAsync(
                new CreateIndexModel<ScenarioDueStateDocument>(
                    Builders<ScenarioDueStateDocument>.IndexKeys
                        .Ascending(x => x.NextEvaluationAt)
                        .Ascending(x => x.ClaimedUntil),
                    new CreateIndexOptions { Name = "idx_due_claim" }),
                cancellationToken: cancellationToken);
            _indexesReady = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }
}

public sealed class InMemoryScenarioDueStateStore : IScenarioDueStateStore
{
    private readonly ConcurrentDictionary<string, ScenarioDueStateDocument> _states = new(StringComparer.Ordinal);

    public Task UpsertAsync(ScenarioDueStateDocument state, CancellationToken cancellationToken = default)
    {
        _states.AddOrUpdate(state.Id, Clone(state), (_, existing) =>
            existing.ClaimedUntil.HasValue ? existing : Clone(state));
        return Task.CompletedTask;
    }

    public Task CancelAsync(
        string domainName,
        string ruleId,
        string nodeId,
        string groupKey,
        CancellationToken cancellationToken = default)
    {
        _states.TryRemove(ScenarioDueStateKeys.Create(domainName, ruleId, nodeId, groupKey), out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ScenarioDueStateDocument>> ClaimDueAsync(
        DateTime now,
        TimeSpan lease,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var result = new List<ScenarioDueStateDocument>();
        foreach (var pair in _states.OrderBy(x => x.Value.NextEvaluationAt))
        {
            if (result.Count >= limit) break;
            lock (pair.Value)
            {
                if (pair.Value.NextEvaluationAt > now
                    || pair.Value.ClaimedUntil.HasValue && pair.Value.ClaimedUntil >= now)
                    continue;
                pair.Value.ClaimToken = Guid.NewGuid().ToString("N");
                pair.Value.ClaimedUntil = now.Add(lease);
                pair.Value.Attempts++;
                result.Add(Clone(pair.Value));
            }
        }
        return Task.FromResult<IReadOnlyList<ScenarioDueStateDocument>>(result);
    }

    public Task<bool> CompleteAsync(string id, string claimToken, CancellationToken cancellationToken = default)
    {
        if (!_states.TryGetValue(id, out var item) || item.ClaimToken != claimToken)
            return Task.FromResult(false);
        return Task.FromResult(_states.TryRemove(id, out _));
    }

    public Task<bool> IsClaimValidAsync(
        string id,
        string claimToken,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_states.TryGetValue(id, out var item) && item.ClaimToken == claimToken);

    public Task ReleaseAsync(
        string id,
        string claimToken,
        DateTime retryAt,
        CancellationToken cancellationToken = default)
    {
        if (_states.TryGetValue(id, out var item))
        {
            lock (item)
            {
                if (item.ClaimToken == claimToken)
                {
                    item.NextEvaluationAt = retryAt;
                    item.ClaimToken = null;
                    item.ClaimedUntil = null;
                }
            }
        }
        return Task.CompletedTask;
    }

    private static ScenarioDueStateDocument Clone(ScenarioDueStateDocument state) => new()
    {
        Id = state.Id,
        DomainId = state.DomainId,
        DomainName = state.DomainName,
        RuleId = state.RuleId,
        ScenarioVersion = state.ScenarioVersion,
        NodeId = state.NodeId,
        NodeType = state.NodeType,
        GroupKey = state.GroupKey,
        NextEvaluationAt = state.NextEvaluationAt,
        Observation = new ScenarioDueObservation
        {
            Kind = state.Observation.Kind,
            Key = state.Observation.Key,
            Value = state.Observation.Value,
            Timestamp = state.Observation.Timestamp,
            Dimensions = new Dictionary<string, object?>(state.Observation.Dimensions, StringComparer.Ordinal)
        },
        ClaimToken = state.ClaimToken,
        ClaimedUntil = state.ClaimedUntil,
        Attempts = state.Attempts,
        UpdatedAt = state.UpdatedAt
    };
}
