/** @dashboards layout.meta — Odak Sipariş özel pano yüzeyleri */

export type OdakDashboardSurfaceKind = 'odak-siparis' | 'odak-musteriler';

export interface OdakSiparisDashboardLayoutMeta {
  surfaceKind: OdakDashboardSurfaceKind;
}

export function isOdakSiparisOperationsDashboardMeta(meta: unknown): boolean {
  return (
    typeof meta === 'object' &&
    meta !== null &&
    (meta as OdakSiparisDashboardLayoutMeta).surfaceKind === 'odak-siparis'
  );
}

export function isOdakMusterilerDashboardMeta(meta: unknown): boolean {
  return (
    typeof meta === 'object' &&
    meta !== null &&
    (meta as OdakSiparisDashboardLayoutMeta).surfaceKind === 'odak-musteriler'
  );
}
