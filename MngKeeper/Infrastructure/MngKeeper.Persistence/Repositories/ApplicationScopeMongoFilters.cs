using MngKeeper.Application.Directory;
using MngKeeper.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MngKeeper.Infrastructure.Persistence.Repositories;

/// <summary>
/// Legacy kayıtlar (alan yok) için provisioningSource ile birlikte uygulama kapsamı filtresi.
/// </summary>
internal static class ApplicationScopeMongoFilters
{
    public static FilterDefinition<BsonDocument> IncludeInApplicationEquals(bool value)
    {
        var builder = Builders<BsonDocument>.Filter;
        if (value)
        {
            var explicitTrue = builder.Eq(ApplicationScopeDefaults.BsonField, true);
            var legacyLocalVisible = builder.And(
                builder.Or(
                    builder.Exists(ApplicationScopeDefaults.BsonField, false),
                    builder.Eq(ApplicationScopeDefaults.BsonField, BsonNull.Value)),
                builder.Or(
                    builder.Exists("provisioningSource", false),
                    builder.Eq("provisioningSource", (int)UserProvisioningSource.Local)));
            return builder.Or(explicitTrue, legacyLocalVisible);
        }

        var explicitFalse = builder.Eq(ApplicationScopeDefaults.BsonField, false);
        var legacyDirectoryHidden = builder.And(
            builder.Or(
                builder.Exists(ApplicationScopeDefaults.BsonField, false),
                builder.Eq(ApplicationScopeDefaults.BsonField, BsonNull.Value)),
            builder.Eq("provisioningSource", (int)UserProvisioningSource.Directory));
        return builder.Or(explicitFalse, legacyDirectoryHidden);
    }
}
