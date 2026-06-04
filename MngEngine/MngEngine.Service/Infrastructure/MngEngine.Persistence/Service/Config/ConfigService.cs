using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using MngEngine.Application.Features.EngineConfig;
using MngEngine.Application.Interfaces;
using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Serilog;

namespace MngEngine.Persistence.Service.Config
{
    public class ConfigService : IConfigService
    {
        private const string ConfigCacheKey = "config";
        private const string EngineConfigPayloadCacheKey = "engineConfigPayload";

        private readonly ICryptProcessing _cryptProcessing;
        private readonly IMemoryCache _memoryCache;
        private readonly string _configFilePath;

        public ConfigService(ICryptProcessing cryptProcessing, IMemoryCache memoryCache, IConfiguration configuration)
        {
            _cryptProcessing = cryptProcessing;
            _memoryCache = memoryCache;
            _configFilePath = configuration["MngEngine:Config:FilePath"]?.Trim() ?? "config.txt";
        }

        private void EnsureConfigDirectory()
        {
            var dir = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }

        private static string? GetString(JsonObject obj, string pascalKey, string camelKey)
        {
            var node = obj[pascalKey] ?? obj[camelKey];
            return node?.GetValue<string>()?.Trim();
        }

        /// <summary>Gelen config string: sadece form-urlencoded geri alınır (space→+). Başka düzenleme yok.</summary>
        private async Task<JsonObject> GetConfigJsonAsync(string configText)
        {
            var b64 = (configText ?? "").Replace(' ', '+');
            if (string.IsNullOrEmpty(b64))
                throw new InvalidOperationException("Config string boş.");

            Log.Information("Config decode adim: 1-Dis Base64 decode basliyor. b64 uzunluk={Len} (4eBolum={Kalan} esittirSayisi={EqCount})", b64.Length, b64.Length % 4, b64.Count(c => c == '='));
            for (var i = 0; i < b64.Length; i++)
            {
                var c = b64[i];
                var ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '=';
                if (!ok)
                    Log.Warning("Config base64 gecersiz karakter: Pozisyon={Pos} ASCII/Unicode={Code} (0x{CodeHex}) karakter='{Char}'", i, (int)c, (int)c, c);
            }
            byte[] rawDataBytes;
            try
            {
                rawDataBytes = Convert.FromBase64String(b64);
                Log.Information("Config decode adim: 1-Dis Base64 decode tamamlandi. Decode byte uzunluk={Len}", rawDataBytes.Length);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Config decode adim: 1-Dis Base64 decode HATA. b64 uzunluk={Len}", b64.Length);
                throw;
            }

            var resultString = Encoding.UTF8.GetString(rawDataBytes);
            var dataObj = JsonSerializer.Deserialize<JsonObject>(resultString)
                ?? throw new InvalidOperationException("Config JSON geçersiz.");

            var compressPbkEnc = GetString(dataObj, "CompressPbk", "compressPbk")
                ?? throw new InvalidOperationException("Config'te CompressPbk yok.");
            var compressPrkEnc = GetString(dataObj, "CompressPrk", "compressPrk")
                ?? throw new InvalidOperationException("Config'te CompressPrk yok.");
            var engineInfoB64 = GetString(dataObj, "EngineInfo", "engineInfo")
                ?? throw new InvalidOperationException("Config'te EngineInfo yok.");

            Log.Information("Config decode adim: 2-CompressPbk Decrypt basliyor. Uzunluk={Len}", compressPbkEnc?.Length ?? 0);
            string compressPbk;
            try
            {
                compressPbk = await _cryptProcessing.Decrypt(compressPbkEnc ?? "");
                Log.Information("Config decode adim: 2-CompressPbk Decrypt tamamlandi.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Config decode adim: 2-CompressPbk Decrypt HATA.");
                throw;
            }

            Log.Information("Config decode adim: 3-CompressPrk Decrypt basliyor. Uzunluk={Len}", compressPrkEnc?.Length ?? 0);
            string compressPrk;
            try
            {
                compressPrk = await _cryptProcessing.Decrypt(compressPrkEnc ?? "");
                Log.Information("Config decode adim: 3-CompressPrk Decrypt tamamlandi.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Config decode adim: 3-CompressPrk Decrypt HATA.");
                throw;
            }

            Log.Information("Config decode adim: 4-EngineInfo Base64 decode basliyor. Uzunluk={Len}", engineInfoB64?.Length ?? 0);
            byte[] engineInfoBytes;
            try
            {
                engineInfoBytes = Convert.FromBase64String(engineInfoB64 ?? "");
                Log.Information("Config decode adim: 4-EngineInfo Base64 decode tamamlandi.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Config decode adim: 4-EngineInfo Base64 decode HATA.");
                throw;
            }

            Log.Information("Config decode adim: 5-DeCompress basliyor.");
            var engineInfoTxt = await _cryptProcessing.DeCompress(engineInfoBytes, compressPrk, compressPbk);
            Log.Information("Config decode adim: 5-DeCompress tamamlandi.");

            var configObj = new JsonObject
            {
                ["CompressPbk"] = compressPbk,
                ["CompressPrk"] = compressPrk,
                ["EngineInfo"] = JsonSerializer.Deserialize<JsonObject>(engineInfoTxt)
            };

            return configObj;
        }

