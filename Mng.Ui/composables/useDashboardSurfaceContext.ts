import { ref, computed, watch } from 'vue';
import type { SurfaceContext } from '@/types/apps/widgetManifest';
import { surfacePresetToTimeRange, type SurfaceTimePreset } from '@/utils/widgets/surfaceTimeRange';
import type { DashboardSurfaceMutations } from '@/utils/widgets/dashboardSurfaceMutations';

export function useDashboardSurfaceContext(initialPreset: SurfaceTimePreset = '24h') {
  const timePreset = ref<SurfaceTimePreset>(initialPreset);
  const severity = ref<number | null>(null);
  const workspaceId = ref('');
  const customTimeRange = ref<{ from: string; to: string } | null>(null);
  const crossFilterVariables = ref<Record<string, string | number | boolean | null>>({});

  const context = computed<SurfaceContext>(() => {
    const baseTime = customTimeRange.value
      ? {
          from: customTimeRange.value.from,
          to: customTimeRange.value.to,
          hours: undefined,
          preset: undefined,
        }
      : surfacePresetToTimeRange(timePreset.value);

    return {
      locale: 'tr',
      timeRange: baseTime,
      variables: {
        ...(severity.value != null ? { severity: severity.value } : {}),
        ...(workspaceId.value.trim() ? { workspaceId: workspaceId.value.trim() } : {}),
        ...crossFilterVariables.value,
      },
    };
  });

  watch(timePreset, () => {
    customTimeRange.value = null;
  });

  function setTimeRangeFromZoom(from: string, to: string) {
    customTimeRange.value = { from, to };
  }

  function setCrossFilterVariable(name: string, value: string | number | boolean | null) {
    crossFilterVariables.value = { ...crossFilterVariables.value, [name]: value };
  }

  function clearCrossFilterVariable(name: string) {
    const next = { ...crossFilterVariables.value };
    delete next[name];
    crossFilterVariables.value = next;
  }

  function reset() {
    timePreset.value = initialPreset;
    severity.value = null;
    workspaceId.value = '';
    customTimeRange.value = null;
    crossFilterVariables.value = {};
  }

  const mutations: DashboardSurfaceMutations = {
    setTimeRangeFromZoom,
    setCrossFilterVariable,
    clearCrossFilterVariable,
    hasCustomTimeRange: () => customTimeRange.value != null,
  };

  return {
    timePreset,
    severity,
    workspaceId,
    customTimeRange,
    crossFilterVariables,
    context,
    mutations,
    reset,
  };
}
