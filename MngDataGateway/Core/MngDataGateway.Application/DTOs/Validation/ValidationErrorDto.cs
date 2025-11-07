using System.Collections.Generic;

namespace MngDataGateway.Application.DTOs.Validation
{
    /// <summary>
    /// Validation error details
    /// </summary>
    public class ValidationErrorDto
    {
        public string Field { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public object? Value { get; set; }
    }

    /// <summary>
    /// Validation result
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationErrorDto> Errors { get; set; } = new();

        public static ValidationResult Success() => new ValidationResult { IsValid = true };
        
        public static ValidationResult Failure(List<ValidationErrorDto> errors) => 
            new ValidationResult { IsValid = false, Errors = errors };
        
        public static ValidationResult Failure(string field, string message, object? value = null) =>
            new ValidationResult
            {
                IsValid = false,
                Errors = new List<ValidationErrorDto>
                {
                    new ValidationErrorDto { Field = field, Message = message, Value = value }
                }
            };
    }
}

