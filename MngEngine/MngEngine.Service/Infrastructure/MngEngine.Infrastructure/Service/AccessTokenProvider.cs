using System.Text.Json.Nodes;
using MngEngine.Application.Interfaces;
using MngEngine.Infrastructure.Context;
using RestSharp;
using Serilog;

namespace MngEngine.Infrastructure.Service;

public class AccessTokenProvider : IAccessTokenProvider
{
    private readonly ILogger _logger;
    private readonly IEngineConfigProvider _configProvider;
    private readonly IRestContext _context;

    public AccessTokenProvider(
        ILogger logger,
        IEngineConfigProvider configProvider,
        IRestContext context)
    {
        _logger = logger;
        _configProvider = configProvider;
        _context = context;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var config = _configProvider.GetConfig();
        if (config == null || string.IsNullOrEmpty(config.Username) || string.IsNullOrEmpty(config.Password)
            || string.IsNullOrEmpty(config.TokenUrl))
            return null;

        var tokenUrl = config.TokenUrl.TrimEnd('/');
        var request = new RestRequest("", Method.Post)
            .AddJsonBody(new
            {
                username = config.Username,
                password = config.Password,
                domain = config.Domain
            })
            .AddHeader("Content-Type", "application/json");

        try
        {
            var client = _context.RestClient(tokenUrl);
            var response = await client.ExecuteAsync<JsonNode>(request, ct);
            if (response.IsSuccessful && response.Data != null)
            {
                var obj = response.Data.AsObject();
                var token = obj["accessToken"]?.GetValue<string>();
                return token;
            }
            else
            {
                throw new Exception(response.ErrorMessage ?? $"HTTP {(int)(response.StatusCode)}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Token alınamadı");
        }

        return null;
    }
}
