using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngReactor.Application.Abstractions.Crypt;
using MngReactor.Application.Abstractions.Engine;
using MngReactor.Application.Features.Command.Data;
using MngReactor.Application.Features.Query;
using System.Text.Json.Nodes;

namespace MngReactor.Api.Controllers.Monitoring;

/// <summary>
/// mon_engines CRUD - mng_{domain}. Engine tanımları (veri toplama cihazları).
/// </summary>
[Route("api/v1/monitoring/engines")]
[ApiController]
[Authorize]
public class MonEnginesController : BaseContoller
{
    private readonly IMediator _mediator;
    private readonly ICryptProcessing _cryptProcessing;
    private readonly IMqttSyncPublisher _mqttSyncPublisher;

    public MonEnginesController(IMediator mediator, ICryptProcessing cryptProcessing, IMqttSyncPublisher mqttSyncPublisher)
    {
        _mediator = mediator;
        _cryptProcessing = cryptProcessing;
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
            Collection = "mon_engines",
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

        if (data?["password"] != null && data["password"]?.GetValue<string>() is { } pwd && !string.IsNullOrEmpty(pwd))
            data["password"] = await _cryptProcessing.Encrypt(pwd);

        var dataRequest = new DataCommandRequest
        {
            Access_Token = userInfo.accessToken,
            Collection = "mon_engines",
            Database = "mng_" + userInfo.domain,
            Method = DataOperationType.Insert,
            Data = data!,
            Options = new DataCommandOptions { useCreatedBy = true },
            UserName = userInfo.userName,
            PublishMQTT = false,
            Domain = userInfo.domain
        };
        var res = await _mediator.Send(dataRequest);
        if (res.IsSuccess && res.Data?["__dataId"] is JsonArray arr && arr.Count > 0 && arr[0]?.GetValue<string>() is { } engineId)
            await _mqttSyncPublisher.PublishSyncAsync((string)userInfo.domain, engineId);
        return Ok(res);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] JsonNode data)
    {
        var userInfo = await GetUserInfo();
        if (string.IsNullOrEmpty((string)userInfo.domain)) return Unauthorized();

        if (data?["password"] != null && data["password"]?.GetValue<string>() is { } pwd && !string.IsNullOrEmpty(pwd))
            data["password"] = await _cryptProcessing.Encrypt(pwd);

        var dataRequest = new DataCommandRequest
        {
            Access_Token = userInfo.accessToken,
            Collection = "mon_engines",
            Database = "mng_" + userInfo.domain,
            Method = DataOperationType.Update,
            Data = data!,
            Options = new DataCommandOptions { useUpdatedBy = true },
            UserName = userInfo.userName,
            PublishMQTT = false,
            Domain = userInfo.domain
        };
        var res = await _mediator.Send(dataRequest);
        if (res.IsSuccess && data?["__dataId"]?.GetValue<string>() is { } engineId)
            await _mqttSyncPublisher.PublishSyncAsync((string)userInfo.domain, engineId);
        return Ok(res);
    }

    [HttpDelete]
    public async Task<IActionResult> Delete([FromBody] JsonNode data)
    {
        var userInfo = await GetUserInfo();
        if (string.IsNullOrEmpty((string)userInfo.domain)) return Unauthorized();

        var dataRequest = new DataCommandRequest
        {
            Access_Token = userInfo.accessToken,
            Collection = "mon_engines",
            Database = "mng_" + userInfo.domain,
            Method = DataOperationType.Delete,
            Data = data!,
            Options = new DataCommandOptions(),
            UserName = userInfo.userName,
            PublishMQTT = false,
            Domain = userInfo.domain
        };
        var res = await _mediator.Send(dataRequest);
        if (res.IsSuccess && data?["__dataId"]?.GetValue<string>() is { } engineId)
            await _mqttSyncPublisher.PublishSyncAsync((string)userInfo.domain, engineId);
        return Ok(res);
    }
}
