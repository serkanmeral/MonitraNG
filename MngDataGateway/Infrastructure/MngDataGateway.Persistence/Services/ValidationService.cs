using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ValidationService(
            ILogger<ValidationService> logger,
            IMongoClient mongoClient,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public async Task<ValidationResult> ValidateDataAsync(
            DatasetSchema schema,
            Dictionary<string, object> data,
            string databaseName,
            bool isUpdate = false,
            string? dataId = null)
        {
            var errors = new List<ValidationErrorDto>();

            // Aşama 1: Temel validasyonlar (hızlı, MongoDB'ye gitmeden)
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

            // 4. Field-level validation (min/max, regex, range, etc.)
            var fieldLevelResult = ValidateFieldLevelRules(schema, data);
            if (!fieldLevelResult.IsValid)
                errors.AddRange(fieldLevelResult.Errors);

            // Aşama 2: Database validasyonları (MongoDB sorgusu gerektirir)
            // 5. Unique constraints
            var uniqueResult = await ValidateUniqueConstraintsAsync(schema, data, databaseName, dataId);
            if (!uniqueResult.IsValid)
                errors.AddRange(uniqueResult.Errors);

            // Aşama 3: Expression-based validation (karmaşık kurallar)
            // 6. Expression-based validation
            var expressionResult = ValidateExpressions(schema, data, isUpdate);
            if (!expressionResult.IsValid)
                errors.AddRange(expressionResult.Errors);

            // Aşama 4: HTTP validation (external validation endpoints)
            // 7. HTTP-based validation
            var authorizationHeader = GetAuthorizationHeader();
            var httpResult = await ValidateHttpValidationsAsync(schema, data, isUpdate, authorizationHeader);
            if (!httpResult.IsValid)
                errors.AddRange(httpResult.Errors);

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

        /// <summary>
        /// Validate field-level validation rules (min/max, regex, range, etc.)
        /// </summary>
        public ValidationResult ValidateFieldLevelRules(DatasetSchema schema, Dictionary<string, object> data)
        {
            var errors = new List<ValidationErrorDto>();

            foreach (var field in schema.fields)
            {
                if (field.validation == null)
                    continue;

                if (!data.ContainsKey(field.name))
                    continue;

                var value = data[field.name];
                if (value == null)
                    continue;

                var rules = field.validation;
                var fieldErrors = ValidateFieldRules(field, value, rules);
                errors.AddRange(fieldErrors);
            }

            return errors.Any()
                ? ValidationResult.Failure(errors)
                : ValidationResult.Success();
        }

        /// <summary>
        /// Validate a single field against its validation rules
        /// </summary>
        private List<ValidationErrorDto> ValidateFieldRules(FieldDefinition field, object value, FieldValidationRules rules)
        {
            var errors = new List<ValidationErrorDto>();

            // Number validations
            if (field.fieldType == "number" && IsNumber(value))
            {
                var numValue = Convert.ToDouble(value);

                if (rules.min.HasValue && numValue < rules.min.Value)
                {
                    errors.Add(new ValidationErrorDto
                    {
                        Field = field.name,
                        Message = rules.message ?? $"Field '{field.name}' must be at least {rules.min.Value}",
                        Value = value
                    });
                }

                if (rules.max.HasValue && numValue > rules.max.Value)
                {
                    errors.Add(new ValidationErrorDto
                    {
                        Field = field.name,
                        Message = rules.message ?? $"Field '{field.name}' must be at most {rules.max.Value}",
                        Value = value
                    });
                }
            }

            // Text validations
            if (field.fieldType == "text" && value is string strValue)
            {
                if (rules.minLength.HasValue && strValue.Length < rules.minLength.Value)
                {
                    errors.Add(new ValidationErrorDto
                    {
                        Field = field.name,
                        Message = rules.message ?? $"Field '{field.name}' must be at least {rules.minLength.Value} characters",
                        Value = value
                    });
                }

                if (rules.maxLength.HasValue && strValue.Length > rules.maxLength.Value)
                {
                    errors.Add(new ValidationErrorDto
                    {
                        Field = field.name,
                        Message = rules.message ?? $"Field '{field.name}' must be at most {rules.maxLength.Value} characters",
                        Value = value
                    });
                }

                if (!string.IsNullOrEmpty(rules.pattern))
                {
                    try
                    {
                        var regex = new System.Text.RegularExpressions.Regex(rules.pattern);
                        if (!regex.IsMatch(strValue))
                        {
                            errors.Add(new ValidationErrorDto
                            {
                                Field = field.name,
                                Message = rules.message ?? $"Field '{field.name}' does not match the required pattern",
                                Value = value
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Invalid regex pattern '{Pattern}' for field '{FieldName}'", rules.pattern, field.name);
                    }
                }
            }

            // Array validations
            if (field.isArray && value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var items = enumerable.Cast<object>().ToList();

                if (rules.minItems.HasValue && items.Count < rules.minItems.Value)
                {
                    errors.Add(new ValidationErrorDto
                    {
                        Field = field.name,
                        Message = rules.message ?? $"Field '{field.name}' must have at least {rules.minItems.Value} items",
                        Value = value
                    });
                }

                if (rules.maxItems.HasValue && items.Count > rules.maxItems.Value)
                {
                    errors.Add(new ValidationErrorDto
                    {
                        Field = field.name,
                        Message = rules.message ?? $"Field '{field.name}' must have at most {rules.maxItems.Value} items",
                        Value = value
                    });
                }
            }

            // DateTime validations
            if (field.fieldType == "datetime")
            {
                DateTime? dateValue = null;

                if (value is DateTime dt)
                {
                    dateValue = dt;
                }
                else if (value is string dateStr && DateTime.TryParse(dateStr, out var parsedDate))
                {
                    dateValue = parsedDate;
                }

                if (dateValue.HasValue)
                {
                    if (rules.minDate.HasValue && dateValue.Value < rules.minDate.Value)
                    {
                        errors.Add(new ValidationErrorDto
                        {
                            Field = field.name,
                            Message = rules.message ?? $"Field '{field.name}' must be after {rules.minDate.Value:yyyy-MM-dd}",
                            Value = value
                        });
                    }

                    if (rules.maxDate.HasValue && dateValue.Value > rules.maxDate.Value)
                    {
                        errors.Add(new ValidationErrorDto
                        {
                            Field = field.name,
                            Message = rules.message ?? $"Field '{field.name}' must be on or before {rules.maxDate.Value:yyyy-MM-dd}",
                            Value = value
                        });
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Validate expression-based validation rules
        /// </summary>
        public ValidationResult ValidateExpressions(DatasetSchema schema, Dictionary<string, object> data, bool isUpdate)
        {
            var errors = new List<ValidationErrorDto>();

            if (schema.validations == null || schema.validations.Count == 0)
                return ValidationResult.Success();

            // Filter validations by type and when
            var expressionValidations = schema.validations
                .Where(v => v.type == "expression" && !string.IsNullOrEmpty(v.expression))
                .Where(v =>
                {
                    var when = v.when ?? "both";
                    return when == "both" || 
                           (when == "create" && !isUpdate) || 
                           (when == "update" && isUpdate);
                })
                .OrderBy(v => v.order ?? 0)
                .ToList();

            foreach (var validation in expressionValidations)
            {
                try
                {
                    var isValid = EvaluateExpression(validation.expression!, data);
                    if (!isValid)
                    {
                        // Use description if available, otherwise use name, fallback to expression
                        var errorMessage = !string.IsNullOrWhiteSpace(validation.description)
                            ? validation.description
                            : !string.IsNullOrWhiteSpace(validation.name)
                                ? $"Validation '{validation.name}' failed"
                                : $"Validation failed: {validation.expression}";
                        
                        errors.Add(new ValidationErrorDto
                        {
                            Field = "_expression",
                            Message = errorMessage,
                            Value = null
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error evaluating expression '{Expression}' for validation '{ValidationName}'", validation.expression, validation.name);
                    var errorMessage = !string.IsNullOrWhiteSpace(validation.name)
                        ? $"Error evaluating validation '{validation.name}': {ex.Message}"
                        : $"Error evaluating validation: {ex.Message}";
                    
                    errors.Add(new ValidationErrorDto
                    {
                        Field = "_expression",
                        Message = errorMessage,
                        Value = null
                    });
                }
            }

            return errors.Any()
                ? ValidationResult.Failure(errors)
                : ValidationResult.Success();
        }

        /// <summary>
        /// Evaluate a simple expression (supports basic operators and field references)
        /// </summary>
        private bool EvaluateExpression(string expression, Dictionary<string, object> data)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return true;

            // Simple expression evaluator for basic operations
            // Supports: >, <, >=, <=, ==, !=, +, -, *, /
            // Field references: fieldName (replaced with actual value)

            try
            {
                // First, extract all field names from expression
                var fieldNamesInExpression = System.Text.RegularExpressions.Regex.Matches(expression, @"\b[a-zA-Z_][a-zA-Z0-9_]*\b")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => m.Value)
                    .Where(name => !name.Equals("null", StringComparison.OrdinalIgnoreCase) &&
                                   !name.Equals("true", StringComparison.OrdinalIgnoreCase) &&
                                   !name.Equals("false", StringComparison.OrdinalIgnoreCase) &&
                                   !name.Equals("DateTime", StringComparison.OrdinalIgnoreCase) &&
                                   !name.Equals("Parse", StringComparison.OrdinalIgnoreCase) &&
                                   !name.Equals("Now", StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .ToList();

                // Replace field names with values
                var evaluatedExpression = expression;
                foreach (var fieldName in fieldNamesInExpression)
                {
                    // Check if field exists in data
                    if (!data.TryGetValue(fieldName, out var fieldValue))
                    {
                        // Field doesn't exist, replace with "null"
                        evaluatedExpression = System.Text.RegularExpressions.Regex.Replace(
                            evaluatedExpression,
                            $@"\b{System.Text.RegularExpressions.Regex.Escape(fieldName)}\b",
                            "null",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        continue;
                    }

                    // Replace field name with value (handle different types)
                    string replacement;
                    if (fieldValue == null)
                    {
                        replacement = "null";
                    }
                    else if (fieldValue is string str)
                    {
                        replacement = $"\"{str.Replace("\"", "\\\"")}\"";
                    }
                    else if (fieldValue is bool boolVal)
                    {
                        replacement = boolVal.ToString().ToLowerInvariant();
                    }
                    else if (IsNumber(fieldValue))
                    {
                        replacement = Convert.ToDouble(fieldValue).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                    else if (fieldValue is DateTime dt)
                    {
                        replacement = $"DateTime.Parse(\"{dt:yyyy-MM-ddTHH:mm:ssZ}\")";
                    }
                    else
                    {
                        // For complex types, try to convert to number or use string
                        if (double.TryParse(fieldValue.ToString(), out var num))
                        {
                            replacement = num.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            replacement = $"\"{fieldValue}\"";
                        }
                    }

                    // Replace field name (whole word match to avoid partial replacements)
                    evaluatedExpression = System.Text.RegularExpressions.Regex.Replace(
                        evaluatedExpression,
                        $@"\b{System.Text.RegularExpressions.Regex.Escape(fieldName)}\b",
                        replacement,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }

                // Evaluate the expression using DataTable.Compute (simple but limited)
                // For more complex expressions, consider using Jint or NCalc
                var result = EvaluateSimpleExpression(evaluatedExpression);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error evaluating expression: {Expression}", expression);
                return false;
            }
        }

        /// <summary>
        /// Evaluate a simple mathematical/logical expression
        /// </summary>
        private bool EvaluateSimpleExpression(string expression)
        {
            try
            {
                // Remove quotes for string comparison
                expression = expression.Trim();

                // Handle boolean literals
                if (expression.Equals("true", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (expression.Equals("false", StringComparison.OrdinalIgnoreCase))
                    return false;

                // Handle logical OR (||) - must be checked before other operators
                if (expression.Contains("||"))
                {
                    return EvaluateLogicalOr(expression);
                }

                // Handle logical AND (&&) - must be checked before comparison operators
                if (expression.Contains("&&"))
                {
                    return EvaluateLogicalAnd(expression);
                }

                // Handle comparison operators
                if (expression.Contains("=="))
                {
                    var parts = expression.Split(new[] { "==" }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        var left = EvaluateValue(parts[0].Trim());
                        var right = EvaluateValue(parts[1].Trim());
                        return left == right;
                    }
                }

                if (expression.Contains("!="))
                {
                    var parts = expression.Split(new[] { "!=" }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        var left = EvaluateValue(parts[0].Trim());
                        var right = EvaluateValue(parts[1].Trim());
                        return left != right;
                    }
                }

                if (expression.Contains(">="))
                {
                    var parts = expression.Split(new[] { ">=" }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        // Evaluate left side as expression (might contain arithmetic)
                        var leftExpr = parts[0].Trim();
                        var leftValue = EvaluateNumericExpression(leftExpr);
                        var left = leftValue ?? EvaluateNumeric(leftExpr);
                        
                        // Evaluate right side
                        var right = EvaluateNumeric(parts[1].Trim());
                        return left >= right;
                    }
                }

                if (expression.Contains("<="))
                {
                    var parts = expression.Split(new[] { "<=" }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        // Evaluate left side as expression (might contain arithmetic)
                        var leftExpr = parts[0].Trim();
                        var leftValue = EvaluateNumericExpression(leftExpr);
                        var left = leftValue ?? EvaluateNumeric(leftExpr);
                        
                        // Evaluate right side
                        var right = EvaluateNumeric(parts[1].Trim());
                        return left <= right;
                    }
                }

                if (expression.Contains(">") && !expression.Contains(">="))
                {
                    var parts = expression.Split('>');
                    if (parts.Length == 2)
                    {
                        // Evaluate left side as expression (might contain arithmetic)
                        var leftExpr = parts[0].Trim();
                        var leftValue = EvaluateNumericExpression(leftExpr);
                        var left = leftValue ?? EvaluateNumeric(leftExpr);
                        
                        // Evaluate right side
                        var right = EvaluateNumeric(parts[1].Trim());
                        return left > right;
                    }
                }

                if (expression.Contains("<") && !expression.Contains("<="))
                {
                    var parts = expression.Split('<');
                    if (parts.Length == 2)
                    {
                        // Evaluate left side as expression (might contain arithmetic)
                        var leftExpr = parts[0].Trim();
                        var leftValue = EvaluateNumericExpression(leftExpr);
                        var left = leftValue ?? EvaluateNumeric(leftExpr);
                        
                        // Evaluate right side
                        var right = EvaluateNumeric(parts[1].Trim());
                        return left < right;
                    }
                }

                // Handle arithmetic expressions in parentheses
                if (expression.Contains("(") && expression.Contains(")"))
                {
                    // Handle nested expressions with parentheses
                    // Example: (pageCount == null) || (price != null && price > 0)
                    // First, evaluate innermost expressions
                    var innerExpression = expression;
                    while (innerExpression.Contains("(") && innerExpression.Contains(")"))
                    {
                        var startIdx = innerExpression.LastIndexOf('(');
                        var endIdx = innerExpression.IndexOf(')', startIdx);
                        if (startIdx >= 0 && endIdx > startIdx)
                        {
                            var innerPart = innerExpression.Substring(startIdx + 1, endIdx - startIdx - 1);
                            var innerResult = EvaluateSimpleExpression(innerPart);
                            innerExpression = innerExpression.Substring(0, startIdx) + 
                                            (innerResult ? "true" : "false") + 
                                            innerExpression.Substring(endIdx + 1);
                        }
                        else
                        {
                            break;
                        }
                    }
                    return EvaluateSimpleExpression(innerExpression);
                }

                // Try to evaluate as numeric expression
                var result = EvaluateNumericExpression(expression);
                if (result.HasValue)
                {
                    return result.Value != 0;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool EvaluateLogicalOr(string expression)
        {
            // Handle || operator
            if (expression.Contains("||"))
            {
                var parts = expression.Split(new[] { "||" }, StringSplitOptions.None);
                foreach (var part in parts)
                {
                    var trimmedPart = part.Trim();
                    // Remove outer parentheses if any
                    if (trimmedPart.StartsWith("(") && trimmedPart.EndsWith(")"))
                    {
                        trimmedPart = trimmedPart.Substring(1, trimmedPart.Length - 2).Trim();
                    }
                    if (EvaluateSimpleExpression(trimmedPart))
                    {
                        return true; // OR: if any part is true, return true
                    }
                }
                return false; // OR: all parts are false
            }
            return EvaluateSimpleExpression(expression);
        }

        private bool EvaluateLogicalAnd(string expression)
        {
            // Handle && operator
            if (expression.Contains("&&"))
            {
                var parts = expression.Split(new[] { "&&" }, StringSplitOptions.None);
                foreach (var part in parts)
                {
                    var trimmedPart = part.Trim();
                    // Remove outer parentheses if any
                    if (trimmedPart.StartsWith("(") && trimmedPart.EndsWith(")"))
                    {
                        trimmedPart = trimmedPart.Substring(1, trimmedPart.Length - 2).Trim();
                    }
                    if (!EvaluateSimpleExpression(trimmedPart))
                    {
                        return false; // AND: if any part is false, return false
                    }
                }
                return true; // AND: all parts are true
            }
            return EvaluateSimpleExpression(expression);
        }

        private object? EvaluateValue(string value)
        {
            value = value.Trim().Trim('"', '\'');

            if (value.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;

            if (bool.TryParse(value, out var boolVal))
                return boolVal;

            if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
                return num;

            return value;
        }

        private double EvaluateNumeric(string value)
        {
            value = value.Trim().Trim('"', '\'');

            if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
                return num;

            return 0;
        }

        private double? EvaluateNumericExpression(string expression)
        {
            try
            {
                // Simple arithmetic evaluation using DataTable.Compute
                var dataTable = new System.Data.DataTable();
                var result = dataTable.Compute(expression, null);
                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToDouble(result);
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }

        /// <summary>
        /// Get authorization header from current HTTP context
        /// </summary>
        private string? GetAuthorizationHeader()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Request.Headers.TryGetValue("Authorization", out var authHeader) == true)
            {
                return authHeader.ToString();
            }
            return null;
        }

        /// <summary>
        /// Validate HTTP-based validation rules
        /// </summary>
        private async Task<ValidationResult> ValidateHttpValidationsAsync(
            DatasetSchema schema,
            Dictionary<string, object> data,
            bool isUpdate,
            string? authorizationHeader = null)
        {
            var errors = new List<ValidationErrorDto>();

            if (schema.validations == null || schema.validations.Count == 0)
                return ValidationResult.Success();

            // Filter validations by type and when
            var httpValidations = schema.validations
                .Where(v => v.type == "http" && !string.IsNullOrEmpty(v.url))
                .Where(v =>
                {
                    var when = v.when ?? "both";
                    return when == "both" ||
                           (when == "create" && !isUpdate) ||
                           (when == "update" && isUpdate);
                })
                .OrderBy(v => v.order ?? 0)
                .ToList();

            if (!httpValidations.Any())
                return ValidationResult.Success();

            // Get timeout from configuration
            var timeoutSeconds = _configuration.GetValue<int>("MngDataGatewaySettings:Validation:HttpValidationTimeout", 30);
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);

            // Create HTTP client
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = timeout;

            // Add authorization header if provided
            if (!string.IsNullOrEmpty(authorizationHeader))
            {
                httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authorizationHeader);
            }

            // Execute validations sequentially
            foreach (var validation in httpValidations)
            {
                try
                {
                    _logger.LogDebug("Executing HTTP validation '{ValidationName}' for dataset '{DatasetName}' at URL: {Url}",
                        validation.name, schema.name, validation.url);

                    var method = validation.method?.ToUpperInvariant() ?? "POST";
                    HttpResponseMessage response;

                    if (method == "GET")
                    {
                        // For GET, data might be sent as query parameters (not implemented yet, using POST only)
                        response = await httpClient.GetAsync(validation.url);
                    }
                    else
                    {
                        // POST - send data as JSON in body
                        var jsonContent = JsonSerializer.Serialize(data, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
                        var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                        response = await httpClient.PostAsync(validation.url, content);
                    }

                    // Handle response
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var validationResponse = JsonSerializer.Deserialize<HttpValidationResponse>(responseContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (validationResponse == null)
                        {
                            _logger.LogWarning("HTTP validation '{ValidationName}' returned 200 but response could not be parsed", validation.name);
                            // If response cannot be parsed, consider it valid (safe default)
                            continue;
                        }

                        if (!validationResponse.IsValid)
                        {
                            var errorMessage = validationResponse.ErrorMessage ?? $"HTTP validation '{validation.name}' failed";
                            errors.Add(new ValidationErrorDto
                            {
                                Field = "_http_validation",
                                Message = errorMessage,
                                Value = validation.name
                            });

                            _logger.LogWarning("HTTP validation '{ValidationName}' failed: {ErrorMessage}",
                                validation.name, errorMessage);
                        }
                        else
                        {
                            _logger.LogDebug("HTTP validation '{ValidationName}' passed", validation.name);
                        }
                    }
                    else
                    {
                        // Non-200 status codes are considered valid (validation endpoint issues)
                        _logger.LogWarning("HTTP validation '{ValidationName}' returned status {StatusCode}, considering validation passed",
                            validation.name, response.StatusCode);
                    }
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    // Timeout - consider validation passed (safe default)
                    _logger.LogWarning(ex, "HTTP validation '{ValidationName}' timed out after {Timeout}s, considering validation passed",
                        validation.name, timeoutSeconds);
                }
                catch (HttpRequestException ex)
                {
                    // Network error - consider validation passed (safe default)
                    _logger.LogWarning(ex, "HTTP validation '{ValidationName}' failed to reach endpoint, considering validation passed",
                        validation.name);
                }
                catch (Exception ex)
                {
                    // Other errors - consider validation passed (safe default)
                    _logger.LogError(ex, "Error executing HTTP validation '{ValidationName}': {ErrorMessage}, considering validation passed",
                        validation.name, ex.Message);
                }
            }

            return errors.Any()
                ? ValidationResult.Failure(errors)
                : ValidationResult.Success();
        }

        /// <summary>
        /// HTTP validation response model
        /// </summary>
        private class HttpValidationResponse
        {
            public bool IsValid { get; set; }
            public string? ErrorMessage { get; set; }
        }
    }
}

