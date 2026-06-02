import { computed, watch, type Ref } from 'vue';
import type { RouteLocationNormalizedLoaded, Router } from 'vue-router';

export const OC_WORKSPACE_DEFINITION_VALUES_TAB_KEYS = [
  'types',
  'states',
  'priorities',
  'fields',
] as const;

export type OcWorkspaceDefinitionValuesTabKey =
  (typeof OC_WORKSPACE_DEFINITION_VALUES_TAB_KEYS)[number];

export function isOcWorkspaceDefinitionValuesTabKey(
  value: unknown
): value is OcWorkspaceDefinitionValuesTabKey {
  return (
    typeof value === 'string' &&
    (OC_WORKSPACE_DEFINITION_VALUES_TAB_KEYS as readonly string[]).includes(value)
  );
}

export function legacyMainTabToValuesSubTab(
  mainTab: string
): OcWorkspaceDefinitionValuesTabKey | null {
  if (isOcWorkspaceDefinitionValuesTabKey(mainTab)) return mainTab;
  return null;
}

export function useOcWorkspaceDefinitionValuesTabs(
  route: RouteLocationNormalizedLoaded,
  router: Router,
  activeValuesTab: Ref<OcWorkspaceDefinitionValuesTabKey>
) {
  function syncFromRoute() {
    const q = route.query.valuesTab;
    if (isOcWorkspaceDefinitionValuesTabKey(q)) {
      activeValuesTab.value = q;
    } else {
      activeValuesTab.value = 'types';
    }
  }

  function setValuesTab(tab: OcWorkspaceDefinitionValuesTabKey) {
    activeValuesTab.value = tab;
    router.replace({
      path: route.path,
      query: { ...route.query, tab: 'values', valuesTab: tab },
    });
  }

  watch(
    () => route.query.valuesTab,
    () => syncFromRoute(),
    { immediate: true }
  );

  const valuesTabModel = computed({
    get: () => activeValuesTab.value,
    set: (value: unknown) => {
      if (isOcWorkspaceDefinitionValuesTabKey(value)) setValuesTab(value);
    },
  });

  return { syncFromRoute, setValuesTab, valuesTabModel };
}
