/** Automated Form — liste yüzeyi (tablo | kart) ve isteğe bağlı rapor runner. */

export type AfListViewMode = 'default' | 'report';
export type AfListPresentation = 'table' | 'cards';

export interface AfCardTemplate {
  imageField?: string | null;
  titleFields: string[];
  lineFields: string[];
  cornerField?: string | null;
}

export interface AfListViewConfig {
  mode: AfListViewMode;
  /** mode=report iken rapor logical id (örn. rpt_zimmet_urun_gruplari). */
  reportId?: string | null;
  /** mode=default iken satırların basıldığı yüzey. */
  presentation?: AfListPresentation;
  /** Kart yüzeyinin alan yerleşimi. Boş alanlar görünür sütunlardan tamamlanır. */
  card?: AfCardTemplate | null;
}

export function emptyAfCardTemplate(): AfCardTemplate {
  return {
    imageField: null,
    titleFields: [],
    lineFields: [],
    cornerField: null,
  };
}

function uniqueFieldNames(raw: unknown): string[] {
  if (!Array.isArray(raw)) return [];
  const seen = new Set<string>();
  const names: string[] = [];
  for (const item of raw) {
    const name = typeof item === 'string' ? item.trim() : '';
    if (!name || seen.has(name)) continue;
    seen.add(name);
    names.push(name);
  }
  return names;
}

export function normalizeAfCardTemplate(raw: unknown): AfCardTemplate {
  if (!raw || typeof raw !== 'object') return emptyAfCardTemplate();
  const card = raw as Record<string, unknown>;
  const imageField = typeof card.imageField === 'string' ? card.imageField.trim() : '';
  const cornerField = typeof card.cornerField === 'string' ? card.cornerField.trim() : '';
  return {
    imageField: imageField || null,
    titleFields: uniqueFieldNames(card.titleFields),
    lineFields: uniqueFieldNames(card.lineFields),
    cornerField: cornerField || null,
  };
}

export function normalizeAfListView(
  raw?: Partial<AfListViewConfig> | null
): AfListViewConfig {
  const mode: AfListViewMode = raw?.mode === 'report' ? 'report' : 'default';
  const reportId =
    typeof raw?.reportId === 'string' && raw.reportId.trim() ? raw.reportId.trim() : null;
  const presentation: AfListPresentation = raw?.presentation === 'cards' ? 'cards' : 'table';
  return {
    mode,
    reportId,
    presentation: mode === 'default' ? presentation : 'table',
    card: mode === 'default' ? normalizeAfCardTemplate(raw?.card) : emptyAfCardTemplate(),
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

export function afListPresentation(listConfig?: {
  listView?: Partial<AfListViewConfig> | null;
} | null): AfListPresentation {
  return normalizeAfListView(listConfig?.listView).presentation ?? 'table';
}

/**
 * Kart yuvalarını görünür sütunlara indirger.
 * Başlık boşsa ilk uygun sütun başlık olur.
 * Satırlar boşsa başlık, köşe ve görsel dışında kalan sütunlar satır olur.
 */
export function resolveAfCardSlots(
  template: AfCardTemplate | null | undefined,
  visibleFields: string[]
): AfCardTemplate {
  const card = normalizeAfCardTemplate(template);
  const allowed = new Set(visibleFields);
  const keep = (names: string[]) => names.filter((name) => allowed.has(name));

  let imageField = card.imageField && allowed.has(card.imageField) ? card.imageField : null;
  let cornerField = card.cornerField && allowed.has(card.cornerField) ? card.cornerField : null;
  let titleFields = keep(card.titleFields);
  let lineFields = keep(card.lineFields);

  if (imageField) {
    titleFields = titleFields.filter((name) => name !== imageField);
    lineFields = lineFields.filter((name) => name !== imageField);
    if (cornerField === imageField) cornerField = null;
  }
  if (cornerField) {
    titleFields = titleFields.filter((name) => name !== cornerField);
    lineFields = lineFields.filter((name) => name !== cornerField);
  }
  lineFields = lineFields.filter((name) => !titleFields.includes(name));

  if (titleFields.length === 0) {
    const first = visibleFields.find((name) => name !== imageField && name !== cornerField);
    if (first) titleFields = [first];
  }

  const savedLines = keep(card.lineFields).length > 0;
  if (!savedLines) {
    const used = new Set<string>([
      ...titleFields,
      ...(cornerField ? [cornerField] : []),
      ...(imageField ? [imageField] : []),
    ]);
    lineFields = visibleFields.filter((name) => !used.has(name));
  }

  return { imageField, titleFields, lineFields, cornerField };
}
