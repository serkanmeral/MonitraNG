using Microsoft.AspNetCore.Mvc;
using MngKeeper.Application.Interfaces;
using MngKeeper.Api.Attributes;

namespace MngKeeper.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AdminAuthorization]
    public class RabbitMqController : ControllerBase
    {
        private readonly IRabbitMqService _rabbitMqService;
        private readonly IEventPublisher _eventPublisher;

        public RabbitMqController(IRabbitMqService rabbitMqService, IEventPublisher eventPublisher)
        {
            _rabbitMqService = rabbitMqService;
            _eventPublisher = eventPublisher;
        }

        [HttpGet("health")]
        public async Task<ActionResult<object>> Health()
        {
            try
            {
                var isConnected = await _rabbitMqService.IsConnectedAsync();
                return Ok(new { 
                    Status = "RabbitMQ Controller is working!",
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
                await _rabbitMqService.ConnectAsync();
                return Ok(new { 
                    Status = "Connected to RabbitMQ successfully",
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

        [HttpPost("publish")]
        public async Task<ActionResult<object>> Publish([FromBody] PublishMessageRequest request)
        {
            try
            {
                await _rabbitMqService.PublishAsync(request.Exchange, request.RoutingKey, request.Message);
                return Ok(new { 
                    Status = "Message published successfully",
                    Exchange = request.Exchange,
                    RoutingKey = request.RoutingKey,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Publish failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost("publish-event")]
        public async Task<ActionResult<object>> PublishEvent([FromBody] PublishEventRequest request)
        {
            try
            {
                // Get domain from token claims
                var claims = HttpContext.Items["TokenClaims"] as TokenClaims;
                if (claims?.DomainId == null)
                {
                    return BadRequest(new { Message = "Domain information not found in token." });
                }

                var testEvent = new UserCreatedEvent
                {
                    UserId = request.UserId ?? Guid.NewGuid().ToString(),
                    Username = request.Username ?? "testuser",
                    Email = request.Email ?? "test@example.com",
                    Groups = request.Groups ?? new List<string> { "guests" }
                };

                await _eventPublisher.PublishAsync(testEvent, claims.DomainId);
                return Ok(new { 
                    Status = "Event published successfully",
                    EventType = "UserCreatedEvent",
                    DomainId = claims.DomainId,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Event publish failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }

    public class PublishMessageRequest
    {
        public string Exchange { get; set; } = "mngkeeper.events";
        public string RoutingKey { get; set; } = "test.message";
        public string Message { get; set; } = "Hello RabbitMQ!";
    }

    public class PublishEventRequest
    {
        public string? UserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public List<string>? Groups { get; set; }
    }
}
