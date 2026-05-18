import { fetchFromDataGateway } from './apiService';
import type { DataSourceConfigData, Widget } from '@/stores/apps/widget';

/**
 * Widget Data Service
 * Widget'ların dataSource yapılandırmasına göre veri çekme işlemlerini yönetir.
 */

export interface WidgetDataResponse {
  data: any;
  total?: number; // For pagination (default GET only)
  [key: string]: any;
}

/**
 * Widget'ın dataSource yapılandırmasına göre veri çeker
 * @param widget Widget entity
 * @returns Widget verisi
 */
export async function fetchWidgetData(widget: Widget): Promise<WidgetDataResponse> {
  const dataSource = widget.dataSource;

  // Validate dataSource
  if (!dataSource || dataSource.type !== 'data') {
    throw new Error('Widget dataSource yapılandırması geçersiz veya eksik');
  }

  if (!dataSource.dataset) {
    throw new Error('Widget dataSource dataset belirtilmemiş');
  }

  const dataset = dataSource.dataset;
  const getMethod = dataSource.getMethod || 'default';

  try {
    switch (getMethod) {
      case 'default':
        return await fetchWidgetDataDefault(
          dataset,
          dataSource.default || {},
          (widget.config || {}) as Record<string, any>
        );

      case 'query':
        return await fetchWidgetDataQuery(dataset, dataSource.query || { match: {} });

      case 'aggregate':
        return await fetchWidgetDataAggregate(dataset, dataSource.aggregate || { pipeline: [] });

      case 'predefined':
        if (!dataSource.predefined?.queryName) {
          throw new Error('Predefined query için queryName belirtilmemiş');
        }
        return await fetchWidgetDataPredefined(
          dataset,
          dataSource.predefined.queryName,
          dataSource.predefined.parameters || {}
        );

      default:
        throw new Error(`Geçersiz getMethod: ${getMethod}`);
    }
  } catch (error: any) {
    throw new Error(`Widget verisi çekilirken hata: ${error.message || error}`);
  }
}

/**
 * Default GET işlemi - Liste çekme
 */
async function fetchWidgetDataDefault(
  dataset: string,
  config: DataSourceConfigData['default'],
  widgetConfig?: Record<string, any>
): Promise<WidgetDataResponse> {
  const q = new URLSearchParams();

  let filter = config?.filter;

  // Monitoring widget: runtime'da timestamp filtresi ekle
  if (dataset === 'mon_metrics' && widgetConfig?.monitoring) {
    const minutes = widgetConfig.timeRangeMinutes;
    if (minutes != null && minutes > 0) {
      const since = new Date(Date.now() - minutes * 60 * 1000);
      const tsFilter = `timestamp:gte:${since.toISOString()}`;
      filter = filter ? `${filter},${tsFilter}` : tsFilter;
    }
  }

  // Limit: widgetConfig override (monitoring) veya dataSource default
  const effectiveLimit = widgetConfig?.limit ?? config?.limit;
  if (config?.skip !== undefined) q.set('skip', String(config.skip));
  if (effectiveLimit !== undefined) q.set('limit', String(effectiveLimit));
  if (config?.sort) q.set('sort', config.sort);
  if (filter) q.set('filter', filter);
  if (config?.fields) q.set('fields', config.fields);
  if (config?.search) q.set('search', config.search);
  if (config?.format) q.set('format', config.format);
  if (config?.expand !== undefined) q.set('expand', String(config.expand));
  if (config?.deep !== undefined) q.set('deep', String(config.deep));
  if (config?.showHistory !== undefined) q.set('showHistory', String(config.showHistory));

  const url = `/api/v1/data/${dataset}?${q.toString()}`;
  const data = await fetchFromDataGateway(url, 'GET');

  // X-Total-Count header'ı fetchFromDataGateway tarafından response._totalCount'a ekleniyor
  let items = Array.isArray(data) ? data : [];
  const total = (data as any)?._totalCount ?? items.length;

  // Monitoring multi-series: mon_metrics verisini pivotlayarak her asset için ayrı seri oluştur
  if (
    dataset === 'mon_metrics' &&
    widgetConfig?.monitoring &&
    widgetConfig?.multiSeries &&
    widgetConfig?.series?.length > 1 &&
    items.length > 0
  ) {
    items = pivotMonMetricsForMultiSeries(items, widgetConfig.series);
  }

  return {
    data: items,
    total,
  };
}

/**
 * mon_metrics verisini multi-series chart için pivotlar.
 * Girdi: [{ timestamp, value, meta: { assetId } }, ...]
 * Çıktı: [{ timestamp, [assetId1]: v1, [assetId2]: v2, ... }, ...] - timestamp'e göre sıralı
 */
