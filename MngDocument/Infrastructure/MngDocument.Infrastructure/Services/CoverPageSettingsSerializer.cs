using System.Text.Json;
using MngDocument.Application.Contracts.CoverPages;
using MngDocument.Application.Contracts.Letterheads;
using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Infrastructure.Services;

public static class CoverPageSettingsSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static CoverPageSettingsDto CreateDefault() => new()
    {
        PageLayout = LetterheadBrandingDefaults.DefaultPageLayoutDto()
    };

    public static CoverPageSettingsDto Normalize(CoverPageSettingsDto? settings)
    {
        var source = settings ?? CreateDefault();
        return new CoverPageSettingsDto
        {
            PageLayout = source.PageLayout ?? LetterheadBrandingDefaults.DefaultPageLayoutDto()
        };
    }

    public static CoverPageSettingsDto Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return CreateDefault();

        try
        {
            return Normalize(JsonSerializer.Deserialize<CoverPageSettingsDto>(json, JsonOptions));
        }
        catch
        {
            return CreateDefault();
        }
    }

    public static string Serialize(CoverPageSettingsDto settings) =>
        JsonSerializer.Serialize(Normalize(settings), JsonOptions);

    public static CoverPageDefinitionDto NormalizeDefinition(CoverPageDefinitionDto? definition) =>
        definition ?? new CoverPageDefinitionDto();

    public static CoverPageDefinitionDto ParseDefinition(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new CoverPageDefinitionDto();

        try
        {
            return NormalizeDefinition(JsonSerializer.Deserialize<CoverPageDefinitionDto>(json, JsonOptions));
        }
        catch
        {
            return new CoverPageDefinitionDto();
        }
    }

    public static string SerializeDefinition(CoverPageDefinitionDto definition) =>
        JsonSerializer.Serialize(NormalizeDefinition(definition), JsonOptions);
}
