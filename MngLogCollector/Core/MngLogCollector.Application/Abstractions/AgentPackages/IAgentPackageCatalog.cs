using MngLogCollector.Application.Contracts.AgentPackages;

namespace MngLogCollector.Application.Abstractions.AgentPackages;

public interface IAgentPackageCatalog
{
    AgentPackageCatalogResponse GetCatalog(string? requestBaseUrl);

    AgentPackageFile? GetFile(string id);
}
