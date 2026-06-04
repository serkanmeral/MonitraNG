using MediatR;
using MngEngine.Application.Collector.Common;
using MngEngine.Application.Collector.HttpHost;
using MngEngine.Application.Collector.LinuxHost;
using MngEngine.Application.Collector.SnmpHost;
using MngEngine.Application.Collector.WindowsHost;
using MngEngine.Application.Features.Ingest;
using MngEngine.Application.Interfaces;
using Quartz;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MngEngine.Persistence.Jobs
{
    public class CollectorJob : IJob
    {
        private readonly ILogger _logger;
        private readonly IAssetService _assetService;
        private readonly IMediator _mediator;
        private readonly IEngineConfigProvider _configProvider;
        private readonly IMetricBatchQueue _metricBatchQueue;
        private readonly IEngineErrorBuffer _errorBuffer;

        public CollectorJob(
            ILogger logger,
            IAssetService assetService,
            IMediator mediator,
            IEngineConfigProvider configProvider,
            IMetricBatchQueue metricBatchQueue,
            IEngineErrorBuffer errorBuffer)
        {
            _logger = logger;
            _assetService = assetService;
            _mediator = mediator;
            _configProvider = configProvider;
            _metricBatchQueue = metricBatchQueue;
            _errorBuffer = errorBuffer;
            _logger.Information("CollectorJob created!");
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                var engineId = GetEngineId();
                if (string.IsNullOrEmpty(engineId))
                {
                    _logger.Warning("CollectorJob: engineId bulunamadı, config yüklenmemiş olabilir");
                    return;
                }

                // PeriodExpression config sync ile job'a eklenir; eski/manuel job'larda olmayabilir
                var periodExpr = context.MergedJobDataMap.ContainsKey("PeriodExpression")
                    ? context.MergedJobDataMap.GetString("PeriodExpression")
                    : null;
                var reqList = await _assetService.GetCollectorRequests(periodExpr);
                _logger.Information("CollectorJob çalışıyor. Period={Period}, Asset sayısı: {Count}", periodExpr ?? "(tümü)", reqList.Count);

                foreach (var req in reqList)
                {
                    try
                    {
                        var res = await _mediator.Send(req, context.CancellationToken);
                        var batch = ToIngestBatch(req, res, engineId);
                        if (batch != null)
                        {
                            _metricBatchQueue.Enqueue(batch);
                            _logger.Debug("CollectorJob: Batch queue'ya eklendi. Asset={AssetId}", batch.AssetId);
                        }
                    }
                    catch (Exception ex)
                    {
                        var assetId = req.Asset?.Asset_Id ?? "?";
                        var agentId = req.AgentId ?? "unknown";
                        var errorCode = DeriveErrorCode(req, ex);
                        _logger.Error(ex, "CollectorJob: Toplama hatası. Asset={AssetId} Hata={Error}", assetId, ex.Message);
                        _errorBuffer.Add(assetId, agentId, errorCode, ex.Message);
                        // Uygulama crash olmadan devam; hata buffer'a kaydedildi, status job ile Reactor'a gönderilecek
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "CollectorJob: Beklenmeyen hata (job yine de başarıyla tamamlandı sayılır, crash önlendi)");
                // Üst seviye güvenlik: hiçbir exception Quartz/host'a sızmamalı
            }
        }

        private static IngestBatch? ToIngestBatch(BaseCollectorRequest req, dynamic response, string engineId)
        {
            if (req?.Asset == null) return null;

            var assetId = req.Asset.Asset_Id;
            var agentId = req.AgentId ?? "default-agent";
            var metrics = (response as SnmpCollectorResponse)?.Metrics
                ?? (response as HttpCollectorResponse)?.Metrics;
            if (metrics == null || metrics.Count == 0)
                metrics = [new IngestMetric { CollectibleCode = "heartbeat", Value = 1, Unit = null }];

            return new IngestBatch
            {
                AssetId = assetId,
                ItemId = null,
                AgentId = agentId,
                EngineId = engineId,
                CollectedAt = DateTime.UtcNow,
                Metrics = metrics
            };
        }

        private static string DeriveErrorCode(BaseCollectorRequest req, Exception ex)
        {
            var msg = (ex.Message ?? "").ToLowerInvariant();
            if (msg.Contains("timeout") || msg.Contains("timed out")) return "connection_timeout";
            if (msg.Contains("auth") || msg.Contains("password") || msg.Contains("unauthorized") || msg.Contains("permission denied")) return "auth_failed";
            if (req is SnmpCollectorRequest) return "snmp_error";
            if (req is LinuxHostCollectorRequest) return "ssh_error";
            if (req is WindowsHostCollectorRequest) return "wmi_error";
            if (req is HttpCollectorRequest) return "http_error";
            return "unknown";
        }

        private string? GetEngineId() => _configProvider.GetConfig()?.EngineId;
    }
}