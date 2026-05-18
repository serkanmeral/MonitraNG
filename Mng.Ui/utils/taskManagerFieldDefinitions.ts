/**
 * Alan havuzu (`tm_field_definitions`) — UI ve form için yardımcılar.
 * Adım 1: cardinality + optionsJson; ileride yeni fieldType değerleri burada dokümante edilir.
 */

import type { TmFieldCardinality, TmFieldDefinition } from '@/types/apps/taskManager';

/** tm_issues üzerindeki havuz / çekirdek alanların semantik türü (fieldType sütunu) */
export type TmIssueFieldSemanticType =
  | 'incremental'
  | 'text'
  | 'number'
  | 'bool'
  | 'date'
  | 'datetime'
  | 'relation'
  | 'persons'
  | 'person'
  | 'group'
  | 'tags'
  | 'file'
  | 'array'
  | string;

/** optionsJson içinde kullanılabilecek yaygın anahtarlar (zorunlu değil) */
export interface TmFieldOptionsJson {
  /** number */
  min?: number;
  max?: number;
  step?: number;
  /** file */
  maxFiles?: number;
  maxSizeMb?: number;
  allowedMimeTypes?: string[];
  /** relation / tags — hedef dataset veya sabit liste */
  relationDataset?: string;
  /** sabit seçenekler (enum benzeri) */
  enumValues?: string[];
}

export function parseTmFieldOptionsJson(raw: string | null | undefined): TmFieldOptionsJson | null {
  if (raw == null || String(raw).trim() === '') return null;
  try {
    const v = JSON.parse(String(raw)) as unknown;
    if (v === null || typeof v !== 'object' || Array.isArray(v)) return null;
    return v as TmFieldOptionsJson;
  } catch {
    return null;
  }
}

export function normalizeFieldCardinality(
  raw: string | null | undefined,
  fallback: TmFieldCardinality = 'single'
): TmFieldCardinality {
  const s = (raw ?? '').toLowerCase().trim();
  if (s === 'multi' || s === 'multiple') return 'multi';
  return fallback;
}

/** cardinality boşsa: labels → multi, aksi halde single */
export function effectiveFieldCardinality(fd: Pick<TmFieldDefinition, 'cardinality' | 'key'>): TmFieldCardinality {
  const c = fd.cardinality;
  if (c === 'multi' || c === 'single') return c;
  if (fd.key === 'labels') return 'multi';
  return 'single';
}

/** Havuz CRUD — veri tipi seçenekleri (tm_field_definitions.fieldType) */
export const TM_POOL_FIELD_TYPE_VALUES = [
  'text',
  'number',
  'bool',
  'date',
  'datetime',
  'relation',
  'persons',
  'person',
  'group',
  'tags',
  'file',
  'incremental',
  'array',
] as const;

/** Alan anahtarı: harf veya _ ile başlar; sonrası harf, rakam, _ */
export const TM_FIELD_KEY_PATTERN = /^[a-zA-Z_][a-zA-Z0-9_]*$/;
