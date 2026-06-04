using MediatR;
using Microsoft.Extensions.Logging;
using MngEngine.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Application.Features.Config
{
    public class ConfigCommandHandler : IRequestHandler<ConfigCommandRequest, ConfigCommandResponse>
    {
        private readonly IConfigService _configService;
        private readonly IInitApplicationService _initApplicationService;
        private readonly ILogger<ConfigCommandHandler> _logger;

        public ConfigCommandHandler(
            IConfigService configService,
            IInitApplicationService initApplicationService,
            ILogger<ConfigCommandHandler> logger)
        {
            _configService = configService;
            _initApplicationService = initApplicationService;
            _logger = logger;
        }

        public async Task<ConfigCommandResponse> Handle(ConfigCommandRequest request, CancellationToken cancellationToken)
        {
            var configText = request.ConfigText ?? "";
            _logger.LogInformation("Config apply isteği alındı. Uzunluk={Length}", configText.Length);
            var first10 = configText.Length > 0 ? string.Join(",", configText.Take(10).Select(c => (int)c)) : "";
            var last5 = configText.Length > 5 ? string.Join(",", configText.Skip(configText.Length - 5).Select(c => (int)c)) : "";
            _logger.LogInformation("Config gelen string Ilk10ASCII=[{First}] Son5ASCII=[{Last}]", first10, last5);
            _logger.LogInformation("Config gelen base64 (tam): {Base64}", configText);
            if (configText.Length > 800)
            {
                _logger.LogInformation("Config gelen base64 ilk 400 karakter: {Ilk}", configText.Substring(0, 400));
                _logger.LogInformation("Config gelen base64 son 400 karakter: {Son}", configText.Substring(configText.Length - 400));
            }

            if (string.IsNullOrWhiteSpace(configText))
            {
                _logger.LogWarning("Config apply: ConfigText boş, işlem atlanıyor.");
                return new ConfigCommandResponse { Result = false };
            }

            var (applied, errorMessage) = await _configService.ApplyConfig(configText);
            if (!applied)
            {
                _logger.LogWarning("Config apply başarısız. Sync çalıştırılmayacak. Hata: {Error}", errorMessage ?? "bilinmiyor");
                var msg = errorMessage ?? "";
                _logger.LogWarning("Config apply HATA_ANALIZ: HataMesajiUzunlugu={Len} (donen hata metninin karakter sayisi). Tam hata metni: {TamHata}", msg.Length, msg);
                return new ConfigCommandResponse { Result = false };
            }

            _logger.LogInformation("Config kaydedildi. Reactor sync başlatılıyor...");
            try
            {
                await _initApplicationService.RunConfigSyncAndRescheduleAsync();
                _logger.LogInformation("Config sync ve job güncelleme tamamlandı.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Config sync veya job güncelleme sırasında hata. Config kaydedildi ancak sync uygulanamadı.");
            }

            return new ConfigCommandResponse { Result = true };
        }
    }
}
