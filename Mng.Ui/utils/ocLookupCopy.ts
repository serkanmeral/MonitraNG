import { ocExecuteDatasetQuery, ocListDataset } from '@/services/operationCoreService';
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
  const [relationPatch, resolvePatch] = await Promise.all([
    applyRelationCopies(fieldKey, value, meta),
    applyCardResolveCopies(value, meta),
  ]);
  return { ...relationPatch, ...resolvePatch };
}

async function applyRelationCopies(
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

type CardResolveCopy = { from: string; to: string };

type CardResolveConfig = {
  dataset: string;
  query: string;
  parameter: string;
  copies: CardResolveCopy[];
};

function readCardResolve(options: unknown): CardResolveConfig | null {
  if (!options || typeof options !== 'object') return null;
  const raw = (options as Record<string, unknown>).cardResolve;
  if (!raw || typeof raw !== 'object') return null;
  const row = raw as Record<string, unknown>;
  const dataset = typeof row.dataset === 'string' ? row.dataset.trim() : '';
  const query = typeof row.query === 'string' ? row.query.trim() : '';
  const parameter = typeof row.parameter === 'string' ? row.parameter.trim() : '';
  const copies = Array.isArray(row.copies)
    ? row.copies.flatMap((item) => {
        if (!item || typeof item !== 'object') return [];
        const copy = item as Record<string, unknown>;
        const from = typeof copy.from === 'string' ? copy.from.trim() : '';
        const to = typeof copy.to === 'string' ? copy.to.trim() : '';
        return from && to ? [{ from, to }] : [];
      })
    : [];
  if (!dataset || !query || !parameter || !copies.length) return null;
  return { dataset, query, parameter, copies };
}

/**
 * persons alanı options.cardResolve: seçilen Keeper kimliğiyle adlı sorgu çalışır,
 * dönen satırın alanları forma yazılır. Seçim boşalınca hedefler temizlenir.
 */
async function applyCardResolveCopies(
  value: unknown,
  meta?: OcFormFieldRuntimeDto | null
): Promise<Record<string, unknown>> {
  const resolve = readCardResolve(meta?.options);
  if (!resolve || Array.isArray(value)) return {};

  const patch: Record<string, unknown> = {};
  const id = typeof value === 'string' ? value.trim() : '';
  if (!id) {
    for (const copy of resolve.copies) patch[copy.to] = null;
    return patch;
  }

  try {
    const rows = await ocExecuteDatasetQuery(resolve.dataset, resolve.query, { [resolve.parameter]: id });
    const row = (rows[0] ?? {}) as Record<string, unknown>;
    for (const copy of resolve.copies) patch[copy.to] = storedScalar(row[copy.from]);
    return patch;
  } catch {
    return {};
  }
}
