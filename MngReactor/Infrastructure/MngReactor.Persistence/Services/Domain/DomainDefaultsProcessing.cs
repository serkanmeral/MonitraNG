using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Abstractions.Domain;
using MngReactor.Application.Configuration;

namespace MngReactor.Persistence.Services.Domain;

public class DomainDefaultsProcessing : IDomainDefaultsService
{
    private readonly ILogger<DomainDefaultsProcessing> _logger;
    private readonly IDataGatewayClient _dg;
    private readonly IOptions<MngReactorSettings> _options;

    public DomainDefaultsProcessing(ILogger<DomainDefaultsProcessing> logger, IDataGatewayClient dg, IOptions<MngReactorSettings> options)
    {
        _logger = logger;
        _dg = dg;
        _options = options;
    }

    public async Task<bool> CreateDefaultsAsync(string domainName, string? accessToken = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domainName))
        {
            _logger.LogWarning("DomainDefaults: domainName boş");
            return false;
        }

        var token = ResolveToken(domainName, accessToken);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("DomainDefaults: token bulunamadı - DomainInitController ile manuel init gerekebilir");
            return false;
        }

        try
        {
            var scheduleId = Guid.NewGuid().ToString();
            var periodId = Guid.NewGuid().ToString();

            // Var olan kayıtları kontrol et - DG filter: name:eq:Sürekli, name:eq:1 dakika
            var schedulesArr = await _dg.GetListAsync("mon_schedules", "name:eq:Sürekli", token, 10, cancellationToken);
            var periodsArr = await _dg.GetListAsync("mon_collection_periods", "name:eq:1 dakika", token, 10, cancellationToken);

            if (schedulesArr.Count > 0 && periodsArr.Count > 0)
            {
                _logger.LogInformation("DomainDefaults: {Domain} için varsayılanlar zaten mevcut", domainName);
                return true;
            }

            if (schedulesArr.Count == 0)
            {
                var schedule = new JsonObject
                {
                    ["__dataId"] = scheduleId,
                    ["name"] = "Sürekli",
                    ["description"] = "7/24 izleme",
                    ["type"] = "always"
                };
                var res = await _dg.CreateAsync("mon_schedules", schedule, token, cancellationToken);
                var success = res["success"]?.GetValue<bool>() ?? res["Success"]?.GetValue<bool>() ?? false;
                if (success)
                    _logger.LogInformation("DomainDefaults: {Domain} için mon_schedules 'Sürekli' oluşturuldu", domainName);
                else
                    _logger.LogWarning("DomainDefaults: mon_schedules oluşturma başarısız");
            }

            if (periodsArr.Count == 0)
            {
                var period = new JsonObject
                {
                    ["__dataId"] = periodId,
                    ["name"] = "1 dakika",
                    ["description"] = "Her dakika toplama",
                    ["expression"] = "*/1 * * * *"
                };
                var res = await _dg.CreateAsync("mon_collection_periods", period, token, cancellationToken);
                var success = res["success"]?.GetValue<bool>() ?? res["Success"]?.GetValue<bool>() ?? false;
                if (success)
                    _logger.LogInformation("DomainDefaults: {Domain} için mon_collection_periods '1 dakika' oluşturuldu", domainName);
                else
                    _logger.LogWarning("DomainDefaults: mon_collection_periods oluşturma başarısız");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DomainDefaults: {Domain} için varsayılan kayıt oluşturma hatası", domainName);
            return false;
        }
    }

    private string? ResolveToken(string domainName, string? accessToken)
    {
        if (!string.IsNullOrEmpty(accessToken)) return accessToken;
        var domain = domainName.Trim().ToLowerInvariant();
        return _options.Value?.DataGateway?.DomainTokens?.GetValueOrDefault(domain);
    }
}
