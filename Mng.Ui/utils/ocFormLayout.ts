import type { OpFormLayoutSection } from '@/types/apps/operationCore';

/** 12 sütunlu Vuetify ızgarası — Task Manager ile uyumlu. */
export const OC_GRID_COL_VALUES = [12, 6, 4, 3, 2, 1] as const;

export type OcGridColValue = (typeof OC_GRID_COL_VALUES)[number];

export function normalizeOcGridCol(value: unknown, fallback: number = 12): number {
  const n = typeof value === 'number' ? value : Number.parseInt(String(value ?? ''), 10);
  if (!Number.isFinite(n) || n < 1) return fallback;
  return Math.min(12, Math.max(1, Math.round(n)));
}

export function gridColSelectItems(
  labelFn: (cols: number, fullRow: boolean) => string
): { title: string; value: number }[] {
  return OC_GRID_COL_VALUES.map((cols) => ({
    value: cols,
    title: labelFn(cols, cols === 12),
  }));
}

/** Yeni iş / form önizleme modalı varsayılan genişlik (px). */
export const DEFAULT_OC_FORM_DIALOG_MAX_WIDTH = 920;

export const OC_FORM_DIALOG_WIDTH_PRESETS = [560, 640, 720, 840, 920, 960, 1080, 1200] as const;

const MIN_OC_FORM_DIALOG_WIDTH = 480;
const MAX_OC_FORM_DIALOG_WIDTH = 1400;

export function normalizeOcDialogMaxWidthPx(raw: unknown): number {
  const n = typeof raw === 'number' ? raw : Number.parseInt(String(raw ?? ''), 10);
  if (!Number.isFinite(n)) return DEFAULT_OC_FORM_DIALOG_MAX_WIDTH;
  return Math.min(MAX_OC_FORM_DIALOG_WIDTH, Math.max(MIN_OC_FORM_DIALOG_WIDTH, Math.round(n)));
}

export function ocFormDialogWidthSelectItems(
  labelFn: (px: number) => string
): { title: string; value: number }[] {
  return OC_FORM_DIALOG_WIDTH_PRESETS.map((px) => ({
    value: px,
    title: labelFn(px),
  }));
}

export interface ParsedOpFormLayout {
  formHeading: string;
  formIntro: string;
  /** Form yardım dokümanı (Markdown, op_forms.layout içinde). */
  helpMarkdown: string;
  dialogMaxWidth: number;
  sections: OpFormLayoutSection[];
  sectionOrder: string[];
  sectionCols: Record<string, number>;
  fieldCols: Record<string, number>;
}

export function parseOpFormLayout(layoutRaw: unknown): ParsedOpFormLayout {
  const layout =
    layoutRaw && typeof layoutRaw === 'object' && !Array.isArray(layoutRaw)
      ? (layoutRaw as Record<string, unknown>)
      : {};

  const formHeading = String(layout.formHeading ?? layout.FormHeading ?? '').trim();
  const formIntro = String(layout.formIntro ?? layout.FormIntro ?? '').trim();
  const helpMarkdown = String(layout.helpMarkdown ?? layout.HelpMarkdown ?? '').trim();
  const dialogMaxWidth = normalizeOcDialogMaxWidthPx(layout.dialogMaxWidth ?? layout.DialogMaxWidth);

  const sectionColsRaw = (layout.sectionCols ?? layout.SectionCols ?? {}) as Record<string, unknown>;
  const fieldColsRaw = (layout.fieldCols ?? layout.FieldCols ?? {}) as Record<string, unknown>;

  const sectionCols: Record<string, number> = {};
  for (const [key, val] of Object.entries(sectionColsRaw)) {
    if (key) sectionCols[key] = normalizeOcGridCol(val);
  }

  const fieldCols: Record<string, number> = {};
  for (const [key, val] of Object.entries(fieldColsRaw)) {
    if (key) fieldCols[key] = normalizeOcGridCol(val);
  }

  const sectionsRaw = layout.sections ?? layout.Sections;
  const sections: OpFormLayoutSection[] = [];
  if (Array.isArray(sectionsRaw)) {
    for (const item of sectionsRaw) {
      if (!item || typeof item !== 'object') continue;
      const o = item as Record<string, unknown>;
      const key = String(o.key ?? o.Key ?? '').trim();
      if (!key) continue;
      const fields = parseFieldKeys(o.fields ?? o.Fields);
      const colsFromSection = o.cols ?? o.Cols;
      const cols =
        colsFromSection != null
          ? normalizeOcGridCol(colsFromSection)
          : sectionCols[key] != null
            ? sectionCols[key]
            : 12;
      sectionCols[key] = cols;
      sections.push({
        key,
        title: o.title != null ? String(o.title) : o.Title != null ? String(o.Title) : null,
        cols,
        fields,
      });
    }
  }

  const orderRaw = layout.sectionOrder ?? layout.SectionOrder;
  let sectionOrder: string[] = [];
  if (Array.isArray(orderRaw)) {
    sectionOrder = orderRaw.map((x) => String(x).trim()).filter(Boolean);
  }

  if (sectionOrder.length) {
    const byKey = new Map(sections.map((s) => [s.key, s]));
    const ordered: OpFormLayoutSection[] = [];
    const used = new Set<string>();
    for (const key of sectionOrder) {
      const sec = byKey.get(key);
      if (sec) {
        ordered.push(sec);
        used.add(key);
      }
    }
    for (const sec of sections) {
      if (!used.has(sec.key)) ordered.push(sec);
    }
    return {
      formHeading,
      formIntro,
      helpMarkdown,
      dialogMaxWidth,
      sections: ordered,
      sectionOrder: ordered.map((s) => s.key),
      sectionCols,
      fieldCols,
    };
  }

  return {
    formHeading,
    formIntro,
    helpMarkdown,
    dialogMaxWidth,
    sections,
    sectionOrder: sections.map((s) => s.key),
    sectionCols,
    fieldCols,
  };
}

