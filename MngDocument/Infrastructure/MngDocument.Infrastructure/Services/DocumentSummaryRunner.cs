using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

/// <summary>İndeks metnini parçalayıp özetler. Model iş bitince bellekten iner.</summary>
public sealed class DocumentSummaryRunner
{
    private const string PersistFailed = "Özet üretildi, kayda yazılamadı.";

    private readonly IMngDataGatewayClient _dg;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SummaryJobBoard _board;
    private readonly MngDocumentSettings _settings;
    private readonly ILogger<DocumentSummaryRunner> _logger;

    public DocumentSummaryRunner(
        IMngDataGatewayClient dg,
        IHttpClientFactory httpClientFactory,
        SummaryJobBoard board,
        IOptions<MngDocumentSettings> settings,
        ILogger<DocumentSummaryRunner> logger)
    {
        _dg = dg;
        _httpClientFactory = httpClientFactory;
        _board = board;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(string id, string token, string domain, int version, CancellationToken ct)
    {
        var state = new JobState();
        try
        {
            await PublishAsync(id, token, state, "running", "Metin okunuyor", null, false, 0, 0, "Metin okunuyor", ct);

            var text = await ReadIndexedTextAsync(domain, id, ct);
            if (string.IsNullOrWhiteSpace(text))
            {
                await FailAsync(id, token, state, "İndeks metni bulunamadı.", ct);
                return;
            }

            var chunkSize = Math.Clamp(_settings.Summary.ChunkChars, 1500, 12_000);
            var chunks = Chunk(text, chunkSize);
            var chunked = chunks.Count > 1;
            var total = chunks.Count + (chunked ? 1 : 0);
            var pieces = new List<string>(chunks.Count);
            for (var i = 0; i < chunks.Count; i++)
            {
                var step = chunked
                    ? $"Parça {i + 1}/{chunks.Count} özetleniyor"
                    : "Özet yazılıyor";
                await PublishAsync(id, token, state, "running", step, null, chunked, i, total, step, ct);

                var started = Stopwatch.StartNew();
                var piece = await GenerateAsync(
                    BuildChunkPrompt(chunks[i]),
                    _settings.Summary.ChunkPredict,
                    unload: !chunked && i == chunks.Count - 1,
                    ct);
                pieces.Add(piece);
                var doneLine = chunked
                    ? $"Parça {i + 1}/{chunks.Count} bitti · {Format(started.Elapsed)}"
                    : $"Özet yazıldı · {Format(started.Elapsed)}";
                await PublishAsync(id, token, state, "running", doneLine, null, chunked, i + 1, total, doneLine, ct);
            }

            var summary = pieces[0];
            if (chunked)
            {
                await PublishAsync(id, token, state, "running", "Özet birleştiriliyor", null, true, chunks.Count, total, "Özet birleştiriliyor", ct);
                var started = Stopwatch.StartNew();
                summary = await GenerateAsync(BuildMergePrompt(pieces), _settings.Summary.MergePredict, unload: true, ct);
                var merged = $"Özet birleştirildi · {Format(started.Elapsed)}";
                await PublishAsync(id, token, state, "running", merged, null, true, chunks.Count, total, merged, ct);
            }

            summary = summary.Trim();
            if (summary.Length > 8000)
                summary = summary[..8000];

            var saved = await PublishAsync(
                id, token, state, "done", "Özet hazır", summary, chunked, total, total, "Özet hazır", ct, version);
            if (saved)
                _board.Remove(id);
            else
                await PublishAsync(id, token, state, "done", PersistFailed, summary, chunked, total, total, PersistFailed, ct, version, persist: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Document summary failed for {ResourceId}", id);
            await FailAsync(id, token, state, "Özet yazılamadı.", ct);
        }
    }

    private async Task FailAsync(string id, string token, JobState state, string error, CancellationToken ct)
    {
        await PublishAsync(id, token, state, "failed", error, state.Text, state.Chunked, state.Completed, state.Total, error, ct, persist: true);
    }

    private async Task<bool> PublishAsync(
        string id,
        string token,
        JobState state,
        string status,
        string step,
        string? text,
        bool chunked,
        int completed,
        int total,
        string? logLine,
        CancellationToken ct,
        int? version = null,
        bool persist = true)
    {
        state.Status = status;
        state.Step = step;
        state.Text = text ?? state.Text;
        state.Chunked = chunked;
        state.Completed = completed;
        state.Total = total;
        state.Error = status == "failed" ? step : status == "done" && step == PersistFailed ? PersistFailed : null;
        state.UpdatedAt = DateTime.UtcNow;
        if (status == "done" && state.Error is null)
            state.SummarizedAt = state.UpdatedAt;
        if (!string.IsNullOrWhiteSpace(logLine))
            state.Append(logLine);

        var snapshot = state.ToSnapshot();
        _board.Set(id, snapshot);

        if (!persist)
            return false;

        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["summaryStatus"] = status,
                ["summaryStep"] = step,
                ["summaryError"] = state.Error,
                ["summaryChunked"] = chunked,
                ["summaryCompleted"] = completed,
                ["summaryTotal"] = total,
                ["summaryUpdatedAt"] = state.UpdatedAt,
                ["summaryLogText"] = state.LogText()
            };
            if (text is not null)
                payload["summaryText"] = text;
            if (status == "done" && state.Error is null)
            {
                payload["summarizedAt"] = state.SummarizedAt;
                if (version is int summarizedVersion)
                    payload["summarizedVersionNumber"] = summarizedVersion;
            }

            await _dg.UpdateAsync<DmResource>(DmDatasets.Resources, id, payload, token, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not store summary progress for {ResourceId}", id);
            return false;
        }
    }

    private async Task<string> ReadIndexedTextAsync(string domain, string id, CancellationToken ct)
    {
        var baseUrl = (_settings.OpenSearch.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("OpenSearch base URL is empty.");

        var client = _httpClientFactory.CreateClient("OpenSearch");
        using var response = await client.GetAsync($"{baseUrl}/{DiSearchIndex.Name(domain)}/_source/{id}", ct);
        if (!response.IsSuccessStatusCode)
            return string.Empty;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.TryGetProperty("text", out var text) ? text.GetString() ?? string.Empty : string.Empty;
    }

    private async Task<string> GenerateAsync(string prompt, int predict, bool unload, CancellationToken ct)
    {
        var baseUrl = (_settings.Summary.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("Summary base URL is empty.");

        var keepAlive = unload ? 0 : Math.Clamp(_settings.Summary.KeepAliveSeconds, 60, 3600);
        var numPredict = Math.Clamp(predict <= 0 ? 150 : predict, 64, 800);
        var client = _httpClientFactory.CreateClient("Ollama");
        using var response = await client.PostAsJsonAsync($"{baseUrl}/api/generate", new
        {
            model = string.IsNullOrWhiteSpace(_settings.Summary.Model) ? "qwen2.5:3b" : _settings.Summary.Model,
            prompt,
            stream = false,
            keep_alive = keepAlive,
            options = new
            {
                temperature = 0.2,
                num_ctx = _settings.Summary.NumCtx <= 0 ? 4096 : _settings.Summary.NumCtx,
                num_predict = numPredict
            }
        }, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama {(int)response.StatusCode}: {Trim(body)}");

        using var doc = JsonDocument.Parse(body);
        var text = doc.RootElement.TryGetProperty("response", out var responseText)
            ? responseText.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Ollama returned an empty summary.");
        return text.Trim();
    }

    private static string BuildChunkPrompt(string text) =>
        "Aşağıdaki belge parçasını Türkçe özetle. Metinde olmayan bilgi ekleme. En fazla 5 cümle yaz.\n\nMetin:\n" + text;

    private static string BuildMergePrompt(IReadOnlyList<string> pieces) =>
        "Aşağıdaki parça özetlerini tek bir Türkçe özette birleştir. Yeni bilgi ekleme. En fazla 12 cümle yaz.\n\n" +
        string.Join("\n\n", pieces);

    private static string Format(TimeSpan elapsed)
    {
        var seconds = Math.Max(1, (int)elapsed.TotalSeconds);
        if (seconds < 60)
            return $"{seconds} sn";
        return $"{seconds / 60} dk {seconds % 60} sn";
    }

    private static List<string> Chunk(string text, int size)
    {
        if (text.Length <= size)
            return new List<string> { text };

        var parts = new List<string>();
        var current = new StringBuilder();
        foreach (var para in text.Split('\n'))
        {
            if (para.Length > size)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }

                for (var i = 0; i < para.Length; i += size)
                    parts.Add(para.Substring(i, Math.Min(size, para.Length - i)));
                continue;
            }

            if (current.Length > 0 && current.Length + para.Length + 1 > size)
            {
                parts.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
                current.Append('\n');
            current.Append(para);
        }

        if (current.Length > 0)
            parts.Add(current.ToString());
        return parts;
    }

    private static string Trim(string value) =>
        value.Length <= 300 ? value : value[..300];

    private sealed class JobState
    {
        private readonly List<string> _log = new();

        public string Status { get; set; } = "running";
        public string? Text { get; set; }
        public string? Error { get; set; }
        public string? Step { get; set; }
        public bool Chunked { get; set; }
        public DateTime? SummarizedAt { get; set; }
        public int Completed { get; set; }
        public int Total { get; set; }
        public DateTime UpdatedAt { get; set; }

        public void Append(string line)
        {
            _log.Add(line);
            if (_log.Count > 24)
                _log.RemoveAt(0);
        }

        public string LogText() => string.Join('\n', _log);

        public SummaryJobSnapshot ToSnapshot() => new()
        {
            Status = Status,
            Text = Text,
            Error = Error,
            Step = Step,
            Chunked = Chunked,
            SummarizedAt = SummarizedAt,
            Completed = Completed,
            Total = Total,
            UpdatedAt = UpdatedAt,
            Log = _log.ToArray()
        };
    }
}
