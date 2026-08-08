import type { ScenarioCatalogItem, ScenarioHealthLevel, ScenarioOperationalStatus } from '@/types/apps/scenario';

/** Combined catalog/header chip: version axis + listening axis for published flows. */
export type LifecycleChipKind = 'draft' | 'publishedOn' | 'publishedOff' | 'archived';

export function resolveOperationalStatus(item: Pick<
  ScenarioCatalogItem,
  'publishedVersion' | 'enabled' | 'latestStatus'
>): ScenarioOperationalStatus {
  if (item.publishedVersion != null) {
    return item.enabled ? 'running' : 'stopped';
  }
  if (item.latestStatus === 'archived') return 'archived';
  return 'draft';
}

export function resolveLifecycleChip(item: Pick<
  ScenarioCatalogItem,
  'publishedVersion' | 'enabled' | 'latestStatus'
>): LifecycleChipKind {
  if (item.publishedVersion != null) {
    return item.enabled ? 'publishedOn' : 'publishedOff';
  }
  if (item.latestStatus === 'archived') return 'archived';
  return 'draft';
}

export function lifecycleChipFromVersion(
  status: string | undefined,
  enabled: boolean | undefined,
): LifecycleChipKind {
  if (status === 'published') return enabled ? 'publishedOn' : 'publishedOff';
  if (status === 'archived') return 'archived';
  return 'draft';
}

export function lifecycleChipColor(kind: LifecycleChipKind): string {
  switch (kind) {
    case 'publishedOn': return 'success';
    case 'publishedOff': return 'warning';
    case 'archived': return 'default';
    default: return 'info';
  }
}

export function operationalStatusColor(status: ScenarioOperationalStatus): string {
  switch (status) {
    case 'running': return 'success';
    case 'stopped': return 'warning';
    case 'archived': return 'default';
    default: return 'info';
  }
}

export function healthColor(health: ScenarioHealthLevel | string | undefined): string {
  switch (health) {
    case 'healthy': return 'success';
    case 'warning': return 'warning';
    case 'error': return 'error';
    default: return 'default';
  }
}

export function showHealthBadge(health: ScenarioHealthLevel | string | undefined): boolean {
  return health === 'warning' || health === 'error';
}