function parseFieldKeys(fieldsRaw: unknown): string[] {
  if (!Array.isArray(fieldsRaw)) return [];
  const keys: string[] = [];
  const seen = new Set<string>();
  for (const item of fieldsRaw) {
    let key = '';
    if (typeof item === 'string') key = item.trim();
    else if (item && typeof item === 'object') {
      const o = item as Record<string, unknown>;
      key = String(o.key ?? o.Key ?? '').trim();
    }
    if (!key || seen.has(key)) continue;
    seen.add(key);
    keys.push(key);
  }
  return keys;
}

export interface OcFormLayoutBuildInput {
  formHeading?: string;
  formIntro?: string;
  helpMarkdown?: string;
  dialogMaxWidth?: number;
  sections: OpFormLayoutSection[];
  fieldCols: Record<string, number>;
}

export function buildOcFormLayoutPayload(input: OcFormLayoutBuildInput): Record<string, unknown> {
  const sections = input.sections
    .map((s) => ({
      key: s.key.trim(),
      title: s.title?.trim() || null,
      cols: normalizeOcGridCol(s.cols ?? 12),
      fields: [...s.fields],
    }))
    .filter((s) => s.key && s.fields.length > 0);

  const sectionOrder = sections.map((s) => s.key);
  const sectionCols: Record<string, number> = {};
  for (const s of sections) {
    sectionCols[s.key] = normalizeOcGridCol(s.cols, 12);
  }

  const fieldCols: Record<string, number> = {};
  const usedFieldKeys = new Set<string>();
  for (const s of sections) {
    for (const key of s.fields) usedFieldKeys.add(key);
  }
  for (const key of usedFieldKeys) {
    const col = input.fieldCols[key];
    if (col != null) fieldCols[key] = normalizeOcGridCol(col);
  }

  const payload: Record<string, unknown> = { sections, sectionOrder, sectionCols };
  if (Object.keys(fieldCols).length) payload.fieldCols = fieldCols;
  const heading = input.formHeading?.trim();
  const intro = input.formIntro?.trim();
  const helpMd = input.helpMarkdown?.trim();
  if (heading) payload.formHeading = heading;
  if (intro) payload.formIntro = intro;
  if (helpMd) payload.helpMarkdown = helpMd;
  payload.dialogMaxWidth = normalizeOcDialogMaxWidthPx(input.dialogMaxWidth);
  return payload;
}

export function sectionColSpanForLayout(
  sectionKey: string,
  sectionCols: Record<string, number> | undefined,
  section?: { cols?: number }
): number {
  if (section?.cols != null) return normalizeOcGridCol(section.cols);
  return normalizeOcGridCol(sectionCols?.[sectionKey], 12);
}

export function fieldColSpanForLayout(
  fieldKey: string,
  fieldCols: Record<string, number> | undefined,
  fallback = 6
): number {
  const raw = fieldCols?.[fieldKey];
  if (raw == null) {
    if (fieldKey === 'description') return 12;
    return fallback;
  }
  return normalizeOcGridCol(raw, fallback);
}
