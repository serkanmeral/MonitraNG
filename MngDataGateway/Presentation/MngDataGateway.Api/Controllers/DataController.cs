using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Application.DTOs.Data;
using MngDataGateway.Application.DTOs.Validation;
using MngDataGateway.Application.Services;
using MngDataGateway.Domain.Entities;
using MngDataGateway.Domain.Exceptions;

namespace MngDataGateway.Api.Controllers
{
    /// <summary>
    /// Data CRUD operations controller
    /// Dynamic data management for datasets
    /// </summary>
    [ApiController]
    [Route("api/data/{datasetName}")]
    [Authorize]
    public class DataController : ControllerBase
    {
        private readonly ILogger<DataController> _logger;
        private readonly IDataService _dataService;
        private readonly IMongoContextService _mongoContextService;
        private readonly IUserInfoService _userInfoService;
        private readonly IDatasetService _datasetService;

        public DataController(
            ILogger<DataController> logger,
            IDataService dataService,
            IMongoContextService mongoContextService,
            IUserInfoService userInfoService,
            IDatasetService datasetService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _mongoContextService = mongoContextService ?? throw new ArgumentNullException(nameof(mongoContextService));
            _userInfoService = userInfoService ?? throw new ArgumentNullException(nameof(userInfoService));
            _datasetService = datasetService ?? throw new ArgumentNullException(nameof(datasetService));
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
                var userInfo = _userInfoService.GetCurrentUserInfo();
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                // Convert JsonElement to Dictionary with proper types
                var data = JsonElementToDictionary(request);

                var result = await _dataService.CreateAsync(
                    datasetName,
                    data,
                    domainName,
                    database.DatabaseNamespace.DatabaseName,
                    userInfo.uid,
                    userInfo.userName,
                    ipAddress);

                return Ok(new DataResponseDto<Dictionary<string, object>>
                {
                    Success = true,
                    Data = result,
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}"
                    }
                });
            }
            catch (DataGatewayException ex) when (ex.ValidationErrors != null)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "VALIDATION_ERROR",
                        Message = ex.Message,
                        Details = ex.ValidationErrors
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}"
                    }
                });
            }
            catch (DataGatewayException ex) when (ex.Message.Contains("not found"))
            {
                return NotFound(new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "DATASET_NOT_FOUND",
                        Message = ex.Message
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create data in dataset {DatasetName}", datasetName);
                
                // Include inner exception for debugging
                var errorMessage = ex.Message;
                var innerMessage = ex.InnerException?.Message;
                var stackTrace = ex.StackTrace;
                
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "CREATE_FAILED",
                        Message = "Failed to create data",
                        Details = new {
                            message = errorMessage,
                            innerException = innerMessage,
                            stackTrace = stackTrace?.Split('\n').Take(5).ToArray()
                        }
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}"
                    }
                });
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
            [FromQuery] string? fields = null)
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
                        return NotFound(new ErrorResponseDto
                        {
                            Success = false,
                            Error = new ErrorDetailDto
                            {
                                Code = "DATASET_NOT_FOUND",
                                Message = $"Dataset '{datasetName}' not found"
                            },
                            Meta = new ResponseMetaDto
                            {
                                Timestamp = DateTime.UtcNow,
                                Path = $"/api/data/{datasetName}"
                            }
                        });
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
                    Fields = fields
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

                // Always return array (even if single item)
                return Ok(result.Data);
            }
            catch (DataGatewayException ex) when (ex.ValidationErrors != null)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "VALIDATION_ERROR",
                        Message = ex.Message,
                        Details = ex.ValidationErrors
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}"
                    }
                });
            }
            catch (DataGatewayException ex)
            {
                _logger.LogWarning(ex, "DataGatewayException in List for dataset {DatasetName}: {Message}, Inner: {InnerMessage}", 
                    datasetName, ex.Message, ex.InnerException?.Message);
                return NotFound(new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "DATASET_NOT_FOUND",
                        Message = ex.Message,
                        Details = ex.InnerException?.Message
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list data in dataset {DatasetName}. Exception: {ExceptionType}, Message: {Message}, Inner: {InnerMessage}, StackTrace: {StackTrace}", 
                    datasetName, ex.GetType().Name, ex.Message, ex.InnerException?.Message, ex.StackTrace);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "LIST_FAILED",
                        Message = "Failed to list data",
                        Details = $"{ex.GetType().Name}: {ex.Message}"
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}"
                    }
                });
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

                // Handle showDataset
                if (showDataset)
                {
                    var schemaDto = await _datasetService.GetByNameAsync(datasetName);
                    if (schemaDto == null)
                    {
                        return NotFound(new ErrorResponseDto
                        {
                            Success = false,
                            Error = new ErrorDetailDto
                            {
                                Code = "DATASET_NOT_FOUND",
                                Message = $"Dataset '{datasetName}' not found"
                            },
                            Meta = new ResponseMetaDto
                            {
                                Timestamp = DateTime.UtcNow,
                                Path = $"/api/data/{datasetName}/{dataId}"
                            }
                        });
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
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Error = new ErrorDetailDto
                        {
                            Code = "DATA_NOT_FOUND",
                            Message = $"Data with __dataId '{dataId}' not found"
                        },
                        Meta = new ResponseMetaDto
                        {
                            Timestamp = DateTime.UtcNow,
                            Path = $"/api/data/{datasetName}/{dataId}"
                        }
                    });
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
                return NotFound(new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "DATASET_NOT_FOUND",
                        Message = ex.Message
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/{dataId}"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get data {DataId} in dataset {DatasetName}", dataId, datasetName);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "GET_FAILED",
                        Message = "Failed to get data",
                        Details = ex.Message
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/{dataId}"
                    }
                });
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
                var userInfo = _userInfoService.GetCurrentUserInfo();
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                // Convert JsonElement to Dictionary with proper types
                var data = JsonElementToDictionary(request);

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
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Error = new ErrorDetailDto
                        {
                            Code = "DATA_NOT_FOUND",
                            Message = $"Data with __dataId '{dataId}' not found"
                        },
                        Meta = new ResponseMetaDto
                        {
                            Timestamp = DateTime.UtcNow,
                            Path = $"/api/data/{datasetName}/{dataId}"
                        }
                    });
                }

                return Ok(new DataResponseDto<Dictionary<string, object>>
                {
                    Success = true,
                    Data = result,
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/{dataId}"
                    }
                });
            }
            catch (DataGatewayException ex) when (ex.ValidationErrors != null)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "VALIDATION_ERROR",
                        Message = ex.Message,
                        Details = ex.ValidationErrors
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/{dataId}"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update data {DataId} in dataset {DatasetName}", dataId, datasetName);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "UPDATE_FAILED",
                        Message = "Failed to update data",
                        Details = ex.Message
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/{dataId}"
                    }
                });
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
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Error = new ErrorDetailDto
                        {
                            Code = "DATA_NOT_FOUND",
                            Message = $"Data with __dataId '{dataId}' not found"
                        },
                        Meta = new ResponseMetaDto
                        {
                            Timestamp = DateTime.UtcNow,
                            Path = $"/api/data/{datasetName}/{dataId}"
                        }
                    });
                }

                return Ok(new DataResponseDto<object>
                {
                    Success = true,
                    Data = new { message = "Data deleted successfully", dataId },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/{dataId}"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete data {DataId} in dataset {DatasetName}", dataId, datasetName);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "DELETE_FAILED",
                        Message = "Failed to delete data",
                        Details = ex.Message
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/{dataId}"
                    }
                });
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
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Error = new ErrorDetailDto
                        {
                            Code = "DATA_NOT_FOUND",
                            Message = $"Deleted data with __dataId '{dataId}' not found"
                        },
                        Meta = new ResponseMetaDto
                        {
                            Timestamp = DateTime.UtcNow,
                            Path = $"/api/data/{datasetName}/{dataId}/restore"
                        }
                    });
                }

                return Ok(new DataResponseDto<object>
                {
                    Success = true,
                    Data = new { message = "Data restored successfully", dataId },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/{dataId}/restore"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore data {DataId} in dataset {DatasetName}", dataId, datasetName);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "RESTORE_FAILED",
                        Message = "Failed to restore data",
                        Details = ex.Message
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/{dataId}/restore"
                    }
                });
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
                        return NotFound(new ErrorResponseDto
                        {
                            Success = false,
                            Error = new ErrorDetailDto
                            {
                                Code = "DATASET_NOT_FOUND",
                                Message = $"Dataset '{datasetName}' not found"
                            },
                            Meta = new ResponseMetaDto
                            {
                                Timestamp = DateTime.UtcNow,
                                Path = $"/api/data/{datasetName}/query"
                            }
                        });
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
                    matchDict = JsonElementToDictionary(request.Match);
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
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "VALIDATION_ERROR",
                        Message = ex.Message,
                        Details = ex.ValidationErrors
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/query"
                    }
                });
            }
            catch (DataGatewayException ex)
            {
                return NotFound(new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "DATASET_NOT_FOUND",
                        Message = ex.Message
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/query"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query data in dataset {DatasetName}", datasetName);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "QUERY_FAILED",
                        Message = "Failed to query data",
                        Details = ex.Message
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/query"
                    }
                });
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
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Error = new ErrorDetailDto
                        {
                            Code = "DATASET_NOT_FOUND",
                            Message = $"Dataset '{datasetName}' not found"
                        },
                        Meta = new ResponseMetaDto
                        {
                            Timestamp = DateTime.UtcNow,
                            Path = $"/api/data/{datasetName}/aggregate"
                        }
                    });
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
                _logger.LogError(ex, "Invalid aggregate pipeline for dataset {DatasetName}", datasetName);
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "INVALID_PIPELINE",
                        Message = "Invalid aggregate pipeline",
                        Details = ex.Message
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/aggregate"
                    }
                });
            }
            catch (MongoDB.Driver.MongoCommandException ex)
            {
                _logger.LogError(ex, "MongoDB command error for dataset {DatasetName}", datasetName);
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "MONGO_ERROR",
                        Message = "MongoDB error",
                        Details = ex.Message
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/aggregate"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute aggregate in dataset {DatasetName}", datasetName);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "AGGREGATE_FAILED",
                        Message = "Failed to execute aggregate",
                        Details = ex.Message
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/aggregate"
                    }
                });
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
                if (ex.Message.Contains("not found"))
                {
                    return NotFound(new ErrorResponseDto
                    {
                        Success = false,
                        Error = new ErrorDetailDto
                        {
                            Code = "QUERY_NOT_FOUND",
                            Message = ex.Message
                        },
                        Meta = new ResponseMetaDto
                        {
                            Timestamp = DateTime.UtcNow,
                            Path = $"/api/data/{datasetName}/queries/{queryName}"
                        }
                    });
                }

                _logger.LogError(ex, "DataGatewayException in ExecutePredefinedQuery for dataset {DatasetName}. Message: {Message}, Inner: {InnerMessage}, StackTrace: {StackTrace}", 
                    datasetName, ex.Message, ex.InnerException?.Message, ex.StackTrace);
                return BadRequest(new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "QUERY_ERROR",
                        Message = $"Failed to execute predefined query '{queryName}'",
                        Details = $"{ex.GetType().Name}: {ex.Message}" + (ex.InnerException != null ? $" | Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}" : "")
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/queries/{queryName}"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute predefined query '{QueryName}' in dataset {DatasetName}", queryName, datasetName);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "QUERY_EXECUTION_FAILED",
                        Message = "Failed to execute predefined query",
                        Details = ex.Message
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/queries/{queryName}"
                    }
                });
            }
        }

        #region Helper Methods

        /// <summary>
        /// Convert JsonElement to Dictionary with proper type preservation
        /// </summary>
        private Dictionary<string, object> JsonElementToDictionary(JsonElement element)
        {
            var dictionary = new Dictionary<string, object>();

            if (element.ValueKind != JsonValueKind.Object)
                return dictionary;

            foreach (var property in element.EnumerateObject())
            {
                dictionary[property.Name] = GetValue(property.Value);
            }

            return dictionary;
        }

        private object GetValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString()!,
                JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                JsonValueKind.Array => element.EnumerateArray().Select(GetValue).ToList(),
                JsonValueKind.Object => JsonElementToDictionary(element),
                _ => element.ToString()!
            };
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
            try
            {
                // Parse request body
                if (request.ValueKind != JsonValueKind.Object || !request.TryGetProperty("items", out var itemsElement))
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Success = false,
                        Error = new ErrorDetailDto
                        {
                            Code = "INVALID_REQUEST",
                            Message = "Request must contain 'items' array"
                        },
                        Meta = new ResponseMetaDto
                        {
                            Timestamp = DateTime.UtcNow,
                            Path = $"/api/data/{datasetName}/bulk"
                        }
                    });
                }

                if (itemsElement.ValueKind != JsonValueKind.Array || itemsElement.GetArrayLength() == 0)
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Success = false,
                        Error = new ErrorDetailDto
                        {
                            Code = "INVALID_REQUEST",
                            Message = "Items array is required and cannot be empty"
                        },
                        Meta = new ResponseMetaDto
                        {
                            Timestamp = DateTime.UtcNow,
                            Path = $"/api/data/{datasetName}/bulk"
                        }
                    });
                }

                // Convert JsonElement array to List<Dictionary<string, object>>
                var items = new List<Dictionary<string, object>>();
                foreach (var itemElement in itemsElement.EnumerateArray())
                {
                    var itemDict = JsonElementToDictionary(itemElement);
                    items.Add(itemDict);
                }

                // Limit check (prevent DoS)
                if (items.Count > 1000)
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        Success = false,
                        Error = new ErrorDetailDto
                        {
                            Code = "BATCH_SIZE_EXCEEDED",
                            Message = "Maximum 1000 items allowed per bulk insert"
                        },
                        Meta = new ResponseMetaDto
                        {
                            Timestamp = DateTime.UtcNow,
                            Path = $"/api/data/{datasetName}/bulk"
                        }
                    });
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

                return Ok(new DataResponseDto<BulkInsertResultDto>
                {
                    Success = true,
                    Data = result,
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/bulk"
                    }
                });
            }
            catch (DataGatewayException ex) when (ex.Message.Contains("not found"))
            {
                return NotFound(new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "DATASET_NOT_FOUND",
                        Message = ex.Message
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/bulk"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bulk create data in dataset {DatasetName}", datasetName);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "BULK_CREATE_FAILED",
                        Message = "Failed to bulk create data",
                        Details = ex.Message
                    },
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}/bulk"
                    }
                });
            }
        }

        #endregion
    }
}

