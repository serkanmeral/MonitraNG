using Microsoft.AspNetCore.Mvc;
using MngKeeper.Application.Interfaces;
using MngKeeper.Api.Attributes;

namespace MngKeeper.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AdminAuthorization]
    public class MqttController : ControllerBase
    {
        private readonly IMqttService _mqttService;
        private readonly ILogger<MqttController> _logger;

        public MqttController(IMqttService mqttService, ILogger<MqttController> logger)
        {
            _mqttService = mqttService;
            _logger = logger;

            // Set up event handlers
            _mqttService.MessageReceived += OnMessageReceived;
            _mqttService.ConnectionStateChanged += OnConnectionStateChanged;
        }

        [HttpGet("health")]
        public async Task<ActionResult<object>> Health()
        {
            try
            {
                var isConnected = await _mqttService.IsConnectedAsync();
                var subscribedTopics = await _mqttService.GetSubscribedTopicsAsync();

                return Ok(new { 
                    Status = "MQTT Controller is working!",
                    Connected = isConnected,
                    SubscribedTopics = subscribedTopics,
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
                await _mqttService.ConnectAsync();
                return Ok(new { 
                    Status = "Connected to MQTT broker successfully",
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

        [HttpPost("disconnect")]
        public async Task<ActionResult<object>> Disconnect()
        {
            try
            {
                await _mqttService.DisconnectAsync();
                return Ok(new { 
                    Status = "Disconnected from MQTT broker successfully",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Disconnect failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost("publish")]
        public async Task<ActionResult<object>> Publish([FromBody] MqttPublishMessageRequest request)
        {
            try
            {
                await _mqttService.PublishAsync(request.Topic, request.Payload, request.Qos, request.Retain);
                return Ok(new { 
                    Status = "Message published successfully",
                    Topic = request.Topic,
                    Qos = request.Qos,
                    Retain = request.Retain,
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

        [HttpPost("publish-device-message")]
        public async Task<ActionResult<object>> PublishDeviceMessage([FromBody] PublishDeviceMessageRequest request)
        {
            try
            {
                // Get domain from token claims
                var claims = HttpContext.Items["TokenClaims"] as TokenClaims;
                if (claims?.DomainId == null)
                {
                    return BadRequest(new { Message = "Domain information not found in token." });
                }

                var deviceMessage = new MqttDeviceMessage
                {
                    DeviceId = request.DeviceId,
                    DomainId = claims.DomainId,
                    MessageType = request.MessageType,
                    Data = request.Data,
                    CorrelationId = request.CorrelationId
                };

                var topic = $"mngkeeper/{claims.DomainId}/device/{request.DeviceId}/message";
                await _mqttService.PublishAsync(topic, deviceMessage, request.Qos, request.Retain);

                return Ok(new { 
                    Status = "Device message published successfully",
                    Topic = topic,
                    DeviceId = request.DeviceId,
                    DomainId = claims.DomainId,
                    MessageType = request.MessageType,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Device message publish failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost("publish-device-command")]
        public async Task<ActionResult<object>> PublishDeviceCommand([FromBody] PublishDeviceCommandRequest request)
        {
            try
            {
                // Get domain from token claims
                var claims = HttpContext.Items["TokenClaims"] as TokenClaims;
                if (claims?.DomainId == null)
                {
                    return BadRequest(new { Message = "Domain information not found in token." });
                }

                var deviceCommand = new MqttDeviceCommand
                {
                    DeviceId = request.DeviceId,
                    DomainId = claims.DomainId,
                    Command = request.Command,
                    Parameters = request.Parameters,
                    CorrelationId = request.CorrelationId
                };

                var topic = $"mngkeeper/{claims.DomainId}/device/{request.DeviceId}/command";
                await _mqttService.PublishAsync(topic, deviceCommand, request.Qos, request.Retain);

                return Ok(new { 
                    Status = "Device command published successfully",
                    Topic = topic,
                    DeviceId = request.DeviceId,
                    DomainId = claims.DomainId,
                    Command = request.Command,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Device command publish failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost("subscribe")]
        public async Task<ActionResult<object>> Subscribe([FromBody] SubscribeRequest request)
        {
            try
            {
                await _mqttService.SubscribeAsync(request.Topic, request.Qos);
                return Ok(new { 
                    Status = "Subscribed successfully",
                    Topic = request.Topic,
                    Qos = request.Qos,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Subscribe failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpPost("unsubscribe")]
        public async Task<ActionResult<object>> Unsubscribe([FromBody] UnsubscribeRequest request)
        {
            try
            {
                await _mqttService.UnsubscribeAsync(request.Topic);
                return Ok(new { 
                    Status = "Unsubscribed successfully",
                    Topic = request.Topic,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Unsubscribe failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpGet("subscribed-topics")]
        public async Task<ActionResult<object>> GetSubscribedTopics()
        {
            try
            {
                var topics = await _mqttService.GetSubscribedTopicsAsync();
                return Ok(new { 
                    SubscribedTopics = topics,
                    Count = topics.Count,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Status = "Get subscribed topics failed",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        private void OnMessageReceived(object? sender, MqttMessageReceivedEventArgs e)
        {
            _logger.LogInformation("MQTT Message Received - Topic: {Topic}, QoS: {Qos}, Payload: {Payload}", 
                e.Topic, e.Qos, e.Payload);
        }

        private void OnConnectionStateChanged(object? sender, MqttConnectionEventArgs e)
        {
            _logger.LogInformation("MQTT Connection State Changed - Connected: {IsConnected}, Reason: {Reason}", 
                e.IsConnected, e.Reason);
        }
    }

    public class MqttPublishMessageRequest
    {
        public string Topic { get; set; } = "test/topic";
        public string Payload { get; set; } = "Hello MQTT!";
        public int Qos { get; set; } = 1;
        public bool Retain { get; set; } = false;
    }

    public class PublishDeviceMessageRequest
    {
        public string DeviceId { get; set; } = "test-device-001";
        public string MessageType { get; set; } = "status";
        public object Data { get; set; } = new { temperature = 25.5, humidity = 60.0 };
        public string? CorrelationId { get; set; }
        public int Qos { get; set; } = 1;
        public bool Retain { get; set; } = false;
    }

    public class PublishDeviceCommandRequest
    {
        public string DeviceId { get; set; } = "test-device-001";
        public string Command { get; set; } = "restart";
        public object Parameters { get; set; } = new { delay = 5 };
        public string? CorrelationId { get; set; }
        public int Qos { get; set; } = 1;
        public bool Retain { get; set; } = false;
    }

    public class SubscribeRequest
    {
        public string Topic { get; set; } = "test/topic";
        public int Qos { get; set; } = 1;
    }

    public class UnsubscribeRequest
    {
        public string Topic { get; set; } = "test/topic";
    }
}
