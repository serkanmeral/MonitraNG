import type { SurfaceContext } from '@/types/apps/widgetManifest';
import { durationPresetToHours } from '@/utils/widgets/widgetManifestAdapter';

export type SurfaceTimePreset = '1h' | '6h' | '24h' | '7d';

export const SURFACE_TIME_PRESETS: SurfaceTimePreset[] = ['1h', '6h', '24h', '7d'];

export function surfacePresetToTimeRange(preset: SurfaceTimePreset | string): NonNullable<SurfaceContext['timeRange']> {
  const hours = durationPresetToHours(preset);
  const to = new Date();
  const from = new Date(to.getTime() - hours * 60 * 60 * 1000);
  return {
    preset,
    hours,
    from: from.toISOString(),
    to: to.toISOString(),
  };
}
