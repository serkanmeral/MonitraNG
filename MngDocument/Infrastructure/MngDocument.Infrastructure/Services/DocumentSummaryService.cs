using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

/// <summary>Özeti başlatır. Uzun iş istek zaman aşımına takılmasın diye arka planda biter.</summary>
public sealed class DocumentSummaryService : IDocumentSummaryService
{
    private static readonly TimeSpan StaleRunning = TimeSpan.FromMinutes(10);

    private readonly IResourceService _resources;
    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly IServiceScopeFactory _scopes;
    private readonly SummaryJobBoard _board;
    private readonly ILogger<DocumentSummaryService> _logger;

    public DocumentSummaryService(
        IResourceService resources,
        IMngDataGatewayClient dg,
        IRequestContext ctx,
        IServiceScopeFactory scopes,
        SummaryJobBoard board,
        ILogger<DocumentSummaryService> logger)
    {
        _resources = resources;
        _dg = dg;
        _ctx = ctx;
        _scopes = scopes;
        _board = board;
        _logger = logger;
    }

    public async Task<ResourceSummaryResultDto> GetAsync(string id, CancellationToken ct = default)
    {
        var resource = await _resources.GetByIdAsync(id, ct);
        if (_board.TryGet(id, out var live))
            return FromSnapshot(live);

        if (string.Equals(resource.SummaryStatus, "running", StringComparison.OrdinalIgnoreCase)
            && (resource.SummaryUpdatedAt is null || DateTime.UtcNow - resource.SummaryUpdatedAt.Value > StaleRunning))
        {
            return new ResourceSummaryResultDto
            {
                Status = "failed",
                Error = "Özet işi yanıt vermedi.",
                Step = "Özet işi yanıt vermedi.",
                Chunked = resource.SummaryChunked,
                Completed = resource.SummaryCompleted,
                Total = resource.SummaryTotal,
                UpdatedAt = resource.SummaryUpdatedAt,
                Log = resource.SummaryLog
            };
        }

        return FromResource(resource);
    }

    public async Task<ResourceSummaryResultDto> StartAsync(string id, CancellationToken ct = default)
    {
        var resource = await _resources.GetByIdAsync(id, ct);
        if (!string.Equals(resource.IndexStatus, "done", StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "SUMMARY_NEEDS_INDEX",
                "The document must be indexed before it can be summarized.",
                "Özet için belgenin indeksi tamamlanmış olmalı.");
        }

        if (_board.TryGet(id, out var live)
            && (live.Status == "running" || (live.Status == "done" && !string.IsNullOrWhiteSpace(live.Text))))
        {
            return FromSnapshot(live);
        }

        if (string.Equals(resource.SummaryStatus, "done", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(resource.SummaryText))
        {
            return FromResource(resource);
        }

        var domain = string.IsNullOrWhiteSpace(_ctx.DomainName) ? "default" : _ctx.DomainName.Trim();
        var gate = DocumentJobGate.For(domain);
        if (!await gate.WaitAsync(0, ct))
        {
            throw DocumentException.Conflict(
                "DOCUMENT_JOB_BUSY",
                "Another document job is running.",
                "Başka bir belgenin indeks veya özet işi sürüyor. O bitince tekrar deneyin.");
        }

        var token = _ctx.BearerToken ?? string.Empty;
        var version = resource.CurrentVersionNumber;
        var now = DateTime.UtcNow;
        var snapshot = new SummaryJobSnapshot
        {
            Status = "running",
            Step = "Metin okunuyor",
            UpdatedAt = now,
            Log = new[] { "Metin okunuyor" }
        };
        _board.Set(id, snapshot);
        try
        {
            await _dg.UpdateAsync<DmResource>(DmDatasets.Resources, id, new Dictionary<string, object?>
            {
                ["summaryStatus"] = "running",
                ["summaryStep"] = "Metin okunuyor",
                ["summaryError"] = null,
                ["summaryCompleted"] = 0,
                ["summaryTotal"] = 0,
                ["summaryUpdatedAt"] = now,
                ["summaryLogText"] = "Metin okunuyor"
            }, token, ct);
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var runner = scope.ServiceProvider.GetRequiredService<DocumentSummaryRunner>();
                    await runner.ExecuteAsync(id, token, domain, version, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Summary job crashed for {ResourceId}", id);
                    _board.Set(id, new SummaryJobSnapshot
                    {
                        Status = "failed",
                        Error = "Özet yazılamadı.",
                        Step = "Özet yazılamadı.",
                        UpdatedAt = DateTime.UtcNow,
                        Log = new[] { "Metin okunuyor", "Özet yazılamadı." }
                    });
                }
                finally
                {
                    gate.Release();
                }
            });
        }
        catch
        {
            _board.Remove(id);
            gate.Release();
            throw;
        }

        return FromSnapshot(snapshot);
    }

    private static ResourceSummaryResultDto FromSnapshot(SummaryJobSnapshot snapshot) =>
        new()
        {
            Status = snapshot.Status,
            Text = snapshot.Text,
            Error = snapshot.Error,
            Step = snapshot.Step,
            Chunked = snapshot.Chunked,
            SummarizedAt = snapshot.SummarizedAt,
            Completed = snapshot.Completed,
            Total = snapshot.Total,
            UpdatedAt = snapshot.UpdatedAt,
            Log = snapshot.Log
        };

    private static ResourceSummaryResultDto FromResource(ResourceDto resource) =>
        new()
        {
            Status = resource.SummaryStatus,
            Text = resource.SummaryText,
            Error = resource.SummaryError,
            Step = resource.SummaryStep,
            Chunked = resource.SummaryChunked,
            SummarizedAt = resource.SummarizedAt,
            Completed = resource.SummaryCompleted,
            Total = resource.SummaryTotal,
            UpdatedAt = resource.SummaryUpdatedAt,
            Log = resource.SummaryLog
        };
}
