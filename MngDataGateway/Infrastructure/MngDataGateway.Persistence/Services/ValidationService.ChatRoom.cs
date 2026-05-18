using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MngDataGateway.Application.DTOs.Validation;
using MngDataGateway.Domain.Entities;
using MngDataGateway.Persistence.Extensions;

namespace MngDataGateway.Persistence.Services;

/// <summary>
/// Chat Room (<c>cht_messages</c>) iş kuralları: yazar, DM / konu / grup oda erişimi, Keeper grup üyeliği.
/// </summary>
public partial class ValidationService
{
    private const string ChtMessagesDataset = "cht_messages";
    private const string ChtDirectConversations = "cht_direct_conversations";
    private const string ChtTopicRooms = "cht_topic_rooms";
    private const string ChtTopicMembers = "cht_topic_members";
    private const string ChtGroupChats = "cht_group_chats";

    private async Task<List<ValidationErrorDto>> ValidateChtMessagesBusinessRulesAsync(
        DatasetSchema schema,
        Dictionary<string, object> data,
        string databaseName,
        bool isUpdate,
        string? dataId)
    {
        var errors = new List<ValidationErrorDto>();
        if (!string.Equals(schema.name, ChtMessagesDataset, StringComparison.OrdinalIgnoreCase))
            return errors;

        var currentUserId = _mongoContextService.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            errors.Add(new ValidationErrorDto
            {
                Field = "_auth",
                Message = "cht_messages doğrulaması için kullanıcı kimliği (JWT) gerekir.",
                Value = null
            });
            return errors;
        }

