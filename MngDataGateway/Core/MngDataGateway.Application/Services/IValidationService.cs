using System.Collections.Generic;
using System.Threading.Tasks;
using MngDataGateway.Application.DTOs.Validation;
using MngDataGateway.Domain.Entities;

namespace MngDataGateway.Application.Services
{
    /// <summary>
    /// Data validation service
    /// </summary>
    public interface IValidationService
    {
        /// <summary>
        /// Validate data against schema (full validation)
        /// </summary>
        Task<ValidationResult> ValidateDataAsync(
            DatasetSchema schema,
            Dictionary<string, object> data,
            string databaseName,
            bool isUpdate = false,
            string? dataId = null);

        /// <summary>
        /// Validate mandatory fields
        /// </summary>
        ValidationResult ValidateMandatoryFields(DatasetSchema schema, Dictionary<string, object> data);

        /// <summary>
        /// Validate field types
        /// </summary>
        ValidationResult ValidateFieldTypes(DatasetSchema schema, Dictionary<string, object> data);

        /// <summary>
        /// Validate forceSchema (no extra fields in strict mode)
        /// </summary>
        ValidationResult ValidateForceSchema(DatasetSchema schema, Dictionary<string, object> data);

        /// <summary>
        /// Validate unique constraints
        /// </summary>
        Task<ValidationResult> ValidateUniqueConstraintsAsync(
            DatasetSchema schema,
            Dictionary<string, object> data,
            string databaseName,
            string? excludeDataId = null);
    }
}

