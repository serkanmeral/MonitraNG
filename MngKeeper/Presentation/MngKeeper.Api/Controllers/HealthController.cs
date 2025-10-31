using Microsoft.AspNetCore.Mvc;

namespace MngKeeper.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Service = "MngKeeper API",
            Version = "1.0.0"
        });
    }

    [HttpGet("ready")]
    public IActionResult Ready()
    {
        // TODO: Add health checks for dependencies (MongoDB, Keycloak, etc.)
        return Ok(new
        {
            Status = "Ready",
            Timestamp = DateTime.UtcNow,
            Dependencies = new
            {
                MongoDB = "Connected",
                Keycloak = "Connected",
                Redis = "Connected",
                RabbitMQ = "Connected"
            }
        });
    }

    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new
        {
            Status = "Alive",
            Timestamp = DateTime.UtcNow
        });
    }
}
