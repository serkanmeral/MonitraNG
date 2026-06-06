export const SIEM_DASHBOARD_REFRESH_INTERVALS_SEC = [0, 60, 120, 300] as const;

export type SiemDashboardRefreshIntervalSec =
  (typeof SIEM_DASHBOARD_REFRESH_INTERVALS_SEC)[number];

export const SIEM_DASHBOARD_DEFAULT_REFRESH_SEC: SiemDashboardRefreshIntervalSec = 120;

const STORAGE_KEY = 'siem-dashboard-auto-refresh-seconds-v1';

function isValidInterval(sec: number): sec is SiemDashboardRefreshIntervalSec {
  return (SIEM_DASHBOARD_REFRESH_INTERVALS_SEC as readonly number[]).includes(sec);
}

export function loadSiemDashboardRefreshIntervalSec(): SiemDashboardRefreshIntervalSec {
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

export function saveSiemDashboardRefreshIntervalSec(sec: SiemDashboardRefreshIntervalSec): void {
  if (import.meta.server) return;
  localStorage.setItem(STORAGE_KEY, String(sec));
}
