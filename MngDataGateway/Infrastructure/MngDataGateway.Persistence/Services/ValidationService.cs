using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MngDataGateway.Application.DTOs.Validation;
using MngDataGateway.Application.Services;
using MngDataGateway.Domain.Entities;

namespace MngDataGateway.Persistence.Services
{
    /// <summary>
    /// Data validation service implementation
    /// </summary>
    public class ValidationService : IValidationService
    {
        private readonly ILogger<ValidationService> _logger;
        private readonly IMongoClient _mongoClient;

        public ValidationService(
            ILogger<ValidationService> logger,
            IMongoClient mongoClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
        }

        public async Task<ValidationResult> ValidateDataAsync(
            DatasetSchema schema,
            Dictionary<string, object> data,
            string databaseName,
            bool isUpdate = false,
            string? dataId = null)
        {
            var errors = new List<ValidationErrorDto>();

            // 1. Mandatory fields (skip for update if not provided)
            if (!isUpdate)
            {
                var mandatoryResult = ValidateMandatoryFields(schema, data);
                if (!mandatoryResult.IsValid)
                    errors.AddRange(mandatoryResult.Errors);
            }

            // 2. Field types
            var typeResult = ValidateFieldTypes(schema, data);
            if (!typeResult.IsValid)
                errors.AddRange(typeResult.Errors);

            // 3. ForceSchema (strict mode)
            var schemaResult = ValidateForceSchema(schema, data);
            if (!schemaResult.IsValid)
                errors.AddRange(schemaResult.Errors);

            // 4. Unique constraints
            var uniqueResult = await ValidateUniqueConstraintsAsync(schema, data, databaseName, dataId);
            if (!uniqueResult.IsValid)
                errors.AddRange(uniqueResult.Errors);

            return errors.Any()
                ? ValidationResult.Failure(errors)
                : ValidationResult.Success();
        }

        public ValidationResult ValidateMandatoryFields(DatasetSchema schema, Dictionary<string, object> data)
        {
            var errors = new List<ValidationErrorDto>();

            foreach (var field in schema.fields.Where(f => f.mandatory))
            {
                // Skip incremental fields (auto-generated)
                if (field.fieldType == "incremental")
                    continue;

                if (!data.ContainsKey(field.name) || data[field.name] == null)
                {
                    errors.Add(new ValidationErrorDto
                    {
                        Field = field.name,
                        Message = $"Field '{field.name}' is required",
                        Value = null
                    });
                }
                else
                {
                    // Check for empty strings
                    var value = data[field.name];
                    if (value is string strValue && string.IsNullOrWhiteSpace(strValue))
                    {
                        errors.Add(new ValidationErrorDto
                        {
                            Field = field.name,
                            Message = $"Field '{field.name}' cannot be empty",
                            Value = strValue
                        });
                    }
                }
            }

            return errors.Any()
                ? ValidationResult.Failure(errors)
                : ValidationResult.Success();
        }

        public ValidationResult ValidateFieldTypes(DatasetSchema schema, Dictionary<string, object> data)
        {
            var errors = new List<ValidationErrorDto>();

            foreach (var field in schema.fields)
            {
                if (!data.ContainsKey(field.name))
                    continue;

                var value = data[field.name];
                if (value == null)
                    continue;

                // Skip validation for incremental fields (auto-generated)
                if (field.fieldType == "incremental")
                    continue;

                // Check if array
                if (field.isArray)
                {
                    if (value is not System.Collections.IEnumerable || value is string)
                    {
                        errors.Add(new ValidationErrorDto
                        {
                            Field = field.name,
                            Message = $"Field '{field.name}' must be an array",
                            Value = value
                        });
                    }
                    continue;
                }

                // Validate type - more lenient for JSON deserialization
                var isValid = field.fieldType switch
                {
                    "text" => value is string || value.GetType().Name == "String",
                    "number" => IsNumber(value) || IsNumericType(value),
                    "bool" => value is bool || value.GetType().Name == "Boolean",
                    "datetime" => IsDateTime(value),
                    "object" => true, // Accept any object/dictionary
                    "relation" => value is string,
                    "persons" => true, // Accept string or array
                    "personGroups" => true, // Accept string or array
                    _ => true // Unknown types allowed
                };

                if (!isValid)
                {
                    errors.Add(new ValidationErrorDto
                    {
                        Field = field.name,
                        Message = $"Field '{field.name}' must be of type '{field.fieldType}' (received: {value.GetType().Name})",
                        Value = value
                    });
                }
            }

            return errors.Any()
                ? ValidationResult.Failure(errors)
                : ValidationResult.Success();
        }

