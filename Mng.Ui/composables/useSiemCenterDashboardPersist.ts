import { ref } from 'vue';
import { useDashboardStore } from '@/stores/apps/dashboard';
import {
  SIEM_CENTER_DASHBOARD_SLUG,
  SIEM_CENTER_TEMPLATE_MAP,
} from '@/utils/widgets/siemCenterWidgets';
import {
  normalizeSiemDashboardLayout,
  saveSiemDashboardLayout,
  type SiemDashboardLayout,
} from '@/composables/useSiemDashboardLayout';
import { isSiemCenterDashboardMeta } from '@/types/apps/siemDashboardSurface';

export function useSiemCenterDashboardPersist() {
  const dashboardStore = useDashboardStore();
  const dashboardId = ref<string | null>(null);
  const loadedFromServer = ref(false);

  async function loadServerLayout(): Promise<SiemDashboardLayout | null> {
    try {
      const dashboard = await dashboardStore.fetchDashboardBySlug(SIEM_CENTER_DASHBOARD_SLUG);
      dashboardId.value = dashboard.__dataId ?? dashboard.dataId ?? null;
      const meta = dashboard.layout?.meta;
      if (isSiemCenterDashboardMeta(meta) && meta.siemPanel) {
        loadedFromServer.value = true;
        return normalizeSiemDashboardLayout(meta.siemPanel);
      }
    } catch {
      loadedFromServer.value = false;
    }
    return null;
  }

  async function saveServerLayout(layout: SiemDashboardLayout): Promise<boolean> {
    const id = dashboardId.value;
    if (!id) return false;

    try {
      const current = dashboardStore.currentDashboard;
      const baseLayout = current?.layout ?? { type: 'rows' as const, rows: [] };
      await dashboardStore.updateDashboard(id, {
        layout: {
          ...baseLayout,
          meta: {
            surfaceKind: 'siem-center',
            siemPanel: layout,
            templateSlots: { ...SIEM_CENTER_TEMPLATE_MAP },
          },
        },
      });
      saveSiemDashboardLayout(layout);
      return true;
    } catch {
      return false;
    }
  }

  return {
    dashboardId,
    loadedFromServer,
    loadServerLayout,
    saveServerLayout,
  };
}
