<script setup lang="ts">
import { ref, computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcWorkspaceDefinitionsTypesTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsTypesTab.vue';
import OcWorkspaceDefinitionsStatesTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsStatesTab.vue';
import OcWorkspaceDefinitionsPrioritiesTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsPrioritiesTab.vue';
import OcWorkspaceDefinitionsFieldsTab from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceDefinitionsFieldsTab.vue';
import {
  OC_WORKSPACE_DEFINITION_VALUES_TAB_KEYS,
  type OcWorkspaceDefinitionValuesTabKey,
  useOcWorkspaceDefinitionValuesTabs,
} from '@/composables/useOcWorkspaceDefinitionValuesTabs';

const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();
const route = useRoute();
const router = useRouter();

const activeValuesTab = ref<OcWorkspaceDefinitionValuesTabKey>('types');
const { valuesTabModel } = useOcWorkspaceDefinitionValuesTabs(route, router, activeValuesTab);

const subTabItems = computed(() =>
  OC_WORKSPACE_DEFINITION_VALUES_TAB_KEYS.map((key) => ({
    key,
    label: t(`operationCore.workspaceDefinitions.valuesTabs.${key}`),
    icon:
      key === 'types'
        ? 'mdi-shape-outline'
        : key === 'states'
          ? 'mdi-list-status'
          : key === 'priorities'
            ? 'mdi-flag-outline'
            : 'mdi-form-textbox',
  }))
);
</script>

<template>
  <div class="oc-ws-values-tab">
    <div class="pa-4 pa-md-6 pb-2">
      <h3 class="text-subtitle-1 font-weight-medium mb-1">
        {{ t('operationCore.workspaceDefinitions.values.title') }}
      </h3>
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('operationCore.workspaceDefinitions.values.subtitle') }}
      </p>
    </div>

    <v-tabs
      v-model="valuesTabModel"
      color="primary"
      density="comfortable"
      class="oc-ws-values-tab__subtabs px-2"
      show-arrows
    >
      <v-tab
        v-for="sub in subTabItems"
        :key="sub.key"
        :value="sub.key"
        class="text-none"
      >
        <v-icon :icon="sub.icon" start size="18" />
        {{ sub.label }}
      </v-tab>
    </v-tabs>
    <v-divider />

    <v-tabs-window v-model="valuesTabModel">
      <v-tabs-window-item
        v-for="sub in subTabItems"
        :key="sub.key"
        :value="sub.key"
        class="oc-ws-values-tab__panel"
      >
        <OcWorkspaceDefinitionsTypesTab
          v-if="sub.key === 'types'"
          :workspace-id="workspaceId"
        />
        <OcWorkspaceDefinitionsStatesTab
          v-else-if="sub.key === 'states'"
          :workspace-id="workspaceId"
        />
        <OcWorkspaceDefinitionsPrioritiesTab
          v-else-if="sub.key === 'priorities'"
          :workspace-id="workspaceId"
        />
        <OcWorkspaceDefinitionsFieldsTab
          v-else-if="sub.key === 'fields'"
          :workspace-id="workspaceId"
        />
      </v-tabs-window-item>
    </v-tabs-window>
  </div>
</template>

<style scoped>
.oc-ws-values-tab__subtabs :deep(.v-tab) {
  min-width: auto;
}

.oc-ws-values-tab__panel :deep(.oc-ws-types-tab),
.oc-ws-values-tab__panel :deep(.oc-ws-states-tab),
.oc-ws-values-tab__panel :deep(.oc-ws-priorities-tab),
.oc-ws-values-tab__panel :deep(.oc-ws-fields-tab) {
  padding-top: 0;
}
</style>
