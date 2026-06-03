using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngReactor.Application.Abstractions.Engine;
using MngReactor.Application.Features.Command.Data;
using MngReactor.Application.Features.Query;
using System.Text.Json.Nodes;

namespace MngReactor.Api.Controllers.Monitoring;

/// <summary>
/// mon_agents CRUD - mng_{domain}. Agent tanımları (veri toplama yapılandırması).
/// </summary>
[Route("api/v1/monitoring/agents")]
[ApiController]
[Authorize]
public class MonAgentsController : BaseContoller
{
    private readonly IMediator _mediator;
    private readonly IMqttSyncPublisher _mqttSyncPublisher;

    public MonAgentsController(IMediator mediator, IMqttSyncPublisher mqttSyncPublisher)
    {
        _mediator = mediator;
        _mqttSyncPublisher = mqttSyncPublisher;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userInfo = await GetUserInfo();
        if (string.IsNullOrEmpty((string)userInfo.domain)) return Unauthorized();

        var request = new GetDataQueryRequest
        {
            Access_Token = userInfo.accessToken,
            Collection = "mon_agents",
            Database = "mng_" + userInfo.domain,
            Query = new JsonObject()
        };
        var res = await _mediator.Send(request);
        return Ok(res);
    }

    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] JsonNode data)
    {
        var userInfo = await GetUserInfo();
        if (string.IsNullOrEmpty((string)userInfo.domain)) return Unauthorized();

        var engineId = data?["engineId"]?.GetValue<string>();
        var dataRequest = new DataCommandRequest
        {
            Access_Token = userInfo.accessToken,
            Collection = "mon_agents",
            Database = "mng_" + userInfo.domain,
            Method = DataOperationType.Insert,
            Data = data!,
            Options = new DataCommandOptions { useCreatedBy = true },
            UserName = userInfo.userName,
            PublishMQTT = false,
            Domain = userInfo.domain
        };
        var res = await _mediator.Send(dataRequest);
        if (res.IsSuccess && !string.IsNullOrEmpty(engineId))
            await _mqttSyncPublisher.PublishSyncAsync((string)userInfo.domain, engineId);
        return Ok(res);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] JsonNode data)
    {
        var userInfo = await GetUserInfo();
        if (string.IsNullOrEmpty((string)userInfo.domain)) return Unauthorized();

        var engineId = data?["engineId"]?.GetValue<string>();
        var dataRequest = new DataCommandRequest
        {
            Access_Token = userInfo.accessToken,
            Collection = "mon_agents",
            Database = "mng_" + userInfo.domain,
            Method = DataOperationType.Update,
            Data = data!,
            Options = new DataCommandOptions { useUpdatedBy = true },
            UserName = userInfo.userName,
            PublishMQTT = false,
            Domain = userInfo.domain
        };
        var res = await _mediator.Send(dataRequest);
        if (res.IsSuccess && !string.IsNullOrEmpty(engineId))
            await _mqttSyncPublisher.PublishSyncAsync((string)userInfo.domain, engineId);
        return Ok(res);
    }

    [HttpDelete]
    public async Task<IActionResult> Delete([FromBody] JsonNode data)
    {
        var userInfo = await GetUserInfo();
        if (string.IsNullOrEmpty((string)userInfo.domain)) return Unauthorized();

        var engineId = data?["engineId"]?.GetValue<string>();
        var dataRequest = new DataCommandRequest
        {
            Access_Token = userInfo.accessToken,
            Collection = "mon_agents",
            Database = "mng_" + userInfo.domain,
            Method = DataOperationType.Delete,
            Data = data!,
            Options = new DataCommandOptions(),
            UserName = userInfo.userName,
            PublishMQTT = false,
            Domain = userInfo.domain
        };
        var res = await _mediator.Send(dataRequest);
        if (res.IsSuccess && !string.IsNullOrEmpty(engineId))
            await _mqttSyncPublisher.PublishSyncAsync((string)userInfo.domain, engineId);
        return Ok(res);
    }
}
