import type { OpPriority, OpState, OpWorkItemType } from '@/types/apps/operationCore';
import { isLegacyHexStatusColor, isTmStatusThemeColor } from '@/utils/taskManagerStatusColor';
import {
  defaultIconForPriorityLevel,
  defaultIconForStateCategory,
  defaultIconForTypeCategory,
} from '@/utils/ocCatalogDefaultIcons';

export type OcCatalogKind = 'state' | 'priority' | 'type';

export interface OcCatalogDisplayItem {
  id: string;
  name: string;
  color?: string | null;
  icon?: string | null;
}

export function toCatalogDisplayItem(
  row: Pick<OpState | OpPriority | OpWorkItemType, '__dataId' | 'name' | 'color' | 'icon'>
): OcCatalogDisplayItem {
  return {
    id: row.__dataId,
    name: row.name,
    color: row.color ?? null,
    icon: row.icon ?? null,
  };
}

export function buildCatalogDisplayMap<T extends { __dataId: string; name: string; color?: string | null; icon?: string | null }>(
  rows: T[]
): Map<string, OcCatalogDisplayItem> {
  return new Map(rows.map((row) => [row.__dataId, toCatalogDisplayItem(row)]));
}

export function resolveCatalogDisplayItem(
  map: Map<string, OcCatalogDisplayItem>,
  id: string | null | undefined,
  fallbackName?: string | null
): OcCatalogDisplayItem | null {
  if (!id?.trim()) return null;
  const hit = map.get(id);
  if (hit) return hit;
  const name = fallbackName?.trim() || id;
  return { id, name, color: null, icon: null };
}

/** Tanımlı ikon yoksa category/level'e göre varsayılan Tabler ikonu uygular. */
export function enrichCatalogDisplayIcon(
  item: OcCatalogDisplayItem | null,
  kind: OcCatalogKind,
  meta?: { category?: string | null; level?: number | string | null }
): OcCatalogDisplayItem | null {
  if (!item) return null;
  if (item.icon?.trim()) return item;
  const fallback =
    kind === 'state'
      ? defaultIconForStateCategory(meta?.category ?? null)
      : kind === 'priority'
        ? defaultIconForPriorityLevel(meta?.level ?? null)
        : defaultIconForTypeCategory(meta?.category ?? null);
  if (!fallback) return item;
  return { ...item, icon: fallback };
}

/** Metin rengi — Vuetify tema sınıfı veya hex inline style. */
export function catalogTextPresentation(color: string | null | undefined): {
  className?: string;
  style?: Record<string, string>;
} {
  const c = color?.trim();
  if (!c) return {};
  if (isTmStatusThemeColor(c)) return { className: `text-${c}` };
  if (isLegacyHexStatusColor(c)) return { style: { color: c } };
  return {};
}

/** İkon rengi — v-icon color prop veya hex inline style. */
export function catalogIconPresentation(color: string | null | undefined): {
  color?: string;
  style?: Record<string, string>;
} {
  const c = color?.trim();
  if (!c) return {};
  if (isTmStatusThemeColor(c)) return { color: c };
  if (isLegacyHexStatusColor(c)) return { style: { color: c } };
  return {};
}
