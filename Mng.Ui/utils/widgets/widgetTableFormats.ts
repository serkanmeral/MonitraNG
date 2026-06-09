import { formatSeverityLabel } from '@/utils/widgets/widgetFieldMappingBridge';

export type WidgetTableColumnFormat =
  | 'text'
  | 'number'
  | 'currency'
  | 'date'
  | 'boolean'
  | 'severity'
  | 'severity-chip'
  | 'status-chip'
  | 'datetime-relative'
  | 'alarm-summary'
  | 'custom';

const ALARM_STATUS_LABELS: Record<string, string> = {
  Active: 'Aktif',
  Acknowledged: 'Onaylandı',
  Resolved: 'Çözüldü',
  Suppressed: 'Bastırıldı',
  '0': 'Aktif',
  '1': 'Onaylandı',
  '2': 'Çözüldü',
  '3': 'Bastırıldı',
};

export function isRichTableColumnFormat(format?: string): boolean {
  return (
    format === 'severity-chip' ||
    format === 'status-chip' ||
    format === 'datetime-relative' ||
    format === 'alarm-summary' ||
    format === 'siem-event-action' ||
    format === 'siem-event-outcome'
  );
}

/** 0–4 Monitra ve 0–10 SIEM ölçeği */
export function severityChipColor(severity: unknown): string {
  const n = Number(severity);
  if (Number.isNaN(n)) return 'default';
  if (n >= 8) return 'error';
  if (n >= 5) return 'warning';
  if (n >= 4) return 'error';
  if (n >= 3) return 'warning';
  if (n >= 2) return 'warning';
  return 'info';
}

export function alarmStatusChipColor(status: unknown): string {
  const s = String(status ?? '');
  if (s === 'Active' || s === '0') return 'error';
  if (s === 'Acknowledged' || s === '1') return 'warning';
  if (s === 'Resolved' || s === '2') return 'success';
  if (s === 'Suppressed' || s === '3') return 'default';
  return 'default';
}

export function formatAlarmStatusLabel(status: unknown): string {
  if (status == null || status === '') return '—';
  const key = String(status);
  return ALARM_STATUS_LABELS[key] ?? key;
}

export function formatWidgetDateTime(
  value: unknown,
  options?: { dateFormat?: string },
): string {
  if (value == null || value === '') return '—';
  try {
    const date = new Date(String(value));
    if (Number.isNaN(date.getTime())) return String(value);
    const format = options?.dateFormat || 'dd.MM.yyyy HH:mm';
    const day = String(date.getDate()).padStart(2, '0');
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const year = date.getFullYear();
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    if (format.includes('HH:mm')) {
      return `${day}.${month}.${year} ${hours}:${minutes}`;
    }
    return `${day}.${month}.${year}`;
  } catch {
    return String(value);
  }
}

export function formatRelativeTimeSimple(value: unknown, locale = 'tr'): string {
  if (value == null || value === '') return '';
  const date = new Date(String(value));
  if (Number.isNaN(date.getTime())) return '';
  const diffMs = Date.now() - date.getTime();
  const absMin = Math.abs(Math.floor(diffMs / 60_000));
  const tr = locale.startsWith('tr');

  if (absMin < 1) return tr ? 'az önce' : 'just now';
  if (absMin < 60) return tr ? `${absMin} dk önce` : `${absMin}m ago`;
  const absHours = Math.floor(absMin / 60);
  if (absHours < 24) return tr ? `${absHours} sa önce` : `${absHours}h ago`;
  const absDays = Math.floor(absHours / 24);
  return tr ? `${absDays} gün önce` : `${absDays}d ago`;
}

/** Alarm satır özeti — context alanları veya dedupKey */
export function formatAlarmRowSummary(row: Record<string, unknown>): string {
  const ctx = (row.context ?? row.Context) as Record<string, unknown> | undefined;
  const parts: string[] = [];
  if (ctx && typeof ctx === 'object') {
    for (const field of ['userId', 'srcIp', 'dstIp', 'windowCount', 'value']) {
      const val = ctx[field];
      if (val != null && String(val).trim()) parts.push(String(val));
    }
  }
  if (parts.length > 0) return parts.join(' · ');
  const dk = String(row.dedupKey ?? row.DedupKey ?? '');
  if (!dk) return '—';
  return dk.length > 72 ? `${dk.slice(0, 72)}…` : dk;
}

export function formatSeverityChipLabel(value: unknown): string {
  return formatSeverityLabel(value);
}

const SIEM_EVENT_ACTION_LABELS: Record<string, string> = {
  login_failed: 'Başarısız giriş',
  login_success_after_failures: 'Başarılı giriş (önceki hatalar)',
  privileged_login_outside_window: 'Ayrıcalıklı giriş (pencere dışı)',
  denied_flow: 'Reddedilen akış',
  allowed_flow: 'İzin verilen akış',
  rule_change: 'Kural değişikliği',
  new_flow: 'Yeni akış',
  group_member_added: 'Gruba üye eklendi',
  account_created: 'Hesap oluşturuldu',
  directory_object_modified: 'Dizin nesnesi değişti',
};

const SIEM_EVENT_OUTCOME_LABELS: Record<string, string> = {
  success: 'Başarılı',
  failure: 'Başarısız',
  unknown: 'Bilinmiyor',
};

export function formatSiemEventAction(value: unknown, row?: Record<string, unknown>): string {
  if (value == null || value === '') return '—';
  const action = String(value);
  if (row?.baselineNewFlowPair || row?.BaselineNewFlowPair) {
    return `${SIEM_EVENT_ACTION_LABELS.new_flow ?? 'Yeni akış'} (+ baseline)`;
  }
  return SIEM_EVENT_ACTION_LABELS[action] ?? action.replace(/_/g, ' ');
}

export function formatSiemEventOutcome(value: unknown): string {
  if (value == null || value === '') return '—';
  const key = String(value).toLowerCase();
  return SIEM_EVENT_OUTCOME_LABELS[key] ?? String(value);
}

export function siemEventOutcomeChipColor(value: unknown): string {
  const key = String(value ?? '').toLowerCase();
  if (key === 'success') return 'success';
  if (key === 'failure') return 'error';
  return 'default';
}
