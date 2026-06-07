using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MngAlarm.Domain.Constants;
using MngAlarm.Infrastructure.Persistence;

namespace MngAlarm.Infrastructure.Persistence.Repositories;

[BsonIgnoreExtraElements]
public sealed class AlarmNotificationCooldownDocument
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("lastDispatchedAt")]
    public DateTime LastDispatchedAt { get; set; }
}

public interface IAlarmNotificationCooldownStore
{
    Task<bool> TryAcquireAsync(
        string domainName,
        string policyId,
        string alarmId,
        int cooldownMinutes,
        CancellationToken cancellationToken = default);

    Task MarkDispatchedAsync(
        string domainName,
        string policyId,
        string alarmId,
        CancellationToken cancellationToken = default);
}

public sealed class AlarmNotificationCooldownStore(IAlarmMongoContext context) : IAlarmNotificationCooldownStore
{
    public async Task<bool> TryAcquireAsync(
        string domainName,
        string policyId,
        string alarmId,
        int cooldownMinutes,
        CancellationToken cancellationToken = default)
    {
        if (cooldownMinutes <= 0)
            return true;

        var id = BuildId(policyId, alarmId);
        var col = Collection(domainName);
        var existing = await col.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
        if (existing == null)
            return true;

        return DateTime.UtcNow - existing.LastDispatchedAt >= TimeSpan.FromMinutes(cooldownMinutes);
    }

    public async Task MarkDispatchedAsync(
        string domainName,
        string policyId,
        string alarmId,
        CancellationToken cancellationToken = default)
    {
        var id = BuildId(policyId, alarmId);
        var now = DateTime.UtcNow;
        await Collection(domainName).ReplaceOneAsync(
            x => x.Id == id,
            new AlarmNotificationCooldownDocument { Id = id, LastDispatchedAt = now },
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    private static string BuildId(string policyId, string alarmId) => $"{policyId}:{alarmId}";

    private IMongoCollection<AlarmNotificationCooldownDocument> Collection(string domainName) =>
        context.GetDatabase(domainName).GetCollection<AlarmNotificationCooldownDocument>(AlarmCollectionNames.NotificationCooldowns);
}
