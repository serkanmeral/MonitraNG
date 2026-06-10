/**
 * op_states / op_priorities / op_work_item_types — tanımlı ikon yoksa
 * category / level / type category'ye göre varsayılan Tabler ikon adı.
 * OcBoardCatalogLabel + getTmStatusTablerIconComponent ile uyumlu.
 */

const STATE_CATEGORY_ICON: Record<string, string> = {
  open: 'CircleDotIcon',
  in_progress: 'ProgressIcon',
  on_hold: 'PlayerPauseIcon',
  closed: 'CircleCheckIcon',
  cancelled: 'CircleXIcon',
};

const PRIORITY_LEVEL_ICON: Record<number, string> = {
  1: 'AlertTriangleIcon',
  2: 'FlagIcon',
  3: 'BookmarkIcon',
  4: 'MinusIcon',
  5: 'ChevronDownIcon',
};

const TYPE_CATEGORY_ICON: Record<string, string> = {
  operational: 'PackageIcon',
  incident: 'AlertCircleIcon',
  problem: 'BugIcon',
  change: 'RefreshIcon',
  task: 'ListCheckIcon',
  service_request: 'UserCheckIcon',
};

export function defaultIconForStateCategory(category: string | null | undefined): string | null {
  const key = (category ?? '').trim().toLowerCase();
  return STATE_CATEGORY_ICON[key] ?? 'CircleDotIcon';
}

export function defaultIconForPriorityLevel(level: number | string | null | undefined): string | null {
  const n =
    typeof level === 'number'
      ? level
      : level != null && String(level).trim() !== ''
        ? Number(String(level).trim())
        : NaN;
  if (Number.isFinite(n)) {
    const rounded = Math.max(1, Math.min(5, Math.round(n)));
    return PRIORITY_LEVEL_ICON[rounded] ?? 'BookmarkIcon';
  }
  return 'BookmarkIcon';
}

export function defaultIconForTypeCategory(category: string | null | undefined): string | null {
  const key = (category ?? '').trim().toLowerCase();
  return TYPE_CATEGORY_ICON[key] ?? 'ListCheckIcon';
}
