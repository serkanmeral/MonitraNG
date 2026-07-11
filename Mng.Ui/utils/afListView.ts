/** Automated Form — liste görünümü (varsayılan tablo | rapor runner). */

export type AfListViewMode = 'default' | 'report';

export interface AfListViewConfig {
  mode: AfListViewMode;
  /** mode=report iken rapor logical id (örn. rpt_zimmet_urun_gruplari). */
  reportId?: string | null;
}

export function normalizeAfListView(
  raw?: Partial<AfListViewConfig> | null
): AfListViewConfig {
  const mode: AfListViewMode = raw?.mode === 'report' ? 'report' : 'default';
  const reportId =
    typeof raw?.reportId === 'string' && raw.reportId.trim() ? raw.reportId.trim() : null;
  return {
    mode,
    reportId: mode === 'report' ? reportId : reportId,
  };
}

/** Rapor listesi kullanılacak mı? (mode=report ve reportId dolu). */
export function isAfReportListView(listConfig?: {
  listView?: Partial<AfListViewConfig> | null;
} | null): boolean {
  const view = normalizeAfListView(listConfig?.listView);
  return view.mode === 'report' && Boolean(view.reportId);
}

export function afListViewReportId(listConfig?: {
  listView?: Partial<AfListViewConfig> | null;
} | null): string | null {
  const view = normalizeAfListView(listConfig?.listView);
  if (view.mode !== 'report') return null;
  return view.reportId ?? null;
}
