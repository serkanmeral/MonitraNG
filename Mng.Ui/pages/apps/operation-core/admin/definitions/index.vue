<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OcDefinitionsStatesTab from '@/components/apps/operation-core/definitions/OcDefinitionsStatesTab.vue';
import OcDefinitionsPrioritiesTab from '@/components/apps/operation-core/definitions/OcDefinitionsPrioritiesTab.vue';
import OcDefinitionsTypesTab from '@/components/apps/operation-core/definitions/OcDefinitionsTypesTab.vue';
import OcDefinitionsFieldsTab from '@/components/apps/operation-core/definitions/OcDefinitionsFieldsTab.vue';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import {
  OC_DEFINITION_TAB_KEYS,
  type OcDefinitionTabKey,
  useOcDefinitionTabs,
} from '@/composables/useOcDefinitionTabs';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const route = useRoute();
const router = useRouter();
const auth = useAuthStore();

const activeTab = ref<OcDefinitionTabKey>('states');
const { tabIndex } = useOcDefinitionTabs(route, router, activeTab);

const { breadcrumbs } = useOperationCoreBreadcrumbs({
  tail: computed(() => ({
    text: t('operationCore.definitions.systemMenuTitle'),
    disabled: true,
  })),
});

const tabItems = computed(() =>
  OC_DEFINITION_TAB_KEYS.map((key) => ({
    key,
    label: t(`operationCore.definitions.tabs.${key}`),
    icon:
      key === 'states'
        ? 'mdi-circle-outline'
        : key === 'priorities'
          ? 'mdi-flag-outline'
          : key === 'types'
            ? 'mdi-shape-outline'
            : 'mdi-form-textbox',
  }))
);

onMounted(() => {
  if (!auth.isManager) {
    void navigateTo('/unauthorized');
  }
});
</script>

<template>
  <div class="oc-flow oc-definitions-page">
    <BaseBreadcrumb
      :title="t('operationCore.definitions.pageTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <div class="oc-hero mb-4 pa-4 pa-md-5 rounded-lg">
      <h1 class="text-h5 font-weight-bold mb-2">
        {{ t('operationCore.definitions.pageTitle') }}
      </h1>
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('operationCore.definitions.pageSubtitle') }}
      </p>
    </div>

    <v-card variant="outlined" class="rounded-lg">
      <v-tabs v-model="tabIndex" color="primary" class="px-2 pt-2" show-arrows>
        <v-tab
          v-for="tab in tabItems"
          :key="tab.key"
          :value="OC_DEFINITION_TAB_KEYS.indexOf(tab.key)"
          class="text-none"
        >
          <v-icon :icon="tab.icon" start size="20" />
          {{ tab.label }}
        </v-tab>
      </v-tabs>
      <v-divider />
      <v-tabs-window v-model="tabIndex">
        <v-tabs-window-item
          v-for="(tab, idx) in tabItems"
          :key="tab.key"
          :value="idx"
        >
          <OcDefinitionsStatesTab v-if="tab.key === 'states'" />
          <OcDefinitionsPrioritiesTab v-else-if="tab.key === 'priorities'" />
          <OcDefinitionsTypesTab v-else-if="tab.key === 'types'" />
          <OcDefinitionsFieldsTab v-else-if="tab.key === 'fields'" />
        </v-tabs-window-item>
      </v-tabs-window>
    </v-card>
  </div>
</template>
