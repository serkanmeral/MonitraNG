using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using MngDataGateway.Application.DTOs.Validation;
using MngDataGateway.Domain.Constants;
using MngDataGateway.Domain.Exceptions;

namespace MngDataGateway.Persistence.Helpers;

/// <summary>
/// Maps infrastructure and framework exceptions to typed DataGateway exceptions with HTTP-friendly codes.
/// </summary>
public static partial class DgExceptionMapper
{
    public static DataGatewayException Map(Exception ex, string contextMessage)
    {
        if (ex is DataGatewayException dgEx)
            return dgEx;

        return ex switch
        {
            MongoWriteException mongoWrite => MapMongoWriteException(mongoWrite, contextMessage),
            MongoBulkWriteException bulk => MapBulkWriteException(bulk, contextMessage),
            MongoConnectionException => CreateServerError(contextMessage, ex, ErrorCodes.DATABASE_ERROR),
            MongoCommandException cmd => MapMongoCommandException(cmd, contextMessage),
            BsonException bson => new BadRequestException(bson.Message, ErrorCodes.INVALID_FORMAT),
            ArgumentNullException => new BadRequestException("Required argument is missing", ErrorCodes.INVALID_ARGUMENT),
            ArgumentException arg => new BadRequestException(arg.Message, ErrorCodes.INVALID_ARGUMENT),
            FormatException fmt => new BadRequestException(fmt.Message, ErrorCodes.INVALID_FORMAT),
            InvalidOperationException inv => new BadRequestException(inv.Message, ErrorCodes.INVALID_OPERATION),
            KeyNotFoundException knf => new NotFoundException(
                string.IsNullOrWhiteSpace(knf.Message) ? contextMessage : knf.Message),
            UnauthorizedAccessException => new UnauthorizedException(ex.Message),
            TimeoutException => CreateServerError(contextMessage, ex, ErrorCodes.TIMEOUT),
            _ => CreateServerError(contextMessage, ex, ErrorCodes.INTERNAL_ERROR)
        };
    }

    public static bool IsClientError(DataGatewayException ex) =>
        ex is ConflictException
            or ValidationException
            or NotFoundException
            or BadRequestException
            or UnauthorizedException
            or ForbiddenException
        || ex.ValidationErrors is { Count: > 0 }
        || ex.ErrorCode is ErrorCodes.INVALID_ARGUMENT
            or ErrorCodes.INVALID_FORMAT
            or ErrorCodes.INVALID_OPERATION
            or ErrorCodes.MISSING_PARAMETER
            or ErrorCodes.DATABASE_WRITE_ERROR
            or ErrorCodes.VALIDATION_ERROR
            or ErrorCodes.DUPLICATE_KEY
            or ErrorCodes.VALIDATION_UNIQUE_CONSTRAINT
            or ErrorCodes.DATASET_NOT_FOUND
            or ErrorCodes.DATA_NOT_FOUND
            or ErrorCodes.RESOURCE_NOT_FOUND
            or ErrorCodes.QUERY_NOT_FOUND;

    private static DataGatewayException MapMongoWriteException(MongoWriteException ex, string contextMessage)
    {
        if (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            return CreateDuplicateKeyException(ex.WriteError.Message);

        return new BadRequestException(
            "Database write failed. Please check your input and try again.",
            ex,
            ErrorCodes.DATABASE_WRITE_ERROR);
    }

    private static DataGatewayException MapBulkWriteException(MongoBulkWriteException ex, string contextMessage)
    {
        var duplicate = ex.WriteErrors?.FirstOrDefault(e => e.Category == ServerErrorCategory.DuplicateKey);
        if (duplicate != null)
            return CreateDuplicateKeyException(duplicate.Message);

        return new BadRequestException(
            "Database write failed. Please check your input and try again.",
            ex,
            ErrorCodes.DATABASE_WRITE_ERROR);
    }

    private static DataGatewayException MapMongoCommandException(MongoCommandException ex, string contextMessage)
    {
        // MongoDB duplicate key error code
        if (ex.Code == 11000 || ex.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            return CreateDuplicateKeyException(ex.Message);

        return new BadRequestException(ex.Message, ex, ErrorCodes.DATABASE_WRITE_ERROR);
    }

    private static ConflictException CreateDuplicateKeyException(string? mongoMessage)
    {
        var field = ExtractFieldFromMongoMessage(mongoMessage);
        var validationErrors = new List<object>();

        if (!string.IsNullOrEmpty(field))
        {
            validationErrors.Add(new ValidationErrorDto
            {
                Field = field,
                Code = ErrorCodes.VALIDATION_UNIQUE_CONSTRAINT,
                Message = $"Field '{field}' must be unique. A record with this value already exists."
            });
        }

        return new ConflictException("A record with this value already exists.")
        {
            ValidationErrors = validationErrors.Count > 0 ? validationErrors : null
        };
    }

    public static string? ExtractFieldFromMongoMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        // index: collection_fieldName_1 dup key: { fieldName: "value" }
        var dupKeyMatch = DupKeyFieldRegex().Match(message);
        if (dupKeyMatch.Success)
            return dupKeyMatch.Groups[1].Value;

        // index: fieldName_1
        var indexMatch = IndexFieldRegex().Match(message);
        if (indexMatch.Success)
            return indexMatch.Groups[1].Value;

        return null;
    }

    private static DataGatewayException CreateServerError(string contextMessage, Exception ex, string errorCode) =>
        new(contextMessage, ex) { ErrorCode = errorCode };

    [GeneratedRegex(@"dup key:\s*\{\s*(\w+):", RegexOptions.IgnoreCase)]
    private static partial Regex DupKeyFieldRegex();

    [GeneratedRegex(@"index:\s*\S+_(\w+)_\d+", RegexOptions.IgnoreCase)]
    private static partial Regex IndexFieldRegex();
}
