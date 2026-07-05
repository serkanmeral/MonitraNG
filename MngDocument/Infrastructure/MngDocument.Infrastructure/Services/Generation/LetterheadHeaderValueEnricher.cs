using System.Globalization;
using MngDocument.Application.Contracts.Letterheads;
using MngDocument.Application.Interfaces;
using MngDocument.Infrastructure.Services.Generation;

namespace MngDocument.Infrastructure.Services;

public sealed class LetterheadHeaderValueEnricher
{
    private readonly DocumentIncrementalAllocator _incremental;

    public LetterheadHeaderValueEnricher(DocumentIncrementalAllocator incremental)
    {
        _incremental = incremental;
    }

    public async Task EnrichAsync(
        Dictionary<string, string> values,
        TemplateModelDocument model,
        LetterheadDto? letterhead,
        string? templateName,
        IRequestContext ctx,
        bool allocateCounters,
        string? token,
        CancellationToken ct)
    {
        if (letterhead is null || !letterhead.Letterhead.Enabled)
            return;

        var settings = LetterheadSettingsSerializer.Normalize(letterhead.Settings);
        var header = settings.HeaderFields;

        if (header.DocumentName
            && (!values.TryGetValue(LetterheadConstants.DocumentNameKey, out var documentName)
                || string.IsNullOrWhiteSpace(documentName)))
        {
            values[LetterheadConstants.DocumentNameKey] = templateName?.Trim() ?? letterhead.Name;
        }

        if (header.GeneratedAt
            && (!values.TryGetValue(LetterheadConstants.GeneratedAtKey, out var generatedAt)
                || string.IsNullOrWhiteSpace(generatedAt)))
        {
            values[LetterheadConstants.GeneratedAtKey] =
                DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        if (header.CreatePerson)
        {
            var person = ctx.DisplayName?.Trim();
            if (string.IsNullOrWhiteSpace(person))
                person = ctx.Username?.Trim();
            if (!string.IsNullOrWhiteSpace(person))
                values[LetterheadConstants.CreatePersonKey] = person;
        }

        if (!header.DocNo)
            return;

        if (HasTemplateIncrementalDocNo(model, values))
            return;

        if (settings.GeneralDocNo.Enabled)
        {
            if (allocateCounters)
            {
                var incremental = new TemplateIncrementalModel
                {
                    Format = settings.GeneralDocNo.Format,
                    StartValue = settings.GeneralDocNo.StartValue,
                    IncrementStep = settings.GeneralDocNo.IncrementStep,
                    ScopeKey = LetterheadSettingsSerializer.ResolveScopeKey(settings.GeneralDocNo, letterhead.Code),
                    ResetPolicy = settings.GeneralDocNo.ResetPolicy
                };
                values[LetterheadConstants.DocNoKey] =
                    await _incremental.AllocateAsync(incremental, token, ct);
            }

            return;
        }

        if (!allocateCounters)
            return;

        if (values.TryGetValue(LetterheadConstants.DocNoKey, out var existing)
            && !string.IsNullOrWhiteSpace(existing))
            return;

        values[LetterheadConstants.DocNoKey] = await _incremental.AllocateAsync(
            new TemplateIncrementalModel
            {
                Format = LetterheadConstants.DefaultDocNoFormat,
                StartValue = 1,
                IncrementStep = 1,
                ScopeKey = LetterheadConstants.DomainScopeKey,
                ResetPolicy = "yearly"
            },
            token,
            ct);
    }

    private static bool HasTemplateIncrementalDocNo(TemplateModelDocument model, Dictionary<string, string> values)
    {
        var param = model.Parameters.FirstOrDefault(p =>
            string.Equals(p.Key, LetterheadConstants.DocNoKey, StringComparison.OrdinalIgnoreCase)
            && !TemplateModelSerializer.IsHeaderBoundLetterheadDocNo(p));

        if (param is null)
            return false;

        if (!string.Equals(param.ValueSourceMode, "incremental", StringComparison.OrdinalIgnoreCase)
            || param.Incremental is null)
            return false;

        return values.TryGetValue(LetterheadConstants.DocNoKey, out var docNo)
               && !string.IsNullOrWhiteSpace(docNo);
    }
}
