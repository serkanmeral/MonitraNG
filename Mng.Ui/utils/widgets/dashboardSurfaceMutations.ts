import type { InjectionKey } from 'vue';
import type { SurfaceContext } from '@/types/apps/widgetManifest';

export interface DashboardSurfaceMutations {
  setTimeRangeFromZoom: (from: string, to: string) => void;
  setCrossFilterVariable: (name: string, value: string | number | boolean | null) => void;
  clearCrossFilterVariable: (name: string) => void;
  /** Özel zoom aralığı aktif mi */
  readonly hasCustomTimeRange: () => boolean;
}

export const DASHBOARD_SURFACE_MUTATIONS_KEY: InjectionKey<DashboardSurfaceMutations> = Symbol(
  'dashboardSurfaceMutations',
);
