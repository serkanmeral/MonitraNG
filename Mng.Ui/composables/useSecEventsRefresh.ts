export {
  SIEM_DASHBOARD_REFRESH_INTERVALS_SEC as SEC_EVENTS_REFRESH_INTERVALS_SEC,
  SIEM_DASHBOARD_DEFAULT_REFRESH_SEC as SEC_EVENTS_DEFAULT_REFRESH_SEC,
  type SiemDashboardRefreshIntervalSec as SecEventsRefreshIntervalSec,
} from '@/composables/useSiemDashboardRefresh';

import {
  SIEM_DASHBOARD_DEFAULT_REFRESH_SEC,
  SIEM_DASHBOARD_REFRESH_INTERVALS_SEC,
  type SiemDashboardRefreshIntervalSec,
} from '@/composables/useSiemDashboardRefresh';

const STORAGE_KEY = 'siem-events-auto-refresh-seconds-v1';

function isValidInterval(sec: number): sec is SiemDashboardRefreshIntervalSec {
  return (SIEM_DASHBOARD_REFRESH_INTERVALS_SEC as readonly number[]).includes(sec);
}

export function loadSecEventsRefreshIntervalSec(): SiemDashboardRefreshIntervalSec {
  if (import.meta.server) return SIEM_DASHBOARD_DEFAULT_REFRESH_SEC;
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return SIEM_DASHBOARD_DEFAULT_REFRESH_SEC;
    const parsed = Number.parseInt(raw, 10);
    return isValidInterval(parsed) ? parsed : SIEM_DASHBOARD_DEFAULT_REFRESH_SEC;
  } catch {
    return SIEM_DASHBOARD_DEFAULT_REFRESH_SEC;
  }
}

export function saveSecEventsRefreshIntervalSec(sec: SiemDashboardRefreshIntervalSec): void {
  if (import.meta.server) return;
  localStorage.setItem(STORAGE_KEY, String(sec));
}
