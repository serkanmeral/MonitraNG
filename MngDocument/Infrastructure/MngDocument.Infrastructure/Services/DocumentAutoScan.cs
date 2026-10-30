using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

/// <summary>Yüklenen dosyayı tarar. Temizse indeksi kuyruğa alır. Model ve istek zaman aşımına bağlı değildir.</summary>
public sealed class DocumentAutoScan : IDocumentAutoScan
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IRequestContext _ctx;
    private readonly ILogger<DocumentAutoScan> _logger;

    public DocumentAutoScan(
        IServiceScopeFactory scopes,
        IRequestContext ctx,
        ILogger<DocumentAutoScan> logger)
    {
        _scopes = scopes;
        _ctx = ctx;
        _logger = logger;
    }

    public void Schedule(string resourceId, string? bearerToken = null)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            return;

        var token = string.IsNullOrWhiteSpace(bearerToken) ? _ctx.BearerToken : bearerToken.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Auto scan skipped for {ResourceId}: bearer token is missing", resourceId);
            return;
        }

        var id = resourceId.Trim();
        _ = Task.Run(() => ScanAsync(id, token));
    }

    private async Task ScanAsync(string id, string token)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var settings = scope.ServiceProvider.GetRequiredService<IOptions<MngDocumentSettings>>().Value;
            if (!settings.Antivirus.Enabled)
                return;

            var dg = scope.ServiceProvider.GetRequiredService<IMngDataGatewayClient>();
            var scanner = scope.ServiceProvider.GetRequiredService<ClamAvScanner>();
            var autoIndex = scope.ServiceProvider.GetRequiredService<IDocumentAutoIndex>();
            var resource = await dg.GetByIdAsync<DmResource>(DmDatasets.Resources, id, token, CancellationToken.None)
                .ConfigureAwait(false);
            if (resource is null || string.IsNullOrWhiteSpace(resource.__dataId))
                return;
            if (!ResourceScanStatus.IsUploadFile(resource.type, resource.origin))
                return;

            var (path, _) = ReadFileField(resource.file);
            if (string.IsNullOrWhiteSpace(path))
            {
                await WriteAsync(dg, id, token, ResourceScanStatus.Failed, null, "Dosya okunamadı.", resource.currentVersionNumber)
                    .ConfigureAwait(false);
                return;
            }

            byte[] bytes;
            try
            {
                bytes = await dg.DownloadFileAsync(path, token, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto scan could not read {ResourceId}", id);
                await WriteAsync(dg, id, token, ResourceScanStatus.Failed, null, "Dosya okunamadı.", resource.currentVersionNumber)
                    .ConfigureAwait(false);
                return;
            }

            var result = await scanner.ScanAsync(bytes, settings.Antivirus, CancellationToken.None).ConfigureAwait(false);
            if (result.Kind == ClamScanKind.Clean)
            {
                await WriteAsync(dg, id, token, ResourceScanStatus.Clean, null, null, resource.currentVersionNumber)
                    .ConfigureAwait(false);
                autoIndex.Schedule(id, token);
                _logger.LogInformation("Auto scan {ResourceId}: clean", id);
                return;
            }

            if (result.Kind == ClamScanKind.Threat)
            {
                await WriteAsync(dg, id, token, ResourceScanStatus.Threat, result.Detail, null, resource.currentVersionNumber)
                    .ConfigureAwait(false);
                _logger.LogInformation("Auto scan {ResourceId}: threat {Signature}", id, result.Detail);
                return;
            }

            await WriteAsync(dg, id, token, ResourceScanStatus.Failed, null, result.Detail ?? "Tarama tamamlanamadı.", resource.currentVersionNumber)
                .ConfigureAwait(false);
            _logger.LogInformation("Auto scan {ResourceId}: failed {Detail}", id, result.Detail);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto scan failed for {ResourceId}", id);
        }
    }

    private static async Task WriteAsync(
        IMngDataGatewayClient dg,
        string id,
        string token,
        string status,
        string? threat,
        string? error,
        int? version)
    {
        var payload = new Dictionary<string, object?>
        {
            ["scanStatus"] = status,
            ["scanThreat"] = threat,
            ["scanError"] = error,
            ["scannedAt"] = DateTime.UtcNow
        };
        if (version is int scannedVersion)
            payload["scannedVersionNumber"] = scannedVersion;
        await dg.UpdateAsync<DmResource>(DmDatasets.Resources, id, payload, token, CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static (string? Path, string? Name) ReadFileField(JsonElement? file)
    {
        if (file is null || file.Value.ValueKind != JsonValueKind.Object)
            return (null, null);

        string? path = file.Value.TryGetProperty("path", out var pathValue) && pathValue.ValueKind == JsonValueKind.String
            ? pathValue.GetString()
            : null;
        string? name = file.Value.TryGetProperty("file_name", out var nameValue) && nameValue.ValueKind == JsonValueKind.String
            ? nameValue.GetString()
            : null;
        return (path, name);
    }
}
