using Microsoft.AspNetCore.Mvc;
using MngAlarm.Application.Contracts;
using MngAlarm.Application.Observations;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Entities;
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
[Route("api/v1/scenarios")]
public sealed class AlarmScenariosController(
    IScenarioService scenarios,
    IScenarioPackageImportAuthorizer packageImport,
    IScenarioSchedulerService scheduler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool includeDrafts = true,
        CancellationToken cancellationToken = default) =>
        Ok(await scenarios.ListAsync(includeDrafts, cancellationToken));

    [HttpPost("drafts")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreateScenarioDraftRequest request,
        CancellationToken cancellationToken)
    {
        var draft = await scenarios.CreateDraftAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { scenarioId = draft.ScenarioId, version = draft.Version }, draft);
    }

    [HttpPost("{scenarioId}/drafts")]
    public async Task<IActionResult> CreateNextDraft(
        string scenarioId,
        [FromBody] CreateScenarioDraftRequest? request,
        CancellationToken cancellationToken)
    {
        var draft = await scenarios.CreateNextDraftAsync(scenarioId, request, cancellationToken);
        return draft == null
            ? NotFound()
            : CreatedAtAction(nameof(Get), new { scenarioId = draft.ScenarioId, version = draft.Version }, draft);
    }

    [HttpPost("{scenarioId}/versions/{version:int}/clone-to-draft")]
    public async Task<IActionResult> CloneTemplate(
        string scenarioId,
        int version,
        CancellationToken cancellationToken)
    {
        var draft = await scenarios.CloneTemplateAsync(scenarioId, version, cancellationToken);
        return draft == null
            ? Conflict(new { code = "product_template_required" })
            : CreatedAtAction(nameof(Get), new { scenarioId = draft.ScenarioId, version = draft.Version }, draft);
    }

    [HttpGet("{scenarioId}")]
    public async Task<IActionResult> Get(
        string scenarioId,
        [FromQuery] int? version,
        CancellationToken cancellationToken)
    {
        var item = await scenarios.GetAsync(scenarioId, version, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPut("{scenarioId}/versions/{version:int}/draft")]
    public async Task<IActionResult> UpdateDraft(
        string scenarioId,
        int version,
        [FromBody] UpdateScenarioDraftRequest request,
        CancellationToken cancellationToken)
    {
        var item = await scenarios.UpdateDraftAsync(scenarioId, version, request, cancellationToken);
        return item == null ? Conflict(new { code = "immutable_or_missing", message = "Only an existing draft can be edited." }) : Ok(item);
    }

    [HttpPost("{scenarioId}/versions/{version:int}/validate")]
    public async Task<IActionResult> Validate(string scenarioId, int version, CancellationToken cancellationToken)
    {
        var result = await scenarios.ValidateAsync(scenarioId, version, cancellationToken);
        return result == null ? Conflict(new { code = "immutable_or_missing" }) : Ok(result);
    }

    [HttpPost("{scenarioId}/versions/{version:int}/publish")]
    public async Task<IActionResult> Publish(string scenarioId, int version, CancellationToken cancellationToken)
    {
        var item = await scenarios.PublishAsync(scenarioId, version, cancellationToken);
        if (item == null)
            return Conflict(new { code = "not_validated_or_missing" });
        return item.Status == ScenarioLifecycleStatuses.Published
            ? Ok(item)
            : Conflict(new { code = "validation_failed", validation = item.Validation });
    }

    [HttpPost("{scenarioId}/versions/{version:int}/enabled")]
    public async Task<IActionResult> SetEnabled(
        string scenarioId,
        int version,
        [FromBody] SetScenarioEnabledRequest request,
        CancellationToken cancellationToken)
    {
        var item = await scenarios.SetEnabledAsync(scenarioId, version, request.Enabled, cancellationToken);
        return item == null
            ? Conflict(new { code = "published_version_required", message = "Only a published user scenario can be started or stopped." })
            : Ok(item);
    }

    [HttpPost("{scenarioId}/versions/{version:int}/archive")]
    public async Task<IActionResult> Archive(string scenarioId, int version, CancellationToken cancellationToken)
    {
        var item = await scenarios.ArchiveAsync(scenarioId, version, cancellationToken);
        return item == null ? Conflict(new { code = "not_archivable" }) : Ok(item);
    }

    [HttpPost("{scenarioId}/versions/{version:int}/rollback")]
    public async Task<IActionResult> Rollback(string scenarioId, int version, CancellationToken cancellationToken)
    {
        var item = await scenarios.RollbackAsync(scenarioId, version, cancellationToken);
        return item == null ? Conflict(new { code = "published_version_required" }) : Ok(item);
    }

    [HttpGet("{scenarioId}/audit")]
    public async Task<IActionResult> Audit(string scenarioId, CancellationToken cancellationToken) =>
        Ok(await scenarios.AuditAsync(scenarioId, cancellationToken));

    [HttpGet("{scenarioId}/executions")]
    public async Task<IActionResult> ListExecutions(
        string scenarioId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default) =>
        Ok(await scenarios.ListExecutionsAsync(scenarioId, limit, cancellationToken));

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] ScenarioPreviewRequest request, CancellationToken cancellationToken) =>
        Ok(await scenarios.PreviewAsync(null, null, request, cancellationToken));

    [HttpPost("compile")]
    public async Task<IActionResult> Compile([FromBody] ScenarioPreviewRequest request, CancellationToken cancellationToken) =>
        Ok(await scenarios.CompileAsync(null, null, request, cancellationToken));

    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate([FromBody] ScenarioPreviewRequest request, CancellationToken cancellationToken) =>
        Ok(await scenarios.PreviewAsync(null, null, request, cancellationToken));

    [HttpPost("{scenarioId}/versions/{version:int}/simulate")]
    public async Task<IActionResult> SimulateVersion(
        string scenarioId,
        int version,
        [FromBody] ScenarioPreviewRequest request,
        CancellationToken cancellationToken) =>
        Ok(await scenarios.PreviewAsync(scenarioId, version, request, cancellationToken));

    [HttpPost("{scenarioId}/versions/{version:int}/schedule/trigger")]
    public async Task<IActionResult> TriggerSchedule(
        string scenarioId,
        int version,
        [FromBody] ScenarioScheduleTriggerRequest request,
        CancellationToken cancellationToken) =>
        Ok(await scheduler.TriggerAsync(scenarioId, version, request, cancellationToken));

    [HttpPost("packages/import")]
    public async Task<IActionResult> ImportPackage(
        [FromHeader(Name = "X-Scenario-Package-Key")] string? importKey,
        [FromBody] ImportScenarioPackageRequest request,
        CancellationToken cancellationToken)
    {
        if (!packageImport.IsAuthorized(importKey))
            return Forbid();
        return Ok(await scenarios.ImportProductPackageAsync(request, cancellationToken));
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

    [HttpGet("trend-buckets")]
    public async Task<IActionResult> TrendBuckets(
        [FromQuery] int rangeHours = 24,
        CancellationToken cancellationToken = default) =>
        Ok(await alarms.GetTrendBucketsAsync(rangeHours, cancellationToken));

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
