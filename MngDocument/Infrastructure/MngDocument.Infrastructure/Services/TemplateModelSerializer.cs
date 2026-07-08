using System.Text.Json;
using System.Text.Json.Serialization;
using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Infrastructure.Services;

public sealed class TemplateModelDocument
{
    public string SchemaVersion { get; set; } = TemplateModelSerializer.CurrentSchemaVersion;

    /// <summary>Primary context type for parameter context bindings (e.g. odak.siparis.line).</summary>
    [JsonPropertyName("primaryContextType")]
    public string? PrimaryContextType { get; set; }

    /// <summary>Document generation profile code this template is eligible for (e.g. odak.coc.fromLine).</summary>
    [JsonPropertyName("generationProfile")]
    public string? GenerationProfile { get; set; }

    [JsonPropertyName("defaultLetterheadId")]
    public string? DefaultLetterheadId { get; set; }

    [JsonPropertyName("defaultCoverPageId")]
    public string? DefaultCoverPageId { get; set; }

    [JsonPropertyName("letterhead")]
    public TemplateLetterheadModel? Letterhead { get; set; }

    [JsonPropertyName("footer")]
    public TemplateFooterModel? Footer { get; set; }

    [JsonPropertyName("pageLayout")]
    public TemplatePageLayoutModel? PageLayout { get; set; }

    [JsonPropertyName("parameters")]
    public List<TemplateParameterModel> Parameters { get; set; } = new();
}

public sealed class TemplateLetterheadModel
{
    public bool Enabled { get; set; }
    public bool ShowLogo { get; set; }
    public bool ShowDocumentName { get; set; }
    public bool ShowDocumentNumber { get; set; }
    public bool ShowGeneratedAt { get; set; }
    public bool ShowCreatePerson { get; set; }
}

public sealed class TemplateFooterModel
{
    public bool Enabled { get; set; }
    public bool ShowFormRevision { get; set; } = true;
    public bool ShowOfficeColumns { get; set; } = true;
    public bool ShowAddresses { get; set; } = true;
    public bool ShowContacts { get; set; } = true;
    public bool ShowDividerLine { get; set; } = true;
}

public sealed class TemplatePageLayoutModel
{
    public int MarginTopTwips { get; set; } = OdakPageLayout.DefaultMarginTopTwips;
    public int MarginRightTwips { get; set; } = OdakPageLayout.DefaultMarginRightTwips;
    public int MarginBottomTwips { get; set; } = OdakPageLayout.DefaultMarginBottomTwips;
    public int MarginLeftTwips { get; set; } = OdakPageLayout.DefaultMarginLeftTwips;
    public int HeaderDistanceTwips { get; set; } = OdakPageLayout.DefaultHeaderDistanceTwips;
    public int FooterDistanceTwips { get; set; } = OdakPageLayout.DefaultFooterDistanceTwips;
    public int FooterLeftIndentTwips { get; set; } = OdakPageLayout.DefaultFooterLeftIndentTwips;

    public static TemplatePageLayoutModel CreateDefault() => new();
}

public sealed class TemplateParameterModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    /// <summary>scalar · table · list · chart (default scalar).</summary>
    public string Kind { get; set; } = "scalar";

    public string DataType { get; set; } = "text";
    public string ValueSourceMode { get; set; } = "manual";

    /// <summary>Catalog ref to <c>dm_data_sources.code</c> (G4); inline <see cref="ValueSource"/> is fallback.</summary>
    [JsonPropertyName("dataSourceRef")]
    public string? DataSourceRef { get; set; }
    public string? DefaultValue { get; set; }
    public string? Format { get; set; }
    public TemplateIncrementalModel? Incremental { get; set; }

    [JsonPropertyName("valueSource")]
    public TemplateValueSourceModel? ValueSource { get; set; }

    /// <summary>DOCX region binding (designer paragraph selection).</summary>
    [JsonPropertyName("docBinding")]
    public TemplateDocBindingModel? DocBinding { get; set; }

    /// <summary>Legacy JSON name — migrated to <see cref="DocBinding"/> on read.</summary>
    [JsonPropertyName("sourceBinding")]
    public TemplateDocBindingModel? SourceBinding
    {
        get => DocBinding;
        set => DocBinding ??= value;
    }

    [JsonPropertyName("contextBinding")]
    public TemplateContextBindingModel? ContextBinding { get; set; }
}

