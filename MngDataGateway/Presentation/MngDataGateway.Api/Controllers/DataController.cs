using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Application.DTOs.Data;
using MngDataGateway.Application.DTOs.Validation;
using MngDataGateway.Application.Services;
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

        public DataController(
            ILogger<DataController> logger,
            IDataService dataService,
            IMongoContextService mongoContextService,
            IUserInfoService userInfoService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _mongoContextService = mongoContextService ?? throw new ArgumentNullException(nameof(mongoContextService));
            _userInfoService = userInfoService ?? throw new ArgumentNullException(nameof(userInfoService));
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
        /// List data with pagination
        /// </summary>
        /// <param name="datasetName">Dataset name</param>
        /// <param name="skip">Number of records to skip (default: 0)</param>
        /// <param name="limit">Maximum records to return (default: 50, max: 1000)</param>
        /// <returns>List of data with pagination info</returns>
        [HttpGet]
        [ProducesResponseType(typeof(DataResponseDto<PagedResultDto<Dictionary<string, object>>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List(
            [FromRoute] string datasetName,
            [FromQuery] int skip = 0,
            [FromQuery] int limit = 50)
        {
            try
            {
                // Validate pagination params
                if (skip < 0) skip = 0;
                if (limit < 1 || limit > 1000) limit = 50;

                var database = _mongoContextService.GetDatabase();

                var (data, totalCount) = await _dataService.ListAsync(
                    datasetName,
                    database.DatabaseNamespace.DatabaseName,
                    skip,
                    limit);

                var pagedResult = new PagedResultDto<Dictionary<string, object>>
                {
                    Items = data,
                    TotalCount = totalCount,
                    PageNumber = (skip / limit) + 1,
                    PageSize = limit
                };

                return Ok(new DataResponseDto<PagedResultDto<Dictionary<string, object>>>
                {
                    Success = true,
                    Data = pagedResult,
                    Meta = new ResponseMetaDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Path = $"/api/data/{datasetName}"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list data in dataset {DatasetName}", datasetName);
                return StatusCode(500, new ErrorResponseDto
                {
                    Success = false,
                    Error = new ErrorDetailDto
                    {
                        Code = "LIST_FAILED",
                        Message = "Failed to list data",
                        Details = ex.Message
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
        /// Get single data by ID
        /// </summary>
        /// <param name="datasetName">Dataset name</param>
        /// <param name="dataId">Data ID (__dataId)</param>
        /// <returns>Single data record</returns>
        [HttpGet("{dataId}")]
        [ProducesResponseType(typeof(DataResponseDto<Dictionary<string, object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(
            [FromRoute] string datasetName,
            [FromRoute] string dataId)
        {
            try
            {
                var database = _mongoContextService.GetDatabase();

                var result = await _dataService.GetByIdAsync(
                    datasetName,
                    dataId,
                    database.DatabaseNamespace.DatabaseName);

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
                        Path = $"/api/datasets/{datasetName}/data/{dataId}"
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
                        Path = $"/api/datasets/{datasetName}/data/{dataId}"
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
                        Path = $"/api/datasets/{datasetName}/data/{dataId}"
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
                        Path = $"/api/datasets/{datasetName}/data/{dataId}"
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
                        Path = $"/api/datasets/{datasetName}/data/{dataId}"
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
                        Path = $"/api/datasets/{datasetName}/data/{dataId}"
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
                        Path = $"/api/datasets/{datasetName}/data/{dataId}"
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

        #endregion
    }
}

