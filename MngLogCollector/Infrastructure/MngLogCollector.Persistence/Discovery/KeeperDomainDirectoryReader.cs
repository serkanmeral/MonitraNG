using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MngLogCollector.Application.Abstractions.Discovery;
using MngLogCollector.Application.Configuration;

namespace MngLogCollector.Persistence.Discovery;

public sealed class KeeperDomainDirectoryReader : IKeeperDomainDirectoryReader
{
    private readonly IMongoClient _mongo;
    private readonly MngLogCollectorSettings _settings;
    private readonly ILogger<KeeperDomainDirectoryReader> _logger;

    public KeeperDomainDirectoryReader(
        IMongoClient mongo,
        IOptions<MngLogCollectorSettings> settings,
        ILogger<KeeperDomainDirectoryReader> logger)
    {
        _mongo = mongo;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<DiscoveryDomainInfo?> GetByNameOrIdAsync(string domainNameOrId, CancellationToken ct = default)
    {
        var collection = Domains();
        BsonDocument? doc = null;

        if (ObjectId.TryParse(domainNameOrId, out var oid))
        {
            doc = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefaultAsync(ct);
        }

        if (doc is null)
        {
            var nameFilter = Builders<BsonDocument>.Filter.Eq("name", domainNameOrId);
            doc = await collection.Find(nameFilter).FirstOrDefaultAsync(ct);
        }

        if (doc is null)
        {
            _logger.LogWarning("Keeper domain not found: {Key}", domainNameOrId);
            return null;
        }

        return Map(doc);
    }

    public async Task<IReadOnlyList<DiscoveryDomainInfo>> GetActiveDomainsWithLdapAsync(CancellationToken ct = default)
    {
        var active = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("status", "Active"),
            Builders<BsonDocument>.Filter.Eq("status", 1));
        var ldapEnabled = Builders<BsonDocument>.Filter.Eq("settings.directoryLdap.enabled", true);
        var filter = Builders<BsonDocument>.Filter.And(active, ldapEnabled);

        var docs = await Domains().Find(filter).ToListAsync(ct);
        return docs.Select(Map).Where(d => d.DirectoryLdap is { Enabled: true }).ToList();
    }

    private IMongoCollection<BsonDocument> Domains()
    {
        var dbName = string.IsNullOrWhiteSpace(_settings.MongoDB.KeeperDatabaseName)
            ? "mngkeeper"
            : _settings.MongoDB.KeeperDatabaseName;
        return _mongo.GetDatabase(dbName).GetCollection<BsonDocument>("domains");
    }

    private static DiscoveryDomainInfo Map(BsonDocument d)
    {
        var name = ReadString(d, "name");
        if (string.IsNullOrWhiteSpace(name))
            name = ReadString(d, "realmName");

        var databaseName = ReadString(d, "databaseName");
        if (string.IsNullOrWhiteSpace(databaseName))
            databaseName = $"mng_{name.ToLowerInvariant()}";

        DirectoryLdapConfig? ldap = null;
        if (d.TryGetValue("settings", out var settingsVal) && settingsVal.IsBsonDocument)
        {
            var settings = settingsVal.AsBsonDocument;
            if (settings.TryGetValue("directoryLdap", out var ldapVal) && ldapVal.IsBsonDocument)
            {
                var l = ldapVal.AsBsonDocument;
                ldap = new DirectoryLdapConfig
                {
                    Enabled = l.GetValue("enabled", false).ToBoolean(),
                    Host = ReadString(l, "host"),
                    Port = l.GetValue("port", 389).ToInt32(),
                    UseSsl = l.GetValue("useSsl", false).ToBoolean(),
                    BaseDn = ReadString(l, "baseDn"),
                    BindUsername = ReadString(l, "bindUsername"),
                    BindPassword = ReadString(l, "bindPassword")
                };
            }
        }

        return new DiscoveryDomainInfo
        {
            Id = d.Contains("_id") ? d["_id"].ToString() ?? string.Empty : string.Empty,
            Name = name,
            DatabaseName = databaseName,
            DirectoryLdap = ldap
        };
    }

    private static string ReadString(BsonDocument d, string field)
    {
        if (!d.TryGetValue(field, out var v) || v.IsBsonNull)
            return string.Empty;
        return v.ToString() ?? string.Empty;
    }
}
