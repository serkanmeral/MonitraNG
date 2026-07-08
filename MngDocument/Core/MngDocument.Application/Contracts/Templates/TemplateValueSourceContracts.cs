namespace MngDocument.Application.Contracts.Templates;

/// <summary>Declarative data source binding on a template parameter (schema 1.6+).</summary>
public sealed class TemplateValueSourceModel
{
    /// <summary>context · getById · queryPage · namedQuery · manual · static · incremental · generated</summary>
    public string Mode { get; set; } = "manual";

    public string Provider { get; set; } = "dg";
    public string? Dataset { get; set; }
    public string? QueryName { get; set; }

    /// <summary>getById id template, e.g. {{runtime.contextId}}</summary>
    public string? IdFrom { get; set; }

    /// <summary>Query string suffix for queryPage (sort, limit, …).</summary>
    public string? Query { get; set; }

    /// <summary>Match filter for queryPage — values may contain runtime tokens.</summary>
    public Dictionary<string, object?>? Match { get; set; }

    /// <summary>Named query parameters — values may contain runtime tokens.</summary>
    public Dictionary<string, object?>? Parameters { get; set; }

    /// <summary>Legacy context path when mode=context.</summary>
    public string? Path { get; set; }
    public string? FallbackPath { get; set; }

    /// <summary>Scalar: field path on single/ first row. Table: column definitions.</summary>
    public string? Field { get; set; }
    public List<TemplateTableColumnModel>? Columns { get; set; }

    public string? Format { get; set; }
    public string? DefaultValue { get; set; }
}

public sealed class TemplateTableColumnModel
{
    public string SourceField { get; set; } = string.Empty;
    public string? Header { get; set; }
    public string? Format { get; set; }
}
