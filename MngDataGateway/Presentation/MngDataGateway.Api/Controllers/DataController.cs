using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDataGateway.Api.Helpers;
using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Application.DTOs.Data;
using MngDataGateway.Application.DTOs.Validation;
using MngDataGateway.Application.Services;
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

        public DataController(
            ILogger<DataController> logger,
            IDataService dataService,
            IMongoContextService mongoContextService,
            IUserInfoService userInfoService,
            IDatasetService datasetService,
            IPermissionService permissionService,
            CsvConverter csvConverter)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _mongoContextService = mongoContextService ?? throw new ArgumentNullException(nameof(mongoContextService));
            _userInfoService = userInfoService ?? throw new ArgumentNullException(nameof(userInfoService));
            _datasetService = datasetService ?? throw new ArgumentNullException(nameof(datasetService));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _csvConverter = csvConverter ?? throw new ArgumentNullException(nameof(csvConverter));
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
        /// <returns>Created data with generated fields</returns>
        [HttpPost]
        [ProducesResponseType(typeof(DataResponseDto<Dictionary<string, object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create(
            [FromRoute] string datasetName,
            [FromBody] JsonElement request)
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

                // Convert JsonElement to Dictionary with proper types
                var data = request.ToDictionary();

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
                    ipAddress);

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
        /// <returns>Updated data</returns>
        [HttpPut("{dataId}")]
        [ProducesResponseType(typeof(DataResponseDto<Dictionary<string, object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            [FromRoute] string datasetName,
            [FromRoute] string dataId,
            [FromBody] JsonElement request)
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

                // Convert JsonElement to Dictionary with proper types
                var data = request.ToDictionary();

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
                    ipAddress);

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
        /// <returns>Success status</returns>
        [HttpDelete("{dataId}")]
        [ProducesResponseType(typeof(DataResponseDto<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            [FromRoute] string datasetName,
            [FromRoute] string dataId)
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
                    ipAddress);

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
        /// <returns>Success status</returns>
        [HttpPost("{dataId}/restore")]
        [ProducesResponseType(typeof(DataResponseDto<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Restore(
            [FromRoute] string datasetName,
            [FromRoute] string dataId)
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
                    ipAddress);

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
                    continue;  // Field not present in data (optional field)

                var fieldValue = data[field.name];

                // Handle single file field
                if (!field.isArray)
                {
                    if (fieldValue is string filePath)
                    {
                        var validationResult = ValidateFilePath(filePath, domainName, datasetName, field.name);
                        if (validationResult != null)
                            return validationResult;
                    }
                    else if (fieldValue != null)
                    {
                        return this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_FIELD",
                            $"Field '{field.name}' must be a string (file path) or null");
                    }
                }
                // Handle array file field
                else
                {
                    if (fieldValue is List<object> filePaths)
                    {
                        foreach (var path in filePaths)
                        {
                            if (path is string filePathStr)
                            {
                                var validationResult = ValidateFilePath(filePathStr, domainName, datasetName, field.name);
                                if (validationResult != null)
                                    return validationResult;
                            }
                            else
                            {
                                return this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_FIELD",
                                    $"Array field '{field.name}' must contain string values (file paths)");
                            }
                        }
                    }
                    else if (fieldValue != null)
                    {
                        return this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_FIELD",
                            $"Array field '{field.name}' must be an array of strings (file paths) or null");
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

            // Check path format: /mng-{domain}/data/{dataset}/...
            var pathParts = filePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            if (pathParts.Length < 4)
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

            // Check dataset match
            if (pathParts[2] != datasetName)
            {
                return this.ErrorResponse(GetApiPath(datasetName), "INVALID_FILE_PATH",
                    $"File path dataset '{pathParts[2]}' does not match dataset '{datasetName}'");
            }

            // Path format is valid
            return null;
        }

        /// <summary>
        /// Bulk create multiple data records
        /// </summary>
        /// <param name="datasetName">Dataset name (e.g., @tasks)</param>
        /// <param name="request">Bulk create request with items array</param>
        /// <returns>Bulk insert result with successful items and errors</returns>
        [HttpPost("bulk")]
        [ProducesResponseType(typeof(DataResponseDto<BulkInsertResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> BulkCreate(
            [FromRoute] string datasetName,
            [FromBody] JsonElement request)
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
                    ipAddress);

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

