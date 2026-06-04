using Microsoft.AspNetCore.Mvc;
using MngEngine.Api.Logging;

namespace MngEngine.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class LogsController : ControllerBase
{
    private readonly InMemoryLogSink _logSink;

    public LogsController(InMemoryLogSink logSink)
    {
        _logSink = logSink;
    }

    /// <summary>
    /// Son log kayıtlarını döner. tail: son N kayıt (varsayılan 200).
    /// </summary>
    [HttpGet]
    public IActionResult GetLogs([FromQuery] int tail = 200)
    {
        var entries = _logSink.GetRecent(Math.Clamp(tail, 1, 1000));
        return Ok(entries);
    }

    /// <summary>
    /// Bellekteki log kayıtlarını temizler.
    /// </summary>
    [HttpDelete]
    public IActionResult ClearLogs()
    {
        _logSink.Clear();
        return Ok(new { success = true });
    }
}
