<script setup lang="ts">
import { computed } from 'vue';
import OcWorkspaceFormLayoutEditor from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceFormLayoutEditor.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { FieldDefinition } from '@/stores/apps/dataset';
import type { ReportingExpandConfig } from '@/types/apps/reporting';
import type { OpFormLayoutSection } from '@/types/apps/operationCore';
import {
  defaultReportingExpandConfigFromFields,
  reportingExpandChildTabs,
  reportingLayoutFieldItems,
} from '@/utils/reportingExpandLayout';

const props = defineProps<{
  expandConfig: ReportingExpandConfig;
  fields: FieldDefinition[];
  disabled?: boolean;
}>();

const emit = defineEmits<{ reset: [] }>();

const { t } = useAppI18n();

const layoutFieldItems = computed(() => reportingLayoutFieldItems(props.fields));

const childTabs = computed(() => reportingExpandChildTabs(props.expandConfig));

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

    <v-alert
      v-if="expandConfig.enabled && childTabs.length"
      type="info"
      variant="tonal"
      density="compact"
      class="mb-4"
    >
      <div class="text-subtitle-2 mb-1">{{ t('reporting.expand.childTabsTitle') }}</div>
      <p class="text-caption mb-2">{{ t('reporting.expand.childTabsHint') }}</p>
      <ul class="text-body-2 pl-4 mb-0">
        <li v-for="tab in childTabs" :key="tab.id">
          {{ tab.title }} — {{ tab.childList.datasetName }}
        </li>
      </ul>
    </v-alert>

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
  </div>
</template>
