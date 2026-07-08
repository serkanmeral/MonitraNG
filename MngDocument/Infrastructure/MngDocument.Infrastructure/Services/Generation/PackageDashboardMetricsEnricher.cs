using System.Globalization;
using System.Text.Json.Nodes;
using MngDocument.Application.Contracts.Generation;
using MngDocument.Application.Interfaces;

namespace MngDocument.Infrastructure.Services.Generation;

/// <summary>İş paketi kontrol paneli / müşteri sunumu için UI dashboard ile uyumlu KPI scalar + tablo değerleri.</summary>
public sealed class PackageDashboardMetricsEnricher
{
    private static readonly string[] SupportedProfiles =
    [
        "odak.package.dashboard.fromPackage",
        "odak.package.brief.fromPackage"
    ];

    private const string LinesDataset = "odak_siparis_kalemleri";
    private const string ShipmentsDataset = "odak_sevkiyatlar";
    private const string ShipmentLinesDataset = "odak_sevkiyat_kalemleri";
    private const string NcrDataset = "odak_ncr";
    private const string CapaDataset = "odak_capa";

    private readonly IMngDataGatewayClient _dg;

    public PackageDashboardMetricsEnricher(IMngDataGatewayClient dg) => _dg = dg;

    public bool AppliesTo(string? profileCode) =>
        SupportedProfiles.Any(p =>
            string.Equals(profileCode?.Trim(), p, StringComparison.OrdinalIgnoreCase));

    public async Task EnrichAsync(
        ParameterResolutionResult result,
        string packageId,
        JsonObject contextTree,
        string? token,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            return;

        var match = new Dictionary<string, object?> { ["parentPackageId"] = packageId.Trim() };

        var linesTask = _dg.QueryPageAsync(LinesDataset, match, "sort=lineNo&limit=500", token, ct);
        var shipmentsTask = _dg.QueryPageAsync(ShipmentsDataset, match, "sort=-shipmentDate&limit=200", token, ct);
        var shipmentLinesTask = _dg.QueryPageAsync(ShipmentLinesDataset, match, "sort=lineNo&limit=500", token, ct);
        var ncrsTask = _dg.QueryPageAsync(NcrDataset, match, "limit=200", token, ct);
        var capasTask = _dg.QueryPageAsync(CapaDataset, match, "limit=200", token, ct);

        await Task.WhenAll(linesTask, shipmentsTask, shipmentLinesTask, ncrsTask, capasTask);

        var lines = linesTask.Result.Items;
        var shipments = shipmentsTask.Result.Items;
        var shipmentLines = shipmentLinesTask.Result.Items;
        var ncrs = ncrsTask.Result.Items;
        var capas = capasTask.Result.Items;

        var partCount = ReadDouble(contextTree, "partCount");
        var stockCount = ReadDouble(contextTree, "stockCount");
        var shippedFromPackage = ReadDouble(contextTree, "shippedCount");
        var lineCountFromPackage = ReadDouble(contextTree, "lineCount");

        var lineAggregate = AggregateLineQuantities(lines);
        var shippedFromLines = SumField(shipmentLines, "shippedQuantity");
        var shippedCount = shippedFromPackage > 0 ? shippedFromPackage
            : lineAggregate.TotalShipped > 0 ? lineAggregate.TotalShipped
            : shippedFromLines;

        var lineCount = lineCountFromPackage > 0 ? (int)lineCountFromPackage : lines.Count;
        var remainingQuantity = lineAggregate.TotalRemaining;
        var fulfillmentPct = FulfillmentPercent(partCount, shippedCount);

        var shipmentTotal = shipments.Count;
        var shipmentCompleted = shipments.Count(s => NormalizeShipmentStatus(ReadString(s, "status")) == "Tamamlandi");
        var openNcrCount = ncrs.Count(n => NormalizeNcrStatus(ReadString(n, "ncStatus")) != "Kapalı");
        var openCapaCount = capas.Count(c => !string.Equals(ReadString(c, "capaStatus"), "Kapali", StringComparison.OrdinalIgnoreCase));

        var status = ReadString(contextTree, "status") ?? "open";
        var deliveryDateRaw = ReadString(contextTree, "deliveryDate");
        var daysLeft = DaysUntilDelivery(deliveryDateRaw);
        var urgencyLabel = BuildUrgencyLabel(status, daysLeft);

        SetScalar(result, "statusLabel", PackageStatusLabel(status));
        SetScalar(result, "lineCount", lineCount.ToString(CultureInfo.InvariantCulture));
        SetScalar(result, "partCount", partCount > 0 ? partCount.ToString("N0", CultureInfo.InvariantCulture) : "0");
        SetScalar(result, "stockCount", stockCount > 0 ? stockCount.ToString("N0", CultureInfo.InvariantCulture) : "0");
        SetScalar(result, "shippedCount", shippedCount > 0 ? shippedCount.ToString("N0", CultureInfo.InvariantCulture) : "0");
        SetScalar(result, "remainingQuantity", remainingQuantity.ToString("N0", CultureInfo.InvariantCulture));
        SetScalar(result, "fulfillmentPct", fulfillmentPct.HasValue
            ? fulfillmentPct.Value.ToString(CultureInfo.InvariantCulture)
            : "—");
        SetScalar(result, "fulfillmentPctLabel", fulfillmentPct.HasValue
            ? $"{fulfillmentPct.Value}%"
            : "—");
        SetScalar(result, "shipmentSummary", $"{shipmentCompleted}/{shipmentTotal}");
        SetScalar(result, "shipmentCompleted", shipmentCompleted.ToString(CultureInfo.InvariantCulture));
        SetScalar(result, "shipmentTotal", shipmentTotal.ToString(CultureInfo.InvariantCulture));
        SetScalar(result, "openNcrCount", openNcrCount.ToString(CultureInfo.InvariantCulture));
        SetScalar(result, "openCapaCount", openCapaCount.ToString(CultureInfo.InvariantCulture));
        SetScalar(result, "deliveryUrgencyLabel", urgencyLabel);
        SetScalar(result, "beginDate", FormatDate(ReadString(contextTree, "beginDate")));
        SetScalar(result, "status", PackageStatusLabel(status));

        var donutShipped = Math.Max(0, shippedCount);
        var donutRemaining = Math.Max(0, remainingQuantity);
        var donutStock = Math.Max(0, stockCount);
        result.Tables["donutSlices"] = new List<IReadOnlyDictionary<string, object?>>
        {
            Row("category", "Sevk", "amount", donutShipped),
            Row("category", "Kalan", "amount", donutRemaining),
            Row("category", "Stok", "amount", donutStock)
        };

        var projectedLines = ProjectLineRows(lines);
        result.Tables["packageLines"] = projectedLines;
        result.Tables["chartLines"] = projectedLines
            .Select(r => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["lineNo"] = r.TryGetValue("lineNo", out var ln) ? ln : null,
                ["quantity"] = r.TryGetValue("quantity", out var q) ? q : null,
                ["shippedQuantity"] = r.TryGetValue("shippedQuantity", out var sq) ? sq : null
            })
            .ToList();