public sealed class TemplateDocBindingModel
{
    public string RegionKind { get; set; } = "paragraph";
    public int ParagraphIndex { get; set; }
    public string? OriginalText { get; set; }
    public int? CharStart { get; set; }
    public int? CharEnd { get; set; }

    /// <summary>0-based index among top-level <c>w:tbl</c> in document body (G2).</summary>
    public int? TableIndex { get; set; }
    public int? HeaderRowIndex { get; set; }
    public int? TemplateRowIndex { get; set; }
}

public sealed class TemplateContextBindingModel
{
    public string Path { get; set; } = string.Empty;
    public string? FallbackPath { get; set; }
    public string? DefaultValue { get; set; }
    public string? Format { get; set; }
}

public sealed class TemplateIncrementalModel
{
    public string Format { get; set; } = string.Empty;
    public int StartValue { get; set; } = 1;
    public int IncrementStep { get; set; } = 1;
    public string? ScopeKey { get; set; }
    public string ResetPolicy { get; set; } = "none";
}

public static class TemplateModelSerializer
{
    public const string CurrentSchemaVersion = "1.6";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static TemplateModelDocument Parse(string? modelJson)
    {
        if (string.IsNullOrWhiteSpace(modelJson))
            return NewEmpty();

        try
        {
            return JsonSerializer.Deserialize<TemplateModelDocument>(modelJson, JsonOptions) ?? NewEmpty();
        }
        catch
        {
            return NewEmpty();
        }
    }

    public static string Serialize(TemplateModelDocument model) =>
        JsonSerializer.Serialize(model, JsonOptions);

