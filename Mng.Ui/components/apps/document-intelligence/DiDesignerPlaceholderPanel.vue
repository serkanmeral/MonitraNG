<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { DiDocxPlaceholder, DiTemplateParameter } from '@/types/apps/documentIntelligence';
import {
  isPlaceholderDefined,
  missingPlaceholderDefinitions,
  undefinedDocPlaceholders,
} from '@/utils/diDesignerPlaceholders';

const props = defineProps<{
  placeholders: DiDocxPlaceholder[];
  warnings: string[];
  parameters: DiTemplateParameter[];
  loading?: boolean;
  fileName?: string | null;
}>();

const emit = defineEmits<{
  importAll: [];
  select: [key: string];
}>();

const { t } = useAppI18n();

const missingCount = computed(
  () => missingPlaceholderDefinitions(props.placeholders, props.parameters).length
);

const extraCount = computed(
  () => undefinedDocPlaceholders(props.placeholders, props.parameters).length
);
</script>

<template>
  <v-card variant="outlined" rounded="lg" min-height="420">
    <v-card-title class="text-subtitle-2 font-weight-bold d-flex align-center justify-space-between flex-wrap ga-2">
      <span>{{ t('documentIntelligence.designer.placeholderInventory') }}</span>
      <v-chip v-if="fileName" size="x-small" variant="tonal">{{ fileName }}</v-chip>
    </v-card-title>
    <v-divider />
    <div class="px-4 py-2 text-caption text-medium-emphasis border-b">
      {{ t('documentIntelligence.designer.placeholderHint') }}
    </div>
    <v-card-text class="pa-0">
      <v-progress-linear v-if="loading" indeterminate color="primary" />
      <template v-else>
        <v-alert
          v-for="(warn, wi) in warnings"
          :key="wi"
          type="warning"
          variant="tonal"
          density="compact"
          class="ma-3 mb-0 text-caption"
        >
          {{ warn }}
        </v-alert>

        <div v-if="!placeholders.length" class="pa-6 text-body-2 text-medium-emphasis text-center">
          {{ t('documentIntelligence.designer.noPlaceholders') }}
        </div>

        <v-list v-else density="compact" class="py-0 di-ph-list">
          <v-list-item
            v-for="ph in placeholders"
            :key="ph.key"
            class="di-ph-item"
            @click="emit('select', ph.key)"
          >
            <template #prepend>
              <v-icon
                :icon="isPlaceholderDefined(ph.key, parameters) ? 'mdi-check-circle-outline' : 'mdi-alert-circle-outline'"
                :color="isPlaceholderDefined(ph.key, parameters) ? 'success' : 'warning'"
                size="20"
              />
            </template>
            <v-list-item-title class="text-body-2 font-weight-medium">
              <code>{{ ph.token }}</code>
            </v-list-item-title>
            <v-list-item-subtitle class="text-caption">
              {{ t('documentIntelligence.designer.placeholderOccurrences', { count: ph.occurrenceCount }) }}
            </v-list-item-subtitle>
            <template #append>
              <v-chip
                size="x-small"
                :color="isPlaceholderDefined(ph.key, parameters) ? 'success' : 'warning'"
                variant="tonal"
              >
                {{
                  isPlaceholderDefined(ph.key, parameters)
                    ? t('documentIntelligence.designer.placeholderDefined')
                    : t('documentIntelligence.designer.placeholderMissing')
                }}
              </v-chip>
            </template>
          </v-list-item>
        </v-list>

        <div v-if="placeholders.length" class="pa-3 border-t d-flex flex-wrap ga-2 align-center">
          <v-chip v-if="missingCount" size="small" color="warning" variant="tonal">
            {{ t('documentIntelligence.designer.placeholderMissingCount', { count: missingCount }) }}
          </v-chip>
          <v-chip v-if="extraCount" size="small" color="info" variant="tonal">
            {{ t('documentIntelligence.designer.placeholderExtraCount', { count: extraCount }) }}
          </v-chip>
          <v-spacer />
          <v-btn
            size="small"
            color="primary"
            variant="flat"
            class="text-none"
            :disabled="missingCount === 0"
            @click="emit('importAll')"
          >
            {{ t('documentIntelligence.designer.importPlaceholders') }}
          </v-btn>
        </div>
      </template>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.border-b {
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}

.border-t {
  border-top: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}

.di-ph-list {
  max-height: 420px;
  overflow: auto;
}

.di-ph-item {
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  cursor: pointer;
}

.di-ph-item:hover {
  background: rgba(var(--v-theme-primary), 0.04);
}
</style>