        SetScalar(result, "linesSummary", BuildLinesSummary(projectedLines, 8));
    }

    private static string BuildLinesSummary(IReadOnlyList<IReadOnlyDictionary<string, object?>> lines, int maxLines)
    {
        if (lines.Count == 0)
            return "Bu pakette kalem bulunamadı.";

        var sb = new System.Text.StringBuilder();
        foreach (var line in lines.Take(maxLines))
        {
            var no = line.TryGetValue("lineNo", out var ln) ? ln?.ToString()?.Trim() : null;
            var desc = line.TryGetValue("description", out var d) ? d?.ToString()?.Trim() : null;
            var qty = line.TryGetValue("quantity", out var q) ? q : null;
            var shipped = line.TryGetValue("shippedQuantity", out var s) ? s : null;
            sb.Append("• ");
            sb.Append(string.IsNullOrWhiteSpace(no) ? "—" : no);
            sb.Append(" — ");
            sb.Append(string.IsNullOrWhiteSpace(desc) ? "—" : desc);
            sb.Append(" (");
            sb.Append(shipped?.ToString() ?? "0");
            sb.Append('/');
            sb.Append(qty?.ToString() ?? "0");
            sb.Append(')');
            sb.AppendLine();
        }

        if (lines.Count > maxLines)
            sb.AppendLine($"... +{lines.Count - maxLines} kalem daha");

        return sb.ToString().TrimEnd();
    }

    private static List<IReadOnlyDictionary<string, object?>> ProjectLineRows(
        IReadOnlyList<Dictionary<string, object?>> lines)
    {
        var result = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var line in lines)
        {
            var qty = ReadDouble(line, "quantity");
            var shipped = ReadDouble(line, "shippedQuantity");
            var isFai = ReadBool(line, "isFai");
            var isFaiComplete = ReadBool(line, "isFaiComplete");
            var cocDocNo = ReadString(line, "cocDocNo");

            result.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["lineNo"] = ReadString(line, "lineNo") ?? ReadDouble(line, "lineNo").ToString(CultureInfo.InvariantCulture),
                ["customerPoItemNo"] = ReadString(line, "customerPoItemNo") ?? string.Empty,
                ["description"] = ReadString(line, "description") ?? string.Empty,
                ["quantity"] = qty,
                ["shippedQuantity"] = shipped,
                ["remainingQuantity"] = Math.Max(0, qty - shipped),
                ["deliveryDate"] = FormatDate(ReadString(line, "deliveryDate")),
                ["faiStatus"] = !isFai ? "—" : isFaiComplete ? "Tamam" : "Bekliyor",
                ["cocStatus"] = string.IsNullOrWhiteSpace(cocDocNo) ? "—" : "Var",
                ["cocDocNo"] = cocDocNo ?? string.Empty
            });
        }

        return result;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!TryRead(row, key, out var raw) || raw is null)
            return false;

        return raw switch
        {
            bool b => b,
            _ => bool.TryParse(raw.ToString(), out var parsed) && parsed
        };
    }

    private static void SetScalar(ParameterResolutionResult result, string key, string value) =>
        result.Scalars[key] = value;

    private static IReadOnlyDictionary<string, object?> Row(string k1, object? v1, string k2, object? v2) =>
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [k1] = v1,
            [k2] = v2
        };

    private sealed record LineAggregate(double TotalShipped, double TotalRemaining);

    private static LineAggregate AggregateLineQuantities(IReadOnlyList<Dictionary<string, object?>> lines)
    {
        double totalShipped = 0;
        double totalRemaining = 0;
        foreach (var line in lines)
        {
            var qty = ReadDouble(line, "quantity");
            var shipped = ReadDouble(line, "shippedQuantity");
            totalShipped += shipped;
            totalRemaining += Math.Max(0, qty - shipped);
        }

        return new LineAggregate(totalShipped, totalRemaining);
    }

    private static double SumField(IReadOnlyList<Dictionary<string, object?>> rows, string field)
    {
        double sum = 0;
        foreach (var row in rows)
            sum += ReadDouble(row, field);
        return sum;
    }

    private static int? FulfillmentPercent(double partCount, double shippedCount)
    {
        if (partCount <= 0)
            return null;
        return Math.Min(100, (int)Math.Round(shippedCount / partCount * 100));
    }

    private static int? DaysUntilDelivery(string? deliveryDateRaw)
    {
        if (string.IsNullOrWhiteSpace(deliveryDateRaw))
            return null;
        if (!DateTime.TryParse(deliveryDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt)
            && !DateTime.TryParse(deliveryDateRaw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal, out dt))
            return null;

        var today = DateTime.UtcNow.Date;
        return (dt.Date - today).Days;
    }

    private static string BuildUrgencyLabel(string status, int? daysLeft)
    {
        if (string.Equals(status, "closed", StringComparison.OrdinalIgnoreCase))
            return "Kapalı paket";

        if (daysLeft is null)
            return "Termin tanımsız";

        if (daysLeft < 0)
            return $"{Math.Abs(daysLeft.Value)} gün gecikme";

        if (daysLeft == 0)
            return "Bugün termin";

        if (daysLeft <= 7)
            return $"{daysLeft} gün kaldı";

        return $"{daysLeft} gün kaldı";
    }

    private static string PackageStatusLabel(string status) =>
        string.Equals(status, "closed", StringComparison.OrdinalIgnoreCase) ? "Kapalı" : "Açık";

    private static string NormalizeShipmentStatus(string? value)
    {
        var key = value?.Trim() ?? string.Empty;
        if (key.Equals("Tamamlandi", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Tamamlandı", StringComparison.OrdinalIgnoreCase))
            return "Tamamlandi";
        return key;
    }

    private static string NormalizeNcrStatus(string? value)
    {
        var key = value?.Trim() ?? string.Empty;
        if (key.StartsWith("Kapal", StringComparison.OrdinalIgnoreCase))
            return "Kapalı";
        return key;
    }

    private static string FormatDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "—";
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt)
            || DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal, out dt))
            return dt.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        return raw;
    }

    private static double ReadDouble(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!TryRead(row, key, out var raw) || raw is null)
            return 0;

        return raw switch
        {
            double d => d,
            float f => f,
            decimal m => (double)m,
            int or long or short or byte => Convert.ToDouble(raw, CultureInfo.InvariantCulture),
            _ => double.TryParse(raw.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0
        };
    }

    private static double ReadDouble(JsonObject tree, string path)
    {
        var raw = DocumentContextPathResolver.GetString(tree, path);
        return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> row, string key) =>
        TryRead(row, key, out var raw) ? raw?.ToString()?.Trim() : null;

    private static string? ReadString(JsonObject tree, string path) =>
        DocumentContextPathResolver.GetString(tree, path);

    private static bool TryRead(IReadOnlyDictionary<string, object?> row, string key, out object? value)
    {
        if (row.TryGetValue(key, out value))
            return true;

        foreach (var kv in row)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}
