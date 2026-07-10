<script setup lang="ts">
import { computed, ref } from 'vue';
import OcWorkspaceFormLayoutEditor from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceFormLayoutEditor.vue';
import ReportingExpandChildTabsPanel from '@/components/apps/reporting/ReportingExpandChildTabsPanel.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { FieldDefinition } from '@/stores/apps/dataset';
import type { ReportingExpandConfig } from '@/types/apps/reporting';
import type { OpFormLayoutSection } from '@/types/apps/operationCore';
import { reportingLayoutFieldItems } from '@/utils/reportingExpandLayout';

const props = defineProps<{
  expandConfig: ReportingExpandConfig;
  fields: FieldDefinition[];
  disabled?: boolean;
  domainKey?: string;
}>();

const emit = defineEmits<{ reset: [] }>();

const { t } = useAppI18n();

const innerTab = ref<'settings' | 'tabs'>('settings');

const layoutFieldItems = computed(() => reportingLayoutFieldItems(props.fields));

function updateSections(sections: OpFormLayoutSection[]) {
  props.expandConfig.sections = sections;
}

function updateFieldCols(fieldCols: Record<string, number>) {
  props.expandConfig.fieldCols = fieldCols;
}

function resetDefaults() {
  emit('reset');
}
</script>

<template>
  <div>
    <v-tabs v-model="innerTab" density="compact" color="primary" class="mb-3">
      <v-tab value="settings">{{ t('reporting.expand.designerTabs.settings') }}</v-tab>
      <v-tab value="tabs">{{ t('reporting.expand.designerTabs.tabs') }}</v-tab>
    </v-tabs>

    <v-window v-model="innerTab">
      <v-window-item value="settings">
        <v-alert type="info" variant="tonal" density="compact" class="mb-4">
          {{ t('reporting.expand.hint') }}
        </v-alert>

        <v-switch
          v-model="expandConfig.enabled"
          :disabled="disabled"
          :label="t('reporting.expand.enabled')"
          color="primary"
          hide-details
          class="mb-3"
        />

        <v-switch
          v-model="expandConfig.hideEmptyFields"
          :disabled="disabled || !expandConfig.enabled"
          :label="t('reporting.expand.hideEmptyFields')"
          color="primary"
          hide-details
          class="mb-4"
        />

        <v-text-field
          v-model="expandConfig.heading"
          :label="t('reporting.expand.heading')"
          :disabled="disabled || !expandConfig.enabled"
          density="compact"
          variant="outlined"
          hide-details
          class="mb-3"
        />

        <v-textarea
          v-model="expandConfig.intro"
          :label="t('reporting.expand.intro')"
          :disabled="disabled || !expandConfig.enabled"
          density="compact"
          variant="outlined"
          rows="2"
          hide-details
          class="mb-4"
        />

        <div class="d-flex justify-end mb-3">
          <v-btn
            size="small"
            variant="text"
            :disabled="disabled"
            @click="resetDefaults"
          >
            {{ t('reporting.expand.resetLayout') }}
          </v-btn>
        </div>

        <OcWorkspaceFormLayoutEditor
          v-if="expandConfig.enabled && fields.length"
          :sections="expandConfig.sections"
          :field-cols="expandConfig.fieldCols"
          :layout-field-items="layoutFieldItems"
          @update:sections="updateSections"
          @update:field-cols="updateFieldCols"
        />

        <v-alert
          v-else-if="!disabled && expandConfig.enabled && !fields.length"
          type="warning"
          variant="tonal"
          density="compact"
        >
          {{ t('reporting.expand.noSchema') }}
        </v-alert>
      </v-window-item>

      <v-window-item value="tabs">
        <ReportingExpandChildTabsPanel
          :expand-config="expandConfig"
          :disabled="disabled"
          :domain-key="domainKey"
        />
      </v-window-item>
    </v-window>
  </div>
</template>