        Dictionary<string, object>? existing = null;
        if (isUpdate && !string.IsNullOrWhiteSpace(dataId))
        {
            try
            {
                var db = _mongoClient.GetDatabase(databaseName);
                var coll = db.GetCollection<BsonDocument>(ChtMessagesDataset);
                var doc = await coll.Find(Builders<BsonDocument>.Filter.Eq("__dataId", dataId)).FirstOrDefaultAsync();
                existing = doc?.ToDictionary();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "cht_messages mevcut kayıt okunamadı: {DataId}", dataId);
                errors.Add(new ValidationErrorDto
                {
                    Field = "__dataId",
                    Message = "Mesaj kaydı yüklenemedi.",
                    Value = dataId
                });
                return errors;
            }
        }

        string? GetMerged(string key)
        {
            if (data.TryGetValue(key, out var v) && v != null)
                return CoerceToTrimmedString(v);
            if (existing != null && existing.TryGetValue(key, out var ev) && ev != null)
                return CoerceToTrimmedString(ev);
            return null;
        }

        var roomKind = GetMerged("roomKind");
        var roomRecordId = GetMerged("roomRecordId");
        var authorPersonId = GetMerged("authorPersonId");

        if (string.IsNullOrWhiteSpace(roomKind))
            errors.Add(new ValidationErrorDto { Field = "roomKind", Message = "roomKind gerekli.", Value = roomKind });
        if (string.IsNullOrWhiteSpace(roomRecordId))
            errors.Add(new ValidationErrorDto { Field = "roomRecordId", Message = "roomRecordId gerekli.", Value = roomRecordId });
        if (string.IsNullOrWhiteSpace(authorPersonId))
            errors.Add(new ValidationErrorDto { Field = "authorPersonId", Message = "authorPersonId gerekli.", Value = authorPersonId });

        if (errors.Any())
            return errors;

        if (!string.Equals(authorPersonId, currentUserId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ValidationErrorDto
            {
                Field = "authorPersonId",
                Message = "authorPersonId, oturum açmış kullanıcı ile eşleşmelidir.",
                Value = authorPersonId
            });
            return errors;
        }

        var domainCtx = await ResolveKeeperDomainContextAsync();
        if (domainCtx == null)
        {
            errors.Add(new ValidationErrorDto
            {
                Field = "_domain",
                Message = "Keeper / domain bağlamı çözülemedi (domain_id veya domain_name).",
                Value = null
            });
            return errors;
        }

        var (domainId, keeperTenantDbName) = domainCtx.Value;

        try
        {
            switch (roomKind!.Trim().ToLowerInvariant())
            {
                case "direct":
                    await ValidateChtDirectRoomAsync(databaseName, roomRecordId!, authorPersonId!, errors);
                    break;
                case "topic":
                    await ValidateChtTopicRoomAsync(databaseName, roomRecordId!, authorPersonId!, errors);
                    break;
                case "group":
                    await ValidateChtGroupRoomAsync(databaseName, roomRecordId!, domainId, keeperTenantDbName, authorPersonId!, errors);
                    break;
                default:
                    errors.Add(new ValidationErrorDto
                    {
                        Field = "roomKind",
                        Message = "roomKind 'direct', 'topic' veya 'group' olmalıdır.",
                        Value = roomKind
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "cht_messages oda doğrulaması başarısız: {RoomKind} {RoomRecordId}", roomKind, roomRecordId);
            errors.Add(new ValidationErrorDto
            {
                Field = "roomRecordId",
                Message = "Oda erişim doğrulaması sırasında hata oluştu.",
                Value = roomRecordId
            });
        }

        return errors;
    }

    private async Task<(string domainId, string keeperTenantDb)?> ResolveKeeperDomainContextAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var domainIdClaim = user?.FindFirst("domain_id")?.Value?.Trim();
        if (!string.IsNullOrEmpty(domainIdClaim))
        {
            var dbName = await GetKeeperTenantDatabaseNameForDomainIdAsync(domainIdClaim);
            if (!string.IsNullOrEmpty(dbName))
                return (domainIdClaim, dbName);
        }

        var domainName = _mongoContextService.GetCurrentDomainName()?.Trim();
        if (string.IsNullOrEmpty(domainName))
            return null;

        var registryName = _configuration["MngDataGatewaySettings:MongoDB:MngKeeperDatabaseName"]
                           ?? _configuration["MongoDB:MngKeeperDatabaseName"]
                           ?? "mngkeeper";
        var registry = _mongoClient.GetDatabase(registryName);
        var domains = registry.GetCollection<BsonDocument>("domains");
        var doc = await domains.Find(Builders<BsonDocument>.Filter.Eq("name", domainName)).FirstOrDefaultAsync();
        if (doc == null)
            return null;

        var id = doc.Contains("_id") && doc["_id"].IsObjectId ? doc["_id"].AsObjectId.ToString() : doc["_id"].ToString();
        var databaseName = doc.GetValue("databaseName", BsonNull.Value);
        var tenantDb = databaseName.IsBsonNull ? null : databaseName.AsString;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(tenantDb))
            return null;

        return (id, tenantDb);
    }

    private async Task<string?> GetKeeperTenantDatabaseNameForDomainIdAsync(string domainId)
    {
        if (string.IsNullOrWhiteSpace(domainId) || !ObjectId.TryParse(domainId, out var oid))
            return null;

        var registryName = _configuration["MngDataGatewaySettings:MongoDB:MngKeeperDatabaseName"]
                           ?? _configuration["MongoDB:MngKeeperDatabaseName"]
                           ?? "mngkeeper";
        var registry = _mongoClient.GetDatabase(registryName);
        var domains = registry.GetCollection<BsonDocument>("domains");
        var doc = await domains.Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefaultAsync();
        if (doc == null)
            return null;
        var databaseName = doc.GetValue("databaseName", BsonNull.Value);
        return databaseName.IsBsonNull ? null : databaseName.AsString;
    }

    private static bool ChatAliasSetsOverlap(HashSet<string> left, HashSet<string> right) =>
        left.Any(x => right.Contains(x));

    /// <summary>
    /// Keeper <c>@users</c> üzerinden aynı kişiye ait id varyantları (__dataId, keycloakUserId, istekte gelen id).
    /// JWT <c>sub</c> ile <c>participant*</c> alanında Mongo <c>__dataId</c> karışık kullanıldığında DM yazımını mümkün kılar.
    /// </summary>
    private async Task<HashSet<string>> ResolveKeeperUserIdAliasesAsync(string keeperTenantDbName, string? personId)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(personId))
            return set;

        var trimmed = personId.Trim();
        set.Add(trimmed);

        try
        {
            var keeperDb = _mongoClient.GetDatabase(keeperTenantDbName);
            var users = keeperDb.GetCollection<BsonDocument>("@users");
            var f = Builders<BsonDocument>.Filter;
            var filters = new List<FilterDefinition<BsonDocument>>
            {
                f.Eq("__dataId", trimmed),
                f.Eq("keycloakUserId", trimmed)
            };
            if (ObjectId.TryParse(trimmed, out var oid))
                filters.Add(f.Eq("_id", oid));

            var doc = await users.Find(f.Or(filters)).FirstOrDefaultAsync();
            if (doc == null)
                return set;

            var did = CoerceToTrimmedString(doc.GetValue("__dataId", BsonNull.Value));
            var kid = CoerceToTrimmedString(doc.GetValue("keycloakUserId", BsonNull.Value));
            if (!string.IsNullOrEmpty(did))
                set.Add(did);
            if (!string.IsNullOrEmpty(kid))
                set.Add(kid);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ResolveKeeperUserIdAliasesAsync: {PersonId}", personId);
        }

        return set;
    }

    private async Task ValidateChtDirectRoomAsync(
        string databaseName,
        string roomRecordId,
        string authorPersonId,
        List<ValidationErrorDto> errors)
    {
        var db = _mongoClient.GetDatabase(databaseName);
        var coll = db.GetCollection<BsonDocument>(ChtDirectConversations);
        var doc = await coll.Find(Builders<BsonDocument>.Filter.Eq("__dataId", roomRecordId)).FirstOrDefaultAsync();
        if (doc == null)
        {
            errors.Add(new ValidationErrorDto { Field = "roomRecordId", Message = "Birebir konuşma bulunamadı.", Value = roomRecordId });
            return;
        }

        var a = CoerceToTrimmedString(doc.GetValue("participantAId", BsonNull.Value));
        var b = CoerceToTrimmedString(doc.GetValue("participantBId", BsonNull.Value));
        var ok = string.Equals(authorPersonId, a, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(authorPersonId, b, StringComparison.OrdinalIgnoreCase);

        if (!ok)
        {
            var domainCtx = await ResolveKeeperDomainContextAsync();
            if (domainCtx != null)
            {
                var (_, keeperTenant) = domainCtx.Value;
                var authorAliases = await ResolveKeeperUserIdAliasesAsync(keeperTenant, authorPersonId);
                var aAliases = await ResolveKeeperUserIdAliasesAsync(keeperTenant, a);
                var bAliases = await ResolveKeeperUserIdAliasesAsync(keeperTenant, b);
                ok = ChatAliasSetsOverlap(authorAliases, aAliases) || ChatAliasSetsOverlap(authorAliases, bAliases);
            }
        }

        if (!ok)
            errors.Add(new ValidationErrorDto { Field = "roomRecordId", Message = "Bu birebir konuşmaya yazma yetkiniz yok.", Value = roomRecordId });
    }

    private async Task ValidateChtTopicRoomAsync(
        string databaseName,
        string roomRecordId,
        string authorPersonId,
        List<ValidationErrorDto> errors)
    {
        var db = _mongoClient.GetDatabase(databaseName);
        var rooms = db.GetCollection<BsonDocument>(ChtTopicRooms);

        var chain = new List<BsonDocument>();
        var currentId = roomRecordId;
        const int maxDepth = 64;
        for (var i = 0; i < maxDepth; i++)
        {
            var room = await rooms.Find(Builders<BsonDocument>.Filter.Eq("__dataId", currentId)).FirstOrDefaultAsync();
            if (room == null)
            {
                errors.Add(new ValidationErrorDto { Field = "roomRecordId", Message = "Konu odası bulunamadı.", Value = roomRecordId });
                return;
            }

            chain.Add(room);

            if (!room.Contains("parentTopicRoomId") || room["parentTopicRoomId"].IsBsonNull)
                break;

            var parentId = CoerceToTrimmedString(room["parentTopicRoomId"]);
            if (string.IsNullOrEmpty(parentId))
                break;

            if (string.Equals(parentId, currentId, StringComparison.Ordinal))
            {
                errors.Add(new ValidationErrorDto { Field = "roomRecordId", Message = "Konu oda üst zinciri geçersiz.", Value = roomRecordId });
                return;
            }

            currentId = parentId;
        }

        var leaf = chain[0];
        var root = chain[^1];
        var rootId = CoerceToTrimmedString(root.GetValue("__dataId", BsonNull.Value)) ?? CoerceToTrimmedString(root["_id"]);
        var rootOwner = CoerceToTrimmedString(root.GetValue("ownerPersonId", BsonNull.Value));
        var leafOwner = CoerceToTrimmedString(leaf.GetValue("ownerPersonId", BsonNull.Value));

        if (string.Equals(authorPersonId, leafOwner, StringComparison.OrdinalIgnoreCase))
            return;

        if (string.Equals(authorPersonId, rootOwner, StringComparison.OrdinalIgnoreCase))
            return;

        if (string.IsNullOrEmpty(rootId))
        {
            errors.Add(new ValidationErrorDto { Field = "roomRecordId", Message = "Kök konu odası kimliği çözülemedi.", Value = roomRecordId });
            return;
        }

        var members = db.GetCollection<BsonDocument>(ChtTopicMembers);
        var memberFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("topicRoomId", rootId),
            Builders<BsonDocument>.Filter.Eq("memberPersonId", authorPersonId));
        var isMember = await members.Find(memberFilter).AnyAsync();
        if (!isMember)
            errors.Add(new ValidationErrorDto { Field = "roomRecordId", Message = "Bu konu odasına yazma yetkiniz yok.", Value = roomRecordId });
    }

    private async Task ValidateChtGroupRoomAsync(
        string databaseName,
        string roomRecordId,
        string domainId,
        string keeperTenantDbName,
        string authorPersonId,
        List<ValidationErrorDto> errors)
    {
        var db = _mongoClient.GetDatabase(databaseName);
        var coll = db.GetCollection<BsonDocument>(ChtGroupChats);
        var groupChat = await coll.Find(Builders<BsonDocument>.Filter.Eq("__dataId", roomRecordId)).FirstOrDefaultAsync();
        if (groupChat == null)
        {
            errors.Add(new ValidationErrorDto { Field = "roomRecordId", Message = "Grup sohbet kaydı bulunamadı.", Value = roomRecordId });
            return;
        }

        var keycloakGroupId = CoerceToTrimmedString(groupChat.GetValue("keycloakGroupId", BsonNull.Value));
        if (string.IsNullOrEmpty(keycloakGroupId))
        {
            errors.Add(new ValidationErrorDto { Field = "roomRecordId", Message = "Grup sohbet kaydında keycloakGroupId eksik.", Value = roomRecordId });
            return;
        }

        var keeperDb = _mongoClient.GetDatabase(keeperTenantDbName);
        var groupsColl = keeperDb.GetCollection<BsonDocument>("@groups");
        var displayNameCache = CoerceToTrimmedString(groupChat.GetValue("displayNameCache", BsonNull.Value));
        var groupDoc = await FindKeeperGroupDocumentAsync(groupsColl, keycloakGroupId, domainId, displayNameCache);
        if (groupDoc == null)
        {
            errors.Add(new ValidationErrorDto
            {
                Field = "roomRecordId",
                Message =
                    $"Keeper @groups'ta eşleşen grup yok (cht keycloakGroupId: {keycloakGroupId}, domainId: {domainId}" +
                    (string.IsNullOrEmpty(displayNameCache) ? "" : $", displayNameCache: {displayNameCache}") +
                    "). @groups'ta keycloakGroupId, __dataId veya name ile eşleşme denendi; gerekirse @groups.keycloakGroupId'yi Keycloak UUID ile veya cht satırını Keeper __dataId ile hizalayın.",
                Value = roomRecordId
            });
            return;
        }

        var groupName = CoerceToTrimmedString(groupDoc.GetValue("name", BsonNull.Value));
        if (string.IsNullOrEmpty(groupName))
        {
            errors.Add(new ValidationErrorDto { Field = "roomRecordId", Message = "Keeper grup adı okunamadı.", Value = roomRecordId });
            return;
        }

        var usersColl = keeperDb.GetCollection<BsonDocument>("@users");
        var fb = Builders<BsonDocument>.Filter;
        var userOr = new List<FilterDefinition<BsonDocument>>
        {
            fb.Eq("__dataId", authorPersonId),
            fb.Eq("keycloakUserId", authorPersonId)
        };
        if (ObjectId.TryParse(authorPersonId, out var oid))
            userOr.Add(fb.Eq("_id", oid));

        var userDoc = await usersColl.Find(fb.Or(userOr)).FirstOrDefaultAsync();
        if (userDoc == null)
        {
            errors.Add(new ValidationErrorDto { Field = "authorPersonId", Message = "Keeper kullanıcı kaydı bulunamadı.", Value = authorPersonId });
            return;
        }

        var groupsArr = userDoc.GetValue("groups", new BsonArray()).AsBsonArray;
        var names = groupsArr.Select(v => v.IsString ? v.AsString : v.ToString()).ToHashSet(StringComparer.Ordinal);
        if (!names.Contains(groupName))
            errors.Add(new ValidationErrorDto { Field = "roomRecordId", Message = "Bu grup sohbetinin üyesi değilsiniz.", Value = roomRecordId });
    }

    /// <summary>
    /// Keeper <c>@groups</c> eşlemesi: Keycloak UUID ile Keeper <c>__dataId</c> farklı olabiliyor (kullanıcı/userId örneği gibi).
    /// <c>keycloakGroupId</c>, <c>__dataId</c>, <c>_id</c> (24 hex) ve son çare <c>name</c> ≈ <c>displayNameCache</c> denenir.
    /// </summary>
    private static async Task<BsonDocument?> FindKeeperGroupDocumentAsync(
        IMongoCollection<BsonDocument> groupsColl,
        string keycloakGroupId,
        string domainId,
        string? displayNameOrNameHint)
    {
        var fb = Builders<BsonDocument>.Filter;
        var identityOr = GroupIdentityOrFilter(fb, keycloakGroupId);

        var domainParts = new List<FilterDefinition<BsonDocument>> { fb.Eq("domainId", domainId) };
        if (ObjectId.TryParse(domainId, out var domainOid))
            domainParts.Add(fb.Eq("domainId", domainOid));
        var domainOr = fb.Or(domainParts);

        var doc = await groupsColl.Find(fb.And(identityOr, domainOr)).FirstOrDefaultAsync();
        if (doc != null)
            return doc;

        doc = await groupsColl.Find(identityOr).FirstOrDefaultAsync();
        if (doc != null)
            return doc;

        if (string.IsNullOrWhiteSpace(displayNameOrNameHint))
            return null;

        var escaped = Regex.Escape(displayNameOrNameHint.Trim());
        var nameRx = new BsonRegularExpression($"^{escaped}$", "i");
        doc = await groupsColl.Find(fb.And(fb.Regex("name", nameRx), domainOr)).FirstOrDefaultAsync();
        if (doc != null)
            return doc;

        return await groupsColl.Find(fb.Regex("name", nameRx)).FirstOrDefaultAsync();
    }

    /// <summary>
    /// <c>cht_group_chats.keycloakGroupId</c> JWT/Keycloak UUID iken <c>@groups</c> satırında <c>__dataId</c> farklı olabilir;
    /// tersi (Mongo id cht'te) için de <c>keycloakGroupId</c> alanında aranır.
    /// </summary>
    private static FilterDefinition<BsonDocument> GroupIdentityOrFilter(FilterDefinitionBuilder<BsonDocument> fb, string idFromChat)
    {
        var id = idFromChat.Trim();
        var parts = new List<FilterDefinition<BsonDocument>>
        {
            fb.Eq("keycloakGroupId", id),
            fb.Eq("__dataId", id),
        };
        if (ObjectId.TryParse(id, out var oid))
            parts.Add(fb.Eq("_id", oid));
        return fb.Or(parts);
    }

    private static string? CoerceToTrimmedString(object? v)
    {
        if (v == null)
            return null;
        if (v is string s)
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        if (v is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.String => string.IsNullOrWhiteSpace(je.GetString()) ? null : je.GetString()!.Trim(),
                _ => je.ToString().Trim()
            };
        }
        if (v is BsonValue bv)
        {
            if (bv.IsBsonNull)
                return null;
            if (bv.IsString)
                return string.IsNullOrWhiteSpace(bv.AsString) ? null : bv.AsString.Trim();
            if (bv.IsObjectId)
                return bv.AsObjectId.ToString();
        }

        if (v is IEnumerable en && v is not string && v is not byte[])
        {
            foreach (var x in en)
            {
                var inner = CoerceToTrimmedString(x);
                if (!string.IsNullOrEmpty(inner))
                    return inner;
            }

            return null;
        }

        var t = Convert.ToString(v, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(t) ? null : t.Trim();
    }
}
