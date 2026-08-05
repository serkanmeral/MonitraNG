using MongoDB.Driver;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Persistence;

namespace MngAlarm.Infrastructure.State;

public sealed class MongoSequenceStateStore(IAlarmMongoContext context) : ISequenceStateStore
{
    public SequenceRuntimeState GetOrCreate(string storeKey)
    {
        var domainName = ParseStoreKey(storeKey).DomainName;
        var doc = Collection(domainName).Find(x => x.Id == storeKey).FirstOrDefault();
        return doc == null
            ? new SequenceRuntimeState()
            : new SequenceRuntimeState
            {
                NextStepIndex = doc.NextStepIndex,
                CurrentStepCount = doc.CurrentStepCount,
                AnchorTime = doc.AnchorTime,
                LastStepTime = doc.LastStepTime,
                ConditionSince = doc.ConditionSince,
                Armed = doc.NextStepIndex > 0
            };
    }

    public void Save(string storeKey, SequenceRuntimeState state)
    {
        var (domainName, ruleId) = ParseStoreKey(storeKey);
        Collection(domainName).ReplaceOne(
            x => x.Id == storeKey,
            new SequenceStateDocument
            {
                Id = storeKey,
                DomainName = domainName,
                RuleId = ruleId,
                NextStepIndex = state.NextStepIndex,
                CurrentStepCount = state.CurrentStepCount,
                AnchorTime = state.AnchorTime,
                LastStepTime = state.LastStepTime,
                ConditionSince = state.ConditionSince
            },
            new ReplaceOptions { IsUpsert = true });
    }

    public void Reset(string storeKey)
    {
        var domainName = ParseStoreKey(storeKey).DomainName;
        Collection(domainName).DeleteOne(x => x.Id == storeKey);
    }

    private static (string DomainName, string RuleId) ParseStoreKey(string storeKey)
    {
        var parts = storeKey.Split(':', 3);
        return parts.Length >= 2 ? (parts[0], parts[1]) : (storeKey, string.Empty);
    }

    private IMongoCollection<SequenceStateDocument> Collection(string domainName) =>
        context.GetDatabase(domainName).GetCollection<SequenceStateDocument>(AlarmCollectionNames.SequenceState);
}

public sealed class MongoCorrelationWindowStore(IAlarmMongoContext context) : ICorrelationWindowStore
{
    public int RecordAndCount(string storeKey, DateTime eventTime, TimeSpan window)
    {
        var (domainName, ruleId) = ParseStoreKey(storeKey);
        var col = Collection(domainName);
        var doc = col.Find(x => x.Id == storeKey).FirstOrDefault() ?? new CorrelationWindowDocument
        {
            Id = storeKey,
            DomainName = domainName,
            RuleId = ruleId
        };

        doc.Events.Add(eventTime);
        PruneEvents(doc.Events, eventTime, window);
        col.ReplaceOne(x => x.Id == storeKey, doc, new ReplaceOptions { IsUpsert = true });
        return doc.Events.Count;
    }

    public int GetCount(string storeKey, DateTime now, TimeSpan window)
    {
        var domainName = ParseStoreKey(storeKey).DomainName;
        var doc = Collection(domainName).Find(x => x.Id == storeKey).FirstOrDefault();
        if (doc == null)
            return 0;

        PruneEvents(doc.Events, now, window);
        if (doc.Events.Count == 0)
            Collection(domainName).DeleteOne(x => x.Id == storeKey);

        return doc.Events.Count;
    }

    public IEnumerable<(string StoreKey, int Count)> EnumerateRule(
        string domainName,
        string ruleId,
        TimeSpan window,
        DateTime now)
    {
        var prefix = $"{domainName}:{ruleId}:";
        var docs = Collection(domainName).Find(x => x.Id.StartsWith(prefix)).ToList();
        foreach (var doc in docs)
            yield return (doc.Id, GetCount(doc.Id, now, window));
    }

    public int PruneExpired(string domainName, DateTime now)
    {
        var col = Collection(domainName);
        var docs = col.Find(FilterDefinition<CorrelationWindowDocument>.Empty).ToList();
        var removed = 0;

        foreach (var doc in docs)
        {
            doc.Events.RemoveAll(t => t < now.AddHours(-24));
            if (doc.Events.Count == 0)
            {
                col.DeleteOne(x => x.Id == doc.Id);
                removed++;
            }
            else
            {
                col.ReplaceOne(x => x.Id == doc.Id, doc);
            }
        }

        return removed;
    }

    private static void PruneEvents(List<DateTime> events, DateTime now, TimeSpan window)
    {
        var cutoff = now - window;
        events.RemoveAll(t => t < cutoff);
    }

    private static (string DomainName, string RuleId) ParseStoreKey(string storeKey)
    {
        var parts = storeKey.Split(':', 3);
        return parts.Length >= 2 ? (parts[0], parts[1]) : (storeKey, string.Empty);
    }

    private IMongoCollection<CorrelationWindowDocument> Collection(string domainName) =>
        context.GetDatabase(domainName).GetCollection<CorrelationWindowDocument>(AlarmCollectionNames.CorrelationWindows);
}

public sealed class MongoObservationActivityStore(IAlarmMongoContext context) : IObservationActivityStore
{
    public void Record(string activityKey, DateTime timestamp)
    {
        var (domainName, ruleId) = ParseActivityKey(activityKey);
        var col = Collection(domainName);
        var existing = col.Find(x => x.Id == activityKey).FirstOrDefault();
        if (existing != null && existing.LastSeenAt >= timestamp)
            return;

        col.ReplaceOne(
            x => x.Id == activityKey,
            new ObservationActivityDocument
            {
                Id = activityKey,
                DomainName = domainName,
                RuleId = ruleId,
                LastSeenAt = timestamp
            },
            new ReplaceOptions { IsUpsert = true });
    }

    public DateTime? GetLastSeen(string activityKey)
    {
        var domainName = ParseActivityKey(activityKey).DomainName;
        return Collection(domainName).Find(x => x.Id == activityKey).FirstOrDefault()?.LastSeenAt;
    }

    public IEnumerable<string> EnumerateKeys(string domainName, string ruleId)
    {
        var prefix = $"{domainName}:{ruleId}:";
        return Collection(domainName)
            .Find(x => x.Id.StartsWith(prefix))
            .Project(x => x.Id)
            .ToList();
    }

    private static (string DomainName, string RuleId) ParseActivityKey(string activityKey)
    {
        var parts = activityKey.Split(':', 3);
        return parts.Length >= 2 ? (parts[0], parts[1]) : (activityKey, string.Empty);
    }

    private IMongoCollection<ObservationActivityDocument> Collection(string domainName) =>
        context.GetDatabase(domainName).GetCollection<ObservationActivityDocument>(AlarmCollectionNames.ObservationActivity);
}
