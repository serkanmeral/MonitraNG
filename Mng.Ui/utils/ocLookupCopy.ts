import { ocListDataset } from '@/services/operationCoreService';
import type { OcFormFieldRuntimeDto } from '@/types/apps/operationCore';
import { resolveOcFormFieldType } from '@/utils/ocFormFieldLabels';
import { parseOcLookupFromFieldOptions } from '@/utils/ocLookupFieldOptions';

function storedScalar(raw: unknown): unknown {
  if (raw == null || raw === '') return null;
  if (Array.isArray(raw)) return raw.length ? storedScalar(raw[0]) : null;
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    const id = o.__dataId ?? o.dataId ?? o.id;
    return id == null || id === '' ? null : id;
  }
  return raw;
}

/**
 * Relation lookup.copyFields: seçilen kaydın alanlarını form modelindeki hedef alanlara yazar.
 * Çoklu seçimde kopya yapılmaz.
 */
export async function applyOcLookupFieldCopies(
  fieldKey: string,
  value: unknown,
  meta?: OcFormFieldRuntimeDto | null
): Promise<Record<string, unknown>> {
  const lookup = parseOcLookupFromFieldOptions(meta?.options, resolveOcFormFieldType(fieldKey, meta));
  const copies = lookup?.copyFields ?? [];
  if (!copies.length) return {};

  const dataset = meta?.relationDataset?.trim();
  if (!dataset || Array.isArray(value)) return {};

  const patch: Record<string, unknown> = {};
  const id = typeof value === 'string' ? value.trim() : '';
  if (!id) {
    for (const copy of copies) patch[copy.to] = null;
    return patch;
  }

  try {
    const rows = await ocListDataset(dataset, { filter: `__dataId:eq:${id}`, limit: 1 });
    const row = rows[0] as Record<string, unknown> | undefined;
    if (!row) return {};
    for (const copy of copies) patch[copy.to] = storedScalar(row[copy.from]);
    return patch;
  } catch {
    return {};
  }
}
