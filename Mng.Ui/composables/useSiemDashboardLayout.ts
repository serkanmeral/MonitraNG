export type SiemDashboardWidgetId = 'stats' | 'breakdown' | 'recentAlarms' | 'quickLinks';

export type SiemStatCardId =
  | 'eventsTotal'
  | 'openAlarms'
  | 'loginFailed'
  | 'deniedFlow'
  | 'newFlow';

export interface SiemDashboardLayout {
  widgetOrder: SiemDashboardWidgetId[];
  hiddenWidgets: SiemDashboardWidgetId[];
  statCardOrder: SiemStatCardId[];
  hiddenStatCards: SiemStatCardId[];
}

const STORAGE_KEY = 'siem-dashboard-layout-v1';

export const SIEM_DASHBOARD_WIDGET_IDS: SiemDashboardWidgetId[] = [
  'stats',
  'breakdown',
  'recentAlarms',
  'quickLinks',
];

export const SIEM_STAT_CARD_IDS: SiemStatCardId[] = [
  'eventsTotal',
  'openAlarms',
  'loginFailed',
  'deniedFlow',
  'newFlow',
];

export function defaultSiemDashboardLayout(): SiemDashboardLayout {
  return {
    widgetOrder: [...SIEM_DASHBOARD_WIDGET_IDS],
    hiddenWidgets: [],
    statCardOrder: [...SIEM_STAT_CARD_IDS],
    hiddenStatCards: [],
  };
}

function normalizeOrder<T extends string>(order: T[], all: readonly T[]): T[] {
  const seen = new Set<T>();
  const result: T[] = [];
  for (const id of order) {
    if (all.includes(id) && !seen.has(id)) {
      result.push(id);
      seen.add(id);
    }
  }
  for (const id of all) {
    if (!seen.has(id)) result.push(id);
  }
  return result;
}

export function normalizeSiemDashboardLayout(raw: Partial<SiemDashboardLayout> | null): SiemDashboardLayout {
  const base = defaultSiemDashboardLayout();
  if (!raw) return base;

  return {
    widgetOrder: normalizeOrder(raw.widgetOrder ?? base.widgetOrder, SIEM_DASHBOARD_WIDGET_IDS),
    hiddenWidgets: (raw.hiddenWidgets ?? []).filter((id) =>
      SIEM_DASHBOARD_WIDGET_IDS.includes(id as SiemDashboardWidgetId),
    ) as SiemDashboardWidgetId[],
    statCardOrder: normalizeOrder(raw.statCardOrder ?? base.statCardOrder, SIEM_STAT_CARD_IDS),
    hiddenStatCards: (raw.hiddenStatCards ?? []).filter((id) =>
      SIEM_STAT_CARD_IDS.includes(id as SiemStatCardId),
    ) as SiemStatCardId[],
  };
}

export function loadSiemDashboardLayout(): SiemDashboardLayout {
  if (import.meta.server) return defaultSiemDashboardLayout();
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return defaultSiemDashboardLayout();
    return normalizeSiemDashboardLayout(JSON.parse(raw) as Partial<SiemDashboardLayout>);
  } catch {
    return defaultSiemDashboardLayout();
  }
}

export function saveSiemDashboardLayout(layout: SiemDashboardLayout): void {
  if (import.meta.server) return;
  localStorage.setItem(STORAGE_KEY, JSON.stringify(normalizeSiemDashboardLayout(layout)));
}

export function resetSiemDashboardLayout(): SiemDashboardLayout {
  const layout = defaultSiemDashboardLayout();
  saveSiemDashboardLayout(layout);
  return layout;
}
