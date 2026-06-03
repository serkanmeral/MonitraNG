import type {
  AlarmListQuery,
  AlarmListResponse,
  AlarmRule,
  AlarmSummary,
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

export async function alarmListOpen(query: AlarmListQuery = {}): Promise<AlarmListResponse> {
  const qs = buildQuery({
    openOnly: query.openOnly ?? true,
    status: query.status,
    minSeverity: query.minSeverity,
    skip: query.skip ?? 0,
    limit: query.limit ?? 50,
  });
  return await $fetch<AlarmListResponse>(`/api/alarm/v1/alarms${qs}`, {
    method: 'GET',
    headers: domainHeaders(),
  });
}

export async function alarmGet(alarmId: string): Promise<AlarmSummary> {
  return await $fetch<AlarmSummary>(`/api/alarm/v1/alarms/${encodeURIComponent(alarmId)}`, {
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
