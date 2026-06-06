import type {
  AlarmListQuery,
  AlarmListResponse,
  AlarmRule,
  AlarmSummary,
  AlarmDashboardSnapshot,
  AlarmDashboardSnapshotQuery,
  CreateAlarmRuleRequest,
  UpdateAlarmRuleRequest,
} from '@/types/apps/alarm';
import { useAuthStore } from '@/stores/auth';

function domainHeaders(): Record<string, string> {
  const auth = useAuthStore();
  const headers: Record<string, string> = {};
  if (auth.domainName) {
    headers['X-Domain-Name'] = auth.domainName;
  }
  return headers;
}

function buildQuery(params: Record<string, string | number | boolean | undefined>): string {
  const q = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue;
    q.set(key, String(value));
  }
  const s = q.toString();
  return s ? `?${s}` : '';
}

function normalizeAlarmSummary(raw: Record<string, unknown>): AlarmSummary {
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    ruleId: String(raw.ruleId ?? raw.RuleId ?? ''),
    dedupKey: String(raw.dedupKey ?? raw.DedupKey ?? ''),
    domainId: String(raw.domainId ?? raw.DomainId ?? ''),
    domainName: String(raw.domainName ?? raw.DomainName ?? ''),
    severity: Number(raw.severity ?? raw.Severity ?? 0),
    status: (raw.status ?? raw.Status ?? 'Active') as AlarmSummary['status'],
    firstSeenAt: String(raw.firstSeenAt ?? raw.FirstSeenAt ?? ''),
    lastSeenAt: String(raw.lastSeenAt ?? raw.LastSeenAt ?? ''),
    count: Number(raw.count ?? raw.Count ?? 0),
    correlationId: String(raw.correlationId ?? raw.CorrelationId ?? ''),
    context: (raw.context ?? raw.Context ?? {}) as Record<string, unknown>,
  };
}

function normalizeAlarmListResponse(raw: Record<string, unknown>): AlarmListResponse {
  const itemsRaw = (raw.items ?? raw.Items ?? []) as Record<string, unknown>[];
  const items = Array.isArray(itemsRaw) ? itemsRaw.map(normalizeAlarmSummary) : [];
  const total = Number(raw.total ?? raw.Total ?? items.length);
  return {
    items,
    total: Number.isFinite(total) ? total : items.length,
    skip: Number(raw.skip ?? raw.Skip ?? 0),
    limit: Number(raw.limit ?? raw.Limit ?? items.length),
  };
}

export async function alarmListOpen(query: AlarmListQuery = {}): Promise<AlarmListResponse> {
  const qs = buildQuery({
    openOnly: query.openOnly ?? true,
    status: query.status,
    minSeverity: query.minSeverity,
    ruleId: query.ruleId,
    search: query.search,
    from: query.from,
    to: query.to,
    skip: query.skip ?? 0,
    limit: query.limit ?? 50,
  });
  const raw = await $fetch<Record<string, unknown>>(`/api/alarm/v1/alarms${qs}`, {
    method: 'GET',
    headers: domainHeaders(),
  });
  return normalizeAlarmListResponse(raw);
}

export async function alarmGet(alarmId: string): Promise<AlarmSummary> {
  const raw = await $fetch<Record<string, unknown>>(`/api/alarm/v1/alarms/${encodeURIComponent(alarmId)}`, {
    method: 'GET',
    headers: domainHeaders(),
  });
  return normalizeAlarmSummary(raw);
}

export async function alarmDashboardSnapshot(
  query: AlarmDashboardSnapshotQuery = {},
): Promise<AlarmDashboardSnapshot> {
  const qs = buildQuery({
    rangeHours: query.rangeHours ?? 24,
    minSeverity: query.minSeverity ?? 6,
    openLimit: query.openLimit ?? 15,
  });
  return await $fetch<AlarmDashboardSnapshot>(`/api/alarm/v1/alarms/dashboard-snapshot${qs}`, {
    method: 'GET',
    headers: domainHeaders(),
  });
}

export async function alarmRuleList(): Promise<AlarmRule[]> {
  return await $fetch<AlarmRule[]>('/api/alarm/v1/rules', {
    method: 'GET',
    headers: domainHeaders(),
  });
}

export async function alarmRuleGet(ruleId: string): Promise<AlarmRule> {
  return await $fetch<AlarmRule>(`/api/alarm/v1/rules/${encodeURIComponent(ruleId)}`, {
    method: 'GET',
    headers: domainHeaders(),
  });
}

export async function alarmRuleCreate(body: CreateAlarmRuleRequest): Promise<AlarmRule> {
  return await $fetch<AlarmRule>('/api/alarm/v1/rules', {
    method: 'POST',
    headers: domainHeaders(),
    body,
  });
}

export async function alarmRuleUpdate(ruleId: string, body: UpdateAlarmRuleRequest): Promise<AlarmRule> {
  return await $fetch<AlarmRule>(`/api/alarm/v1/rules/${encodeURIComponent(ruleId)}`, {
    method: 'PUT',
    headers: domainHeaders(),
    body,
  });
}

export async function alarmRuleDelete(ruleId: string): Promise<void> {
  await $fetch(`/api/alarm/v1/rules/${encodeURIComponent(ruleId)}`, {
    method: 'DELETE',
    headers: domainHeaders(),
  });
}

async function postAlarmAction(alarmId: string, action: 'acknowledge' | 'suppress' | 'resolve'): Promise<AlarmSummary> {
  const raw = await $fetch<Record<string, unknown>>(
    `/api/alarm/v1/alarms/${encodeURIComponent(alarmId)}/${action}`,
    { method: 'POST', headers: domainHeaders() },
  );
  return normalizeAlarmSummary(raw);
}

export function alarmAcknowledge(alarmId: string): Promise<AlarmSummary> {
  return postAlarmAction(alarmId, 'acknowledge');
}

export function alarmSuppress(alarmId: string): Promise<AlarmSummary> {
  return postAlarmAction(alarmId, 'suppress');
}

export function alarmResolve(alarmId: string): Promise<AlarmSummary> {
  return postAlarmAction(alarmId, 'resolve');
}