function pivotMonMetricsForMultiSeries(
  items: any[],
  series: Array<{ name: string; field: string }>
): any[] {
  const assetIds = series.map((s) => s.field);
  const byTimestamp = new Map<string, Record<string, number>>();

  for (const item of items) {
    const ts = item.timestamp ?? item.Timestamp;
    const val = item.value ?? item.Value ?? 0;
    const assetId = item.meta?.assetId ?? item.meta?.asset_id ?? item.Meta?.assetId ?? item.Meta?.asset_id;

    if (ts == null || !assetId || !assetIds.includes(assetId)) continue;

    const tsKey = typeof ts === 'string' ? ts : new Date(ts).toISOString();
    if (!byTimestamp.has(tsKey)) {
      byTimestamp.set(tsKey, { timestamp: ts, ...Object.fromEntries(assetIds.map((id) => [id, null as any])) });
    }
    const row = byTimestamp.get(tsKey)!;
    row[assetId] = typeof val === 'number' ? val : parseFloat(val) || 0;
  }

  const sorted = Array.from(byTimestamp.values()).sort((a, b) => {
    const tA = new Date(a.timestamp).getTime();
    const tB = new Date(b.timestamp).getTime();
    return tA - tB;
  });

  return sorted;
}

/**
 * Query POST işlemi - MongoDB match ile sorgulama
 */
async function fetchWidgetDataQuery(
  dataset: string,
  config: DataSourceConfigData['query']
): Promise<WidgetDataResponse> {
  if (!config || !config.match) {
    throw new Error('Query için match objesi gereklidir');
  }

  const q = new URLSearchParams();
  if (config.skip !== undefined) q.set('skip', String(config.skip));
  if (config.limit !== undefined) q.set('limit', String(config.limit));
  if (config.sort) q.set('sort', config.sort);
  if (config.fields) q.set('fields', config.fields);
  if (config.expand !== undefined) q.set('expand', String(config.expand));
  if (config.deep !== undefined) q.set('deep', String(config.deep));
  if (config.showHistory !== undefined) q.set('showHistory', String(config.showHistory));

  const url = `/api/v1/data/${dataset}/query${q.toString() ? `?${q.toString()}` : ''}`;
  const body = {
    match: config.match,
  };

  const data = await fetchFromDataGateway(url, 'POST', body);

  return {
    data: Array.isArray(data) ? data : [data],
  };
}

/**
 * Aggregate POST işlemi - MongoDB aggregation pipeline
 */
async function fetchWidgetDataAggregate(
  dataset: string,
  config: DataSourceConfigData['aggregate']
): Promise<WidgetDataResponse> {
  if (!config || !config.pipeline || !Array.isArray(config.pipeline)) {
    throw new Error('Aggregate için pipeline array gereklidir');
  }

  const url = `/api/v1/data/${dataset}/aggregate`;
  const body = {
    pipeline: config.pipeline,
  };

  const data = await fetchFromDataGateway(url, 'POST', body);

  return {
    data: Array.isArray(data) ? data : [data],
  };
}

/**
 * Predefined Query POST işlemi - Dataset'teki öntanımlı sorgu
 */
async function fetchWidgetDataPredefined(
  dataset: string,
  queryName: string,
  parameters: Record<string, any>
): Promise<WidgetDataResponse> {
  if (!queryName) {
    throw new Error('Predefined query için queryName gereklidir');
  }

  const url = `/api/v1/data/${dataset}/queries/${encodeURIComponent(queryName)}`;
  const body = parameters;

  const data = await fetchFromDataGateway(url, 'POST', body);

  return {
    data: Array.isArray(data) ? data : [data],
  };
}

/**
 * Widget verisini mapping'e göre dönüştürür (opsiyonel)
 * @param response API response
 * @param mapping Mapping yapılandırması
 * @returns Dönüştürülmüş veri
 */
export function mapWidgetData(response: WidgetDataResponse, mapping?: DataSourceConfigData['mapping']): any {
  if (!mapping) {
    return response.data;
  }

  // Mapping varsa, response'dan ilgili alanları çıkar
  const mapped: any = {};

  if (mapping.items) {
    mapped.items = getNestedValue(response, mapping.items);
  }

  if (mapping.total) {
    mapped.total = getNestedValue(response, mapping.total);
  }

  // Diğer mapping alanları
  Object.keys(mapping).forEach((key) => {
    if (key !== 'items' && key !== 'total') {
      mapped[key] = getNestedValue(response, mapping[key]);
    }
  });

  return mapped.items !== undefined ? mapped : response.data;
}

/**
 * Nested object'ten değer çıkarır (örn: "data.items" -> response.data.items)
 */
function getNestedValue(obj: any, path: string): any {
  if (!path) return undefined;

  const keys = path.split('.');
  let value = obj;

  for (const key of keys) {
    if (value === null || value === undefined) {
      return undefined;
    }
    value = value[key];
  }

  return value;
}
