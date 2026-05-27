import { computed, watch, type Ref } from 'vue';
import type { RouteLocationNormalizedLoaded, Router } from 'vue-router';

export const OC_WORKSPACE_DEFINITION_TAB_KEYS = [
  'general',
  'types',
  'fields',
  'flows',
  'forms',
  'boards',
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
    if (isOcWorkspaceDefinitionTabKey(q)) {
      activeTab.value = q;
    } else {
      activeTab.value = 'general';
    }
  }

  function setTab(tab: OcWorkspaceDefinitionTabKey) {
    activeTab.value = tab;
    router.replace({
      path: route.path,
      query: { ...route.query, tab },
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
