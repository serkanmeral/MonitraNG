using System.Text.Json.Serialization;

namespace MngReactor.Application.Features.Engine;

/// <summary>
/// Engine status (heartbeat + hata raporu) istek modeli.
/// Ref: docs/content/MngEngine/support/specs/ENGINE_REACTOR_STATUS_SPEC.md
/// </summary>
public class EngineStatusRequest
{
    /// <summary>Engine kimliği (mon_engines __dataId)</summary>
    public required string EngineId { get; set; }

    /// <summary>Tenant domain</summary>
    public required string Domain { get; set; }

    /// <summary>Rapor zamanı (UTC)</summary>
    public DateTime? Timestamp { get; set; }

    /// <summary>Sağlık durumu: ok | degraded | error</summary>
    public string? Health { get; set; }

    /// <summary>Son toplama hataları (örn. son 50)</summary>
    public IList<EngineStatusErrorItem>? Errors { get; set; }

    /// <summary>Kuyruktaki batch sayısı (opsiyonel)</summary>
    public int? QueueDepth { get; set; }

    /// <summary>Config'teki asset sayısı (opsiyonel)</summary>
    public int? AssetCount { get; set; }

    /// <summary>Engine'in çalıştığı makinenin IP adresi (opsiyonel)</summary>
    [JsonPropertyName("hostAddress")]
    public string? HostAddress { get; set; }
}

/// <summary>
/// Tek bir toplama hatası öğesi.
/// </summary>
public class EngineStatusErrorItem
{
    public required string AssetId { get; set; }
    public required string AgentId { get; set; }
    /// <summary>connection_timeout | auth_failed | ssh_error | snmp_error | wmi_error | unknown</summary>
    public required string ErrorCode { get; set; }
    public required string Message { get; set; }
    public required DateTime OccurredAt { get; set; }
}
