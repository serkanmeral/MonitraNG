import { computed, watch, type Ref } from 'vue';
import type { RouteLocationNormalizedLoaded, Router } from 'vue-router';

export const OC_DEFINITION_TAB_KEYS = ['states', 'priorities', 'types', 'fields'] as const;
export type OcDefinitionTabKey = (typeof OC_DEFINITION_TAB_KEYS)[number];

export function isOcDefinitionTabKey(value: unknown): value is OcDefinitionTabKey {
  return typeof value === 'string' && (OC_DEFINITION_TAB_KEYS as readonly string[]).includes(value);
}

export function useOcDefinitionTabs(route: RouteLocationNormalizedLoaded, router: Router, activeTab: Ref<OcDefinitionTabKey>) {
  function syncFromRoute() {
    const q = route.query.tab;
    if (isOcDefinitionTabKey(q)) {
      activeTab.value = q;
    } else {
      activeTab.value = 'states';
    }
  }

  function setTab(tab: OcDefinitionTabKey) {
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
    get: () => OC_DEFINITION_TAB_KEYS.indexOf(activeTab.value),
    set: (idx: number) => {
      const key = OC_DEFINITION_TAB_KEYS[idx];
      if (key) setTab(key);
    },
  });

  return { syncFromRoute, setTab, tabIndex };
}