        public ValidationResult ValidateForceSchema(DatasetSchema schema, Dictionary<string, object> data)
        {
            // If forceSchema is false, allow extra fields
            if (!schema.forceSchema)
                return ValidationResult.Success();

            var errors = new List<ValidationErrorDto>();
            var definedFields = schema.fields.Select(f => f.name).ToHashSet();

            // Reserved fields (always allowed)
            definedFields.Add("__dataId");
            definedFields.Add("__history");
            definedFields.Add("__isDeleted");
            definedFields.Add("__deleteInfo");
            definedFields.Add("__restoreInfo");

            foreach (var key in data.Keys)
            {
                if (!definedFields.Contains(key))
                {
                    errors.Add(new ValidationErrorDto
                    {
                        Field = key,
                        Message = $"Field '{key}' is not defined in schema (forceSchema: strict)",
                        Value = data[key]
                    });
                }
            }

            return errors.Any()
                ? ValidationResult.Failure(errors)
                : ValidationResult.Success();
        }

        public async Task<ValidationResult> ValidateUniqueConstraintsAsync(
            DatasetSchema schema,
            Dictionary<string, object> data,
            string databaseName,
            string? excludeDataId = null)
        {
            var errors = new List<ValidationErrorDto>();
            var uniqueFields = schema.fields.Where(f => f.unique).ToList();

            if (!uniqueFields.Any())
                return ValidationResult.Success();

            try
            {
                var database = _mongoClient.GetDatabase(databaseName);
                var collection = database.GetCollection<BsonDocument>(schema.CollectionName);

                foreach (var field in uniqueFields)
                {
                    if (!data.ContainsKey(field.name))
                        continue;

                    var value = data[field.name];
                    if (value == null)
                        continue;

                    // Build filter
                    var filterBuilder = Builders<BsonDocument>.Filter;
                    var filter = filterBuilder.Eq(field.name, BsonValue.Create(value));

                    // Exclude current document if updating
                    if (!string.IsNullOrEmpty(excludeDataId))
                    {
                        filter = filterBuilder.And(
                            filter,
                            filterBuilder.Ne("__dataId", excludeDataId)
                        );
                    }

                    // Exclude soft-deleted
                    filter = filterBuilder.And(
                        filter,
                        filterBuilder.Or(
                            filterBuilder.Eq("__isDeleted", false),
                            filterBuilder.Exists("__isDeleted", false)
                        )
                    );

                    var existingDoc = await collection.Find(filter).FirstOrDefaultAsync();
                    if (existingDoc != null)
                    {
                        errors.Add(new ValidationErrorDto
                        {
                            Field = field.name,
                            Message = $"Field '{field.name}' must be unique. Value '{value}' already exists",
                            Value = value
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating unique constraints for dataset {DatasetName}", schema.DatasetName);
                errors.Add(new ValidationErrorDto
                {
                    Field = "_system",
                    Message = "Failed to validate unique constraints",
                    Value = ex.Message
                });
            }

            return errors.Any()
                ? ValidationResult.Failure(errors)
                : ValidationResult.Success();
        }

        private static bool IsNumber(object value)
        {
            return value is int or long or float or double or decimal or 
                   short or byte or sbyte or ushort or uint or ulong;
        }

        private static bool IsNumericType(object value)
        {
            // More lenient - check type name too (for JSON deserialization)
            var typeName = value.GetType().Name;
            return IsNumber(value) || 
                   typeName == "Int32" || typeName == "Int64" || typeName == "Double" || 
                   typeName == "Decimal" || typeName == "Single";
        }

        private static bool IsDateTime(object value)
        {
            if (value is DateTime)
                return true;

            if (value is string strValue)
                return DateTime.TryParse(strValue, out _);

            return false;
        }
    }
}