        private static string ResolveServerUrl(JsonObject? engineInfo)
        {
            if (engineInfo == null) return "";

            var serverUrl = engineInfo["serverUrl"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(serverUrl)) return serverUrl.TrimEnd('/');

            var host = engineInfo["host"]?.GetValue<string>();
            var domain = engineInfo["domain"]?.GetValue<string>();
            if (string.IsNullOrEmpty(host)) return "";

            if (!string.IsNullOrEmpty(domain) && host.Contains("//"))
            {
                var parts = host.Split("//");
                return $"{parts[0]}//{domain}.{parts[1]}".TrimEnd('/');
            }
            return host.TrimEnd('/');
        }

        private static string ResolveTokenUrl(string serverUrl, JsonObject? engineInfo)
        {
            var tokenUrl = engineInfo?["tokenUrl"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(tokenUrl)) return tokenUrl.TrimEnd('/');
            return string.IsNullOrEmpty(serverUrl) ? "" : serverUrl;
        }

        private static string ResolveSendSchedule(JsonObject? engineInfo)
        {
            var sendSchedule = engineInfo?["sendSchedule"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(sendSchedule)) return sendSchedule;

            var segment = engineInfo?["collectIntervalSegment"]?.GetValue<string>();
            var value = engineInfo?["collectIntervalValue"]?.GetValue<int>() ?? 15;
            if (string.Equals(segment, "sn", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(segment))
                return $"0/{value} * * * * ?";
            return $"0/{value} * * * * ?";
        }

        private async Task<EngineConfigPayload?> BuildEngineConfigPayloadAsync(JsonObject configObj)
        {
            var engineInfo = configObj["EngineInfo"] as JsonObject;
            if (engineInfo == null) return null;

            var serverUrl = ResolveServerUrl(engineInfo);
            var tokenUrl = ResolveTokenUrl(serverUrl, engineInfo);

            var username = engineInfo["username"]?.GetValue<string>()
                ?? engineInfo["http_username"]?.GetValue<string>() ?? "";
            var rawPassword = engineInfo["password"]?.GetValue<string>()
                ?? engineInfo["http_password"]?.GetValue<string>() ?? "";
            var password = !string.IsNullOrEmpty(rawPassword)
                ? await _cryptProcessing.Decrypt(rawPassword)
                : "";

            var configSyncMins = engineInfo["configSyncPeriodMinutes"]?.GetValue<int?>() ?? 10;

            var engineName = engineInfo["engineName"]?.GetValue<string>()
                ?? engineInfo["name"]?.GetValue<string>();
            var compressPbk = configObj["CompressPbk"]?.GetValue<string>();
            var compressPrk = configObj["CompressPrk"]?.GetValue<string>();
            return new EngineConfigPayload
            {
                EngineId = engineInfo["engineId"]?.GetValue<string>() ?? "",
                EngineName = !string.IsNullOrEmpty(engineName) ? engineName : null,
                Domain = engineInfo["domain"]?.GetValue<string>() ?? "",
                ServerUrl = serverUrl,
                TokenUrl = tokenUrl,
                Username = username,
                Password = password,
                SendSchedule = ResolveSendSchedule(engineInfo),
                ConfigSyncPeriodMinutes = configSyncMins > 0 ? configSyncMins : 10,
                MqttUrl = engineInfo["mqttUrl"]?.GetValue<string>(),
                CompressPbk = !string.IsNullOrEmpty(compressPbk) ? compressPbk : null,
                CompressPrk = !string.IsNullOrEmpty(compressPrk) ? compressPrk : null
            };
        }

        /// <summary>Config'i siler; engineConfigPayload, engineConfigSync, engineAssets, lastSyncAt temizlenir.</summary>
        public async Task ClearConfigAsync()
        {
            _memoryCache.Remove(ConfigCacheKey);
            _memoryCache.Remove(EngineConfigPayloadCacheKey);
            _memoryCache.Remove("engineConfigSync");
            _memoryCache.Remove("engineAssets");
            _memoryCache.Remove("lastSyncAt");
            _memoryCache.Remove("engineConfigSignature");

            if (File.Exists(_configFilePath))
            {
                try
                {
                    File.Delete(_configFilePath);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "{ConfigFile} silinemedi", _configFilePath);
                }
            }

            Log.Information("Config silindi. Engine sıfır kurulum modunda.");
            await Task.CompletedTask;
        }

