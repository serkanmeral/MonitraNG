import type { ManifestDataBinding } from '@/types/apps/widgetManifest';
import type { WidgetDataResponse } from '@/services/widgetDataService';
import { fetchFromDataGateway } from '@/services/apiService';
import { alarmDashboardSnapshot, alarmTrendBuckets } from '@/services/alarmService';
import { shouldReturnStatShape } from '@/utils/widgets/widgetFieldMappingBridge';
import { secEventDashboardSummary, secEventQuery } from '@/services/secEventService';
import { diGetChildren, diSearch, diGetRecent, diGetDrafts } from '@/services/documentIntelligenceService';
import { parseQueryRef } from '@/utils/widgets/widgetManifestAdapter';
import {
  bindingWantsScenarioRollup,
  extractScenarioRollupFromSnapshot,
  scenarioRollupWidgetResponse,
} from '@/utils/alarm/alarmScenarioRollupNormalize';
import { normalizeManifestBinding } from '@/utils/widgets/widgetManifestServiceRefs';

function getNestedValue(obj: unknown, path: string): unknown {
  if (!path || obj == null) return undefined;
  const keys = path.split('.');
  let value: unknown = obj;
  for (const key of keys) {
    if (value == null || typeof value !== 'object') return undefined;
    value = (value as Record<string, unknown>)[key];
  }
  return value;
}

function applyMapping(raw: unknown, fieldMap?: Record<string, string>, mapping?: Record<string, string>): Record<string, unknown> {
  const out: Record<string, unknown> = { raw };
  if (fieldMap?.value) {
    out.value = getNestedValue(raw, fieldMap.value) ?? (raw as Record<string, unknown>)?.[fieldMap.value];
  }
  if (mapping?.loginFailed) {
    out.loginFailed = getNestedValue(raw, mapping.loginFailed);
  }
  if (fieldMap?.rows) {
    const rowsPath = mapping?.[fieldMap.rows] ?? fieldMap.rows;
    out.items = getNestedValue(raw, rowsPath) ?? (raw as Record<string, unknown>)?.[fieldMap.rows];
  }
  if (fieldMap?.total) {
    out.total = getNestedValue(raw, fieldMap.total) ?? (raw as Record<string, unknown>)?.[fieldMap.total];
  }
  return out;
}

function toWidgetDataResponse(
  mapped: Record<string, unknown>,
  options: { stat?: boolean } = {},
): WidgetDataResponse {
  if (options.stat) {
    const value = mapped.value ?? mapped.loginFailed ?? mapped.total ?? 0;
    return { data: [{ value, ...mapped }], total: 1 };
  }
  const items = mapped.items;
  const rows = Array.isArray(items) ? items : items != null ? [items] : [];
  const total = typeof mapped.total === 'number' ? mapped.total : rows.length;
  return { data: rows, total };
}

function rollupScenarioToSeverityBuckets(
  rollup: Array<{ maxSeverity?: number | null; openCount?: number }> | undefined,
): Array<{ severity: number; count: number }> {
  if (!Array.isArray(rollup)) return [];
  const bySeverity = new Map<number, number>();
  for (const row of rollup) {
    const sev = row.maxSeverity ?? 0;
    bySeverity.set(sev, (bySeverity.get(sev) ?? 0) + (row.openCount ?? 0));
  }
  return Array.from(bySeverity.entries())
    .map(([severity, count]) => ({ severity, count }))
    .sort((a, b) => a.severity - b.severity);
}

