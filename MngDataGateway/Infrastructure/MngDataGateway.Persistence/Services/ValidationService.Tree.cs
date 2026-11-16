using MongoDB.Bson;
using MongoDB.Driver;
using MngDataGateway.Application.DTOs.Validation;
using MngDataGateway.Domain.Constants;
using MngDataGateway.Domain.Entities;
using MngDataGateway.Domain.Trees;

namespace MngDataGateway.Persistence.Services;

public partial class ValidationService
{
    public async Task<ValidationResult> ValidateTreeDeleteAsync(DatasetSchema schema, string databaseName, string dataId)
    {
        var tree = schema.tree;
        if (tree == null || string.IsNullOrWhiteSpace(tree.parentField) || string.IsNullOrWhiteSpace(dataId))
            return ValidationResult.Success();

        var collection = _mongoClient.GetDatabase(databaseName).GetCollection<BsonDocument>(schema.CollectionName);
        var childCount = await collection.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq(tree.parentField, dataId));

        if (childCount == 0)
            return ValidationResult.Success();

        return ValidationResult.Failure(new List<ValidationErrorDto>
        {
            new()
            {
                Field = tree.parentField,
                Code = ErrorCodes.VALIDATION_TREE_CHILDREN,
                Message = "Alt mevkisi olan kayıt silinmez.",
                Value = dataId
            }
        });
    }

    private async Task<ValidationResult> ValidateTreeAsync(
        DatasetSchema schema,
        Dictionary<string, object> data,
        string databaseName,
        bool isUpdate,
        string? dataId)
    {
        var tree = schema.tree;
        if (tree == null || string.IsNullOrWhiteSpace(tree.parentField))
            return ValidationResult.Success();

        var errors = new List<ValidationErrorDto>();
        var parentTouched = data.ContainsKey(tree.parentField);
        var occupantTouched = !string.IsNullOrWhiteSpace(tree.occupantField) && data.ContainsKey(tree.occupantField);
        var staffField = tree.staffField;
        var staffTouched = !string.IsNullOrWhiteSpace(staffField) && data.ContainsKey(staffField);
        var creatingRoot = !isUpdate && !parentTouched;

        if (!parentTouched && !occupantTouched && !staffTouched && !creatingRoot)
            return ValidationResult.Success();

        string? parentId = null;
        if (parentTouched && !TreeWriteRules.TryNormalizeReference(data[tree.parentField], out parentId, out var parentMany))
        {
            errors.Add(TreeError(
                tree.parentField,
                parentMany ? ErrorCodes.VALIDATION_TREE_PARENT : ErrorCodes.VALIDATION_TREE_PARENT,
                parentMany ? "Mevki tek üst alır." : "Üst mevki kimliği okunamadı.",
                data[tree.parentField]));
        }

        string? occupantId = null;
        if (occupantTouched && !TreeWriteRules.TryNormalizeReference(data[tree.occupantField!], out occupantId, out var occupantMany))
        {
            errors.Add(TreeError(
                tree.occupantField!,
                ErrorCodes.VALIDATION_TREE_OCCUPANT,
                occupantMany ? "Mevki tek kişi alır." : "Oturan kişi okunamadı.",
                data[tree.occupantField!]));
        }

        var staffIds = new List<string>();
        if (staffTouched && !TreeWriteRules.TryReadPersonIds(data[staffField!], out staffIds, out var staffDuplicate))
        {
            errors.Add(TreeError(
                staffField!,
                ErrorCodes.VALIDATION_TREE_OCCUPANT,
                staffDuplicate ? "Bu kullanıcı listede iki kez seçili." : "Çalışan kişi okunamadı.",
                data[staffField!]));
        }

        if (errors.Count > 0)
            return ValidationResult.Failure(errors);

        if (!parentTouched && !creatingRoot && occupantId == null && staffIds.Count == 0)
            return ValidationResult.Success();

        var collection = _mongoClient.GetDatabase(databaseName).GetCollection<BsonDocument>(schema.CollectionName);
        var projection = Builders<BsonDocument>.Projection
            .Include("__dataId")
            .Include(tree.parentField);
        if (!string.IsNullOrWhiteSpace(tree.occupantField))
            projection = projection.Include(tree.occupantField);
        if (!string.IsNullOrWhiteSpace(staffField))
            projection = projection.Include(staffField);

        var rows = await collection.Find(FilterDefinition<BsonDocument>.Empty).Project(projection).ToListAsync();
        var parents = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!row.Contains("__dataId") || !row["__dataId"].IsString)
                continue;
            parents[row["__dataId"].AsString] = ReadStoredReference(row, tree.parentField);
        }

        if (parentTouched || creatingRoot)
        {
            if (parentId == null && tree.singleRoot)
            {
                var otherRoots = parents.Count(pair => pair.Value == null && !string.Equals(pair.Key, dataId, StringComparison.Ordinal));
                if (otherRoots > 0)
                {
                    errors.Add(TreeError(tree.parentField, ErrorCodes.VALIDATION_TREE_ROOT, "Kök mevki zaten var.", null));
                }
            }
            else if (parentId != null)
            {
                if (!parents.ContainsKey(parentId))
                {
                    errors.Add(TreeError(tree.parentField, ErrorCodes.VALIDATION_TREE_PARENT, "Üst mevki bulunamadı.", parentId));
                }
                else
                {
                    var walk = TreeWriteRules.WalkParents(
                        isUpdate ? dataId : null,
                        parentId,
                        id => parents.TryGetValue(id, out var next) ? next : null);

                    if (walk == TreeWalkStatus.Cycle)
                        errors.Add(TreeError(tree.parentField, ErrorCodes.VALIDATION_TREE_CYCLE, "Üst mevki döngü oluşturuyor.", parentId));
                    else if (walk == TreeWalkStatus.TooDeep)
                        errors.Add(TreeError(tree.parentField, ErrorCodes.VALIDATION_TREE_DEPTH, "Mevki zinciri çok uzun.", parentId));
                }
            }
        }

        if ((occupantTouched && occupantId != null) || staffIds.Count > 0)
            AddStaffConflicts(rows, isUpdate, dataId, tree.occupantField, staffField, occupantTouched, occupantId, staffTouched, staffIds, errors);

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }

    private static void AddStaffConflicts(
        List<BsonDocument> rows,
        bool isUpdate,
        string? dataId,
        string? occupantField,
        string? staffField,
        bool occupantTouched,
        string? occupantId,
        bool staffTouched,
        List<string> staffIds,
        List<ValidationErrorDto> errors)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        string? storedOccupant = null;
        var storedStaff = new List<string>();

        foreach (var row in rows)
        {
            if (!row.Contains("__dataId") || !row["__dataId"].IsString)
                continue;

            var rowId = row["__dataId"].AsString;
            var rowOccupant = string.IsNullOrWhiteSpace(occupantField) ? null : ReadStoredReference(row, occupantField);
            var rowStaff = ReadStoredPeople(row, staffField);
            if (isUpdate && string.Equals(rowId, dataId, StringComparison.Ordinal))
            {
                storedOccupant = rowOccupant;
                storedStaff = rowStaff;
                continue;
            }

            if (!string.IsNullOrEmpty(rowOccupant))
                taken.Add(rowOccupant);
            foreach (var person in rowStaff)
                taken.Add(person);
        }

        var selfStaff = staffTouched ? staffIds : storedStaff;
        var selfOccupant = occupantTouched ? occupantId : storedOccupant;

        if (occupantTouched && occupantId != null && !string.IsNullOrWhiteSpace(occupantField))
        {
            if (selfStaff.Exists(id => string.Equals(id, occupantId, StringComparison.Ordinal)))
                errors.Add(TreeError(occupantField, ErrorCodes.VALIDATION_TREE_OCCUPANT, "Oturan kişi çalışanlar arasında olamaz.", occupantId));
            else if (taken.Contains(occupantId))
                errors.Add(TreeError(occupantField, ErrorCodes.VALIDATION_TREE_OCCUPANT, "Bu kullanıcı başka bir mevkide oturuyor.", occupantId));
        }

        if (staffIds.Count == 0 || string.IsNullOrWhiteSpace(staffField))
            return;

        foreach (var person in staffIds)
        {
            if (!string.IsNullOrEmpty(selfOccupant) && string.Equals(person, selfOccupant, StringComparison.Ordinal))
            {
                errors.Add(TreeError(staffField, ErrorCodes.VALIDATION_TREE_OCCUPANT, "Oturan kişi çalışanlar arasında olamaz.", person));
                return;
            }

            if (taken.Contains(person))
            {
                errors.Add(TreeError(staffField, ErrorCodes.VALIDATION_TREE_OCCUPANT, "Bu kullanıcı başka bir mevkide oturuyor.", person));
                return;
            }
        }
    }

    private static string? ReadStoredReference(BsonDocument document, string field)
    {
        if (!document.Contains(field) || document[field].IsBsonNull)
            return null;

        if (document[field].IsString)
        {
            var text = document[field].AsString;
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        if (document[field].IsBsonArray && document[field].AsBsonArray.Count == 1 && document[field].AsBsonArray[0].IsString)
        {
            var text = document[field].AsBsonArray[0].AsString;
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        return null;
    }

    private static List<string> ReadStoredPeople(BsonDocument document, string? field)
    {
        var people = new List<string>();
        if (string.IsNullOrWhiteSpace(field) || !document.Contains(field) || document[field].IsBsonNull)
            return people;

        var value = document[field];
        if (value.IsString)
        {
            var text = value.AsString.Trim();
            if (text.Length > 0)
                people.Add(text);
            return people;
        }

        if (!value.IsBsonArray)
            return people;

        foreach (var item in value.AsBsonArray)
        {
            if (!item.IsString)
                continue;
            var text = item.AsString.Trim();
            if (text.Length > 0)
                people.Add(text);
        }

        return people;
    }

    private static ValidationErrorDto TreeError(string field, string code, string message, object? value) =>
        new()
        {
            Field = field,
            Code = code,
            Message = message,
            Value = value
        };
}
