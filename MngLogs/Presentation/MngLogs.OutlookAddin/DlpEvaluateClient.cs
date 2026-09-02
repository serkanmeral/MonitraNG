using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MngLogs.OutlookAddin;

public static class DlpEvaluateClient
{
    public const string HeaderName = "X-MngLogs-DlpKey";
    private static readonly HttpClient Http = CreateClient();
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static DlpEvaluateDto? Evaluate(
        string baseUrl,
        string apiKey,
        object body,
        out bool transportFailed,
        out string? error)
    {
        transportFailed = false;
        error = null;
        try
        {
            var url = baseUrl.TrimEnd('/') + "/dlp/evaluate";
            var json = JsonSerializer.Serialize(body, Json);
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.TryAddWithoutValidation(HeaderName, apiKey);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = Http.SendAsync(req).GetAwaiter().GetResult();
            var payload = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var parsed = JsonSerializer.Deserialize<DlpEvaluateDto>(payload, Json);
            if (parsed is null)
            {
                transportFailed = true;
                error = "Empty DLP evaluate response.";
                return null;
            }

            return parsed;
        }
        catch (Exception ex)
        {
            transportFailed = true;
            error = ex.Message;
            return null;
        }
    }

    public static string ReadApiKey(string dataDirectory)
    {
        var path = Path.Combine(dataDirectory, "dlp-local.key");
        if (!File.Exists(path))
            throw new FileNotFoundException("dlp-local.key missing", path);
        var key = File.ReadAllText(path).Trim();
        if (key.Length < 16)
            throw new InvalidOperationException("dlp-local.key is empty.");
        return key;
    }

    public static (string BaseUrl, string DataDirectory) ReadAgentEndpoints()
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MngLogs",
            "Agent");
        var port = 5092;
        var systemPath = Path.Combine(dataDir, "system.json");
        if (File.Exists(systemPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(systemPath));
                if (doc.RootElement.TryGetProperty("localUiPort", out var p) && p.TryGetInt32(out var parsed) && parsed > 0)
                    port = parsed;
                if (doc.RootElement.TryGetProperty("dataDirectory", out var d))
                {
                    var raw = d.GetString();
                    if (!string.IsNullOrWhiteSpace(raw))
                        dataDir = raw;
                }
            }
            catch
            {
                // default port / dataDir
            }
        }

        return ("http://127.0.0.1:" + port, dataDir);
    }

    private static HttpClient CreateClient()
    {
        return new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    }
}
