using MngKeeper.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MngKeeper.Infrastructure.Persistence.Repositories;

/// <summary>
/// provisioningSource filtresi — legacy kayıtlarda alan yoksa yerel (Local) kabul edilir.
/// </summary>
internal static class ProvisioningSourceMongoFilters
{
    public static FilterDefinition<BsonDocument> Equals(
        FilterDefinitionBuilder<BsonDocument> builder,
        UserProvisioningSource source)
    {
        if (source == UserProvisioningSource.Local)
        {
            return builder.Or(
                builder.Eq("provisioningSource", (int)UserProvisioningSource.Local),
                builder.Exists("provisioningSource", false));
        }

        return builder.Eq("provisioningSource", (int)source);
    }
}
