namespace MngEngine.Application.Features.EngineConfig;

/// <summary>
/// Mimariye uygun config string payload (EngineInfo decrypt edildikten sonra).
/// MonitraNG UI'dan gelen config string'deki alanlarla eşleşir.
/// </summary>
public record EngineConfigPayload
{
    public string EngineId { get; init; } = "";
    public string? EngineName { get; init; }
    public string Domain { get; init; } = "";
    public string ServerUrl { get; init; } = "";
    public string TokenUrl { get; init; } = "";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string SendSchedule { get; init; } = "0 */5 * * * ?";
    public int ConfigSyncPeriodMinutes { get; init; } = 10;
    public string? MqttUrl { get; init; }
    /// <summary>Ingest şifreleme için AES IV (config string'den). Varsa IngestClient şifreli gönderir.</summary>
    public string? CompressPbk { get; init; }
    /// <summary>Ingest şifreleme için AES key (config string'den).</summary>
    public string? CompressPrk { get; init; }
}
