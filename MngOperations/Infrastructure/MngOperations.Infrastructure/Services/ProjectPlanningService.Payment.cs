using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    public async Task<ProjectProgressDto> GetProgressAsync(string projectId, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        return await BuildProgressAsync(projectId, token, ct);
    }

    public async Task<PaymentTermsDto> UpsertPaymentTermsAsync(
        string projectId,
        UpsertPaymentTermsRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var existing = await FindTermsRowAsync(projectId, token, ct);
        var currency = RequireCurrency(request.Currency ?? existing?.currency);
        var cap = NormalizePercent(request.PenaltyCapPercent ?? existing?.penaltyCapPercent ?? 35);
        var amount = NormalizeAmount(request.BaseAmount);
        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["baseAmount"] = amount,
            ["currency"] = currency,
            ["penaltyCapPercent"] = cap
        };

        if (existing is null || string.IsNullOrWhiteSpace(existing.__dataId))
        {
            var created = await _dg.CreateAsync(PmDatasets.PaymentTerms, payload, token, ct);
            if (string.IsNullOrWhiteSpace(ReadId(created)))
                throw new OperationCoreException("CREATE_FAILED", "Payment terms were not created.", "Sözleşme başlığı oluşturulamadı.", 500);
        }
        else
        {
            payload.Remove("projectId");
            await _dg.UpdateAsync(PmDatasets.PaymentTerms, existing.__dataId, payload, token, ct);
        }

        var pack = await BuildProgressAsync(projectId, token, ct);
        await SyncLinkedBudgetLinesAsync(projectId, pack, token, ct);
        return pack.Terms;
    }

    public async Task<PaymentSliceDto> CreatePaymentSliceAsync(
        string projectId,
        CreatePaymentSliceRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var draft = await NormalizeSliceAsync(projectId, request, token, ct);
        var rows = await LoadSliceRowsAsync(projectId, token, ct);
        var sort = request.SortOrder is > 0
            ? request.SortOrder.Value
            : (int)rows.Select(r => r.sortOrder ?? 0).DefaultIfEmpty(0).Max() + 1;
        draft.SortOrder = sort;

        var created = await _dg.CreateAsync(PmDatasets.PaymentSlices, SlicePayload(projectId, draft), token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Payment slice was not created.", "Dilim oluşturulamadı.", 500);

        var row = await LoadSliceRowOrThrowAsync(id, token, ct);
        await EnsureDraftClaimsAsync(row, token, ct);
        var pack = await BuildProgressAsync(projectId, token, ct);
        await SyncBudgetLineActualAsync(projectId, draft.BudgetLineId, pack, token, ct);
        return pack.Slices.First(s => s.Id == id);
    }

    public async Task<PaymentSliceDto> UpdatePaymentSliceAsync(
        string id,
        UpdatePaymentSliceRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadSliceRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var previousBudgetLineId = EmptyToNull(existing.budgetLineId);
        var merged = MergeSlice(existing, request);
        var draft = await NormalizeSliceAsync(projectId, merged, token, ct);
        draft.SortOrder = request.SortOrder ?? (int)(existing.sortOrder ?? 0);

        var payload = SlicePayload(projectId, draft);
        payload.Remove("projectId");
        await _dg.UpdateAsync(PmDatasets.PaymentSlices, id, payload, token, ct);
        var row = await LoadSliceRowOrThrowAsync(id, token, ct);
        await EnsureDraftClaimsAsync(row, token, ct);
        var pack = await BuildProgressAsync(projectId, token, ct);
        await SyncBudgetLineActualAsync(projectId, previousBudgetLineId, pack, token, ct);
        await SyncBudgetLineActualAsync(projectId, draft.BudgetLineId, pack, token, ct);
        return pack.Slices.First(s => s.Id == id);
    }

    public async Task DeletePaymentSliceAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        var slice = await LoadSliceRowOrThrowAsync(id, token, ct);
        var projectId = slice.projectId!;
        var claims = await LoadClaimRowsAsync(projectId, token, ct);
        var mine = claims.Where(c => string.Equals(c.sliceId, id, StringComparison.Ordinal)).ToList();
        if (mine.Any(c => PmProgressClaimStatus.Normalize(c.status) != PmProgressClaimStatus.Draft))
            throw new OperationCoreException(
                "SLICE_LOCKED",
                "A slice with a submitted period cannot be deleted.",
                "Sunulmuş dönemi olan dilim silinemez.",
                409);

        foreach (var claim in mine)
        {
            if (!string.IsNullOrWhiteSpace(claim.__dataId))
                await _dg.DeleteAsync(PmDatasets.ProgressClaims, claim.__dataId, token, ct);
        }

        await _dg.DeleteAsync(PmDatasets.PaymentSlices, id, token, ct);
        var pack = await BuildProgressAsync(projectId, token, ct);
        await SyncBudgetLineActualAsync(projectId, slice.budgetLineId, pack, token, ct);
    }

    public async Task<BudgetLineDto> WriteSlicePlanAsync(string sliceId, CancellationToken ct = default)
    {
        var token = RequireToken();
        var slice = await LoadSliceRowOrThrowAsync(sliceId, token, ct);
        var projectId = slice.projectId!;
        var budgetLineId = EmptyToNull(slice.budgetLineId);
        if (budgetLineId is null)
            throw new OperationCoreException(
                "BUDGET_LINK",
                "The slice is not linked to a budget line.",
                "Dilim bir bütçe satırına bağlı değil.",
                400);

        var line = await LoadBudgetLineRowOrThrowAsync(budgetLineId, token, ct);
        AssertSameProject(line.projectId, projectId, "BUDGET_PROJECT", "Bütçe satırı bu projede değil.");
        var pack = await BuildProgressAsync(projectId, token, ct);
        AssertSameCurrency(line.currency, pack.Terms.Currency);
        var dto = pack.Slices.First(s => s.Id == sliceId);
        var planned = dto.Kind == PmPaymentSliceKind.Unit
            ? RoundMoney(pack.Claims.Where(c => c.SliceId == sliceId).Sum(c => c.Quantity) * dto.UnitPrice)
            : dto.ScheduledAmount;

        await _dg.UpdateAsync(
            PmDatasets.BudgetLines,
            budgetLineId,
            new Dictionary<string, object?> { ["plannedAmount"] = planned },
            token,
            ct);
        return await LoadBudgetLineDtoAsync(budgetLineId, token, ct);
    }

    public async Task<ProgressClaimDto> CreateProgressClaimAsync(
        string projectId,
        CreateProgressClaimRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var slice = await LoadSliceRowOrThrowAsync(request.SliceId, token, ct);
        AssertSameProject(slice.projectId, projectId, "SLICE_PROJECT", "Dilim bu projede değil.");
        var existing = (await LoadClaimRowsAsync(projectId, token, ct))
            .Where(c => string.Equals(c.sliceId, slice.__dataId, StringComparison.Ordinal))
            .ToList();
        var sequence = request.Sequence is > 0
            ? request.Sequence.Value
            : existing.Select(c => (int)(c.sequence ?? 0)).DefaultIfEmpty(0).Max() + 1;
        if (existing.Any(c => (int)(c.sequence ?? 0) == sequence))
            throw new OperationCoreException("CLAIM_SEQUENCE", "This period number already exists.", "Bu dönem numarası zaten var.", 409);

        var label = BoundLabel(string.IsNullOrWhiteSpace(request.PeriodLabel)
            ? $"{slice.name} {sequence}"
            : request.PeriodLabel);
        var resources = CleanIds(request.ResourceIds);
        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["sliceId"] = slice.__dataId,
            ["sequence"] = sequence,
            ["periodLabel"] = label,
            ["dueDate"] = request.DueDate,
            ["claimedAmount"] = NormalizeAmount(request.ClaimedAmount),
            ["acceptedAmount"] = NormalizeAmount(request.AcceptedAmount),
            ["deduction"] = NormalizeAmount(request.Deduction),
            ["adjustmentAmount"] = NormalizeSigned(request.AdjustmentAmount),
            ["quantity"] = NormalizeAmount(request.Quantity),
            ["status"] = PmProgressClaimStatus.Draft,
            ["resourceIds"] = resources,
            ["note"] = BoundNote(request.Note)
        };

        var created = await _dg.CreateAsync(PmDatasets.ProgressClaims, payload, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Progress claim was not created.", "Dönem oluşturulamadı.", 500);
        return await LoadClaimDtoAsync(id, token, ct);
    }

    public async Task<ProgressClaimDto> UpdateProgressClaimAsync(
        string id,
        UpdateProgressClaimRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadClaimRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var slice = await LoadSliceRowOrThrowAsync(existing.sliceId, token, ct);
        var from = PmProgressClaimStatus.Normalize(existing.status);
        var to = request.Status is null ? from : PmProgressClaimStatus.Normalize(request.Status);
        if (!PmProgressClaimStatus.CanMove(from, to))
            throw new OperationCoreException(
                "PROGRESS_STATUS",
                "This status change is not allowed.",
                "Bu durum geçişi yapılamaz. Taslak sunulur, sunulan kabul edilir, kabul ödendi olur.",
                400);

        var resources = request.ResourceIds is not null ? CleanIds(request.ResourceIds) : CleanIds(existing.resourceIds);
        if (PmProgressClaimStatus.NeedsEvidence(to) && resources.Count == 0)
            throw new OperationCoreException(
                "PROGRESS_EVIDENCE",
                "Submitting a period requires evidence.",
                "Sunmak için en az bir kanıt gerekir.",
                400);

        if (PmProgressClaimStatus.ConsumesCap(to))
            await AssertGatePassedAsync(slice, projectId, token, ct);

        var sequence = request.Sequence ?? (int)(existing.sequence ?? 1);
        if (sequence < 1)
            throw new OperationCoreException("CLAIM_SEQUENCE", "Period number must be at least 1.", "Dönem numarası en az 1 olmalı.", 400);

        var siblings = (await LoadClaimRowsAsync(projectId, token, ct))
            .Where(c => string.Equals(c.sliceId, slice.__dataId, StringComparison.Ordinal)
                && !string.Equals(c.__dataId, id, StringComparison.Ordinal))
            .ToList();
        if (siblings.Any(c => (int)(c.sequence ?? 0) == sequence))
            throw new OperationCoreException("CLAIM_SEQUENCE", "This period number already exists.", "Bu dönem numarası zaten var.", 409);

        var candidate = new ProgressClaimDto
        {
            Id = id,
            ProjectId = projectId,
            SliceId = slice.__dataId ?? string.Empty,
            Sequence = sequence,
            PeriodLabel = request.PeriodLabel is not null ? BoundLabel(request.PeriodLabel) : BoundLabel(existing.periodLabel),
            DueDate = request.DueDate ?? existing.dueDate,
            ClaimedAmount = request.ClaimedAmount.HasValue ? NormalizeAmount(request.ClaimedAmount.Value) : RoundMoney(existing.claimedAmount ?? 0),
            AcceptedAmount = request.AcceptedAmount.HasValue ? NormalizeAmount(request.AcceptedAmount.Value) : RoundMoney(existing.acceptedAmount ?? 0),
            Deduction = request.Deduction.HasValue ? NormalizeAmount(request.Deduction.Value) : RoundMoney(existing.deduction ?? 0),
            AdjustmentAmount = request.AdjustmentAmount.HasValue ? NormalizeSigned(request.AdjustmentAmount.Value) : RoundMoney(existing.adjustmentAmount ?? 0),
            Quantity = request.Quantity.HasValue ? NormalizeAmount(request.Quantity.Value) : RoundMoney(existing.quantity ?? 0),
            Status = to,
            ResourceIds = resources,
            Note = request.Note is not null ? BoundNote(request.Note) : EmptyToNull(existing.note)
        };

        var pack = await BuildProgressAsync(projectId, token, ct);
        var claims = pack.Claims.Where(c => c.Id != id).Append(candidate).ToList();
        var slices = pack.Slices.ToList();
        ApplyProgressMoney(pack.Terms, slices, claims);
        var priced = claims.First(c => c.Id == id);
        AssertSliceCap(slices.First(s => s.Id == slice.__dataId), priced, claims);
        await AssertBudgetCurrencyAsync(slice.budgetLineId, pack.Terms.Currency, token, ct);

        var payload = new Dictionary<string, object?>
        {
            ["sequence"] = candidate.Sequence,
            ["periodLabel"] = candidate.PeriodLabel,
            ["dueDate"] = candidate.DueDate,
            ["claimedAmount"] = candidate.ClaimedAmount,
            ["acceptedAmount"] = candidate.AcceptedAmount,
            ["deduction"] = candidate.Deduction,
            ["adjustmentAmount"] = candidate.AdjustmentAmount,
            ["quantity"] = candidate.Quantity,
            ["status"] = candidate.Status,
            ["resourceIds"] = resources,
            ["note"] = candidate.Note
        };
        await _dg.UpdateAsync(PmDatasets.ProgressClaims, id, payload, token, ct);

        var saved = await BuildProgressAsync(projectId, token, ct);
        await SyncBudgetLineActualAsync(projectId, slice.budgetLineId, saved, token, ct);
        return saved.Claims.First(c => c.Id == id);
    }

    public async Task DeleteProgressClaimAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadClaimRowOrThrowAsync(id, token, ct);
        if (PmProgressClaimStatus.Normalize(existing.status) != PmProgressClaimStatus.Draft)
            throw new OperationCoreException(
                "CLAIM_LOCKED",
                "Only a draft period can be deleted.",
                "Yalnız taslak dönem silinebilir.",
                409);

        var slice = await LoadSliceRowOrThrowAsync(existing.sliceId, token, ct);
        await _dg.DeleteAsync(PmDatasets.ProgressClaims, id, token, ct);
        var pack = await BuildProgressAsync(existing.projectId!, token, ct);
        await SyncBudgetLineActualAsync(existing.projectId!, slice.budgetLineId, pack, token, ct);
    }

    private async Task EnsureDraftClaimsAsync(PmPaymentSliceRow slice, string token, CancellationToken ct)
    {
        var projectId = slice.projectId!;
        var sliceId = slice.__dataId!;
        var kind = PmPaymentSliceKind.Normalize(slice.kind);
        var cadence = PmPaymentCadence.Normalize(slice.cadence);
        var count = kind == PmPaymentSliceKind.Unit || cadence == PmPaymentCadence.Once
            ? 1
            : Math.Max(1, (int)(slice.installmentCount ?? 1));
        var interval = cadence == PmPaymentCadence.Installments
            ? Math.Max(1, (int)(slice.intervalMonths ?? 3))
            : 0;
        var existing = (await LoadClaimRowsAsync(projectId, token, ct))
            .Where(c => string.Equals(c.sliceId, sliceId, StringComparison.Ordinal))
            .Select(c => (int)(c.sequence ?? 0))
            .ToHashSet();
        var name = (slice.name ?? string.Empty).Trim();

        for (var i = 1; i <= count; i++)
        {
            if (existing.Contains(i)) continue;
            DateTime? due = slice.anchorDate is null
                ? null
                : slice.anchorDate.Value.AddMonths((i - 1) * interval);
            var label = count == 1 ? name : $"{name} {i}/{count}";
            await _dg.CreateAsync(PmDatasets.ProgressClaims, new Dictionary<string, object?>
            {
                ["projectId"] = projectId,
                ["sliceId"] = sliceId,
                ["sequence"] = i,
                ["periodLabel"] = BoundLabel(label),
                ["dueDate"] = due,
                ["claimedAmount"] = 0,
                ["acceptedAmount"] = 0,
                ["deduction"] = 0,
                ["adjustmentAmount"] = 0,
                ["quantity"] = 0,
                ["status"] = PmProgressClaimStatus.Draft,
                ["resourceIds"] = new List<string>(),
                ["note"] = null
            }, token, ct);
        }
    }

    private async Task AssertGatePassedAsync(PmPaymentSliceRow slice, string projectId, string token, CancellationToken ct)
    {
        var gateId = EmptyToNull(slice.gateId);
        if (gateId is null) return;
        var gate = await LoadStageGateRowOrThrowAsync(gateId, token, ct);
        AssertSameProject(gate.projectId, projectId, "GATE_PROJECT", "Kapı bu projede değil.");
        if (PmStageGateStatus.Normalize(gate.status) != PmStageGateStatus.Passed)
            throw new OperationCoreException(
                "PROGRESS_GATE",
                "The linked gate has not passed.",
                "Bağlı kapı geçmeden dönem kabul edilemez.",
                400);
    }

    private async Task AssertBudgetCurrencyAsync(string? budgetLineId, string termsCurrency, string token, CancellationToken ct)
    {
        var id = EmptyToNull(budgetLineId);
        if (id is null) return;
        var line = await LoadBudgetLineRowOrThrowAsync(id, token, ct);
        AssertSameCurrency(line.currency, termsCurrency);
    }

    private async Task SyncLinkedBudgetLinesAsync(
        string projectId,
        ProjectProgressDto pack,
        string token,
        CancellationToken ct)
    {
        foreach (var lineId in pack.Slices.Select(s => s.BudgetLineId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
            await SyncBudgetLineActualAsync(projectId, lineId, pack, token, ct);
    }

    private async Task SyncBudgetLineActualAsync(
        string projectId,
        string? budgetLineId,
        ProjectProgressDto pack,
        string token,
        CancellationToken ct)
    {
        var id = EmptyToNull(budgetLineId);
        if (id is null) return;
        var line = await LoadBudgetLineRowOrThrowAsync(id, token, ct);
        AssertSameProject(line.projectId, projectId, "BUDGET_PROJECT", "Bütçe satırı bu projede değil.");
        AssertSameCurrency(line.currency, pack.Terms.Currency);
        var sliceIds = pack.Slices
            .Where(s => string.Equals(s.BudgetLineId, id, StringComparison.Ordinal))
            .Select(s => s.Id)
            .ToHashSet(StringComparer.Ordinal);
        var actual = RoundMoney(pack.Claims
            .Where(c => sliceIds.Contains(c.SliceId) && PmProgressClaimStatus.ConsumesCap(c.Status))
            .Sum(c => c.Net));
        await _dg.UpdateAsync(
            PmDatasets.BudgetLines,
            id,
            new Dictionary<string, object?> { ["actualAmount"] = actual },
            token,
            ct);
    }

    private async Task<ProjectProgressDto> BuildProgressAsync(string projectId, string token, CancellationToken ct)
    {
        var terms = ToTermsDto(await FindTermsRowAsync(projectId, token, ct), projectId);
        var slices = (await LoadSliceRowsAsync(projectId, token, ct)).Select(ToSliceDto).ToList();
        var claims = (await LoadClaimRowsAsync(projectId, token, ct)).Select(ToClaimDto).ToList();
        ApplyProgressMoney(terms, slices, claims);

        var percentSlices = slices.Where(s => s.Kind == PmPaymentSliceKind.Percent).ToList();
        var percentIds = percentSlices.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var consuming = claims.Where(c => PmProgressClaimStatus.ConsumesCap(c.Status)).ToList();
        var percentAccepted = RoundMoney(consuming.Where(c => percentIds.Contains(c.SliceId)).Sum(c => c.Net));
        var percentTotal = RoundMoney(percentSlices.Sum(s => s.Percent));
        return new ProjectProgressDto
        {
            Terms = terms,
            PenaltyCapAmount = RoundMoney(terms.BaseAmount * terms.PenaltyCapPercent / 100d),
            AcceptedNet = RoundMoney(consuming.Sum(c => c.Net)),
            PaidNet = RoundMoney(consuming.Where(c => c.Status == PmProgressClaimStatus.Paid).Sum(c => c.Net)),
            PercentScheduled = RoundMoney(percentSlices.Sum(s => s.ScheduledAmount)),
            PercentAcceptedNet = percentAccepted,
            PercentRemaining = RoundMoney(terms.BaseAmount - percentAccepted),
            PercentTotal = percentTotal,
            PercentOver = percentTotal > 100 + PmBudgetMoney.OverEpsilon,
            Deducted = RoundMoney(consuming.Sum(c => c.AppliedDeduction)),
            Outstanding = RoundMoney(consuming.Sum(c => c.OutstandingAmount)),
            Slices = slices.OrderBy(s => s.SortOrder).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            Claims = claims.OrderBy(c => c.Sequence).ThenBy(c => c.PeriodLabel, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static void ApplyProgressMoney(
        PaymentTermsDto terms,
        IReadOnlyList<PaymentSliceDto> slices,
        IReadOnlyList<ProgressClaimDto> claims)
    {
        foreach (var slice in slices)
        {
            slice.ScheduledAmount = slice.Kind == PmPaymentSliceKind.Percent
                ? RoundMoney(terms.BaseAmount * slice.Percent / 100d)
                : 0;
        }

        var room = RoundMoney(terms.BaseAmount * terms.PenaltyCapPercent / 100d);
        var sliceSort = slices.ToDictionary(s => s.Id, s => s.SortOrder, StringComparer.Ordinal);
        var ordered = claims
            .OrderBy(c => sliceSort.TryGetValue(c.SliceId, out var sort) ? sort : 0)
            .ThenBy(c => c.Sequence)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

        foreach (var claim in ordered.Where(c => PmProgressClaimStatus.ConsumesCap(c.Status)))
            room = PriceClaim(claim, room, consume: true);
        foreach (var claim in ordered.Where(c => !PmProgressClaimStatus.ConsumesCap(c.Status)))
            PriceClaim(claim, room, consume: false);
    }

    private static double PriceClaim(ProgressClaimDto claim, double room, bool consume)
    {
        var payable = RoundMoney(claim.AcceptedAmount + claim.AdjustmentAmount);
        var applied = Math.Min(claim.Deduction, Math.Max(0, payable));
        applied = Math.Min(applied, Math.Max(0, room));
        applied = RoundMoney(applied);
        claim.AppliedDeduction = applied;
        claim.OutstandingAmount = RoundMoney(Math.Max(0, claim.Deduction - applied));
        claim.Net = RoundMoney(payable - applied);
        return consume ? RoundMoney(room - applied) : room;
    }

    private static void AssertSliceCap(PaymentSliceDto slice, ProgressClaimDto claim, IReadOnlyList<ProgressClaimDto> claims)
    {
        if (slice.Kind != PmPaymentSliceKind.Percent) return;
        if (!PmProgressClaimStatus.ConsumesCap(claim.Status)) return;
        var others = claims
            .Where(c => c.SliceId == slice.Id && c.Id != claim.Id && PmProgressClaimStatus.ConsumesCap(c.Status))
            .Sum(c => c.Net);
        if (others + claim.Net > slice.ScheduledAmount + PmBudgetMoney.OverEpsilon)
            throw new OperationCoreException(
                "PROGRESS_SLICE_CAP",
                "Accepted net exceeds this slice.",
                "Kabul edilen net, bu dilimin taban yüzdesini aşıyor.",
                400);
    }

    private async Task<ProgressClaimDto> LoadClaimDtoAsync(string id, string token, CancellationToken ct)
    {
        var row = await LoadClaimRowOrThrowAsync(id, token, ct);
        var pack = await BuildProgressAsync(row.projectId!, token, ct);
        return pack.Claims.First(c => c.Id == id);
    }

    private async Task<SliceDraft> NormalizeSliceAsync(
        string projectId,
        CreatePaymentSliceRequest request,
        string token,
        CancellationToken ct)
    {
        var kind = PmPaymentSliceKind.Normalize(request.Kind);
        var cadence = PmPaymentCadence.Normalize(request.Cadence);
        var percent = 0d;
        var unitPrice = 0d;
        var count = 1;
        var interval = 0;
        if (kind == PmPaymentSliceKind.Unit)
        {
            cadence = PmPaymentCadence.Once;
            unitPrice = NormalizeAmount(request.UnitPrice);
        }
        else
        {
            percent = NormalizePercent(request.Percent);
            if (cadence == PmPaymentCadence.Installments)
            {
                count = request.InstallmentCount ?? 4;
                interval = request.IntervalMonths ?? 3;
                if (count < 1 || count > 60)
                    throw new OperationCoreException("INSTALLMENT_COUNT", "Installment count must be between 1 and 60.", "Taksit adedi 1 ile 60 arasında olmalı.", 400);
                if (interval < 1 || interval > 24)
                    throw new OperationCoreException("INSTALLMENT_INTERVAL", "Installment interval must be between 1 and 24 months.", "Taksit aralığı 1 ile 24 ay arasında olmalı.", 400);
            }
        }

        return new SliceDraft
        {
            Name = BoundLabel(request.Name),
            Kind = kind,
            Percent = percent,
            Cadence = cadence,
            InstallmentCount = count,
            IntervalMonths = interval,
            UnitPrice = unitPrice,
            GateId = await OptionalGateAsync(projectId, request.GateId, token, ct),
            WbsId = await OptionalWbsAsync(projectId, request.WbsId, token, ct),
            BudgetLineId = await OptionalBudgetLineAsync(projectId, request.BudgetLineId, token, ct),
            AnchorDate = request.AnchorDate,
            Note = BoundNote(request.Note)
        };
    }

    private static CreatePaymentSliceRequest MergeSlice(PmPaymentSliceRow row, UpdatePaymentSliceRequest request) => new()
    {
        Name = request.Name ?? row.name ?? string.Empty,
        Kind = request.Kind ?? row.kind,
        Percent = request.Percent ?? row.percent ?? 0,
        Cadence = request.Cadence ?? row.cadence,
        InstallmentCount = request.InstallmentCount ?? (int)(row.installmentCount ?? 1),
        IntervalMonths = request.IntervalMonths ?? (int)(row.intervalMonths ?? 0),
        UnitPrice = request.UnitPrice ?? row.unitPrice ?? 0,
        GateId = request.GateId ?? row.gateId,
        WbsId = request.WbsId ?? row.wbsId,
        BudgetLineId = request.BudgetLineId ?? row.budgetLineId,
        AnchorDate = request.AnchorDate ?? row.anchorDate,
        SortOrder = request.SortOrder ?? (int)(row.sortOrder ?? 0),
        Note = request.Note ?? row.note
    };

    private static Dictionary<string, object?> SlicePayload(string projectId, SliceDraft draft) => new()
    {
        ["projectId"] = projectId,
        ["name"] = draft.Name,
        ["kind"] = draft.Kind,
        ["percent"] = draft.Percent,
        ["cadence"] = draft.Cadence,
        ["installmentCount"] = draft.InstallmentCount,
        ["intervalMonths"] = draft.IntervalMonths,
        ["unitPrice"] = draft.UnitPrice,
        ["gateId"] = draft.GateId,
        ["wbsId"] = draft.WbsId,
        ["budgetLineId"] = draft.BudgetLineId,
        ["anchorDate"] = draft.AnchorDate,
        ["sortOrder"] = draft.SortOrder,
        ["note"] = draft.Note
    };

    private async Task<string?> OptionalWbsAsync(string projectId, string? wbsId, string token, CancellationToken ct)
    {
        var id = EmptyToNull(wbsId);
        if (id is null) return null;
        var ids = await NormalizeWbsIdsAsync(projectId, new[] { id }, token, ct);
        return ids[0];
    }

    private async Task<string?> OptionalGateAsync(string projectId, string? gateId, string token, CancellationToken ct)
    {
        var id = EmptyToNull(gateId);
        if (id is null) return null;
        var gate = await LoadStageGateRowOrThrowAsync(id, token, ct);
        AssertSameProject(gate.projectId, projectId, "GATE_PROJECT", "Kapı bu projede değil.");
        return gate.__dataId;
    }

    private async Task<string?> OptionalBudgetLineAsync(string projectId, string? budgetLineId, string token, CancellationToken ct)
    {
        var id = EmptyToNull(budgetLineId);
        if (id is null) return null;
        var line = await LoadBudgetLineRowOrThrowAsync(id, token, ct);
        AssertSameProject(line.projectId, projectId, "BUDGET_PROJECT", "Bütçe satırı bu projede değil.");
        return line.__dataId;
    }

    private async Task<PmPaymentTermsRow?> FindTermsRowAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.PaymentTerms,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmPaymentTermsRow>).FirstOrDefault();
    }

    private async Task<List<PmPaymentSliceRow>> LoadSliceRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.PaymentSlices,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmPaymentSliceRow>).ToList();
    }

    private async Task<PmPaymentSliceRow> LoadSliceRowOrThrowAsync(string? id, string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("SLICE_REQUIRED", "Payment slice is required.", "Dilim zorunludur.", 400);
        var row = await _dg.GetByIdAsync<PmPaymentSliceRow>(PmDatasets.PaymentSlices, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Payment slice not found.", "Dilim bulunamadı.", 404);
        return row;
    }

    private async Task<List<PmProgressClaimRow>> LoadClaimRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.ProgressClaims,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmProgressClaimRow>).ToList();
    }

    private async Task<PmProgressClaimRow> LoadClaimRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmProgressClaimRow>(PmDatasets.ProgressClaims, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Progress claim not found.", "Dönem bulunamadı.", 404);
        return row;
    }

    private static PaymentTermsDto ToTermsDto(PmPaymentTermsRow? row, string projectId)
    {
        if (row is null)
        {
            return new PaymentTermsDto
            {
                ProjectId = projectId,
                Currency = PmBudgetMoney.DefaultCurrency,
                PenaltyCapPercent = 35
            };
        }

        var currency = CurrencyOf(row.currency);
        return new PaymentTermsDto
        {
            Id = row.__dataId ?? string.Empty,
            ProjectId = row.projectId ?? projectId,
            BaseAmount = RoundMoney(row.baseAmount ?? 0),
            Currency = currency,
            PenaltyCapPercent = RoundMoney(row.penaltyCapPercent ?? 35)
        };
    }

    private static PaymentSliceDto ToSliceDto(PmPaymentSliceRow row) => new()
    {
        Id = row.__dataId ?? string.Empty,
        ProjectId = row.projectId ?? string.Empty,
        Name = (row.name ?? string.Empty).Trim(),
        Kind = PmPaymentSliceKind.Normalize(row.kind),
        Percent = RoundMoney(row.percent ?? 0),
        Cadence = PmPaymentCadence.Normalize(row.cadence),
        InstallmentCount = Math.Max(1, (int)(row.installmentCount ?? 1)),
        IntervalMonths = (int)(row.intervalMonths ?? 0),
        UnitPrice = RoundMoney(row.unitPrice ?? 0),
        GateId = EmptyToNull(row.gateId),
        WbsId = EmptyToNull(row.wbsId),
        BudgetLineId = EmptyToNull(row.budgetLineId),
        AnchorDate = row.anchorDate,
        SortOrder = (int)(row.sortOrder ?? 0),
        Note = EmptyToNull(row.note)
    };

    private static ProgressClaimDto ToClaimDto(PmProgressClaimRow row) => new()
    {
        Id = row.__dataId ?? string.Empty,
        ProjectId = row.projectId ?? string.Empty,
        SliceId = row.sliceId ?? string.Empty,
        Sequence = Math.Max(1, (int)(row.sequence ?? 1)),
        PeriodLabel = (row.periodLabel ?? string.Empty).Trim(),
        DueDate = row.dueDate,
        ClaimedAmount = RoundMoney(row.claimedAmount ?? 0),
        AcceptedAmount = RoundMoney(row.acceptedAmount ?? 0),
        Deduction = RoundMoney(row.deduction ?? 0),
        AdjustmentAmount = RoundMoney(row.adjustmentAmount ?? 0),
        Quantity = RoundMoney(row.quantity ?? 0),
        Status = PmProgressClaimStatus.Normalize(row.status),
        ResourceIds = row.resourceIds ?? new List<string>(),
        Note = EmptyToNull(row.note)
    };

    private static void AssertSameProject(string? ownerProjectId, string projectId, string code, string turkish)
    {
        if (!string.Equals(ownerProjectId, projectId, StringComparison.Ordinal))
            throw new OperationCoreException(code, "The record is not in this project.", turkish, 400);
    }

    private static void AssertSameCurrency(string? lineCurrency, string termsCurrency)
    {
        if (!string.Equals(CurrencyOf(lineCurrency), termsCurrency, StringComparison.Ordinal))
            throw new OperationCoreException(
                "PROGRESS_CURRENCY",
                "The budget line currency must match the contract.",
                "Bütçe satırının para birimi sözleşme başlığı ile aynı olmalı.",
                400);
    }

    private static string CurrencyOf(string? raw)
    {
        var currency = PmBudgetMoney.NormalizeCurrency(raw);
        return string.IsNullOrEmpty(currency) ? PmBudgetMoney.DefaultCurrency : currency;
    }

    private static double NormalizePercent(double value)
    {
        if (value < 0 || value > 100)
            throw new OperationCoreException("PROGRESS_PERCENT", "Percent must be between 0 and 100.", "Yüzde 0 ile 100 arasında olmalı.", 400);
        return Math.Round(value, 4, MidpointRounding.AwayFromZero);
    }

    private static double NormalizeSigned(double amount)
    {
        if (Math.Abs(amount) > PmBudgetMoney.MaxAmount)
            throw new OperationCoreException("AMOUNT_RANGE", "Amount is too large.", "Tutar çok büyük.", 400);
        return RoundMoney(amount);
    }

    private static string BoundLabel(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new OperationCoreException("NAME_REQUIRED", "Name is required.", "Ad zorunludur.", 400);
        if (text.Length > 256)
            throw new OperationCoreException("NAME_LENGTH", "Name is too long.", "Ad çok uzun.", 400);
        return text;
    }

    private static string? BoundNote(string? note)
    {
        var text = EmptyToNull(note);
        if (text is not null && text.Length > 2000)
            throw new OperationCoreException("NOTE_LENGTH", "Note is too long.", "Not çok uzun.", 400);
        return text;
    }

    private sealed class SliceDraft
    {
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = PmPaymentSliceKind.Percent;
        public double Percent { get; set; }
        public string Cadence { get; set; } = PmPaymentCadence.Once;
        public int InstallmentCount { get; set; } = 1;
        public int IntervalMonths { get; set; }
        public double UnitPrice { get; set; }
        public string? GateId { get; set; }
        public string? WbsId { get; set; }
        public string? BudgetLineId { get; set; }
        public DateTime? AnchorDate { get; set; }
        public int SortOrder { get; set; }
        public string? Note { get; set; }
    }
}
