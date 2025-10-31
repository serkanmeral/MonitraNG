using Microsoft.AspNetCore.Mvc;
using MngKeeper.Application.Interfaces;
using MngKeeper.Api.Attributes;

namespace MngKeeper.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AdminAuthorization]
    public class RedisController : ControllerBase
    {
        private readonly IRedisService _redisService;
        private readonly ISessionService _sessionService;

        public RedisController(IRedisService redisService, ISessionService sessionService)
        {
            _redisService = redisService;
            _sessionService = sessionService;
        }

        [HttpGet("health")]
        public async Task<ActionResult<object>> Health()
        {
            try
            {
                var isConnected = await _redisService.IsConnectedAsync();
                return Ok(new { 
                    Status = "Redis Controller is working!",
                    Connected = isConnected,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Error",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost("connect")]
        public async Task<ActionResult<object>> Connect()
        {
            try
            {
                await _redisService.ConnectAsync();
                return Ok(new { 
                    Status = "Connected to Redis successfully",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Connection failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost("set")]
        public async Task<ActionResult<object>> Set([FromBody] SetValueRequest request)
        {
            try
            {
                var success = await _redisService.SetAsync(request.Key, request.Value, request.Expiry);
                return Ok(new { 
                    Status = success ? "Value set successfully" : "Failed to set value",
                    Key = request.Key,
                    Success = success,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Set failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpGet("get/{key}")]
        public async Task<ActionResult<object>> Get(string key)
        {
            try
            {
                var value = await _redisService.GetAsync<object>(key);
                var exists = await _redisService.ExistsAsync(key);
                var ttl = await _redisService.GetTimeToLiveAsync(key);

                return Ok(new { 
                    Key = key,
                    Value = value,
                    Exists = exists,
                    TimeToLive = ttl,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Get failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpDelete("delete/{key}")]
        public async Task<ActionResult<object>> Delete(string key)
        {
            try
            {
                var success = await _redisService.DeleteAsync(key);
                return Ok(new { 
                    Status = success ? "Key deleted successfully" : "Failed to delete key",
                    Key = key,
                    Success = success,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Delete failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost("increment/{key}")]
        public async Task<ActionResult<object>> Increment(string key, [FromQuery] long value = 1)
        {
            try
            {
                var result = await _redisService.IncrementAsync(key, value);
                return Ok(new { 
                    Key = key,
                    IncrementValue = value,
                    NewValue = result,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Increment failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost("session/create")]
        public async Task<ActionResult<object>> CreateSession([FromBody] CreateSessionRequest request)
        {
            try
            {
                // Get domain from token claims
                var claims = HttpContext.Items["TokenClaims"] as TokenClaims;
                if (claims?.DomainId == null)
                {
                    return BadRequest(new { Message = "Domain information not found in token." });
                }

                var sessionData = new SessionData
                {
                    UserId = request.UserId ?? claims.DomainId,
                    DomainId = claims.DomainId,
                    Username = request.Username ?? "testuser",
                    Roles = request.Roles ?? new List<string> { "user" },
                    Claims = request.Claims ?? new Dictionary<string, object>()
                };

                var sessionId = await _sessionService.CreateSessionAsync(sessionData, request.Expiry);
                
                if (string.IsNullOrEmpty(sessionId))
                {
                    return StatusCode(500, new { Message = "Failed to create session" });
                }

                return Ok(new { 
                    SessionId = sessionId,
                    UserId = sessionData.UserId,
                    DomainId = sessionData.DomainId,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Session creation failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpGet("session/{sessionId}")]
        public async Task<ActionResult<object>> GetSession(string sessionId)
        {
            try
            {
                var sessionData = await _sessionService.GetSessionAsync(sessionId);
                var isValid = await _sessionService.IsSessionValidAsync(sessionId);

                if (sessionData == null)
                {
                    return NotFound(new { Message = "Session not found" });
                }

                return Ok(new { 
                    SessionId = sessionId,
                    SessionData = sessionData,
                    IsValid = isValid,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Get session failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost("session/{sessionId}/extend")]
        public async Task<ActionResult<object>> ExtendSession(string sessionId, [FromQuery] int? minutes = null)
        {
            try
            {
                var expiry = minutes.HasValue ? TimeSpan.FromMinutes(minutes.Value) : (TimeSpan?)null;
                var success = await _sessionService.ExtendSessionAsync(sessionId, expiry);

                return Ok(new { 
                    SessionId = sessionId,
                    Success = success,
                    ExtensionMinutes = minutes,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Extend session failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpDelete("session/{sessionId}")]
        public async Task<ActionResult<object>> DeleteSession(string sessionId)
        {
            try
            {
                var success = await _sessionService.DeleteSessionAsync(sessionId);
                return Ok(new { 
                    SessionId = sessionId,
                    Success = success,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Delete session failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }

    public class SetValueRequest
    {
        public string Key { get; set; } = "test:key";
        public object Value { get; set; } = "test value";
        public TimeSpan? Expiry { get; set; } = TimeSpan.FromMinutes(30);
    }

    public class CreateSessionRequest
    {
        public string? UserId { get; set; }
        public string? Username { get; set; }
        public List<string>? Roles { get; set; }
        public Dictionary<string, object>? Claims { get; set; }
        public TimeSpan? Expiry { get; set; } = TimeSpan.FromHours(8);
    }
}
