import type { SiemDashboardLayout } from '@/composables/useSiemDashboardLayout';

/** @dashboards layout.meta — SIEM Güvenlik Paneli (D3) */
export interface SiemCenterDashboardLayoutMeta {
  surfaceKind: 'siem-center';
  siemPanel?: Partial<SiemDashboardLayout>;
  templateSlots?: Record<string, string>;
}

export function isSiemCenterDashboardMeta(
  meta: unknown,
): meta is SiemCenterDashboardLayoutMeta {
  return (
    typeof meta === 'object' &&
    meta !== null &&
    (meta as SiemCenterDashboardLayoutMeta).surfaceKind === 'siem-center'
  );
}
