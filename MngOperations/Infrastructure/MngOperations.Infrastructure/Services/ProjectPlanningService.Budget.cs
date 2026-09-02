using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    public async Task<ProjectBudgetDto> GetBudgetAsync(string projectId, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var lines = await LoadBudgetLineDtosAsync(projectId, token, ct);
        return BuildBudget(lines);
    }

    public async Task<BudgetLineDto> CreateBudgetLineAsync(
        string projectId,
        CreateBudgetLineRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var wbsId = await RequireWbsIdAsync(projectId, request.WbsId, token, ct);
        if (!PmBudgetCategory.TryNormalize(string.IsNullOrWhiteSpace(request.Category) ? PmBudgetCategory.Labor : request.Category, out var category))
            throw new OperationCoreException("BUDGET_CATEGORY", "Unknown budget category.", "Bilinmeyen bütçe kalemi türü.", 400);
        var name = RequireBudgetName(request.Name);
        var currency = RequireCurrency(request.Currency);
        var planned = NormalizeAmount(request.PlannedAmount);
        var actual = NormalizeAmount(request.ActualAmount);
        await AssertBudgetLineUniqueAsync(projectId, wbsId, category, name, excludeId: null, token, ct);

        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["wbsId"] = wbsId,
            ["category"] = category,
            ["name"] = name,
            ["plannedAmount"] = planned,
            ["actualAmount"] = actual,
            ["currency"] = currency,
            ["note"] = EmptyToNull(request.Note)
        };

        var created = await _dg.CreateAsync(PmDatasets.BudgetLines, payload, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Budget line create did not return an id.", "Bütçe kalemi oluşturulamadı.", 500);
        return await LoadBudgetLineDtoAsync(id, token, ct);
    }

    public async Task<BudgetLineDto> UpdateBudgetLineAsync(string id, UpdateBudgetLineRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadBudgetLineRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var wbsId = request.WbsId is not null
            ? await RequireWbsIdAsync(projectId, request.WbsId, token, ct)
            : existing.wbsId ?? string.Empty;
        if (!PmBudgetCategory.TryNormalize(request.Category ?? existing.category, out var category))
            throw new OperationCoreException("BUDGET_CATEGORY", "Unknown budget category.", "Bilinmeyen bütçe kalemi türü.", 400);
        var name = request.Name is not null ? RequireBudgetName(request.Name) : RequireBudgetName(existing.name);
        await AssertBudgetLineUniqueAsync(projectId, wbsId, category, name, id, token, ct);

        var payload = new Dictionary<string, object?>();
        if (request.WbsId is not null) payload["wbsId"] = wbsId;
        if (request.Category is not null) payload["category"] = category;
        if (request.Name is not null) payload["name"] = name;
        if (request.PlannedAmount.HasValue) payload["plannedAmount"] = NormalizeAmount(request.PlannedAmount.Value);
        if (request.ActualAmount.HasValue) payload["actualAmount"] = NormalizeAmount(request.ActualAmount.Value);
        if (request.Currency is not null) payload["currency"] = RequireCurrency(request.Currency);
        if (request.Note is not null) payload["note"] = EmptyToNull(request.Note);

        if (payload.Count > 0)
            await _dg.UpdateAsync(PmDatasets.BudgetLines, id, payload, token, ct);
        return await LoadBudgetLineDtoAsync(id, token, ct);
    }

    public async Task DeleteBudgetLineAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadBudgetLineRowOrThrowAsync(id, token, ct);
        await _dg.DeleteAsync(PmDatasets.BudgetLines, id, token, ct);
    }

    private async Task<List<BudgetLineDto>> LoadBudgetLineDtosAsync(string projectId, string token, CancellationToken ct)
    {
        var rows = await LoadBudgetLineRowsAsync(projectId, token, ct);
        return rows
            .Select(ToBudgetLineDto)
            .OrderBy(l => l.WbsId, StringComparer.Ordinal)
            .ThenBy(l => l.Category, StringComparer.Ordinal)
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<PmBudgetLineRow>> LoadBudgetLineRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.BudgetLines,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmBudgetLineRow>).ToList();
    }

    private async Task<PmBudgetLineRow> LoadBudgetLineRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmBudgetLineRow>(PmDatasets.BudgetLines, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Budget line not found.", "Bütçe kalemi bulunamadı.", 404);
        return row;
    }

    private async Task<BudgetLineDto> LoadBudgetLineDtoAsync(string id, string token, CancellationToken ct)
    {
        var row = await LoadBudgetLineRowOrThrowAsync(id, token, ct);
        return ToBudgetLineDto(row);
    }

    private async Task AssertBudgetLineUniqueAsync(
        string projectId,
        string wbsId,
        string category,
        string name,
        string? excludeId,
        string token,
        CancellationToken ct)
    {
        var rows = await LoadBudgetLineRowsAsync(projectId, token, ct);
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(excludeId) && string.Equals(row.__dataId, excludeId, StringComparison.Ordinal))
                continue;
            if (!string.Equals(row.wbsId, wbsId, StringComparison.Ordinal))
                continue;
            if (!PmBudgetCategory.TryNormalize(row.category, out var existingCat) || existingCat != category)
                continue;
            if (string.Equals((row.name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase))
                throw new OperationCoreException("BUDGET_EXISTS", "This budget line already exists on the WBS item.", "Bu bütçe kalemi bu WBS üzerinde zaten var.", 409);
        }
    }

    private static BudgetLineDto ToBudgetLineDto(PmBudgetLineRow row)
    {
        var planned = RoundMoney(row.plannedAmount ?? 0);
        var actual = RoundMoney(row.actualAmount ?? 0);
        var currency = PmBudgetMoney.NormalizeCurrency(row.currency);
        if (string.IsNullOrEmpty(currency))
            currency = PmBudgetMoney.DefaultCurrency;
        PmBudgetCategory.TryNormalize(row.category, out var category);
        if (string.IsNullOrEmpty(category))
            category = PmBudgetCategory.Other;
        return new BudgetLineDto
        {
            Id = row.__dataId ?? string.Empty,
            ProjectId = row.projectId ?? string.Empty,
            WbsId = row.wbsId ?? string.Empty,
            Category = category,
            Name = (row.name ?? string.Empty).Trim(),
            PlannedAmount = planned,
            ActualAmount = actual,
            Currency = currency,
            Note = EmptyToNull(row.note),
            Variance = RoundMoney(planned - actual),
            Over = IsOverBudget(planned, actual)
        };
    }

    internal static ProjectBudgetDto BuildBudget(IReadOnlyList<BudgetLineDto> lines)
    {
        var currency = lines.Select(l => l.Currency).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))
            ?? PmBudgetMoney.DefaultCurrency;
        var packages = lines
            .GroupBy(l => l.WbsId, StringComparer.Ordinal)
            .Select(g =>
            {
                var planned = RoundMoney(g.Sum(x => x.PlannedAmount));
                var actual = RoundMoney(g.Sum(x => x.ActualAmount));
                return new BudgetPackageDto
                {
                    WbsId = g.Key,
                    PlannedAmount = planned,
                    ActualAmount = actual,
                    Variance = RoundMoney(planned - actual),
                    Over = IsOverBudget(planned, actual),
                    Currency = g.Select(x => x.Currency).FirstOrDefault() ?? currency
                };
            })
            .OrderByDescending(p => p.Over)
            .ThenBy(p => p.WbsId, StringComparer.Ordinal)
            .ToList();

        var plannedTotal = RoundMoney(lines.Sum(l => l.PlannedAmount));
        var actualTotal = RoundMoney(lines.Sum(l => l.ActualAmount));
        return new ProjectBudgetDto
        {
            Currency = currency,
            PlannedAmount = plannedTotal,
            ActualAmount = actualTotal,
            Variance = RoundMoney(plannedTotal - actualTotal),
            OverCount = packages.Count(p => p.Over),
            Lines = lines,
            Packages = packages
        };
    }

    private static string RequireBudgetName(string? name)
    {
        var n = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(n))
            throw new OperationCoreException("NAME_REQUIRED", "Budget line name is required.", "Bütçe kalemi adı zorunludur.", 400);
        return n;
    }

    private static string RequireCurrency(string? value)
    {
        var currency = PmBudgetMoney.NormalizeCurrency(string.IsNullOrWhiteSpace(value) ? PmBudgetMoney.DefaultCurrency : value);
        if (string.IsNullOrEmpty(currency))
            throw new OperationCoreException("CURRENCY", "Currency must be a 3-letter ISO code.", "Para birimi 3 harfli ISO kodu olmalı.", 400);
        return currency;
    }

    private static double NormalizeAmount(double amount)
    {
        if (amount < 0)
            throw new OperationCoreException("AMOUNT_RANGE", "Amount cannot be negative.", "Tutar negatif olamaz.", 400);
        if (amount > PmBudgetMoney.MaxAmount)
            throw new OperationCoreException("AMOUNT_RANGE", "Amount is too large.", "Tutar çok büyük.", 400);
        return RoundMoney(amount);
    }

    private static bool IsOverBudget(double planned, double actual) =>
        actual > planned + PmBudgetMoney.OverEpsilon;

    private static double RoundMoney(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
