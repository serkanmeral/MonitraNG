using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Diagnostics;

namespace MngKeeper.Api.Controllers
{
    /// <summary>
    /// API Documentation controller providing information about the API
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ApiDocsController : ControllerBase
    {
        private readonly ILogger<ApiDocsController> _logger;

        public ApiDocsController(ILogger<ApiDocsController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets API information and version details
        /// </summary>
        /// <returns>API information including version, features, and endpoints</returns>
        [HttpGet("info")]
        [ProducesResponseType(typeof(ApiInfo), 200)]
        public ActionResult<ApiInfo> GetApiInfo()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "1.0.0";
            var buildDate = System.IO.File.GetCreationTime(assembly.Location);

            var apiInfo = new ApiInfo
            {
                Name = "MngKeeper API",
                Version = version,
                Description = "Multi-tenant Management System API",
                BuildDate = buildDate,
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                Features = new List<string>
                {
                    "JWT Authentication",
                    "Multi-tenant Domain Management",
                    "User & Group Management",
                    "GraphQL Support",
                    "Real-time Event Publishing",
                    "Redis Caching",
                    "MQTT Device Communication",
                    "RabbitMQ Message Queuing",
                    "MongoDB Persistence",
                    "Global Exception Handling",
                    "Structured Logging"
                },
                Endpoints = new List<EndpointInfo>
                {
                    new() { Path = "/api-docs", Method = "GET", Description = "Swagger UI Documentation" },
                    new() { Path = "/api-docs/v1/swagger.json", Method = "GET", Description = "OpenAPI Specification" },
                    new() { Path = "/graphql", Method = "POST", Description = "GraphQL Endpoint" },
                    new() { Path = "/api/auth/token", Method = "POST", Description = "Authentication Token" },
                    new() { Path = "/api/domain", Method = "GET", Description = "List Domains" },
                    new() { Path = "/api/domain", Method = "POST", Description = "Create Domain" },
                    new() { Path = "/api/users", Method = "GET", Description = "List Users" },
                    new() { Path = "/api/users", Method = "POST", Description = "Create User" },
                    new() { Path = "/api/groups", Method = "GET", Description = "List Groups" },
                    new() { Path = "/api/groups", Method = "POST", Description = "Create Group" },
                    new() { Path = "/api/rabbitmq", Method = "GET", Description = "RabbitMQ Health Check" },
                    new() { Path = "/api/redis", Method = "GET", Description = "Redis Health Check" },
                    new() { Path = "/api/mqtt", Method = "GET", Description = "MQTT Health Check" }
                },
                Authentication = new AuthenticationInfo
                {
                    Type = "JWT Bearer Token",
                    Scheme = "Bearer",
                    Description = "Include 'Authorization: Bearer {token}' header for authenticated endpoints"
                },
                RateLimiting = new RateLimitInfo
                {
                    Enabled = false,
                    Description = "Rate limiting is not currently implemented"
                }
            };

            _logger.LogInformation("API Info requested - Version: {Version}, Environment: {Environment}", 
                version, apiInfo.Environment);

            return Ok(apiInfo);
        }

        /// <summary>
        /// Gets API health status
        /// </summary>
        /// <returns>Health status of the API and its dependencies</returns>
        [HttpGet("health")]
        [ProducesResponseType(typeof(HealthStatus), 200)]
        public ActionResult<HealthStatus> GetHealth()
        {
            var health = new HealthStatus
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
                Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                Services = new List<ServiceHealth>
                {
                    new() { Name = "API", Status = "Healthy", ResponseTime = 0 },
                    new() { Name = "MongoDB", Status = "Unknown", ResponseTime = 0 },
                    new() { Name = "Redis", Status = "Unknown", ResponseTime = 0 },
                    new() { Name = "RabbitMQ", Status = "Unknown", ResponseTime = 0 },
                    new() { Name = "MQTT", Status = "Unknown", ResponseTime = 0 }
                }
            };

            return Ok(health);
        }

        /// <summary>
        /// Gets API statistics
        /// </summary>
        /// <returns>API usage statistics and metrics</returns>
        [HttpGet("stats")]
        [ProducesResponseType(typeof(ApiStats), 200)]
        public ActionResult<ApiStats> GetStats()
        {
            var stats = new ApiStats
            {
                TotalRequests = 0, // TODO: Implement request counting
                ActiveConnections = 0, // TODO: Implement connection tracking
                AverageResponseTime = 0, // TODO: Implement response time tracking
                ErrorRate = 0, // TODO: Implement error rate calculation
                LastUpdated = DateTime.UtcNow
            };

            return Ok(stats);
        }
    }

    public class ApiInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime BuildDate { get; set; }
        public string Environment { get; set; } = string.Empty;
        public List<string> Features { get; set; } = new();
        public List<EndpointInfo> Endpoints { get; set; } = new();
        public AuthenticationInfo Authentication { get; set; } = new();
        public RateLimitInfo RateLimiting { get; set; } = new();
    }

    public class EndpointInfo
    {
        public string Path { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class AuthenticationInfo
    {
        public string Type { get; set; } = string.Empty;
        public string Scheme { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class RateLimitInfo
    {
        public bool Enabled { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class HealthStatus
    {
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Version { get; set; } = string.Empty;
        public TimeSpan Uptime { get; set; }
        public List<ServiceHealth> Services { get; set; } = new();
    }

    public class ServiceHealth
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int ResponseTime { get; set; }
    }

    public class ApiStats
    {
        public long TotalRequests { get; set; }
        public int ActiveConnections { get; set; }
        public double AverageResponseTime { get; set; }
        public double ErrorRate { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
