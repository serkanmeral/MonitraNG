namespace MngDataGateway.Domain.Constants;

/// <summary>
/// Standard API error codes (language-independent; UI translates via i18n).
/// </summary>
public static class ErrorCodes
{
    // Validation / client (4xx)
    public const string VALIDATION_ERROR = "VALIDATION_ERROR";
    public const string VALIDATION_REQUIRED_FIELD = "VALIDATION_REQUIRED_FIELD";
    public const string VALIDATION_INVALID_FORMAT = "VALIDATION_INVALID_FORMAT";
    public const string VALIDATION_MIN_LENGTH = "VALIDATION_MIN_LENGTH";
    public const string VALIDATION_MAX_LENGTH = "VALIDATION_MAX_LENGTH";
    public const string VALIDATION_MIN = "VALIDATION_MIN";
    public const string VALIDATION_MAX = "VALIDATION_MAX";
    public const string VALIDATION_PATTERN = "VALIDATION_PATTERN";
    public const string VALIDATION_UNIQUE_CONSTRAINT = "VALIDATION_UNIQUE_CONSTRAINT";
    public const string VALIDATION_EXPRESSION_FAILED = "VALIDATION_EXPRESSION_FAILED";

    public const string DUPLICATE_KEY = "DUPLICATE_KEY";
    public const string INVALID_ARGUMENT = "INVALID_ARGUMENT";
    public const string INVALID_FORMAT = "INVALID_FORMAT";
    public const string INVALID_OPERATION = "INVALID_OPERATION";
    public const string MISSING_PARAMETER = "MISSING_PARAMETER";

    // Auth (4xx)
    public const string UNAUTHORIZED = "UNAUTHORIZED";
    public const string FORBIDDEN = "FORBIDDEN";

    // Not found (4xx)
    public const string DATASET_NOT_FOUND = "DATASET_NOT_FOUND";
    public const string DATA_NOT_FOUND = "DATA_NOT_FOUND";
    public const string RESOURCE_NOT_FOUND = "RESOURCE_NOT_FOUND";
    public const string QUERY_NOT_FOUND = "QUERY_NOT_FOUND";

    // Database / write (4xx client-facing where applicable)
    public const string DATABASE_WRITE_ERROR = "DATABASE_WRITE_ERROR";

    // Server (5xx)
    public const string INTERNAL_ERROR = "INTERNAL_ERROR";
    public const string DATABASE_ERROR = "DATABASE_ERROR";
    public const string TIMEOUT = "TIMEOUT";
    public const string EXTERNAL_SERVICE_ERROR = "EXTERNAL_SERVICE_ERROR";
}