function normalizeQueryResponse(raw: unknown, binding: ManifestDataBinding): WidgetDataResponse {
  if (Array.isArray(raw)) {
    return { data: raw, total: raw.length };
  }
  if (!raw || typeof raw !== 'object') {
    return { data: [], total: 0 };
  }

  const obj = raw as Record<string, unknown>;
  const rowsKey = binding.mapping?.[binding.fieldMap?.rows ?? ''] ?? binding.fieldMap?.rows ?? 'items';
  const totalKey = binding.fieldMap?.total ?? 'total';
  const valueKey = binding.fieldMap?.value ?? 'total';

  if (Array.isArray(obj[rowsKey])) {
    return {
      data: obj[rowsKey] as unknown[],
      total: typeof obj[totalKey] === 'number' ? (obj[totalKey] as number) : (obj[rowsKey] as unknown[]).length,
    };
  }
  if (Array.isArray(obj.items)) {
    return {
      data: obj.items as unknown[],
      total: typeof obj.total === 'number' ? (obj.total as number) : (obj.items as unknown[]).length,
    };
  }

  const statValue = obj[valueKey] ?? obj.total ?? obj.count;
  if (statValue != null && !Array.isArray(obj.items)) {
    return toWidgetDataResponse({ value: statValue, total: statValue }, { stat: true });
  }

  return { data: [obj], total: 1 };
}

async function fetchQueryRef(binding: ManifestDataBinding): Promise<WidgetDataResponse> {
  if (!binding.queryRef) {
    throw new Error('queryRef eksik');
  }
  const parsed = parseQueryRef(binding.queryRef);
  if (!parsed) {
    throw new Error(`Geçersiz queryRef: ${binding.queryRef}`);
  }
  const url = `/api/v1/data/${parsed.dataset}/queries/${encodeURIComponent(parsed.queryName)}`;
  const raw = await fetchFromDataGateway(url, 'POST', binding.parameters ?? {});
  return normalizeQueryResponse(raw, binding);
}

