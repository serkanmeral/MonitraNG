using MngLogCollector.Application.Contracts.Policy;

namespace MngLogCollector.Application.Services.Policy;

/// <summary>
/// In-memory seed shape for tests. Production uses <see cref="EventLogPackageCatalogService"/>.
/// </summary>
public sealed class BuiltinEventLogPackageCatalogService
{
    public const string CatalogVersion = EventLogPackageCatalogSeed.InitialVersion;

    public EventLogPackageCatalogResponse GetCatalog()
    {
        var docs = EventLogPackageCatalogSeed.CreateSeedDocuments();
        return new EventLogPackageCatalogResponse
        {
            Version = CatalogVersion,
            Source = "collector",
            GeneratedUtc = DateTime.UtcNow,
            Packages = docs.Where(d => d.IsDefault)
                .Select(d => new EventLogPackageDto
                {
                    Name = d.Name,
                    Channel = d.Channel,
                    EventIds = [.. d.EventIds],
                    IsDefault = true
                })
                .ToList(),
            OptionalPackages = docs.Where(d => !d.IsDefault)
                .Select(d => new EventLogPackageDto
                {
                    Name = d.Name,
                    Channel = d.Channel,
                    EventIds = [.. d.EventIds],
                    IsDefault = false
                })
                .ToList()
        };
    }
}
