/** SIEM Güvenlik Paneli — @widgets instance adları (setup-siem-center-widgets.ps1) */
export const SIEM_CENTER_WIDGET_NAMES = {
  eventsTotal: 'siem-center.events-total',
  loginFailed: 'siem-center.login-failed',
  openAlarms: 'siem-center.open-alarms',
  hourlyTrend: 'siem-center.hourly-trend',
  recentAlarms: 'siem-center.recent-alarms',
  scenarios: 'siem-center.scenarios',
} as const;

export type SiemCenterWidgetKey = keyof typeof SIEM_CENTER_WIDGET_NAMES;

export const SIEM_CENTER_TEMPLATE_MAP: Record<SiemCenterWidgetKey, string> = {
  eventsTotal: 'siem.events-total-stat',
  loginFailed: 'siem.login-failed-stat',
  openAlarms: 'siem.open-alarms-stat',
  hourlyTrend: 'siem.events-hourly-trend',
  recentAlarms: 'alarm.recent-table',
  scenarios: 'siem.scenario-cards',
};

export const SIEM_CENTER_DASHBOARD_SLUG = 'siem-center';
export const SIEM_CENTER_DASHBOARD_NAME = 'siem-center-default';