async function fetchServiceRef(
  serviceRef: string,
  parameters: Record<string, unknown>,
  binding: ManifestDataBinding,
): Promise<WidgetDataResponse> {
  const [service, resourceAction] = serviceRef.split(':', 2);
  const action = resourceAction ?? '';

  if (service === 'mngalarm' && action === 'alarms/dashboard-snapshot') {
    const raw = await alarmDashboardSnapshot({
      rangeHours: Number(parameters.rangeHours ?? 24),
      minSeverity: parameters.minSeverity != null ? Number(parameters.minSeverity) : 6,
      openLimit: parameters.openLimit != null ? Number(parameters.openLimit) : undefined,
    });

    if (binding.mapping?.severityBuckets === 'scenarioRollup' || binding.fieldMap?.rows === 'severityBuckets') {
      const mapped = applyMapping(raw, binding.fieldMap, binding.mapping);
      mapped.items = rollupScenarioToSeverityBuckets(
        extractScenarioRollupFromSnapshot(raw) as Array<{ maxSeverity?: number | null; openCount?: number }>,
      );
      return toWidgetDataResponse(mapped);
    }

    if (bindingWantsScenarioRollup(binding)) {
      return scenarioRollupWidgetResponse(raw);
    }

    const mapped = applyMapping(raw, binding.fieldMap, binding.mapping);
    mapped.value = mapped.value ?? raw.openTotal;

    if (shouldReturnStatShape(binding)) {
      return toWidgetDataResponse(mapped, { stat: true });
    }

    if (binding.fieldMap?.rows === 'scenarioRollup') {
      return scenarioRollupWidgetResponse(raw);
    }
    if (binding.fieldMap?.rows === 'openAlarms' || binding.fieldMap?.rows === 'rows') {
      mapped.items = raw.openAlarms ?? [];
      mapped.total = raw.openTotal ?? mapped.items.length;
      return toWidgetDataResponse(mapped);
    }
    return toWidgetDataResponse(mapped, { stat: !binding.fieldMap?.rows });
  }

  if (service === 'mngalarm' && action === 'alarms/trend-buckets') {
    const raw = await alarmTrendBuckets({
      rangeHours: Number(parameters.rangeHours ?? 24),
    });
    const mapped = applyMapping(raw, binding.fieldMap, binding.mapping);
    mapped.items = raw.items ?? [];
    return toWidgetDataResponse(mapped);
  }

  if (service === 'mngreactor' && action === 'sec-events/dashboard-summary') {
    const raw = await secEventDashboardSummary({
      rangeHours: Number(parameters.rangeHours ?? 24),
      excludeUnknown: parameters.excludeUnknown !== false,
    });
    const mapped = applyMapping(raw, binding.fieldMap, binding.mapping);
    if (binding.fieldMap?.value === 'eventsTotal') {
      mapped.value = raw.eventsTotal;
    }
    if (binding.mapping?.loginFailed) {
      mapped.value = getNestedValue(raw, binding.mapping.loginFailed) ?? 0;
    }
    if (binding.fieldMap?.rows === 'hourly') {
      mapped.items = raw.hourly ?? [];
      return toWidgetDataResponse(mapped);
    }
    return toWidgetDataResponse(mapped, { stat: true });
  }

  if (service === 'mngreactor' && action === 'sec-events/list') {
    const raw = await secEventQuery({
      from: parameters.from as string | undefined,
      to: parameters.to as string | undefined,
      limit: Number(parameters.limit ?? 15),
      excludeUnknown: parameters.excludeUnknown !== false,
    });
    return { data: raw.items, total: raw.total };
  }

  if (service === 'mngreactor' && action === 'sec-events/scenario-rollup') {
    return fetchServiceRef(
      'mngalarm:alarms/dashboard-snapshot',
      parameters,
      {
        ...binding,
        serviceRef: 'mngalarm:alarms/dashboard-snapshot',
        fieldMap: { ...binding.fieldMap, rows: 'scenarioRollup' },
        responseShape: 'rows',
      },
    );
  }

  if (service === 'mngdocument' && action === 'resources/children') {
    const parentId = (parameters.folderId ?? parameters.parentId) as string | null | undefined;
    const raw = await diGetChildren(parentId ?? null);
    return { data: raw.items, total: raw.total ?? raw.items.length };
  }

  if (service === 'mngdocument' && action === 'resources/search') {
    const q = String(parameters.q ?? '*').trim() || '*';
    const raw = await diSearch(
      q,
      Number(parameters.skip ?? 0),
      Number(parameters.limit ?? 10),
    );
    return { data: raw.items, total: raw.total ?? raw.items.length };
  }

  if (service === 'mngdocument' && action === 'resources/recent') {
    const raw = await diGetRecent(Number(parameters.limit ?? 10));
    return { data: raw.items, total: raw.total ?? raw.items.length };
  }

  if (service === 'mngdocument' && action === 'resources/drafts') {
    const raw = await diGetDrafts(Number(parameters.limit ?? 50));
    const mapped = applyMapping(raw, binding.fieldMap, binding.mapping);
    mapped.value = raw.total ?? raw.items.length;
    mapped.total = raw.total ?? raw.items.length;
    mapped.items = raw.items;
    return toWidgetDataResponse(mapped, { stat: binding.fieldMap?.value === 'total' });
  }

  throw new Error(`Desteklenmeyen serviceRef: ${serviceRef}`);
}

export async function fetchManifestWidgetData(binding: ManifestDataBinding): Promise<WidgetDataResponse> {
  const resolved = normalizeManifestBinding(binding);
  if (resolved.kind === 'static') {
    return { data: [], total: 0 };
  }
  if (resolved.kind === 'queryRef') {
    return fetchQueryRef(resolved);
  }
  if (resolved.kind === 'serviceRef' && resolved.serviceRef) {
    return fetchServiceRef(resolved.serviceRef, resolved.parameters ?? {}, resolved);
  }
  throw new Error(`Manifest fetch henüz desteklenmiyor: ${resolved.kind}`);
}

export function getManifestBindingFromWidget(widget: { config?: object | null; templateId?: string }): ManifestDataBinding | null {
  const config = (widget.config ?? {}) as Record<string, unknown>;
  const binding = config.manifestBinding as ManifestDataBinding | undefined;
  if (!binding?.kind) return null;
  const templateId =
    (config.templateId as string | undefined) ??
    widget.templateId;
  return normalizeManifestBinding(binding, { templateId });
}
