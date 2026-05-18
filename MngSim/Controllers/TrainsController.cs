using Microsoft.AspNetCore.Mvc;
using MngSim.Models.TrainSim;
using MngSim.Services.TrainSim;

namespace MngSim.Controllers;

[ApiController]
[Route("api/trains")]
public class TrainsController : ControllerBase
{
    private readonly ITrainPositionService _trainPositionService;
    private readonly ITrainEventService _trainEventService;
    private readonly IConfiguration _config;

    public TrainsController(ITrainPositionService trainPositionService, ITrainEventService trainEventService, IConfiguration config)
    {
        _trainPositionService = trainPositionService;
        _trainEventService = trainEventService;
        _config = config;
    }

    /// <summary>Harita sayfası için config (GeoServer WMTS base URL).</summary>
    [HttpGet("map-config")]
    public IActionResult GetMapConfig()
    {
        var baseUrl = _config["TrainSim:GeoServerBaseUrl"]?.Trim();
        return Ok(new { geoServerBaseUrl = string.IsNullOrEmpty(baseUrl) ? null : baseUrl.TrimEnd('/') });
    }

    [HttpGet("events")]
    public IActionResult GetEvents([FromQuery] int maxCount = 100)
    {
        var list = _trainEventService.GetRecentEvents(maxCount);
        return Ok(new { events = list.OrderByDescending(e => e.Timestamp).ToList() });
    }

    [HttpPost("{trainId}/events")]
    public async Task<IActionResult> PublishEvent(string trainId, [FromBody] TrainEventPublishRequest? body, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(trainId)) return BadRequest();
        if (body == null || string.IsNullOrWhiteSpace(body.EventType)) return BadRequest("eventType gerekli");
        var dto = new TrainEventDto
        {
            EventType = body.EventType.Trim(),
            Timestamp = DateTime.UtcNow,
            Zone = body.Zone,
            Severity = body.Severity,
            SpeedKmh = body.SpeedKmh,
            DoorId = body.DoorId
        };
        await _trainEventService.PublishAsync(trainId, dto, ct);
        return Accepted();
    }

    [HttpGet("positions")]
    public IActionResult GetPositions([FromQuery] bool includeSensors = true)
    {
        var response = _trainPositionService.GetAllPositions(includeSensors);
        return Ok(response);
    }

    [HttpGet("{trainId}/position")]
    public IActionResult GetPosition(string trainId, [FromQuery] bool includeSensors = true)
    {
        var pos = _trainPositionService.GetPosition(trainId, includeSensors);
        if (pos == null) return NotFound();
        return Ok(pos);
    }

    [HttpPatch("{trainId}/sensors")]
    public IActionResult PatchSensors(string trainId, [FromBody] TrainSensorsOverride? body)
    {
        if (string.IsNullOrEmpty(trainId)) return BadRequest();
        _trainPositionService.SetSensorOverrides(trainId, body);
        return NoContent();
    }
}
