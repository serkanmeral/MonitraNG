import type { OcFormRuntimeContext, OpField } from '@/types/apps/operationCore';
import {
  OC_CORE_WORK_ITEM_FIELDS,
  resolveOcCoreFieldCardinality,
} from '@/utils/ocFieldDefinitions';

export type OcFieldLabelTranslate = (key: string) => string;

const CORE_FIELD_TYPE = new Map(
  OC_CORE_WORK_ITEM_FIELDS.map((f) => [f.key.toLowerCase(), f.fieldType])
);

export function isOcCoreWorkItemFieldKey(fieldKey: string): boolean {
  return CORE_FIELD_TYPE.has(fieldKey.toLowerCase());
}

/** Kullanıcıya gösterilen alan etiketi (form, önizleme). */
export function resolveOcFieldDisplayLabel(
  key: string,
  options?: {
    poolLabel?: string | null;
    translate?: OcFieldLabelTranslate;
  }
): string {
  const pool = options?.poolLabel?.trim();
  if (pool) return pool;

  const translate = options?.translate;
  if (translate) {
    const i18nKey = `operationCore.fieldLabels.${key}`;
    const value = translate(i18nKey);
    if (value && value !== i18nKey) return value;
  }

  return humanizeFieldKey(key);
}

/** Layout editörü listesi: etiket + teknik key. */
export function resolveOcFieldEditorLabel(
  key: string,
  options?: {
    poolLabel?: string | null;
    translate?: OcFieldLabelTranslate;
  }
): string {
  const display = resolveOcFieldDisplayLabel(key, options);
  if (display === key) return key;
  return `${display} (${key})`;
}

export function resolveOcCoreFieldType(key: string): string {
  return CORE_FIELD_TYPE.get(key.toLowerCase()) ?? 'text';
}

/**
 * Form/profil runtime fieldType.
 * Çekirdek alanlar (description vb.) → UI core katalog; havuz/MO eski "text" kayıtları ezen.
 * Havuz-only alanlar → op_fields tanımı > MO meta.
 */
export function resolveOcFormFieldType(
  fieldKey: string,
  meta?: { fieldType?: string | null } | null,
  pool?: { fieldType?: string | null } | null
): string {
  if (isOcCoreWorkItemFieldKey(fieldKey)) {
    return resolveOcCoreFieldType(fieldKey);
  }

  const poolType = pool?.fieldType?.trim();
  if (poolType) return poolType;

  const metaType = meta?.fieldType?.trim().toLowerCase();
  if (metaType) return metaType;
  return 'text';
}

/** MO / DG runtime — etiket + havuz fieldType/cardinality/relationDataset. */
export function enrichFormRuntimeFields(
  ctx: OcFormRuntimeContext,
  options?: { poolFields?: OpField[]; translate?: OcFieldLabelTranslate }
): OcFormRuntimeContext {
  const fields = { ...ctx.fields };
  for (const [key, meta] of Object.entries(fields)) {
    const pool = options?.poolFields?.find(
      (f) => f.key.toLowerCase() === key.toLowerCase()
    );
    const moLabel = meta.label?.trim();
    const moLooksLikeKey = !moLabel || moLabel === key || moLabel.toLowerCase() === key.toLowerCase();
    fields[key] = {
      ...meta,
      label: resolveOcFieldDisplayLabel(key, {
        poolLabel: pool?.label ?? (moLooksLikeKey ? null : moLabel),
        translate: options?.translate,
      }),
      fieldType: resolveOcFormFieldType(key, meta, pool),
      cardinality: pool?.cardinality ?? meta.cardinality ?? resolveOcCoreFieldCardinality(key),
      relationDataset: pool?.relationDatasetName ?? meta.relationDataset ?? null,
      options: pool?.options ?? meta.options ?? null,
    };
  }
  return { ...ctx, fields };
}

/** @deprecated enrichFormRuntimeFields kullanın */
export const enrichFormRuntimeFieldLabels = enrichFormRuntimeFields;

function humanizeFieldKey(key: string): string {
  const spaced = key
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/_/g, ' ')
    .trim();
  if (!spaced) return key;
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}
