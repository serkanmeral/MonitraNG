using System.Text.Json.Nodes;

namespace MngEngine.Application.Features.EngineConfig;

/// <summary>
/// Reactor GET /api/v1/engine/config yanıtı ile uyumlu model.
/// </summary>
public record EngineConfigSyncResult
{
    public string EngineId { get; init; } = "";
    public string Domain { get; init; } = "";
    /// <summary>Veri gönderim cron (6 alanlı Quartz). Reactor config sync'ten gelir (mon_engines.sendSchedule).</summary>
    public string? SendSchedule { get; init; }
    /// <summary>Config sync periyodu (dakika).</summary>
    public int ConfigSyncPeriodMinutes { get; init; } = 10;
    public List<EngineConfigAgent> Agents { get; init; } = [];
    public List<EngineConfigAsset> AssetConfigs { get; init; } = [];
}

public record EngineConfigAgent
{
    public string AgentId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Status { get; init; } = "active";
}

public record EngineConfigAsset
{
    public string AgentId { get; init; } = "";
    public string AssetId { get; init; } = "";
    public string? ItemId { get; init; }
    public string AgentName { get; init; } = "";
    public string AssetName { get; init; } = "";
    public string? ItemName { get; init; }
    /// <summary>Collection period cron (örn. */1 * * * *). Reactor'dan period.expression ile gelir.</summary>
    public string? PeriodExpression { get; init; }
    public JsonObject? ConnectionInfo { get; init; }
    public string CollectionMethod { get; init; } = "ssh";
    public List<EngineConfigCollectible> Collectibles { get; init; } = [];
}

public record EngineConfigCollectible
{
    public string Code { get; init; } = "";
    public bool Enabled { get; init; } = true;
}
