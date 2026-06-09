import type { OcFormRuntimeContext } from '@/types/apps/operationCore';
import { resolveOcFormFieldType } from '@/utils/ocFormFieldLabels';
import { isMultiCardinality } from '@/utils/ocDynamicFormField';

/** DG inline file upload — create/patch `fields.attachments` ile aynı sözleşme. */
export interface OcFileUploadPayload {
  content: string;
  originalFileName: string;
}

export function isOcFileUploadPayload(value: unknown): value is OcFileUploadPayload {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
  const o = value as Record<string, unknown>;
  return (
    typeof o.content === 'string' &&
    o.content.trim().length > 0 &&
    typeof o.originalFileName === 'string' &&
    o.originalFileName.trim().length > 0
  );
}

export function collectOcFileUploadPayloads(value: unknown): OcFileUploadPayload[] {
  if (isOcFileUploadPayload(value)) return [value];
  if (Array.isArray(value)) {
    return value.filter(isOcFileUploadPayload);
  }
  return [];
}

export function resolveOcFormFileFieldKeys(ctx: OcFormRuntimeContext): string[] {
  const keys: string[] = [];
  for (const [key, meta] of Object.entries(ctx.fields)) {
    if (resolveOcFormFieldType(key, meta).toLowerCase() === 'file') {
      keys.push(key);
    }
  }
  return keys;
}

/** Form modelindeki file alanlarını `op_work_items.attachments` dizisine birleştirir (Ekler sekmesi). */
export function collectWorkItemAttachmentsFromFormModel(
  model: Record<string, unknown>,
  ctx: OcFormRuntimeContext
): OcFileUploadPayload[] {
  const payloads: OcFileUploadPayload[] = [];
  for (const key of resolveOcFormFileFieldKeys(ctx)) {
    payloads.push(...collectOcFileUploadPayloads(model[key]));
  }
  return payloads;
}

export function isOcFileFieldValueFilled(
  value: unknown,
  meta?: { fieldType?: string | null; cardinality?: string | null } | null,
  fieldKey?: string
): boolean {
  const ft = resolveOcFormFieldType(fieldKey ?? '', meta).toLowerCase();
  if (ft !== 'file') return false;
  const payloads = collectOcFileUploadPayloads(value);
  if (isMultiCardinality(fieldKey ?? '', meta)) {
    return payloads.length > 0;
  }
  return payloads.length === 1;
}
