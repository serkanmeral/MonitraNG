using Microsoft.AspNetCore.Mvc;
using MngAlarm.Application.Contracts;
using MngAlarm.Application.Observations;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Enums;

namespace MngAlarm.Api.Controllers;

[ApiController]
[Route("api/v1/rules")]
public sealed class AlarmRulesController(IAlarmRuleService rules) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await rules.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAlarmRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await rules.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { ruleId = rule.Id }, rule);
    }

    [HttpGet("{ruleId}")]
    public async Task<IActionResult> Get(string ruleId, CancellationToken cancellationToken)
    {
        var rule = await rules.GetAsync(ruleId, cancellationToken);
        return rule == null ? NotFound() : Ok(rule);
    }

    [HttpPut("{ruleId}")]
    public async Task<IActionResult> Update(string ruleId, [FromBody] UpdateAlarmRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await rules.UpdateAsync(ruleId, request, cancellationToken);
        return rule == null ? NotFound() : Ok(rule);
    }

    [HttpDelete("{ruleId}")]
    public async Task<IActionResult> Delete(string ruleId, CancellationToken cancellationToken)
    {
        var deleted = await rules.DeleteAsync(ruleId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

[ApiController]
[Route("api/v1/notification-policies")]
public sealed class AlarmNotificationPoliciesController(IAlarmNotificationPolicyService policies) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? isActive, CancellationToken cancellationToken) =>
        Ok(await policies.ListAsync(isActive, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAlarmNotificationPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var policy = await policies.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { policyId = policy.Id }, policy);
    }

    [HttpGet("{policyId}")]
    public async Task<IActionResult> Get(string policyId, CancellationToken cancellationToken)
    {
        var policy = await policies.GetAsync(policyId, cancellationToken);
        return policy == null ? NotFound() : Ok(policy);
    }

    [HttpPut("{policyId}")]
    public async Task<IActionResult> Update(
        string policyId,
        [FromBody] UpdateAlarmNotificationPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var policy = await policies.UpdateAsync(policyId, request, cancellationToken);
        return policy == null ? NotFound() : Ok(policy);
    }

    [HttpDelete("{policyId}")]
    public async Task<IActionResult> Delete(string policyId, CancellationToken cancellationToken)
    {
        var deleted = await policies.DeleteAsync(policyId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

[ApiController]
[Route("api/v1/alarms")]
public sealed class AlarmsController(IAlarmQueryService alarms, IAlarmLifecycleService lifecycle) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] AlarmStatus? status,
        [FromQuery] int? minSeverity,
        [FromQuery] bool openOnly = true,
        [FromQuery] string? ruleId = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await alarms.ListAsync(status, minSeverity, openOnly, skip, limit, ruleId, search, from, to, cancellationToken));

    [HttpGet("dashboard-snapshot")]
    public async Task<IActionResult> DashboardSnapshot(
        [FromQuery] int rangeHours = 24,
        [FromQuery] int minSeverity = 6,
        [FromQuery] int openLimit = 15,
        CancellationToken cancellationToken = default) =>
        Ok(await alarms.GetDashboardSnapshotAsync(rangeHours, minSeverity, openLimit, cancellationToken));

    [HttpGet("{alarmId}")]
    public async Task<IActionResult> Get(string alarmId, CancellationToken cancellationToken)
    {
        var item = await alarms.GetAsync(alarmId, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost("{alarmId}/acknowledge")]
    public async Task<IActionResult> Acknowledge(string alarmId, CancellationToken cancellationToken)
    {
        var item = await lifecycle.AcknowledgeAsync(alarmId, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost("{alarmId}/suppress")]
    public async Task<IActionResult> Suppress(string alarmId, CancellationToken cancellationToken)
    {
        var item = await lifecycle.SuppressAsync(alarmId, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost("{alarmId}/resolve")]
    public async Task<IActionResult> Resolve(string alarmId, CancellationToken cancellationToken)
    {
        var item = await lifecycle.ResolveAsync(alarmId, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }
}

[ApiController]
[Route("api/v1/validation")]
public sealed class AlarmValidationController(IAlarmValidationService validation, IAlarmDomainAccessor domain) : ControllerBase
{
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken cancellationToken)
    {
        var ctx = domain.GetRequiredDomain();
        var result = await validation.RunScanAsync(ctx.DomainName, ctx.DomainId, cancellationToken);
        return Ok(result);
    }
}

[ApiController]
[Route("api/v1/dev/observations")]
public sealed class DevObservationsController : ControllerBase
{
    private readonly IObservationProcessor _processor;
    private readonly IConfiguration _configuration;

    public DevObservationsController(IObservationProcessor processor, IConfiguration configuration)
    {
        _processor = processor;
        _configuration = configuration;
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] IngestObservationRequest request, CancellationToken cancellationToken)
    {
        if (!IsDevEnabled())
            return NotFound();

        var domainName = string.IsNullOrWhiteSpace(request.DomainName) ? "odak" : request.DomainName.Trim();
        var domainId = string.IsNullOrWhiteSpace(request.DomainId) ? domainName : request.DomainId.Trim();

        var envelope = new ObservationEnvelope
        {
            DomainId = domainId,
            DomainName = domainName,
            Kind = request.Kind,
            Key = request.Key,
            Value = request.Value,
            Dimensions = ObservationValueNormalizer.NormalizeDimensions(request.Dimensions),
            Timestamp = DateTime.UtcNow
        };

        var result = await _processor.ProcessAsync(envelope, cancellationToken);
        return Accepted(result);
    }

    private bool IsDevEnabled() =>
        _configuration.GetValue<bool>("EnableDevAlarmEndpoints")
        || string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
}
