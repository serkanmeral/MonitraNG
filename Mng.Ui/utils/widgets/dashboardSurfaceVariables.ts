/** Page-level filters a dashboard declares. Widgets read them through SurfaceContext. */

export type DashboardSurfaceVariableKind = 'person' | 'dateRange';

export interface DashboardSurfaceVariableDef {
  key: string;
  kind: DashboardSurfaceVariableKind;
  /** Person variable stays on the signed-in user unless the viewer is a manager or admin. */
  lockToSelf?: boolean;
}

const ISTANBUL_OFFSET_MS = 3 * 60 * 60 * 1000;

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

export function readSurfaceVariables(meta: unknown): DashboardSurfaceVariableDef[] {
  if (!meta || typeof meta !== 'object') return [];
  const raw = (meta as Record<string, unknown>).surfaceVariables;
  if (!Array.isArray(raw)) return [];
  const defs: DashboardSurfaceVariableDef[] = [];
  for (const item of raw) {
    if (!item || typeof item !== 'object') continue;
    const row = item as Record<string, unknown>;
    const kind = row.kind === 'person' || row.kind === 'dateRange' ? row.kind : null;
    const key = typeof row.key === 'string' ? row.key.trim() : '';
    if (!kind || !key) continue;
    defs.push({ key, kind, lockToSelf: row.lockToSelf !== false && kind === 'person' });
  }
  return defs;
}

/** Inclusive Istanbul days ending today. Seven days is the last week. */
export function istanbulTrailingDaysRange(days = 7, now = new Date()): { from: string; to: string } {
  const count = Math.max(1, Math.floor(days));
  const shifted = new Date(now.getTime() + ISTANBUL_OFFSET_MS);
  const end = new Date(Date.UTC(shifted.getUTCFullYear(), shifted.getUTCMonth(), shifted.getUTCDate()));
  const start = new Date(Date.UTC(end.getUTCFullYear(), end.getUTCMonth(), end.getUTCDate() - (count - 1)));
  const ymd = (value: Date) => `${value.getUTCFullYear()}-${pad(value.getUTCMonth() + 1)}-${pad(value.getUTCDate())}`;
  return { from: ymd(start), to: ymd(end) };
}

/** Monday–Sunday week that contains today, in the Istanbul calendar. */
export function istanbulWeekRange(now = new Date()): { from: string; to: string } {
  const shifted = new Date(now.getTime() + ISTANBUL_OFFSET_MS);
  const day = shifted.getUTCDay();
  const mondayOffset = day === 0 ? -6 : 1 - day;
  const monday = new Date(Date.UTC(shifted.getUTCFullYear(), shifted.getUTCMonth(), shifted.getUTCDate() + mondayOffset));
  const sunday = new Date(Date.UTC(monday.getUTCFullYear(), monday.getUTCMonth(), monday.getUTCDate() + 6));
  const ymd = (value: Date) => `${value.getUTCFullYear()}-${pad(value.getUTCMonth() + 1)}-${pad(value.getUTCDate())}`;
  return { from: ymd(monday), to: ymd(sunday) };
}

export function istanbulMonthRange(now = new Date()): { from: string; to: string } {
  const shifted = new Date(now.getTime() + ISTANBUL_OFFSET_MS);
  const year = shifted.getUTCFullYear();
  const month = shifted.getUTCMonth();
  const last = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
  return {
    from: `${year}-${pad(month + 1)}-01`,
    to: `${year}-${pad(month + 1)}-${pad(last)}`,
  };
}

/** Inclusive Istanbul calendar days as ISO instants for $timeRange.from / $timeRange.to. */
export function istanbulDateRangeToTime(from: string, to: string): { from: string; to: string; preset: 'custom' } | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(from) || !/^\d{4}-\d{2}-\d{2}$/.test(to) || from > to) return null;
  const start = from.split('-').map(Number);
  const end = to.split('-').map(Number);
  return {
    preset: 'custom',
    from: new Date(Date.UTC(start[0], start[1] - 1, start[2], -3, 0, 0, 0)).toISOString(),
    to: new Date(Date.UTC(end[0], end[1] - 1, end[2], 20, 59, 59, 999)).toISOString(),
  };
}
