using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MngDataGateway.Api.Helpers;
using MngDataGateway.Application.Configuration;
using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Application.DTOs.Data;
using MngDataGateway.Application.DTOs.Files;
using MngDataGateway.Application.DTOs.Validation;
using MngDataGateway.Application.Services;
using MngDataGateway.Application.Services.Files;
using MngDataGateway.Domain.Entities;
using MngDataGateway.Domain.Exceptions;
using MngDataGateway.Persistence.Services;

namespace MngDataGateway.Api.Controllers
{
    /// <summary>
    /// Data CRUD operations controller
    /// Dynamic data management for datasets
    /// </summary>
    [ApiController]
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/data/{datasetName}")]
    [Authorize]
    public class DataController : ControllerBase
    {
        private readonly ILogger<DataController> _logger;
        private readonly IDataService _dataService;
        private readonly IMongoContextService _mongoContextService;
        private readonly IUserInfoService _userInfoService;
        private readonly IDatasetService _datasetService;
        private readonly IPermissionService _permissionService;
        private readonly CsvConverter _csvConverter;
        private readonly IFileProcessingPipeline _fileProcessingPipeline;
        private readonly MngDataGatewaySettings _settings;

        public DataController(
            ILogger<DataController> logger,
            IDataService dataService,
            IMongoContextService mongoContextService,
            IUserInfoService userInfoService,
            IDatasetService datasetService,
            IPermissionService permissionService,
            CsvConverter csvConverter,
            IFileProcessingPipeline fileProcessingPipeline,
            IOptions<MngDataGatewaySettings> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _mongoContextService = mongoContextService ?? throw new ArgumentNullException(nameof(mongoContextService));
            _userInfoService = userInfoService ?? throw new ArgumentNullException(nameof(userInfoService));
            _datasetService = datasetService ?? throw new ArgumentNullException(nameof(datasetService));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _csvConverter = csvConverter ?? throw new ArgumentNullException(nameof(csvConverter));
            _fileProcessingPipeline = fileProcessingPipeline ?? throw new ArgumentNullException(nameof(fileProcessingPipeline));
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Check permission for dataset operation
        /// Returns 403 Forbidden if user doesn't have permission
        /// </summary>
        private IActionResult? CheckDatasetPermission(DatasetSchema schema, string permissionType)
        {
            var domainName = _mongoContextService.GetCurrentDomainName();
            if (string.IsNullOrEmpty(domainName))
            {
                return this.ErrorResponse(HttpContext.Request.Path, "FORBIDDEN", "Domain information not found in token", statusCode: 403);
            }

            var userGroups = _permissionService.GetUserGroups(HttpContext);
            var hasPermission = _permissionService.CheckPermission(schema, permissionType, userGroups, domainName);

            if (!hasPermission)
            {
                return this.ErrorResponse(HttpContext.Request.Path, "FORBIDDEN", $"You don't have '{permissionType}' permission for dataset '{schema.name}'", statusCode: 403);
            }

            return null; // Permission granted
        }

        /// <summary>
        /// Create new data in dataset
        /// </summary>
        /// <param name="datasetName">Dataset name (e.g., @tasks)</param>
        /// <param name="request">Data to create</param>
        /// <param name="skipEventPublish">If true, no RabbitMQ/event publish (e.g. monitoring sync). Dataset publish_mode is ignored for this request.</param>
        /// <returns>Created data with generated fields</returns>
        [HttpPost]
        [ProducesResponseType(typeof(DataResponseDto<Dictionary<string, object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create(
            [FromRoute] string datasetName,
            [FromBody] JsonElement request,
            [FromQuery] bool skipEventPublish = false)
        {
            try
            {
                var domainName = _mongoContextService.GetCurrentDomainName() ?? throw new UnauthorizedAccessException("Domain not found in token");
                var database = _mongoContextService.GetDatabase();
                
                // Get dataset schema for permission check
                var schema = await _datasetService.GetSchemaEntityByNameAsync(datasetName);
                if (schema == null)
                {
                    return this.ErrorResponse(GetApiPath(datasetName), "DATASET_NOT_FOUND", $"Dataset '{datasetName}' not found", statusCode: 404);
                }

                // Check permission
                var permissionResult = CheckDatasetPermission(schema, "create");
                if (permissionResult != null)
                    return permissionResult;

                var userInfo = _userInfoService.GetCurrentUserInfo();
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                // Process file fields (upload files if object model is used) - BEFORE ToDictionary
                var fileProcessingResult = await ProcessFileFieldsFromJsonElementAsync(schema, request, datasetName, domainName, recordId: null);
                if (fileProcessingResult.Error != null)
                    return fileProcessingResult.Error;

                // Convert JsonElement to Dictionary with proper types (after file processing)
                var data = fileProcessingResult.Data.ToDictionary();

                // Validate file field paths (if any)
                var fileValidationResult = ValidateFileFields(schema, data, datasetName);
                if (fileValidationResult != null)
                    return fileValidationResult;

                var result = await _dataService.CreateAsync(
                    datasetName,
                    data,
                    domainName,
                    database.DatabaseNamespace.DatabaseName,
                    userInfo.uid,
                    userInfo.userName,
                    ipAddress,
                    skipEventPublish);

                var path = $"/api/v1/data/{datasetName}";
                return this.SuccessResponse(result, path);
            }
            catch (DataGatewayException ex) when (ex.ValidationErrors != null)
            {
                var path = $"/api/v1/data/{datasetName}";
                return this.HandleValidationError(ex, path, _logger);
            }
            catch (DataGatewayException ex) when (ex.Message.Contains("not found"))
            {
                var path = $"/api/v1/data/{datasetName}";
                return this.HandleNotFoundError(ex, path, _logger);
            }
            catch (Exception ex)
            {
                var path = $"/api/v1/data/{datasetName}";
                return this.HandleError(ex, path, "CREATE_FAILED", "Failed to create data", _logger, includeStackTrace: true);
            }
        }

        /// <summary>
        /// List data in dataset with advanced query options
        /// </summary>
        /// <param name="datasetName">Dataset name (e.g., @tasks)</param>
        /// <param name="skip">Number of records to skip (default: 0)</param>
        /// <param name="limit">Maximum records to return (default: 50, max: 1000)</param>
        /// <param name="expand">Enable relation expansion (default: true)</param>
        /// <param name="deep">Maximum depth for nested relations (default: from appsettings)</param>
        /// <param name="showHistory">Include __history field (default: false)</param>
        /// <param name="showQuery">Return aggregate pipeline instead of data (default: false)</param>
        /// <param name="showDataset">Return dataset schema instead of data (default: false)</param>
        /// <param name="sort">Sort definition (MongoDB style: "field1,-field2")</param>
        /// <param name="filter">Filter definition (RESTful style: "field:operator:value")</param>
        /// <param name="fields">Field selection (comma-separated: "field1,field2,field3")</param>
        /// <param name="search">Search term for text search</param>
        /// <param name="format">Response format: "json" or "csv" (default: "json")</param>
        /// <returns>List of data (always array format)</returns>
        [HttpGet]
        [ProducesResponseType(typeof(List<Dictionary<string, object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> List(
            [FromRoute] string datasetName,
            [FromQuery] int skip = 0,
            [FromQuery] int limit = 50,
            [FromQuery] bool expand = true,
            [FromQuery] int? deep = null,
            [FromQuery] bool showHistory = false,
            [FromQuery] bool showQuery = false,
            [FromQuery] bool showDataset = false,
            [FromQuery] string? sort = null,
            [FromQuery] string? filter = null,
            [FromQuery] string? fields = null,
            [FromQuery] string? search = null,
            [FromQuery] string format = "json")
        {
            try
            {
                var database = _mongoContextService.GetDatabase();

                // Get dataset schema for permission check
                var schema = await _datasetService.GetSchemaEntityByNameAsync(datasetName);
                if (schema == null)
                {
                    return this.ErrorResponse(GetApiPath(datasetName), "DATASET_NOT_FOUND", $"Dataset '{datasetName}' not found", statusCode: 404);
                }

                // Check permission (skip if showDataset or showQuery, they are metadata operations)
                if (!showDataset && !showQuery)
                {
                    var permissionResult = CheckDatasetPermission(schema, "read");
                    if (permissionResult != null)
                        return permissionResult;
                }

                // Handle showDataset
                if (showDataset)
                {
                    var schemaDto = await _datasetService.GetByNameAsync(datasetName);
                    if (schemaDto == null)
                    {
                        return this.ErrorResponse(GetApiPath(datasetName), "DATASET_NOT_FOUND", $"Dataset '{datasetName}' not found", statusCode: 404);
                    }

                    // Convert DTO to Dictionary
                    var schemaDict = new Dictionary<string, object>
                    {
                        ["name"] = schemaDto.Name,
                        ["description"] = schemaDto.Description ?? string.Empty,
                        ["category"] = schemaDto.Category ?? string.Empty,
                        ["forceSchema"] = schemaDto.ForceSchema,
                        ["logging"] = schemaDto.Logging,
                        ["publish_mode"] = schemaDto.PublishMode,
                        ["fields"] = (object?)(schemaDto.Fields ?? new List<FieldDefinition>()),
                        ["validations"] = (object?)(schemaDto.Validations ?? new List<ValidationDefinition>()),
                        ["queries"] = (object?)(schemaDto.Queries ?? new List<MngDataGateway.Application.DTOs.Dataset.QueryDefinitionResponseDto>()),
                        ["indexList"] = (object?)(schemaDto.IndexList ?? new List<IndexDefinition>())
                    };

                    return Ok(new List<Dictionary<string, object>> { schemaDict });
                }

                // Build query options
                var options = new QueryOptionsDto
                {
                    Skip = skip,
                    Limit = limit,
                    Expand = expand,
                    Deep = deep,
                    ShowHistory = showHistory,
                    ShowQuery = showQuery,
                    Sort = sort,
                    Filter = filter,
                    Fields = fields,
                    Search = search,
                    Format = format?.ToLowerInvariant() ?? "json"
                };

                var result = await _dataService.QueryAsync(
                    datasetName,
                    database.DatabaseNamespace.DatabaseName,
                    options);

                // Handle showQuery - return pipeline
                if (showQuery)
                {
                    return Ok(new { query = result.Query ?? new List<object>() });
                }

                // Handle CSV format
                if (options.Format == "csv")
                {
                    var csvContent = _csvConverter.ConvertToCsv(result.Data);
                    Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
                    return Content(csvContent, "text/csv", System.Text.Encoding.UTF8);
                }

                // Always return array (even if single item)
                // Add totalCount to response header for pagination
                Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
                return Ok(result.Data);
            }
            catch (DataGatewayException ex) when (ex.ValidationErrors != null)
            {
                return this.HandleValidationError(ex, GetApiPath(datasetName), _logger);
            }
            catch (DataGatewayException ex)
            {
                return this.HandleNotFoundError(ex, GetApiPath(datasetName), _logger);
            }
            catch (Exception ex)
            {
                return this.HandleError(ex, GetApiPath(datasetName), "LIST_FAILED", "Failed to list data", _logger);
            }
        }

        /// <summary>
        /// Get single data by ID with advanced query options
        /// </summary>
        /// <param name="datasetName">Dataset name (e.g., @tasks)</param>
        /// <param name="dataId">Data ID (__dataId)</param>
        /// <param name="expand">Enable relation expansion (default: true)</param>
        /// <param name="deep">Maximum depth for nested relations (default: from appsettings)</param>
        /// <param name="showHistory">Include __history field (default: false)</param>
        /// <param name="showQuery">Return aggregate pipeline instead of data (default: false)</param>
        /// <param name="showDataset">Return dataset schema instead of data (default: false)</param>
        /// <param name="sort">Sort definition (MongoDB style: "field1,-field2")</param>
        /// <param name="fields">Field selection (comma-separated: "field1,field2,field3")</param>
        /// <returns>Single data record (always array format)</returns>
        [HttpGet("{dataId}")]
        [ProducesResponseType(typeof(List<Dictionary<string, object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetById(
            [FromRoute] string datasetName,
            [FromRoute] string dataId,
            [FromQuery] bool expand = true,
            [FromQuery] int? deep = null,
            [FromQuery] bool showHistory = false,
            [FromQuery] bool showQuery = false,
            [FromQuery] bool showDataset = false,
            [FromQuery] string? sort = null,
            [FromQuery] string? fields = null)
        {
            try
            {
                var database = _mongoContextService.GetDatabase();

                // Get dataset schema for permission check
                var schema = await _datasetService.GetSchemaEntityByNameAsync(datasetName);
                if (schema == null)
                {
                    return this.ErrorResponse(GetApiPath(datasetName, dataId), "DATASET_NOT_FOUND", $"Dataset '{datasetName}' not found", statusCode: 404);
                }

                // Check permission (skip if showDataset or showQuery, they are metadata operations)
                if (!showDataset && !showQuery)
                {
                    var permissionResult = CheckDatasetPermission(schema, "read");
                    if (permissionResult != null)
                        return permissionResult;
                }

                // Handle showDataset
                if (showDataset)
                {
                    var schemaDto = await _datasetService.GetByNameAsync(datasetName);
                    if (schemaDto == null)
                    {
                        return this.ErrorResponse(GetApiPath(datasetName, dataId), "DATASET_NOT_FOUND", $"Dataset '{datasetName}' not found", statusCode: 404);
                    }

                    // Convert DTO to Dictionary
                    var schemaDict = new Dictionary<string, object>
                    {
                        ["name"] = schemaDto.Name,
                        ["description"] = schemaDto.Description ?? string.Empty,
                        ["category"] = schemaDto.Category ?? string.Empty,
                        ["forceSchema"] = schemaDto.ForceSchema,
                        ["logging"] = schemaDto.Logging,
                        ["publish_mode"] = schemaDto.PublishMode,
                        ["fields"] = (object?)(schemaDto.Fields ?? new List<FieldDefinition>()),
                        ["validations"] = (object?)(schemaDto.Validations ?? new List<ValidationDefinition>()),
                        ["queries"] = (object?)(schemaDto.Queries ?? new List<MngDataGateway.Application.DTOs.Dataset.QueryDefinitionResponseDto>()),
                        ["indexList"] = (object?)(schemaDto.IndexList ?? new List<IndexDefinition>())
                    };

                    return Ok(new List<Dictionary<string, object>> { schemaDict });
                }

                // Build query options
                var options = new QueryOptionsDto
                {
                    Expand = expand,
                    Deep = deep,
                    ShowHistory = showHistory,
                    ShowQuery = showQuery,
                    Sort = sort,
                    Fields = fields
                };

                var queryResult = await _dataService.QueryByIdAsync(
                    datasetName,
                    dataId,
                    database.DatabaseNamespace.DatabaseName,
                    options);

                if (queryResult.Data == null || !queryResult.Data.Any())
                {
                    return this.ErrorResponse(GetApiPath(datasetName, dataId), "DATA_NOT_FOUND", $"Data with __dataId '{dataId}' not found", statusCode: 404);
                }

                // Handle showQuery - return pipeline
                if (showQuery)
                {
                    return Ok(new { query = queryResult.Query ?? new List<object>() });
                }

                // Always return array (even if single item)
                return Ok(queryResult.Data);
            }
            catch (DataGatewayException ex)
            {
                return this.HandleNotFoundError(ex, GetApiPath(datasetName, dataId), _logger);
            }
            catch (Exception ex)
            {
                return this.HandleError(ex, GetApiPath(datasetName, dataId), "GET_FAILED", "Failed to get data", _logger);
            }
        }

        /// <summary>
        /// Update data
        /// </summary>
        /// <param name="datasetName">Dataset name</param>
        /// <param name="dataId">Data ID (__dataId)</param>
        /// <param name="request">Fields to update</param>
        /// <param name="skipEventPublish">If true, no RabbitMQ/event publish (e.g. lastSeenAt/heartbeat). Use for runtime-only updates that should not trigger sync.</param>
        /// <returns>Updated data</returns>
        [HttpPut("{dataId}")]
        [ProducesResponseType(typeof(DataResponseDto<Dictionary<string, object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            [FromRoute] string datasetName,
            [FromRoute] string dataId,
            [FromBody] JsonElement request,
            [FromQuery] bool skipEventPublish = false)
        {
            try
            {
                var domainName = _mongoContextService.GetCurrentDomainName() ?? throw new UnauthorizedAccessException("Domain not found in token");
                var database = _mongoContextService.GetDatabase();
                
                // Get dataset schema for permission check
                var schema = await _datasetService.GetSchemaEntityByNameAsync(datasetName);
                if (schema == null)
                {
                    return this.ErrorResponse(GetApiPath(datasetName, dataId), "DATASET_NOT_FOUND", $"Dataset '{datasetName}' not found", statusCode: 404);
                }

                // Check permission
                var permissionResult = CheckDatasetPermission(schema, "update");
                if (permissionResult != null)
                    return permissionResult;

                var userInfo = _userInfoService.GetCurrentUserInfo();
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                // Process file fields (upload new files with content, keep existing path objects) - BEFORE validation
                var fileProcessingResult = await ProcessFileFieldsFromJsonElementAsync(schema, request, datasetName, domainName, recordId: dataId);
                if (fileProcessingResult.Error != null)
                    return fileProcessingResult.Error;

                // Convert JsonElement to Dictionary with proper types (after file processing)
                var data = fileProcessingResult.Data.ToDictionary();

                // Validate file field paths (if any)
                var fileValidationResult = ValidateFileFields(schema, data, datasetName);
                if (fileValidationResult != null)
                    return fileValidationResult;

                var result = await _dataService.UpdateAsync(
                    datasetName,
                    dataId,
                    data,
                    domainName,
                    database.DatabaseNamespace.DatabaseName,
                    userInfo.uid,
                    userInfo.userName,
                    ipAddress,
                    skipEventPublish);

                if (result == null)
                {
                    return this.ErrorResponse(GetApiPath(datasetName, dataId), "DATA_NOT_FOUND", $"Data with __dataId '{dataId}' not found", statusCode: 404);
                }

                return this.SuccessResponse(result, GetApiPath(datasetName, dataId));
            }
            catch (DataGatewayException ex) when (ex.ValidationErrors != null)
            {
                return this.HandleValidationError(ex, GetApiPath(datasetName, dataId), _logger);
            }
            catch (Exception ex)
            {
                return this.HandleError(ex, GetApiPath(datasetName, dataId), "UPDATE_FAILED", "Failed to update data", _logger);
            }
        }

        /// <summary>
        /// Delete data (soft delete)
        /// </summary>
        /// <param name="datasetName">Dataset name</param>
        /// <param name="dataId">Data ID (__dataId)</param>
        /// <param name="skipEventPublish">If true, no RabbitMQ/event publish for this request.</param>
        /// <returns>Success status</returns>
        [HttpDelete("{dataId}")]
        [ProducesResponseType(typeof(DataResponseDto<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            [FromRoute] string datasetName,
            [FromRoute] string dataId,
            [FromQuery] bool skipEventPublish = false)
        {
            try
            {
                var domainName = _mongoContextService.GetCurrentDomainName() ?? throw new UnauthorizedAccessException("Domain not found in token");
                var database = _mongoContextService.GetDatabase();
                
                // Get dataset schema for permission check
                var schema = await _datasetService.GetSchemaEntityByNameAsync(datasetName);
                if (schema == null)
                {
                    return this.ErrorResponse(GetApiPath(datasetName, dataId), "DATASET_NOT_FOUND", $"Dataset '{datasetName}' not found", statusCode: 404);
                }

                // Check permission
                var permissionResult = CheckDatasetPermission(schema, "delete");
                if (permissionResult != null)
                    return permissionResult;

                var userInfo = _userInfoService.GetCurrentUserInfo();
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                var success = await _dataService.DeleteAsync(
                    datasetName,
                    dataId,
                    domainName,
                    database.DatabaseNamespace.DatabaseName,
                    userInfo.uid,
                    userInfo.userName,
                    ipAddress,
                    skipEventPublish);

                if (!success)
                {
                    return this.ErrorResponse(GetApiPath(datasetName, dataId), "DATA_NOT_FOUND", $"Data with __dataId '{dataId}' not found", statusCode: 404);
                }

                return this.SuccessResponse(GetApiPath(datasetName, dataId), new { message = "Data deleted successfully", dataId });
            }
            catch (Exception ex)
            {
                return this.HandleError(ex, GetApiPath(datasetName, dataId), "DELETE_FAILED", "Failed to delete data", _logger);
            }
        }

        /// <summary>
        /// Restore deleted data
        /// </summary>
        /// <param name="datasetName">Dataset name</param>
        /// <param name="dataId">Data ID (__dataId)</param>
        /// <param name="skipEventPublish">If true, no RabbitMQ/event publish for this request.</param>
        /// <returns>Success status</returns>
        [HttpPost("{dataId}/restore")]
        [ProducesResponseType(typeof(DataResponseDto<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Restore(
            [FromRoute] string datasetName,
            [FromRoute] string dataId,
            [FromQuery] bool skipEventPublish = false)
        {
            try
            {
                var domainName = _mongoContextService.GetCurrentDomainName() ?? throw new UnauthorizedAccessException("Domain not found in token");
                var database = _mongoContextService.GetDatabase();
                var userInfo = _userInfoService.GetCurrentUserInfo();
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                var success = await _dataService.RestoreAsync(
                    datasetName,
                    dataId,
                    domainName,
                    database.DatabaseNamespace.DatabaseName,
                    userInfo.uid,
                    userInfo.userName,
                    ipAddress,
                    skipEventPublish);

                if (!success)
                {
                    return this.ErrorResponse(GetApiPath(datasetName, dataId, "restore"), "DATA_NOT_FOUND", $"Deleted data with __dataId '{dataId}' not found", statusCode: 404);
                }

                return this.SuccessResponse(GetApiPath(datasetName, dataId, "restore"), new { message = "Data restored successfully", dataId });
            }
            catch (Exception ex)
            {
                return this.HandleError(ex, GetApiPath(datasetName, dataId, "restore"), "RESTORE_FAILED", "Failed to restore data", _logger);
            }
        }

        /// <summary>
        /// Advanced query with MongoDB native match object
        /// </summary>
        /// <param name="datasetName">Dataset name (e.g., @tasks)</param>
        /// <param name="request">MongoDB native match object</param>
        /// <param name="expand">Enable relation expansion (default: true)</param>
        /// <param name="deep">Maximum depth for nested relations (default: from appsettings)</param>
        /// <param name="showHistory">Include __history field (default: false)</param>
        /// <param name="showQuery">Return aggregate pipeline instead of data (default: false)</param>
        /// <param name="showDataset">Return dataset schema instead of data (default: false)</param>
        /// <param name="sort">Sort definition (MongoDB style: "field1,-field2")</param>
        /// <param name="fields">Field selection (comma-separated: "field1,field2,field3")</param>
        /// <param name="skip">Number of records to skip (default: 0)</param>
        /// <param name="limit">Maximum records to return (default: 50, max: 1000)</param>
        /// <returns>List of data (always array format)</returns>
        [HttpPost("query")]
        [ProducesResponseType(typeof(List<Dictionary<string, object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Query(
            [FromRoute] string datasetName,
            [FromBody] QueryRequestDto request,
            [FromQuery] bool expand = true,
            [FromQuery] int? deep = null,
            [FromQuery] bool showHistory = false,
            [FromQuery] bool showQuery = false,
            [FromQuery] bool showDataset = false,
            [FromQuery] string? sort = null,
            [FromQuery] string? fields = null,
            [FromQuery] int skip = 0,
            [FromQuery] int limit = 50)
        {
            try
            {
                var database = _mongoContextService.GetDatabase();

                // Handle showDataset
                if (showDataset)
                {
                    var schemaDto = await _datasetService.GetByNameAsync(datasetName);
                    if (schemaDto == null)
                    {
                        return this.ErrorResponse(GetApiPath(datasetName, action: "query"), "DATASET_NOT_FOUND", $"Dataset '{datasetName}' not found", statusCode: 404);
                    }

                    // Convert DTO to Dictionary
                    var schemaDict = new Dictionary<string, object>
                    {
                        ["name"] = schemaDto.Name,
                        ["description"] = schemaDto.Description ?? string.Empty,
                        ["category"] = schemaDto.Category ?? string.Empty,
                        ["forceSchema"] = schemaDto.ForceSchema,
                        ["logging"] = schemaDto.Logging,
                        ["publish_mode"] = schemaDto.PublishMode,
                        ["fields"] = (object?)(schemaDto.Fields ?? new List<FieldDefinition>()),
                        ["validations"] = (object?)(schemaDto.Validations ?? new List<ValidationDefinition>()),
                        ["queries"] = (object?)(schemaDto.Queries ?? new List<MngDataGateway.Application.DTOs.Dataset.QueryDefinitionResponseDto>()),
                        ["indexList"] = (object?)(schemaDto.IndexList ?? new List<IndexDefinition>())
                    };

                    return Ok(new List<Dictionary<string, object>> { schemaDict });
                }

                // Build query options
                var options = new QueryOptionsDto
                {
                    Skip = skip,
                    Limit = limit,
                    Expand = expand,
                    Deep = deep,
                    ShowHistory = showHistory,
                    ShowQuery = showQuery,
                    Sort = sort,
                    Fields = fields
                };

                // Convert JsonElement to Dictionary
                Dictionary<string, object>? matchDict = null;
                if (request.Match.ValueKind != JsonValueKind.Null && request.Match.ValueKind != JsonValueKind.Undefined)
                {
                    matchDict = request.Match.ToDictionary();
                }

                var result = await _dataService.QueryWithMatchAsync(
                    datasetName,
                    database.DatabaseNamespace.DatabaseName,
                    matchDict,
                    options);

                // Handle showQuery - return pipeline
                if (showQuery)
                {
                    return Ok(new { query = result.Query ?? new List<object>() });
                }

                // Always return array (even if single item)
                return Ok(result.Data);
            }
            catch (DataGatewayException ex) when (ex.ValidationErrors != null)
            {
                return this.HandleValidationError(ex, GetApiPath(datasetName, action: "query"), _logger);
            }
            catch (DataGatewayException ex)
            {
                return this.HandleNotFoundError(ex, GetApiPath(datasetName, action: "query"), _logger);
            }
            catch (Exception ex)
            {
                return this.HandleError(ex, GetApiPath(datasetName, action: "query"), "QUERY_FAILED", "Failed to query data", _logger);
            }
        }

        /// <summary>
        /// Execute raw MongoDB aggregate pipeline
        /// </summary>
        /// <param name="datasetName">Dataset name (e.g., @tasks)</param>
        /// <param name="request">Raw aggregate pipeline</param>
        /// <returns>Aggregate result (always array format)</returns>
        [HttpPost("aggregate")]
        [ProducesResponseType(typeof(List<Dictionary<string, object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Aggregate(
            [FromRoute] string datasetName,
            [FromBody] AggregateRequestDto request)
        {
            try
            {
                var database = _mongoContextService.GetDatabase();

                // Load schema to get collection name
                var schema = await _datasetService.GetSchemaEntityByNameAsync(datasetName);
                if (schema == null)
                {
                    return this.ErrorResponse(GetApiPath(datasetName, action: "aggregate"), "DATASET_NOT_FOUND", $"Dataset '{datasetName}' not found", statusCode: 404);
                }

                // Convert pipeline to BsonDocument list
                var pipeline = new List<MongoDB.Bson.BsonDocument>();
                foreach (var stage in request.Pipeline)
                {
                    var stageJson = System.Text.Json.JsonSerializer.Serialize(stage);
                    var stageBson = MongoDB.Bson.BsonDocument.Parse(stageJson);
                    pipeline.Add(stageBson);
                }

                // Execute aggregate via DataService
                var data = await _dataService.ExecuteRawAggregateAsync(
                    datasetName,
                    database.DatabaseNamespace.DatabaseName,
                    pipeline);

                // Always return array
                return Ok(data);
            }
            catch (MongoDB.Bson.BsonException ex)
            {
                return this.ErrorResponse(GetApiPath(datasetName, action: "aggregate"), "INVALID_PIPELINE", "Invalid aggregate pipeline", ex.Message);
            }
            catch (MongoDB.Driver.MongoCommandException ex)
            {
                return this.ErrorResponse(GetApiPath(datasetName, action: "aggregate"), "MONGO_ERROR", "MongoDB error", ex.Message);
            }
            catch (Exception ex)
            {
                return this.HandleError(ex, GetApiPath(datasetName, action: "aggregate"), "AGGREGATE_FAILED", "Failed to execute aggregate", _logger);
            }
        }

        /// <summary>
        /// Execute predefined query from dataset schema
        /// </summary>
        /// <param name="datasetName">Dataset name (e.g., @tasks)</param>
        /// <param name="queryName">Predefined query name</param>
        /// <param name="request">Query parameters (key-value pairs)</param>
        /// <returns>Query result (always array format)</returns>
        [HttpPost("queries/{queryName}")]
        [ProducesResponseType(typeof(List<Dictionary<string, object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExecutePredefinedQuery(
            [FromRoute] string datasetName,
            [FromRoute] string queryName,
            [FromBody] PredefinedQueryRequestDto? request = null)
        {
            try
            {
                var database = _mongoContextService.GetDatabase();

                // Convert request to dictionary (if null, use empty dictionary)
                var parameters = request?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>();

                var data = await _dataService.ExecutePredefinedQueryAsync(
                    datasetName,
                    queryName,
                    database.DatabaseNamespace.DatabaseName,
                    parameters);

                // Always return array
                return Ok(data);
            }
            catch (DataGatewayException ex)
            {
                var queryPath = GetApiPath(datasetName, action: $"queries/{queryName}");
                if (ex.Message.Contains("not found"))
                {
                    return this.ErrorResponse(queryPath, "QUERY_NOT_FOUND", ex.Message, statusCode: 404);
                }

                return this.HandleDataGatewayError(ex, queryPath, "QUERY_ERROR", _logger);
            }
            catch (Exception ex)
            {
                var queryPath = GetApiPath(datasetName, action: $"queries/{queryName}");
                return this.HandleError(ex, queryPath, "QUERY_EXECUTION_FAILED", "Failed to execute predefined query", _logger);
            }
        }

        #region Helper Methods

        /// <summary>
        /// Get API path for current endpoint
        /// </summary>
        private string GetApiPath(string datasetName, string? dataId = null, string? action = null)
        {
            var basePath = $"/api/v1/data/{datasetName}";
            if (!string.IsNullOrEmpty(dataId))
                basePath += $"/{dataId}";
            if (!string.IsNullOrEmpty(action))
                basePath += $"/{action}";
            return basePath;
        }

        /// <summary>
        /// Processes file fields from JsonElement - uploads files if object model is used
        /// Returns updated JsonElement with file paths replaced.
        /// When recordId is null (Create), a new Guid is used; when set (Update), existing dataId is used for upload paths.
        /// </summary>
        private async Task<(JsonElement Data, IActionResult? Error)> ProcessFileFieldsFromJsonElementAsync(
            DatasetSchema schema,
            JsonElement request,
            string datasetName,
            string domainName,
            string? recordId = null)
        {
            if (schema.fields == null || schema.fields.Count == 0)
                return (request, null);  // No fields to process

            var fileFields = schema.fields.Where(f => f.fieldType == "file").ToList();
            if (fileFields.Count == 0)
                return (request, null);  // No file fields in schema

            if (request.ValueKind != JsonValueKind.Object)
                return (request, null);  // Not an object

            var userInfo = _userInfoService.GetCurrentUserInfo();
            var effectiveRecordId = !string.IsNullOrEmpty(recordId) ? recordId : Guid.NewGuid().ToString();

            // Create a new JSON object with processed file fields
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.Text.Json.Utf8JsonWriter(stream);

            writer.WriteStartObject();

            foreach (var property in request.EnumerateObject())
            {
                var field = fileFields.FirstOrDefault(f => f.name == property.Name);
                
                if (field != null)
                {
                    // Process file field
                    if (!field.isArray)
                    {
                        // Single file field
                        if (property.Value.ValueKind == JsonValueKind.String)
                        {
                            // Legacy path string, keep as is
                            writer.WritePropertyName(property.Name);
                            writer.WriteStringValue(property.Value.GetString() ?? string.Empty);
                        }
                        else if (property.Value.ValueKind == JsonValueKind.Object)
                        {
                            var obj = property.Value;
                            var hasContent = obj.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String;
                            if (hasContent)
                            {
                                // Upload request: content present
                                var uploadResult = await ProcessSingleFileFieldAsync(
                                    obj, property.Name, datasetName, domainName, effectiveRecordId, userInfo.userName);
                                if (uploadResult.Error != null)
                                    return (request, uploadResult.Error);
                                writer.WritePropertyName(property.Name);
                                var el = System.Text.Json.JsonSerializer.SerializeToElement(uploadResult.FileStoredValue!);
                                el.WriteTo(writer);
                            }
                            else
                            {
                                // Already stored object (path, upload_person, etc.), keep as is
                                writer.WritePropertyName(property.Name);
                                property.Value.WriteTo(writer);
                            }
                        }
                        else
                        {
                            writer.WritePropertyName(property.Name);
                            property.Value.WriteTo(writer);
                        }
                    }
                    else
                    {
                        // Array file field
                        if (property.Value.ValueKind == JsonValueKind.Array)
                        {
                            writer.WritePropertyName(property.Name);
                            writer.WriteStartArray();

                            foreach (var itemElement in property.Value.EnumerateArray())
                            {
                                if (itemElement.ValueKind == JsonValueKind.String)
                                {
                                    writer.WriteStringValue(itemElement.GetString() ?? string.Empty);
                                }
                                else if (itemElement.ValueKind == JsonValueKind.Object)
                                {
                                    var hasContent = itemElement.TryGetProperty("content", out var ic) && ic.ValueKind == JsonValueKind.String;
                                    if (hasContent)
                                    {
                                        var uploadResult = await ProcessSingleFileFieldAsync(
                                            itemElement, property.Name, datasetName, domainName, effectiveRecordId, userInfo.userName);
                                        if (uploadResult.Error != null)
                                            return (request, uploadResult.Error);
                                        var el = System.Text.Json.JsonSerializer.SerializeToElement(uploadResult.FileStoredValue!);
                                        el.WriteTo(writer);
                                    }
                                    else
                                    {
                                        itemElement.WriteTo(writer);
                                    }
                                }
                                else
                                {
                                    itemElement.WriteTo(writer);
                                }
                            }

                            writer.WriteEndArray();
                        }
                        else
                        {
                            writer.WritePropertyName(property.Name);
                            property.Value.WriteTo(writer);
                        }
                    }
                }
                else
                {
                    // Not a file field, copy as is
                    writer.WritePropertyName(property.Name);
                    property.Value.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
            writer.Flush();

            // Parse the new JSON
            var jsonBytes = stream.ToArray();
            var updatedRequest = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(jsonBytes);

            return (updatedRequest, null);
        }

        /// <summary>
        /// Processes file fields - uploads files if object model is used, otherwise keeps existing paths
        /// </summary>
        private async Task<(Dictionary<string, object> Data, IActionResult? Error)> ProcessFileFieldsAsync(
            DatasetSchema schema,
            Dictionary<string, object> data,
            string datasetName,
            string domainName)
        {
            if (schema.fields == null || schema.fields.Count == 0)
                return (data, null);  // No fields to process

            var fileFields = schema.fields.Where(f => f.fieldType == "file").ToList();
            if (fileFields.Count == 0)
                return (data, null);  // No file fields in schema

            var userInfo = _userInfoService.GetCurrentUserInfo();
            var recordId = Guid.NewGuid().ToString();  // Generate record ID for new records

            foreach (var field in fileFields)
            {
                if (!data.ContainsKey(field.name))
                    continue;  // Field not present in data (optional field)

                var fieldValue = data[field.name];

                // Handle single file field
                if (!field.isArray)
                {
                    if (fieldValue is string)
                        continue; // Legacy path string, keep as is
                    if (fieldValue is JsonElement je && je.ValueKind == JsonValueKind.Object && !je.TryGetProperty("content", out _))
                        continue; // Already stored object (path, upload_person, etc.), keep as is
                    if (fieldValue is Dictionary<string, object> dict && !dict.ContainsKey("content"))
                        continue; // Already stored object, keep as is
                    if (fieldValue is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                    {
                        var uploadResult = await ProcessSingleFileFieldAsync(
                            jsonElement, field.name, datasetName, domainName, recordId, userInfo.userName);
                        if (uploadResult.Error != null)
                            return (data, uploadResult.Error);
                        data[field.name] = uploadResult.FileStoredValue!;
                    }
                    else if (fieldValue is Dictionary<string, object> fileObj)
                    {
                        var uploadResult = await ProcessSingleFileFieldFromDictAsync(
                            fileObj, field.name, datasetName, domainName, recordId, userInfo.userName);
                        if (uploadResult.Error != null)
                            return (data, uploadResult.Error);
                        data[field.name] = uploadResult.FileStoredValue!;
                    }
                    else if (fieldValue != null)
                    {
                        return (data, this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_FIELD",
                            $"Field '{field.name}' must be a string (file path) or an object with 'content' or 'path'"));
                    }
                }
                else
                {
                    if (fieldValue is JsonElement jsonArrayElement && jsonArrayElement.ValueKind == JsonValueKind.Array)
                    {
                        var processed = new List<object>();
                        foreach (var itemElement in jsonArrayElement.EnumerateArray())
                        {
                            if (itemElement.ValueKind == JsonValueKind.String)
                                processed.Add(itemElement.GetString() ?? string.Empty);
                            else if (itemElement.ValueKind == JsonValueKind.Object)
                            {
                                if (!itemElement.TryGetProperty("content", out _))
                                {
                                    // Already stored object - convert to dict for consistency
                                    processed.Add(System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(itemElement.GetRawText()) ?? new Dictionary<string, object>());
                                }
                                else
                                {
                                    var uploadResult = await ProcessSingleFileFieldAsync(
                                        itemElement, field.name, datasetName, domainName, recordId, userInfo.userName);
                                    if (uploadResult.Error != null)
                                        return (data, uploadResult.Error);
                                    processed.Add(uploadResult.FileStoredValue!);
                                }
                            }
                            else
                            {
                                return (data, this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_FIELD",
                                    $"Array field '{field.name}' must contain strings or objects with 'content' or 'path'"));
                            }
                        }
                        data[field.name] = processed;
                    }
                    else if (fieldValue is List<object> fileList)
                    {
                        var processed = new List<object>();
                        foreach (var item in fileList)
                        {
                            if (item is string s)
                                processed.Add(s);
                            else if (item is JsonElement je && je.ValueKind == JsonValueKind.Object)
                            {
                                if (!je.TryGetProperty("content", out _))
                                    processed.Add(System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(je.GetRawText()) ?? new Dictionary<string, object>());
                                else
                                {
                                    var uploadResult = await ProcessSingleFileFieldAsync(
                                        je, field.name, datasetName, domainName, recordId, userInfo.userName);
                                    if (uploadResult.Error != null)
                                        return (data, uploadResult.Error);
                                    processed.Add(uploadResult.FileStoredValue!);
                                }
                            }
                            else if (item is Dictionary<string, object> fileObj)
                            {
                                if (!fileObj.ContainsKey("content"))
                                    processed.Add(item);
                                else
                                {
                                    var uploadResult = await ProcessSingleFileFieldFromDictAsync(
                                        fileObj, field.name, datasetName, domainName, recordId, userInfo.userName);
                                    if (uploadResult.Error != null)
                                        return (data, uploadResult.Error);
                                    processed.Add(uploadResult.FileStoredValue!);
                                }
                            }
                            else
                            {
                                return (data, this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_FIELD",
                                    $"Array field '{field.name}' must contain strings or objects with 'content' or 'path'"));
                            }
                        }
                        data[field.name] = processed;
                    }
                    else if (fieldValue != null)
                    {
                        return (data, this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_FIELD",
                            $"Array field '{field.name}' must be an array of strings or objects"));
                    }
                }
            }

            return (data, null);
        }

        /// <summary>
        /// Builds the file stored value object for database persistence.
        /// Format: { path, upload_person, upload_time (ISO 8601), file_name, file_ext, file_size (KB) }.
        /// </summary>
        private static Dictionary<string, object> BuildFileStoredValue(
            FileProcessingResult result,
            string userName)
        {
            var ext = (result.Extension ?? string.Empty).TrimStart('.');
            var fileSizeKb = result.OriginalFileSize <= 0 ? 0L : (long)Math.Ceiling(result.OriginalFileSize / 1024.0);
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = result.FilePath ?? string.Empty,
                ["upload_person"] = userName ?? string.Empty,
                ["upload_time"] = result.UploadedAt.Kind == DateTimeKind.Utc
                    ? result.UploadedAt.ToString("o")
                    : result.UploadedAt.ToUniversalTime().ToString("o"),
                ["file_name"] = result.OriginalFileName ?? string.Empty,
                ["file_ext"] = ext,
                ["file_size"] = fileSizeKb
            };
        }

        /// <summary>
        /// Extracts the MinIO path from a file field value (legacy string or new object with "path").
        /// </summary>
        private static string? GetPathFromFileFieldValue(object? value)
        {
            if (value == null) return null;
            if (value is string s) return string.IsNullOrWhiteSpace(s) ? null : s;
            if (value is JsonElement je)
            {
                if (je.ValueKind != JsonValueKind.Object) return null;
                if (je.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String)
                    return string.IsNullOrWhiteSpace(p.GetString()) ? null : p.GetString();
                return null;
            }
            if (value is IDictionary<string, object> d && d.TryGetValue("path", out var pathVal) && pathVal != null)
            {
                if (pathVal is string ps) return string.IsNullOrWhiteSpace(ps) ? null : ps;
                if (pathVal is JsonElement pe && pe.ValueKind == JsonValueKind.String) return pe.GetString();
            }
            return null;
        }

        /// <summary>
        /// Processes a single file field from JsonElement object model.
        /// Returns the file stored value object (path, upload_person, upload_time, file_name, file_ext, file_size).
        /// </summary>
        private async Task<(Dictionary<string, object>? FileStoredValue, IActionResult? Error)> ProcessSingleFileFieldAsync(
            JsonElement fileObj,
            string fieldName,
            string datasetName,
            string domainName,
            string recordId,
            string userName)
        {
            try
            {
                // Extract file upload properties
                if (!fileObj.TryGetProperty("content", out var contentElement) || contentElement.ValueKind != JsonValueKind.String)
                {
                    return (null, this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_FIELD",
                        $"Field '{fieldName}' object must have 'content' property with base64 string"));
                }

                string content = contentElement.GetString() ?? string.Empty;
                string? folder = fileObj.TryGetProperty("folder", out var folderElement) && folderElement.ValueKind == JsonValueKind.String
                    ? folderElement.GetString()
                    : null;
                bool? useCompression = fileObj.TryGetProperty("useCompression", out var compressionElement) && (compressionElement.ValueKind == JsonValueKind.True || compressionElement.ValueKind == JsonValueKind.False)
                    ? compressionElement.GetBoolean()
                    : null;
                bool? useEncryption = fileObj.TryGetProperty("useEncryption", out var encryptionElement) && (encryptionElement.ValueKind == JsonValueKind.True || encryptionElement.ValueKind == JsonValueKind.False)
                    ? encryptionElement.GetBoolean()
                    : null;
                string? originalFileName = fileObj.TryGetProperty("originalFileName", out var ofn) && ofn.ValueKind == JsonValueKind.String
                    ? ofn.GetString()
                    : (fileObj.TryGetProperty("file_name", out var fn) && fn.ValueKind == JsonValueKind.String ? fn.GetString() : null);

                var schema = await _datasetService.GetSchemaEntityByNameAsync(datasetName);
                var field = schema?.fields?.FirstOrDefault(f => f.name == fieldName);
                var fileOptions = GetFileOptionsFromField(field, _settings.FileStorage.Validation);

                var processingResult = await _fileProcessingPipeline.ProcessFileUploadAsync(
                    new FileUploadRequestDto
                    {
                        Content = content,
                        Folder = folder,
                        UseCompression = useCompression,
                        UseEncryption = useEncryption,
                        OriginalFileName = originalFileName
                    },
                    domainName,
                    datasetName,
                    recordId,
                    userName,
                    fileOptions,
                    HttpContext.RequestAborted);

                return (BuildFileStoredValue(processingResult, userName), null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process file field '{FieldName}'", fieldName);
                return (null, this.ErrorResponse(GetApiPath(datasetName), "FILE_UPLOAD_FAILED",
                    $"Failed to upload file for field '{fieldName}': {ex.Message}"));
            }
        }

        /// <summary>
        /// Processes a single file field from Dictionary object model.
        /// Returns the file stored value object (path, upload_person, upload_time, file_name, file_ext, file_size).
        /// </summary>
        private async Task<(Dictionary<string, object>? FileStoredValue, IActionResult? Error)> ProcessSingleFileFieldFromDictAsync(
            Dictionary<string, object> fileObj,
            string fieldName,
            string datasetName,
            string domainName,
            string recordId,
            string userName)
        {
            try
            {
                if (!fileObj.TryGetValue("content", out var contentObj) || contentObj is not string content)
                {
                    return (null, this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_FIELD",
                        $"Field '{fieldName}' object must have 'content' property with base64 string"));
                }

                string? folder = fileObj.TryGetValue("folder", out var folderObj) && folderObj is string folderStr
                    ? folderStr
                    : null;
                bool? useCompression = fileObj.TryGetValue("useCompression", out var compressionObj) && compressionObj is bool compressionBool
                    ? compressionBool
                    : null;
                bool? useEncryption = fileObj.TryGetValue("useEncryption", out var encryptionObj) && encryptionObj is bool encryptionBool
                    ? encryptionBool
                    : null;
                string? originalFileName = (fileObj.TryGetValue("originalFileName", out var ofnObj) && ofnObj is string ofnStr)
                    ? ofnStr
                    : (fileObj.TryGetValue("file_name", out var fnObj) && fnObj is string fnStr ? fnStr : null);

                var schema = await _datasetService.GetSchemaEntityByNameAsync(datasetName);
                var field = schema?.fields?.FirstOrDefault(f => f.name == fieldName);
                var fileOptions = GetFileOptionsFromField(field, _settings.FileStorage.Validation);

                var processingResult = await _fileProcessingPipeline.ProcessFileUploadAsync(
                    new FileUploadRequestDto
                    {
                        Content = content,
                        Folder = folder,
                        UseCompression = useCompression,
                        UseEncryption = useEncryption,
                        OriginalFileName = originalFileName
                    },
                    domainName,
                    datasetName,
                    recordId,
                    userName,
                    fileOptions,
                    HttpContext.RequestAborted);

                return (BuildFileStoredValue(processingResult, userName), null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process file field '{FieldName}'", fieldName);
                return (null, this.ErrorResponse(GetApiPath(datasetName), "FILE_UPLOAD_FAILED",
                    $"Failed to upload file for field '{fieldName}': {ex.Message}"));
            }
        }

        /// <summary>
        /// Gets file processing options from field definition
        /// </summary>
        private FileProcessingOptionsDto GetFileOptionsFromField(
            FieldDefinition? field,
            ValidationSettings validationSettings)
        {
            // For now, use configuration defaults
            // TODO: Phase 2+ - Parse fileOptions from field definition if available
            
            var options = new FileProcessingOptionsDto
            {
                MaxFileSize = validationSettings.MaxFileSize,
                AllowedExtensions = validationSettings.AllowedExtensions ?? new List<string>(),
                MaxFolderDepth = validationSettings.MaxFolderDepth,
                MaxPathLength = validationSettings.MaxPathLength,
                DefaultCompression = _settings.FileStorage.Compression.Enabled,
                DefaultEncryption = _settings.FileStorage.Encryption.Enabled,
                CompressionLevel = _settings.FileStorage.Compression.Level
            };

            return options;
        }

        /// <summary>
        /// Validates file field paths in data dictionary
        /// Checks if file fields contain valid MinIO paths
        /// </summary>
        private IActionResult? ValidateFileFields(
            DatasetSchema schema,
            Dictionary<string, object> data,
            string datasetName)
        {
            if (schema.fields == null || schema.fields.Count == 0)
                return null;  // No fields to validate

            var domainName = _mongoContextService.GetCurrentDomainName();
            if (string.IsNullOrEmpty(domainName))
                return this.ErrorResponse(GetApiPath(datasetName), "FORBIDDEN", "Domain information not found in token", statusCode: 403);

            var fileFields = schema.fields.Where(f => f.fieldType == "file").ToList();
            if (fileFields.Count == 0)
                return null;  // No file fields in schema

            foreach (var field in fileFields)
            {
                if (!data.ContainsKey(field.name))
                    continue;

                var fieldValue = data[field.name];

                if (!field.isArray)
                {
                    if (fieldValue == null) continue;
                    var path = GetPathFromFileFieldValue(fieldValue);
                    if (path == null)
                    {
                        return this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_FIELD",
                            $"Field '{field.name}' must be a string (file path) or an object with 'path'");
                    }
                    var validationResult = ValidateFilePath(path, domainName, datasetName, field.name);
                    if (validationResult != null)
                        return validationResult;
                }
                else
                {
                    if (fieldValue == null) continue;
                    if (fieldValue is not List<object> list)
                    {
                        return this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_FIELD",
                            $"Array field '{field.name}' must be an array of strings (file path) or objects with 'path', or null");
                    }
                    foreach (var item in list)
                    {
                        if (item == null) continue;
                        var path = GetPathFromFileFieldValue(item);
                        if (path == null)
                        {
                            return this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_FIELD",
                                $"Array field '{field.name}' items must be strings (file path) or objects with 'path'");
                        }
                        var validationResult = ValidateFilePath(path, domainName, datasetName, field.name);
                        if (validationResult != null)
                            return validationResult;
                    }
                }
            }

            return null;  // All validations passed
        }

        /// <summary>
        /// Validates a single file path
        /// </summary>
        private IActionResult? ValidateFilePath(
            string filePath,
            string domainName,
            string datasetName,
            string fieldName)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;  // Empty path is allowed (optional field)

            // Check path format: /mng-{domain}/data/users/{dataset}/...
            var pathParts = filePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            if (pathParts.Length < 5)
            {
                return this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_PATH",
                    $"Invalid file path format for field '{fieldName}': {filePath}");
            }

            // Check domain match
            if (!pathParts[0].StartsWith("mng-"))
            {
                return this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_PATH",
                    $"File path must start with '/mng-{{domain}}/': {filePath}");
            }

            var pathDomain = pathParts[0].Replace("mng-", "");
            if (pathDomain != domainName)
            {
                return this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_PATH",
                    $"File path domain '{pathDomain}' does not match current domain '{domainName}'");
            }

            // Check data folder
            if (pathParts[1] != "data")
            {
                return this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_PATH",
                    $"File path must contain '/data/' folder: {filePath}");
            }

            // Check users folder
            if (pathParts[2] != "users")
            {
                return this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_PATH",
                    $"File path must contain '/data/users/' folder: {filePath}");
            }

            // Check dataset match
            if (pathParts[3] != datasetName)
            {
                return this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_PATH",
                    $"File path dataset '{pathParts[3]}' does not match dataset '{datasetName}'");
            }

            // Path format is valid
            return null;
        }

        /// <summary>
        /// Bulk create multiple data records
        /// </summary>
        /// <param name="datasetName">Dataset name (e.g., @tasks)</param>
        /// <param name="request">Bulk create request with items array</param>
        /// <param name="skipEventPublish">If true, no RabbitMQ/event publish for this request.</param>
        /// <returns>Bulk insert result with successful items and errors</returns>
        [HttpPost("bulk")]
        [ProducesResponseType(typeof(DataResponseDto<BulkInsertResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> BulkCreate(
            [FromRoute] string datasetName,
            [FromBody] JsonElement request,
            [FromQuery] bool skipEventPublish = false)
        {
            var bulkPath = GetApiPath(datasetName, action: "bulk");
            try
            {
                // Parse request body
                if (request.ValueKind != JsonValueKind.Object || !request.TryGetProperty("items", out var itemsElement))
                {
                    return this.ErrorResponse(bulkPath, "INVALID_REQUEST", "Request must contain 'items' array");
                }

                if (itemsElement.ValueKind != JsonValueKind.Array || itemsElement.GetArrayLength() == 0)
                {
                    return this.ErrorResponse(bulkPath, "INVALID_REQUEST", "Items array is required and cannot be empty");
                }

                // Convert JsonElement array to List<Dictionary<string, object>>
                var items = itemsElement.ToDictionaryList();

                // Limit check (prevent DoS)
                if (items.Count > 1000)
                {
                    return this.ErrorResponse(bulkPath, "BATCH_SIZE_EXCEEDED", "Maximum 1000 items allowed per bulk insert");
                }

                var domainName = _mongoContextService.GetCurrentDomainName()
                    ?? throw new UnauthorizedAccessException("Domain not found in token");
                var database = _mongoContextService.GetDatabase();
                var userInfo = _userInfoService.GetCurrentUserInfo();
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                var result = await _dataService.BulkCreateAsync(
                    datasetName,
                    items,
                    domainName,
                    database.DatabaseNamespace.DatabaseName,
                    userInfo.uid,
                    userInfo.userName,
                    ipAddress,
                    skipEventPublish);

                return this.SuccessResponse(result, bulkPath);
            }
            catch (DataGatewayException ex) when (ex.Message.Contains("not found"))
            {
                return this.HandleNotFoundError(ex, bulkPath, _logger);
            }
            catch (Exception ex)
            {
                return this.HandleError(ex, bulkPath, "BULK_CREATE_FAILED", "Failed to bulk create data", _logger);
            }
        }

        #endregion
    }
}

