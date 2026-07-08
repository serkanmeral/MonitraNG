namespace MngDocument.Application.Contracts.DataSources;

public enum DataSourceResultShape
{
    Single,
    Rows,
    Scalar
}

public sealed class DataSourceExecutionResult
{
    public DataSourceResultShape Shape { get; init; }
    public IReadOnlyDictionary<string, object?>? Single { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
        = Array.Empty<IReadOnlyDictionary<string, object?>>();
    public object? Scalar { get; init; }
}