    public static TemplateModelDocument NewEmpty() => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        Parameters = new List<TemplateParameterModel>()
    };

    public static TemplateModelDocument BuildWithLetterhead(TemplateLetterheadModel? letterhead)
    {
        var model = NewEmpty();
        if (letterhead is { Enabled: true })
        {
            model.Letterhead = letterhead;
            model.Parameters = EnsureLetterheadParameters(letterhead, model.Parameters);
        }

        return model;
    }

    public static TemplateModelDocument BuildWithFooter(TemplateFooterModel? footer)
    {
        var model = NewEmpty();
        if (footer is { Enabled: true })
            model.Footer = footer;
        return model;
    }

    public static TemplateModelDocument BuildWithBranding(
        TemplateLetterheadModel? letterhead,
        TemplateFooterModel? footer)
    {
        var model = NewEmpty();
        if (letterhead is { Enabled: true })
        {
            model.Letterhead = letterhead;
            model.Parameters = EnsureLetterheadParameters(letterhead, model.Parameters);
        }

        if (footer is { Enabled: true })
            model.Footer = footer;

        model.PageLayout ??= TemplatePageLayoutModel.CreateDefault();

        return model;
    }

    public static List<TemplateParameterModel> RemoveSystemLetterheadParameters(
        IReadOnlyList<TemplateParameterModel> existing) =>
        existing.Where(p => !IsSystemLetterheadKey(p.Key)).ToList();

    public static List<TemplateParameterModel> EnsureLetterheadParameters(
        TemplateLetterheadModel letterhead,
        IReadOnlyList<TemplateParameterModel> existing)
    {
        var list = RemoveSystemLetterheadParameters(existing);

        if (letterhead.ShowDocumentName)
            UpsertSystemParameter(list, CreateDocumentNameParameter());

        if (letterhead.ShowDocumentNumber)
            UpsertSystemParameter(list, CreateDocNoParameter());

        if (letterhead.ShowGeneratedAt)
            UpsertSystemParameter(list, CreateGeneratedAtParameter());

        if (letterhead.ShowCreatePerson)
            UpsertSystemParameter(list, CreateCreatePersonParameter());

        return list;
    }

    public static TemplateLetterheadModel? ToLetterheadModel(Application.Contracts.Templates.TemplateLetterheadDto? dto)
    {
        if (dto is null)
            return null;

        return new TemplateLetterheadModel
        {
            Enabled = dto.Enabled,
            ShowLogo = dto.ShowLogo,
            ShowDocumentName = dto.ShowDocumentName,
            ShowDocumentNumber = dto.ShowDocumentNumber,
            ShowGeneratedAt = dto.ShowGeneratedAt,
            ShowCreatePerson = dto.ShowCreatePerson
        };
    }

    public static Application.Contracts.Templates.TemplateLetterheadDto? ToLetterheadDto(TemplateLetterheadModel? model)
    {
        if (model is null)
            return null;

        return new Application.Contracts.Templates.TemplateLetterheadDto
        {
            Enabled = model.Enabled,
            ShowLogo = model.ShowLogo,
            ShowDocumentName = model.ShowDocumentName,
            ShowDocumentNumber = model.ShowDocumentNumber,
            ShowGeneratedAt = model.ShowGeneratedAt,
            ShowCreatePerson = model.ShowCreatePerson
        };
    }

    public static TemplateFooterModel? ToFooterModel(Application.Contracts.Templates.TemplateFooterDto? dto)
    {
        if (dto is null)
            return null;

        return new TemplateFooterModel
        {
            Enabled = dto.Enabled,
            ShowFormRevision = dto.ShowFormRevision,
            ShowOfficeColumns = dto.ShowOfficeColumns,
            ShowAddresses = dto.ShowAddresses,
            ShowContacts = dto.ShowContacts,
            ShowDividerLine = dto.ShowDividerLine
        };
    }

    public static TemplatePageLayoutModel? ToPageLayoutModel(Application.Contracts.Templates.TemplatePageLayoutDto? dto)
    {
        if (dto is null)
            return null;

        return new TemplatePageLayoutModel
        {
            MarginTopTwips = dto.MarginTopTwips,
            MarginRightTwips = dto.MarginRightTwips,
            MarginBottomTwips = dto.MarginBottomTwips,
            MarginLeftTwips = dto.MarginLeftTwips,
            HeaderDistanceTwips = dto.HeaderDistanceTwips,
            FooterDistanceTwips = dto.FooterDistanceTwips,
            FooterLeftIndentTwips = dto.FooterLeftIndentTwips
        };
    }

    public static Application.Contracts.Templates.TemplatePageLayoutDto? ToPageLayoutDto(TemplatePageLayoutModel? model)
    {
        if (model is null)
            return null;

        return new Application.Contracts.Templates.TemplatePageLayoutDto
        {
            MarginTopTwips = model.MarginTopTwips,
            MarginRightTwips = model.MarginRightTwips,
            MarginBottomTwips = model.MarginBottomTwips,
            MarginLeftTwips = model.MarginLeftTwips,
            HeaderDistanceTwips = model.HeaderDistanceTwips,
            FooterDistanceTwips = model.FooterDistanceTwips,
            FooterLeftIndentTwips = model.FooterLeftIndentTwips
        };
    }

    public static Application.Contracts.Templates.TemplateFooterDto? ToFooterDto(TemplateFooterModel? model)
    {
        if (model is null)
            return null;

        return new Application.Contracts.Templates.TemplateFooterDto
        {
            Enabled = model.Enabled,
            ShowFormRevision = model.ShowFormRevision,
            ShowOfficeColumns = model.ShowOfficeColumns,
            ShowAddresses = model.ShowAddresses,
            ShowContacts = model.ShowContacts,
            ShowDividerLine = model.ShowDividerLine
        };
    }

    private static bool IsSystemLetterheadKey(string key) =>
        string.Equals(key, LetterheadConstants.DocNoKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, LetterheadConstants.GeneratedAtKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, LetterheadConstants.DocumentNameKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, LetterheadConstants.CreatePersonKey, StringComparison.OrdinalIgnoreCase);

    internal static bool IsHeaderBoundLetterheadDocNo(TemplateParameterModel param) =>
        string.Equals(param.Key, LetterheadConstants.DocNoKey, StringComparison.OrdinalIgnoreCase)
        && string.Equals(param.DocBinding?.RegionKind, "header", StringComparison.OrdinalIgnoreCase);

    internal static bool IsHeaderBoundLetterheadCreatePerson(TemplateParameterModel param) =>
        string.Equals(param.Key, LetterheadConstants.CreatePersonKey, StringComparison.OrdinalIgnoreCase)
        && string.Equals(param.DocBinding?.RegionKind, "header", StringComparison.OrdinalIgnoreCase);

    internal static bool IsXlsxLogoParameter(TemplateParameterModel param) =>
        string.Equals(param.Kind, "image", StringComparison.OrdinalIgnoreCase)
        && string.Equals(param.DocBinding?.RegionKind, "xlsxLogo", StringComparison.OrdinalIgnoreCase);

    internal static bool IsPptxLogoParameter(TemplateParameterModel param) =>
        string.Equals(param.Kind, "image", StringComparison.OrdinalIgnoreCase)
        && string.Equals(param.DocBinding?.RegionKind, "pptxLogo", StringComparison.OrdinalIgnoreCase);

    private static void UpsertSystemParameter(List<TemplateParameterModel> list, TemplateParameterModel param)
    {
        var idx = list.FindIndex(p => string.Equals(p.Key, param.Key, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
            list[idx] = param;
        else
            list.Add(param);
    }

    private static TemplateParameterModel CreateDocumentNameParameter() => new()
    {
        Key = LetterheadConstants.DocumentNameKey,
        Label = "Belge Adı",
        DataType = "text",
        ValueSourceMode = "manual",
        DocBinding = new TemplateDocBindingModel
        {
            RegionKind = "header",
            ParagraphIndex = 0,
            OriginalText = LetterheadConstants.DocumentNameToken
        }
    };

    private static TemplateParameterModel CreateDocNoParameter() => new()
    {
        Key = LetterheadConstants.DocNoKey,
        Label = "Belge Numarası",
        DataType = "text",
        ValueSourceMode = "incremental",
        Incremental = new TemplateIncrementalModel
        {
            Format = LetterheadConstants.DefaultDocNoFormat,
            StartValue = 1,
            IncrementStep = 1,
            ScopeKey = LetterheadConstants.DomainScopeKey,
            ResetPolicy = "yearly"
        },
        DocBinding = new TemplateDocBindingModel
        {
            RegionKind = "header",
            ParagraphIndex = 0,
            OriginalText = LetterheadConstants.DocNoToken
        }
    };

    private static TemplateParameterModel CreateGeneratedAtParameter() => new()
    {
        Key = LetterheadConstants.GeneratedAtKey,
        Label = "Oluşturulma Tarihi",
        DataType = "datetime",
        ValueSourceMode = "generated",
        DocBinding = new TemplateDocBindingModel
        {
            RegionKind = "header",
            ParagraphIndex = 0,
            OriginalText = LetterheadConstants.GeneratedAtToken
        }
    };

    private static TemplateParameterModel CreateCreatePersonParameter() => new()
    {
        Key = LetterheadConstants.CreatePersonKey,
        Label = "Oluşturan",
        DataType = "text",
        ValueSourceMode = "manual",
        DocBinding = new TemplateDocBindingModel
        {
            RegionKind = "header",
            ParagraphIndex = 0,
            OriginalText = LetterheadConstants.CreatePersonToken
        }
    };
}

public static class LetterheadConstants
{
    public const string DocNoKey = "docNo";
    public const string PoDocNoKey = "poDocNo";
    public const string GeneratedAtKey = "generatedAt";
    public const string DocumentNameKey = "documentName";
    public const string DocNoToken = "{{docNo}}";
    public const string PoDocNoToken = "{{poDocNo}}";
    public const string GeneratedAtToken = "{{generatedAt}}";
    public const string DocumentNameToken = "{{documentName}}";
    public const string CreatePersonKey = "createPerson";
    public const string CreatePersonToken = "{{createPerson}}";
    public const string DefaultDocNoFormat = "ODK-{yyyy}-{0:D3}";
    public const string DomainScopeKey = "domain";
}