        public async Task InitConfig()
        {
            if (!File.Exists(_configFilePath)) return;

            var fileContent = await File.ReadAllTextAsync(_configFilePath);
            if (string.IsNullOrWhiteSpace(fileContent)) return;

            try
            {
                var (success, _) = await ApplyConfig(fileContent);
                if (!success)
                    Log.Warning("Başlangıçta {ConfigFile} yüklendi ama uygulanamadı.", _configFilePath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Başlangıçta {ConfigFile} yüklenemedi.", _configFilePath);
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> ApplyConfig(string configText)
        {
            if (string.IsNullOrWhiteSpace(configText))
                return (false, "Config metni boş.");

            try
            {
                var configObj = await GetConfigJsonAsync(configText);
                _memoryCache.Set(ConfigCacheKey, configObj);

                var payload = await BuildEngineConfigPayloadAsync(configObj);
                if (payload != null)
                    _memoryCache.Set(EngineConfigPayloadCacheKey, payload);

                EnsureConfigDirectory();
                await File.WriteAllTextAsync(_configFilePath, configText);
                Log.Information("Config uygulandı. EngineId={EngineId}", payload?.EngineId ?? "(yok)");
                return (true, null);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Config uygulanamadı: {Message}", ex.Message);
                Log.Warning("Config ApplyConfig catch: Exception tipi={Type} Message uzunluk={Len} Tam Message=[{Message}]",
                    ex.GetType().FullName, ex.Message?.Length ?? 0, ex.Message ?? "");
                if (ex.InnerException != null)
                    Log.Warning("Config ApplyConfig catch: InnerException tipi={Type} Message=[{InnerMessage}]",
                        ex.InnerException.GetType().FullName, ex.InnerException.Message ?? "");
                Log.Warning("Config ApplyConfig catch: StackTrace: {StackTrace}", ex.StackTrace ?? "");
                return (false, ex.Message);
            }
        }
    }
}
