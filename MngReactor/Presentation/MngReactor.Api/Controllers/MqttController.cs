using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngReactor.Application.Features.Mqtt;
using MngReactor.Domain.Interfaces;

namespace MngReactor.Api.Controllers;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/mqtt")]
[ApiController]
[Authorize]
public sealed class MqttController : ControllerBase
{
    private readonly IMqttService _mqttService;
    private readonly ILogger<MqttController> _logger;

    public MqttController(IMqttService mqttService, ILogger<MqttController> logger)
    {
        _mqttService = mqttService;
        _logger = logger;
    }

    /// <summary>Workflow engine.command → MQTT broker publish (P4).</summary>
    [HttpPost("publish")]
    [ProducesResponseType(typeof(MqttPublishResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<MqttPublishResponse>> Publish(
        [FromBody] MqttPublishRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Topic))
            return BadRequest(new { error = "topic_required", message = "topic is required" });

        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "message_required", message = "message is required" });

        try
        {
            await _mqttService.PublishAsync(request.Topic.Trim(), request.Message);
            _logger.LogInformation("MQTT published topic={Topic} bytes={Bytes}",
                request.Topic, request.Message.Length);

            return Ok(new MqttPublishResponse
            {
                Published = true,
                Topic = request.Topic.Trim()
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MQTT publish failed topic={Topic}", request.Topic);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "mqtt_publish_failed",
                message = ex.Message
            });
        }
    }
}
