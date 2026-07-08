using MngDocument.Application.Contracts.DataSources;
using MngDocument.Application.Contracts.Generation;
using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Application.Interfaces;

/// <summary>Executes declarative data source definitions (DG first; Keeper/HTTP later).</summary>
public interface IDataSourceExecutor
{
    Task<DataSourceExecutionResult> ExecuteAsync(
        TemplateValueSourceModel source,
        ParameterResolutionContext context,
        CancellationToken ct = default);
}
