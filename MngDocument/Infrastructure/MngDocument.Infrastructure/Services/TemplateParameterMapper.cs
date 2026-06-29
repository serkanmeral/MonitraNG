using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Infrastructure.Services;

public static class TemplateParameterMapper
{
    public static TemplateParameterDto ToDto(TemplateParameterModel model) => new()
    {
        Key = model.Key,
        Label = model.Label,
        DataType = model.DataType,
        ValueSourceMode = model.ValueSourceMode,
        DefaultValue = model.DefaultValue,
        Format = model.Format,
        Incremental = model.Incremental is null
            ? null
            : new TemplateIncrementalOptionsDto
            {
                Format = model.Incremental.Format,
                StartValue = model.Incremental.StartValue,
                IncrementStep = model.Incremental.IncrementStep,
                ScopeKey = model.Incremental.ScopeKey,
                ResetPolicy = model.Incremental.ResetPolicy
            },
        DocBinding = MapDocBindingToDto(model.DocBinding),
        ContextBinding = model.ContextBinding is null
            ? null
            : new TemplateContextBindingDto
            {
                Path = model.ContextBinding.Path,
                FallbackPath = model.ContextBinding.FallbackPath,
                DefaultValue = model.ContextBinding.DefaultValue,
                Format = model.ContextBinding.Format
            }
    };

    public static TemplateParameterModel ToModel(TemplateParameterDto dto) => new()
    {
        Key = dto.Key,
        Label = dto.Label,
        DataType = dto.DataType,
        ValueSourceMode = dto.ValueSourceMode,
        DefaultValue = dto.DefaultValue,
        Format = dto.Format,
        Incremental = dto.Incremental is null
            ? null
            : new TemplateIncrementalModel
            {
                Format = dto.Incremental.Format,
                StartValue = dto.Incremental.StartValue,
                IncrementStep = dto.Incremental.IncrementStep,
                ScopeKey = dto.Incremental.ScopeKey,
                ResetPolicy = dto.Incremental.ResetPolicy
            },
        DocBinding = MapDocBindingToModel(dto.DocBinding ?? dto.SourceBinding),
        ContextBinding = dto.ContextBinding is null
            ? null
            : new TemplateContextBindingModel
            {
                Path = dto.ContextBinding.Path,
                FallbackPath = dto.ContextBinding.FallbackPath,
                DefaultValue = dto.ContextBinding.DefaultValue,
                Format = dto.ContextBinding.Format
            }
    };

    private static TemplateDocBindingDto? MapDocBindingToDto(TemplateDocBindingModel? model) =>
        model is null
            ? null
            : new TemplateDocBindingDto
            {
                RegionKind = model.RegionKind,
                ParagraphIndex = model.ParagraphIndex,
                OriginalText = model.OriginalText,
                CharStart = model.CharStart,
                CharEnd = model.CharEnd
            };

    private static TemplateDocBindingModel? MapDocBindingToModel(TemplateDocBindingDto? dto) =>
        dto is null
            ? null
            : new TemplateDocBindingModel
            {
                RegionKind = dto.RegionKind,
                ParagraphIndex = dto.ParagraphIndex,
                OriginalText = dto.OriginalText,
                CharStart = dto.CharStart,
                CharEnd = dto.CharEnd
            };
}
