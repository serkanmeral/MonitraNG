using MngDocument.Application.Interfaces;

namespace MngDocument.Infrastructure.Services.Generation.DataSources;

/// <summary>
/// Resolves package shipment lines when direct <c>parentPackageId</c> match returns no rows
/// (legacy rows may only have <c>parentShipmentId</c> / <c>parentLineId</c>).
/// </summary>
internal static class PackageShipmentLinesQueryFallback
{
    private const string ShipmentLinesDataset = "odak_sevkiyat_kalemleri";
    private const string ShipmentsDataset = "odak_sevkiyatlar";
    private const string PackageLinesDataset = "odak_siparis_kalemleri";

    public static bool IsDirectPackageQuery(string dataset, IReadOnlyDictionary<string, object?> match) =>
        string.Equals(dataset, ShipmentLinesDataset, StringComparison.OrdinalIgnoreCase)
        && match.TryGetValue("parentPackageId", out var packageId)
        && !string.IsNullOrWhiteSpace(packageId?.ToString());

    public static async Task<IReadOnlyList<Dictionary<string, object?>>> TryLoadAsync(
        IMngDataGatewayClient dg,
        IReadOnlyDictionary<string, object?> match,
        string? query,
        string? token,
        CancellationToken ct)
    {
        var packageId = match["parentPackageId"]?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(packageId))
            return Array.Empty<Dictionary<string, object?>>();

        var viaShipments = await QueryByShipmentIdsAsync(dg, packageId, query, token, ct);
        if (viaShipments.Count > 0)
            return viaShipments;

        return await QueryByPackageLineIdsAsync(dg, packageId, query, token, ct);
    }

    private static async Task<IReadOnlyList<Dictionary<string, object?>>> QueryByShipmentIdsAsync(
        IMngDataGatewayClient dg,
        string packageId,
        string? query,
        string? token,
        CancellationToken ct)
    {
        var shipmentsPage = await dg.QueryPageAsync(
            ShipmentsDataset,
            new Dictionary<string, object?> { ["parentPackageId"] = packageId },
            "limit=500",
            token,
            ct);

        var shipmentIds = shipmentsPage.Items
            .Select(ExtractDataId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (shipmentIds.Count == 0)
            return Array.Empty<Dictionary<string, object?>>();

        return await QueryLinesInAsync(dg, "parentShipmentId", shipmentIds, query, token, ct);
    }

    private static async Task<IReadOnlyList<Dictionary<string, object?>>> QueryByPackageLineIdsAsync(
        IMngDataGatewayClient dg,
        string packageId,
        string? query,
        string? token,
        CancellationToken ct)
    {
        var linesPage = await dg.QueryPageAsync(
            PackageLinesDataset,
            new Dictionary<string, object?> { ["parentPackageId"] = packageId },
            "limit=500",
            token,
            ct);

        var lineIds = linesPage.Items
            .Select(ExtractDataId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (lineIds.Count == 0)
            return Array.Empty<Dictionary<string, object?>>();

        return await QueryLinesInAsync(dg, "parentLineId", lineIds, query, token, ct);
    }

    private static async Task<IReadOnlyList<Dictionary<string, object?>>> QueryLinesInAsync(
        IMngDataGatewayClient dg,
        string field,
        IReadOnlyList<string> ids,
        string? query,
        string? token,
        CancellationToken ct)
    {
        if (ids.Count == 1)
        {
            var single = await dg.QueryPageAsync(
                ShipmentLinesDataset,
                new Dictionary<string, object?> { [field] = ids[0] },
                query,
                token,
                ct);
            return single.Items;
        }

        var inMatch = new Dictionary<string, object?>
        {
            [field] = new Dictionary<string, object?> { ["$in"] = ids }
        };

        var page = await dg.QueryPageAsync(ShipmentLinesDataset, inMatch, query, token, ct);
        return page.Items;
    }

    private static string? ExtractDataId(Dictionary<string, object?> row)
    {
        if (row.TryGetValue("__dataId", out var raw) && raw is not null)
            return raw.ToString()?.Trim();

        return null;
    }
}
