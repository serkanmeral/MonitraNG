import { computed, type ComputedRef } from 'vue';
import { useAuthStore } from '@/stores/auth';
import type { SurfaceContext } from '@/types/apps/widgetManifest';
import { durationPresetToHours } from '@/utils/widgets/widgetManifestAdapter';

export function useWidgetSurfaceContext(
  overrides?: SurfaceContext | ComputedRef<SurfaceContext | undefined>,
): ComputedRef<SurfaceContext> {
  const auth = useAuthStore();
  return computed(() => {
    const base = overrides && 'value' in overrides ? overrides.value : overrides;
    const preset = base?.timeRange?.preset ?? '24h';
    return {
      locale: base?.locale ?? 'tr',
      timeRange: {
        preset,
        hours: base?.timeRange?.hours ?? durationPresetToHours(preset),
        from: base?.timeRange?.from,
        to: base?.timeRange?.to,
      },
      variables: {
        currentUserId: auth.userInfo?.mng_person_id ?? auth.userInfo?.sub,
        ...base?.variables,
      },
    };
  });
}
