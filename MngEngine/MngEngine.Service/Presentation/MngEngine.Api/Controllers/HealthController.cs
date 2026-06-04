using Microsoft.AspNetCore.Mvc;

namespace MngEngine.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "healthy" });
    }
}
