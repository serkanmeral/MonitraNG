using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Crypt;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Abstractions.Engine;
using MngReactor.Application.Configuration;

namespace MngReactor.Persistence.Services.Engine;

public class ConfigStringProcessing : IConfigStringService
{
    private readonly ILogger<ConfigStringProcessing> _logger;
    private readonly IDataGatewayClient _dg;
    private readonly ICryptProcessing _cryptProcessing;
    private readonly IOptions<MngReactor.Application.Configuration.MngReactorSettings> _options;

    public ConfigStringProcessing(
        ILogger<ConfigStringProcessing> logger,
        IDataGatewayClient dg,
        ICryptProcessing cryptProcessing,
        IOptions<MngReactor.Application.Configuration.MngReactorSettings> options)
    {
        _logger = logger;
        _dg = dg;
        _cryptProcessing = cryptProcessing;
        _options = options;
    }

    /// <summary>
    /// Engine'ın beklediği format: Base64( JSON( CompressPbk, CompressPrk, EngineInfo ) ).
    /// CompressPbk/CompressPrk RSA ile (Engine public key) şifrelenir; EngineInfo = Base64( AES+GZip(engineInfo JSON) ).
    /// </summary>
    public async Task<string?> CreateConfigStringAsync(string engineId, string domain, string? accessToken, CancellationToken cancellationToken = default)
    {
        var token = ResolveToken(domain, accessToken);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("ConfigString: token bulunamadı domain={Domain}", domain);
            return null;
        }

        var engine = await GetEngineAsync(engineId, token, cancellationToken);
        if (engine == null)
        {
            _logger.LogWarning("Engine {EngineId} not found for config string", engineId);
            return null;
        }

        var keeperUrl = _options.Value.Actors.MngKeeper?.TrimEnd('/') ?? "";
        var keeperEngineUrl = _options.Value.Actors.MngKeeperEngineUrl?.TrimEnd('/') ?? "";
        var keeperForEngine = !string.IsNullOrEmpty(keeperEngineUrl) ? keeperEngineUrl : keeperUrl;

        var serverUrl = _options.Value.EngineServerUrl?.TrimEnd('/') ?? "";
        if (string.IsNullOrEmpty(serverUrl))
            serverUrl = _options.Value.OpenApiServerPath?.TrimEnd('/') ?? "";
        if (string.IsNullOrEmpty(serverUrl))
            serverUrl = $"http://localhost:{_options.Value.Server.Port}";

        var mqttHost = _options.Value.Mqtt.Host;
        var mqttPort = _options.Value.Mqtt.Port;
        var mqttUrl = !string.IsNullOrEmpty(mqttHost) ? $"mqtt://{mqttHost}:{mqttPort}" : "";

        var tokenUrl = string.IsNullOrEmpty(keeperForEngine) ? "" : $"{keeperForEngine}/api/auth/token";

        var password = engine["password"]?.GetValue<string>() ?? "";
        try
        {
            if (!string.IsNullOrEmpty(password))
            {
                var bytes = Convert.FromBase64String(password);
                password = await _cryptProcessing.DeCompress(bytes);
            }
        }
        catch
        {
            _logger.LogWarning("Could not decrypt engine password");
        }

        var engineName = engine["name"]?.GetValue<string>();
        var payload = new
        {
            engineId,
            engineName = !string.IsNullOrEmpty(engineName) ? engineName : (object?)null,
            serverUrl,
            tokenUrl,
            username = engine["username"]?.GetValue<string>() ?? "",
            password,
            sendSchedule = engine["sendSchedule"]?.GetValue<string>() ?? "0 */2 * * *",
            configSyncPeriodMinutes = engine["configSyncPeriodMinutes"]?.GetValue<int>() ?? 10,
            domain,
            mqttUrl
        };

        var engineInfoJson = JsonSerializer.Serialize(payload);
        var engineDataBytes = await _cryptProcessing.Compress(engineInfoJson);

        var compressPbk = _options.Value.Crypt?.IngestEncryptKey ?? "";
        var compressPrk = _options.Value.Crypt?.IngestDecryptKey ?? "";
        if (string.IsNullOrEmpty(compressPbk) || string.IsNullOrEmpty(compressPrk))
        {
            _logger.LogWarning("ConfigString: Crypt keys (IngestEncryptKey/IngestDecryptKey) yok, config string üretilemiyor");
            return null;
        }

        var data = new JsonObject
        {
            ["CompressPbk"] = await _cryptProcessing.Encrypt(compressPbk),
            ["CompressPrk"] = await _cryptProcessing.Encrypt(compressPrk),
            ["EngineInfo"] = Convert.ToBase64String(engineDataBytes)
        };

        // + ve / escape edilmeden (\\u002B olmadan) yazılsın; böylece Base64 decode edilen JSON'da backslash olmaz
        var jsonOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var dataJson = JsonSerializer.Serialize(data, jsonOptions);
        var dataBytes = Encoding.UTF8.GetBytes(dataJson);
        var b64 = Convert.ToBase64String(dataBytes);
        // URL-safe Base64: çıktıda + ve / olmasın; JSON/kopyala-yapıştır ile bozulma olmaz
        return b64.Replace('+', '-').Replace('/', '_');
    }

    private string? ResolveToken(string domain, string? accessToken)
    {
        if (!string.IsNullOrEmpty(accessToken)) return accessToken;
        return _options.Value?.DataGateway?.DomainTokens?.GetValueOrDefault(domain);
    }

    private async Task<JsonObject?> GetEngineAsync(string engineId, string token, CancellationToken ct)
    {
        return await _dg.GetByIdAsync("mon_engines", engineId, token, ct);
    }
}
