/**
 * Liste / child liste sütunundan başka rapora deep link.
 */
export type ReportingColumnLinkOpenIn = 'newTab' | 'sameTab';
export type ReportingColumnLinkSource = 'rowField' | 'parentField' | 'literal';

export interface ReportingColumnLinkParamMapping {
  /** Hedef rapor parametre id (örn. person). */
  targetParamId: string;
  source: ReportingColumnLinkSource;
  /** rowField / parentField */
  field?: string;
  literal?: string;
  /** URL query key; yoksa person → personId, diğerleri targetParamId. */
  queryKey?: string;
}

export interface ReportingColumnLink {
  targetReportId: string;
  openIn?: ReportingColumnLinkOpenIn;
  paramMappings: ReportingColumnLinkParamMapping[];
}

export function normalizeReportingColumnLink(raw: unknown): ReportingColumnLink | undefined {
  if (!raw || typeof raw !== 'object') return undefined;
  const o = raw as Record<string, unknown>;
  const targetReportId = String(o.targetReportId ?? o.TargetReportId ?? '').trim();
  if (!targetReportId) return undefined;

  const openRaw = String(o.openIn ?? o.OpenIn ?? 'newTab').trim();
  const openIn: ReportingColumnLinkOpenIn = openRaw === 'sameTab' ? 'sameTab' : 'newTab';

  const mappingsRaw = o.paramMappings ?? o.ParamMappings;
  const paramMappings: ReportingColumnLinkParamMapping[] = [];
  if (Array.isArray(mappingsRaw)) {
    for (const item of mappingsRaw) {
      if (!item || typeof item !== 'object') continue;
      const m = item as Record<string, unknown>;
      const targetParamId = String(m.targetParamId ?? m.TargetParamId ?? '').trim();
      if (!targetParamId) continue;
      const sourceRaw = String(m.source ?? m.Source ?? 'rowField').trim();
      const source: ReportingColumnLinkSource =
        sourceRaw === 'parentField' || sourceRaw === 'literal' ? sourceRaw : 'rowField';
      const field = String(m.field ?? m.Field ?? '').trim() || undefined;
      const literal = m.literal != null ? String(m.literal) : m.Literal != null ? String(m.Literal) : undefined;
      const queryKey = String(m.queryKey ?? m.QueryKey ?? '').trim() || undefined;
      paramMappings.push({
        targetParamId,
        source,
        ...(field ? { field } : {}),
        ...(literal != null && literal !== '' ? { literal } : {}),
        ...(queryKey ? { queryKey } : {}),
      });
    }
  }

  return {
    targetReportId,
    openIn,
    paramMappings,
  };
}

function readRowField(row: Record<string, unknown> | null | undefined, field: string): string {
  if (!row || !field) return '';
  const direct = row[field];
  if (direct != null && typeof direct !== 'object') return String(direct).trim();
  if (direct && typeof direct === 'object') {
    const o = direct as Record<string, unknown>;
    const id = o.__dataId ?? o.id ?? o.Id ?? o.value;
    if (id != null) return String(id).trim();
  }
  // dotted path
  if (field.includes('.')) {
    const parts = field.split('.');
    let cur: unknown = row;
    for (const p of parts) {
      if (!cur || typeof cur !== 'object') return '';
      cur = (cur as Record<string, unknown>)[p];
    }
    if (cur != null && typeof cur !== 'object') return String(cur).trim();
  }
  return '';
}

export function defaultReportingColumnLinkQueryKey(targetParamId: string): string {
  if (targetParamId === 'person') return 'personId';
  return targetParamId;
}

export function buildReportingColumnLinkQuery(options: {
  link: ReportingColumnLink;
  row: Record<string, unknown>;
  parentRow?: Record<string, unknown> | null;
}): Record<string, string> {
  const query: Record<string, string> = {
    reportId: options.link.targetReportId,
  };
  for (const m of options.link.paramMappings) {
    let value = '';
    if (m.source === 'literal') {
      value = m.literal?.trim() ?? '';
    } else if (m.source === 'parentField') {
      value = readRowField(options.parentRow, m.field ?? '');
    } else {
      value = readRowField(options.row, m.field ?? '');
    }
    if (!value) continue;
    const key = m.queryKey?.trim() || defaultReportingColumnLinkQueryKey(m.targetParamId);
    query[key] = value;
  }
  return query;
}

export function buildReportingColumnLinkHref(options: {
  link: ReportingColumnLink;
  row: Record<string, unknown>;
  parentRow?: Record<string, unknown> | null;
  basePath?: string;
}): string | null {
  if (!options.link.targetReportId) return null;
  const q = buildReportingColumnLinkQuery(options);
  if (!q.reportId) return null;
  const base = options.basePath ?? '/apps/reporting/browse';
  const params = new URLSearchParams();
  for (const [k, v] of Object.entries(q)) {
    if (v) params.set(k, v);
  }
  return `${base}?${params.toString()}`;
}

export function openReportingColumnLink(options: {
  link: ReportingColumnLink;
  row: Record<string, unknown>;
  parentRow?: Record<string, unknown> | null;
}): void {
  const href = buildReportingColumnLinkHref(options);
  if (!href || typeof window === 'undefined') return;
  const openIn = options.link.openIn ?? 'newTab';
  if (openIn === 'sameTab') {
    window.location.assign(href);
    return;
  }
  window.open(href, '_blank', 'noopener,noreferrer');
}
