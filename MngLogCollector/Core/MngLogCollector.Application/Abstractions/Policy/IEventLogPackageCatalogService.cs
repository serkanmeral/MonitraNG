using MngLogCollector.Application.Contracts.Policy;

namespace MngLogCollector.Application.Abstractions.Policy;

public interface IEventLogPackageCatalogService
{
    EventLogPackageCatalogResponse GetCatalog();
}
