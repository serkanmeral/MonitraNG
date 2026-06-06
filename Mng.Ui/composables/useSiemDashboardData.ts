import type { SecEventDashboardSummary } from '@/types/apps/secEvent';
import type { AlarmDashboardSnapshot } from '@/types/apps/alarm';
import { secEventDashboardSummary } from '@/services/secEventService';
import { alarmDashboardSnapshot } from '@/services/alarmService';

export interface SiemDashboardPayload {
  events: SecEventDashboardSummary;
  alarms: AlarmDashboardSnapshot;
  fetchedAt: number;
}

const CACHE_TTL_MS = 60_000;

let cachedPayload: SiemDashboardPayload | null = null;
let inflight: Promise<SiemDashboardPayload> | null = null;

export function invalidateSiemDashboardCache(): void {
  cachedPayload = null;
}

export async function fetchSiemDashboardPayload(options?: {
  force?: boolean;
  rangeHours?: number;
}): Promise<SiemDashboardPayload> {
  const force = options?.force ?? false;
  const now = Date.now();

  if (!force && cachedPayload && now - cachedPayload.fetchedAt < CACHE_TTL_MS) {
    return cachedPayload;
  }

  if (!force && inflight) {
    return inflight;
  }

  inflight = (async () => {
    const rangeHours = options?.rangeHours ?? 24;
    const [events, alarms] = await Promise.all([
      secEventDashboardSummary({ rangeHours }),
      alarmDashboardSnapshot({ rangeHours, minSeverity: 6, openLimit: 15 }),
    ]);
    const payload: SiemDashboardPayload = { events, alarms, fetchedAt: Date.now() };
    cachedPayload = payload;
    return payload;
  })();

  try {
    return await inflight;
  } finally {
    inflight = null;
  }
}

export function getSiemDashboardCacheTtlMs(): number {
  return CACHE_TTL_MS;
}
