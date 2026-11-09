import { ocExecuteDatasetQuery } from '@/services/operationCoreService';
import type { OcFormFieldRuntimeDto } from '@/types/apps/operationCore';

type ComputeParameter = { name: string; from: string; required: boolean };
type ComputeCopy = { from: string; to: string };

type FieldCompute = {
  dataset: string;
  query: string;
  parameters: ComputeParameter[];
  copies: ComputeCopy[];
};

function readCompute(options: unknown): FieldCompute | null {
  if (!options || typeof options !== 'object') return null;
  const raw = (options as Record<string, unknown>).compute;
  if (!raw || typeof raw !== 'object') return null;
  const row = raw as Record<string, unknown>;
  const dataset = typeof row.dataset === 'string' ? row.dataset.trim() : '';
  const query = typeof row.query === 'string' ? row.query.trim() : '';
  const parameters = Array.isArray(row.parameters)
    ? row.parameters.flatMap((item) => {
        if (!item || typeof item !== 'object') return [];
        const param = item as Record<string, unknown>;
        const name = typeof param.name === 'string' ? param.name.trim() : '';
        const from = typeof param.from === 'string' ? param.from.trim() : '';
        if (!name || !from) return [];
        return [{ name, from, required: param.required === true }];
      })
    : [];
  const copies = Array.isArray(row.copies)
    ? row.copies.flatMap((item) => {
        if (!item || typeof item !== 'object') return [];
        const copy = item as Record<string, unknown>;
        const from = typeof copy.from === 'string' ? copy.from.trim() : '';
        const to = typeof copy.to === 'string' ? copy.to.trim() : '';
        return from && to ? [{ from, to }] : [];
      })
    : [];
  if (!dataset || !query || !parameters.length || !copies.length) return null;
  return { dataset, query, parameters, copies };
}

function computesOf(fields: Record<string, OcFormFieldRuntimeDto>): FieldCompute[] {
  const list: FieldCompute[] = [];
  for (const meta of Object.values(fields)) {
    const compute = readCompute(meta?.options);
    if (compute) list.push(compute);
  }
  return list;
}

function isEmpty(value: unknown): boolean {
  if (value == null) return true;
  if (typeof value === 'string') return value.trim().length === 0;
  if (Array.isArray(value)) return value.length === 0;
  return false;
}

function scalar(value: unknown): string | number | boolean | null {
  if (typeof value === 'boolean' || typeof value === 'number') return value;
  if (typeof value === 'string') {
    const text = value.trim();
    return text || null;
  }
  return null;
}

function sameValue(left: unknown, right: unknown): boolean {
  const leftEmpty = left == null || left === '';
  const rightEmpty = right == null || right === '';
  if (leftEmpty || rightEmpty) return leftEmpty && rightEmpty;
  if (typeof left === 'number' || typeof right === 'number') {
    const a = Number(left);
    const b = Number(right);
    return Number.isFinite(a) && Number.isFinite(b) && a === b;
  }
  return left === right;
}

/** Kaynak alanların imzası. Hedef alanlar değişince hesap yeniden çalışmaz. */
export function fieldComputeSignature(
  model: Record<string, unknown>,
  fields: Record<string, OcFormFieldRuntimeDto>
): string {
  return computesOf(fields)
    .map((compute) => compute.parameters.map((param) => String(model[param.from] ?? '')).join('\u001f'))
    .join('\u001e');
}

/**
 * Alan options.compute: kaynaklar dolunca adlı sorgu çalışır, kopyalar forma yazılır.
 * Zorunlu kaynak boşsa hedefler temizlenir.
 */
export async function runFieldComputes(
  model: Record<string, unknown>,
  fields: Record<string, OcFormFieldRuntimeDto>
): Promise<Record<string, unknown>> {
  const patch: Record<string, unknown> = {};
  for (const compute of computesOf(fields)) {
    const missing = compute.parameters.some((param) => param.required && isEmpty(model[param.from]));
    if (missing) {
      for (const copy of compute.copies) {
        if (!isEmpty(model[copy.to])) patch[copy.to] = null;
      }
      continue;
    }

    const parameters: Record<string, string | number | boolean> = {};
    for (const param of compute.parameters) {
      const value = scalar(model[param.from]);
      if (value == null) continue;
      parameters[param.name] = value;
    }

    try {
      const rows = await ocExecuteDatasetQuery(compute.dataset, compute.query, parameters);
      const row = (rows[0] ?? {}) as Record<string, unknown>;
      for (const copy of compute.copies) {
        const next = scalar(row[copy.from]);
        if (!sameValue(model[copy.to], next)) patch[copy.to] = next;
      }
    } catch {
      // Keep the previous value. The transition query still calculates on submit.
    }
  }
  return patch;
}
