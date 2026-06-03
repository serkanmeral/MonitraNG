using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MngReactor.Application.Abstractions.Engine;

/// <summary>
/// Config Sync API yanıt formatı (MONITORING_ENGINE_ARCHITECTURE 5.3)
/// </summary>
public class EngineConfigSyncResult
{
    public string EngineId { get; set; } = "";
    public string Domain { get; set; } = "";
    /// <summary>Veri gönderim cron (6 alanlı Quartz). UI'da engine sendSchedule değişince Config Sync ile Engine'e ulaşır.</summary>
    public string? SendSchedule { get; set; }
    /// <summary>Config sync periyodu (dakika).</summary>
    public int ConfigSyncPeriodMinutes { get; set; } = 10;
    public List<EngineConfigAgent> Agents { get; set; } = [];
    public List<EngineConfigAsset> AssetConfigs { get; set; } = [];
}

public class EngineConfigAgent
{
    public string AgentId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "active";
    public JsonObject? DefaultPeriod { get; set; }
    public JsonObject? DefaultSchedule { get; set; }
}

public class EngineConfigAsset
{
    public string AgentId { get; set; } = "";
    public string AssetId { get; set; } = "";
    public string? ItemId { get; set; }
    /// <summary>Agent adı (UI'da ID yerine gösterim için)</summary>
    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = "";
    /// <summary>Asset adı (UI'da ID yerine gösterim için)</summary>
    [JsonPropertyName("assetName")]
    public string AssetName { get; set; } = "";
    /// <summary>Item adı (UI'da ID yerine gösterim için)</summary>
    [JsonPropertyName("itemName")]
    public string? ItemName { get; set; }
    public JsonObject? Period { get; set; }
    public JsonObject? Schedule { get; set; }
    public bool Active { get; set; } = true;
    public JsonObject? ConnectionInfo { get; set; }
    public string CollectionMethod { get; set; } = "";
    public List<EngineConfigCollectible> Collectibles { get; set; } = [];
}

public class EngineConfigCollectible
{
    public string Code { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public JsonObject? Params { get; set; }
}
