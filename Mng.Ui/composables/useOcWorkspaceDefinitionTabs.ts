import { computed, watch, type Ref } from 'vue';
import type { RouteLocationNormalizedLoaded, Router } from 'vue-router';
import {
  isOcWorkspaceDefinitionValuesTabKey,
  legacyMainTabToValuesSubTab,
} from '@/composables/useOcWorkspaceDefinitionValuesTabs';

export const OC_WORKSPACE_DEFINITION_TAB_KEYS = [
  'general',
  'values',
  'tags',
  'flows',
  'forms',
  'boards',
  'policies',
  'rules',
  'scheduled',
  'sla',
] as const;
export type OcWorkspaceDefinitionTabKey = (typeof OC_WORKSPACE_DEFINITION_TAB_KEYS)[number];

export function isOcWorkspaceDefinitionTabKey(
  value: unknown
): value is OcWorkspaceDefinitionTabKey {
  return (
    typeof value === 'string' &&
    (OC_WORKSPACE_DEFINITION_TAB_KEYS as readonly string[]).includes(value)
  );
}

export function useOcWorkspaceDefinitionTabs(
  route: RouteLocationNormalizedLoaded,
  router: Router,
  activeTab: Ref<OcWorkspaceDefinitionTabKey>
) {
  function syncFromRoute() {
    const q = route.query.tab;
    const legacySub = typeof q === 'string' ? legacyMainTabToValuesSubTab(q) : null;
    if (legacySub) {
      activeTab.value = 'values';
      if (!isOcWorkspaceDefinitionValuesTabKey(route.query.valuesTab)) {
        router.replace({
          path: route.path,
          query: { ...route.query, tab: 'values', valuesTab: legacySub },
        });
      }
      return;
    }
    if (isOcWorkspaceDefinitionTabKey(q)) {
      activeTab.value = q;
    } else {
      activeTab.value = 'general';
    }
  }

  function setTab(tab: OcWorkspaceDefinitionTabKey) {
    activeTab.value = tab;
    const query: Record<string, unknown> = { ...route.query, tab };
    if (tab !== 'values') {
      delete query.valuesTab;
    } else if (!isOcWorkspaceDefinitionValuesTabKey(query.valuesTab)) {
      query.valuesTab = 'types';
    }
    router.replace({
      path: route.path,
      query,
    });
  }

  watch(
    () => route.query.tab,
    () => syncFromRoute(),
    { immediate: true }
  );

  const tabIndex = computed({
    get: () => OC_WORKSPACE_DEFINITION_TAB_KEYS.indexOf(activeTab.value),
    set: (idx: number) => {
      const key = OC_WORKSPACE_DEFINITION_TAB_KEYS[idx];
      if (key) setTab(key);
    },
  });

  return { syncFromRoute, setTab, tabIndex };
}
