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
/// mon_assets CRUD - mng_{domain}. Asset (izlenen varlık) kayıtları.
/// connection_info içindeki password/privateKey şifrelenir.
/// </summary>
[Route("api/v1/monitoring/assets")]
[ApiController]
[Authorize]
public class MonAssetsController : BaseContoller
{
    private readonly IMediator _mediator;
    private readonly ICryptProcessing _cryptProcessing;
    private readonly IMqttSyncPublisher _mqttSyncPublisher;

    public MonAssetsController(IMediator mediator, ICryptProcessing cryptProcessing, IMqttSyncPublisher mqttSyncPublisher)
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
            Collection = "mon_assets",
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

        await EncryptConnectionInfoAsync(data);

        var dataRequest = new DataCommandRequest
        {
            Access_Token = userInfo.accessToken,
            Collection = "mon_assets",
            Database = "mng_" + userInfo.domain,
            Method = DataOperationType.Insert,
            Data = data!,
            Options = new DataCommandOptions { useCreatedBy = true },
            UserName = userInfo.userName,
            PublishMQTT = false,
            Domain = userInfo.domain
        };
        var res = await _mediator.Send(dataRequest);
        if (res.IsSuccess && res.Data?["__dataId"] is JsonArray arr && arr.Count > 0 && arr[0]?.GetValue<string>() is { } assetId)
            await _mqttSyncPublisher.PublishSyncForAssetAsync((string)userInfo.domain, assetId);
        return Ok(res);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] JsonNode data)
    {
        var userInfo = await GetUserInfo();
        if (string.IsNullOrEmpty((string)userInfo.domain)) return Unauthorized();

        await EncryptConnectionInfoAsync(data);

        var dataRequest = new DataCommandRequest
        {
            Access_Token = userInfo.accessToken,
            Collection = "mon_assets",
            Database = "mng_" + userInfo.domain,
            Method = DataOperationType.Update,
            Data = data!,
            Options = new DataCommandOptions { useUpdatedBy = true },
            UserName = userInfo.userName,
            PublishMQTT = false,
            Domain = userInfo.domain
        };
        var res = await _mediator.Send(dataRequest);
        if (res.IsSuccess && data?["__dataId"]?.GetValue<string>() is { } assetId)
            await _mqttSyncPublisher.PublishSyncForAssetAsync((string)userInfo.domain, assetId);
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
            Collection = "mon_assets",
            Database = "mng_" + userInfo.domain,
            Method = DataOperationType.Delete,
            Data = data!,
            Options = new DataCommandOptions(),
            UserName = userInfo.userName,
            PublishMQTT = false,
            Domain = userInfo.domain
        };
        var res = await _mediator.Send(dataRequest);
        if (res.IsSuccess && data?["__dataId"]?.GetValue<string>() is { } assetId)
            await _mqttSyncPublisher.PublishSyncForAssetAsync((string)userInfo.domain, assetId);
        return Ok(res);
    }

    private async Task EncryptConnectionInfoAsync(JsonNode? data)
    {
        if (data?["connection_info"] is not JsonObject conn)
            return;

        var sensitiveKeys = new[] { "password", "privateKey", "community" };
        await EncryptSensitiveFieldsAsync(conn, sensitiveKeys);
        if (conn["auth"] is JsonObject auth)
            await EncryptSensitiveFieldsAsync(auth, sensitiveKeys);
    }

    private async Task EncryptSensitiveFieldsAsync(JsonObject obj, string[] keys)
    {
        foreach (var key in keys)
        {
            if (obj[key]?.GetValue<string>() is { } val && !string.IsNullOrEmpty(val))
                obj[key] = await _cryptProcessing.Encrypt(val);
        }
    }
}
