using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MngDocument.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "healthy",
        service = "MngDocument.Api",
        timestamp = DateTime.UtcNow
    });
}
