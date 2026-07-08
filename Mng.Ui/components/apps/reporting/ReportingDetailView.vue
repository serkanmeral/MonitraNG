<script setup lang="ts">
import { computed } from 'vue';
import type { FieldDefinition } from '@/stores/apps/dataset';
import type { OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import {
  applyListColumnFormatting,
  getListColumnCellStyle,
} from '@/utils/afListColumnFormat';
import {
  fieldColSpanForLayout,
  sectionColSpanForLayout,
  type ParsedOpFormLayout,
} from '@/utils/ocFormLayout';
import {
  columnConfigByField,
  isReportingBoolField,
  parseReportingBoolValue,
  reportingCellRaw,
  reportingFieldLabel,
} from '@/utils/reportingListConfig';
import { useAppI18n } from '@/composables/useAppI18n';

const props = defineProps<{
  layout: ParsedOpFormLayout;
  fields: FieldDefinition[];
  row: Record<string, unknown>;
  listConfig: OdakHubListConfig;
  hideEmptyFields?: boolean;
  canViewField?: (fieldName: string, row: Record<string, unknown>) => boolean;
}>();

const { t } = useAppI18n();

const fieldMap = computed(() => new Map(props.fields.map((f) => [f.name, f])));

const sections = computed(() => {
  const base = props.layout.sections
    .map((section) => ({
      ...section,
      fields: section.fields.filter((key) => isFieldAllowed(key)),
    }))
    .filter((s) => s.fields.length > 0);
  if (!props.hideEmptyFields) return base;
  return base
    .map((section) => ({
      ...section,
      fields: section.fields.filter((key) => fieldHasContent(key)),
    }))
    .filter((s) => s.fields.length > 0);
});

function isFieldAllowed(fieldName: string): boolean {
  return !props.canViewField || props.canViewField(fieldName, props.row);
}

const heading = computed(() => props.layout.formHeading?.trim() ?? '');
const intro = computed(() => props.layout.formIntro?.trim() ?? '');

function fieldLabel(fieldName: string): string {
  return reportingFieldLabel(fieldMap.value.get(fieldName), fieldName);
}

function fieldHasContent(fieldName: string): boolean {
  const raw = reportingCellRaw(props.row, fieldName);
  if (isReportingBoolField(props.fields, fieldName)) {
    return parseReportingBoolValue(props.row[fieldName]) !== null;
  }
  return raw.trim().length > 0;
}

function cellDisplay(fieldName: string): string {
  const raw = reportingCellRaw(props.row, fieldName);
  const col = columnConfigByField(props.listConfig, fieldName);
  return applyListColumnFormatting(raw, col?.format);
}

function cellStyle(fieldName: string): Record<string, string> {
  const raw = reportingCellRaw(props.row, fieldName);
  const col = columnConfigByField(props.listConfig, fieldName);
  return getListColumnCellStyle(raw, fieldName, col?.format, props.row);
}

function boolValue(fieldName: string): boolean | null {
  return parseReportingBoolValue(props.row[fieldName]);
}

function sectionSpan(sectionKey: string, section: { cols?: number }): number {
  return sectionColSpanForLayout(sectionKey, props.layout.sectionCols, section);
}

function fieldMdCols(fieldKey: string): number {
  return fieldColSpanForLayout(fieldKey, props.layout.fieldCols);
}
</script>

<template>
  <div class="reporting-detail-view">
    <header v-if="heading || intro" class="mb-4">
      <h4 v-if="heading" class="text-subtitle-1 font-weight-semibold mb-1">
        {{ heading }}
      </h4>
      <p v-if="intro" class="text-body-2 text-medium-emphasis mb-0">
        {{ intro }}
      </p>
    </header>

    <div v-if="!sections.length" class="text-body-2 text-medium-emphasis py-4 text-center">
      {{ t('reporting.expand.noFields') }}
    </div>

    <div v-else class="reporting-detail-view__sections">
      <section
        v-for="section in sections"
        :key="section.key"
        class="reporting-detail-view__section"
        :style="{ gridColumn: `span ${sectionSpan(section.key, section)}` }"
      >
        <v-card variant="outlined" rounded="lg" class="h-100">
          <v-card-text class="pa-4">
            <div v-if="section.title" class="d-flex align-center ga-2 mb-3">
              <span class="reporting-detail-view__accent" aria-hidden="true" />
              <span class="text-subtitle-2 font-weight-semibold">{{ section.title }}</span>
            </div>
            <v-row dense>
              <v-col
                v-for="fieldKey in section.fields"
                :key="fieldKey"
                cols="12"
                :md="fieldMdCols(fieldKey)"
              >
                <div class="reporting-detail-view__field mb-2">
                  <div class="text-caption text-medium-emphasis mb-1">
                    {{ fieldLabel(fieldKey) }}
                  </div>
                  <div
                    v-if="isReportingBoolField(fields, fieldKey)"
                    class="d-flex align-center"
                  >
                    <v-icon
                      v-if="boolValue(fieldKey) === true"
                      icon="mdi-check-circle"
                      color="success"
                      size="20"
                      :title="t('reporting.bool.true')"
                    />
                    <v-icon
                      v-else-if="boolValue(fieldKey) === false"
                      icon="mdi-close-circle-outline"
                      color="error"
                      size="20"
                      :title="t('reporting.bool.false')"
                    />
                    <span v-else class="text-body-2 text-medium-emphasis">—</span>
                  </div>
                  <div v-else class="text-body-2" :style="cellStyle(fieldKey)">
                    {{ cellDisplay(fieldKey) || '—' }}
                  </div>
                </div>
              </v-col>
            </v-row>
          </v-card-text>
        </v-card>
      </section>
    </div>
  </div>
</template>

<style scoped>
.reporting-detail-view__sections {
  display: grid;
  grid-template-columns: repeat(12, minmax(0, 1fr));
  gap: 16px;
  align-items: start;
}

.reporting-detail-view__section {
  min-width: 0;
}

.reporting-detail-view__accent {
  width: 4px;
  height: 1.1rem;
  border-radius: 4px;
  background: rgb(var(--v-theme-primary));
  flex-shrink: 0;
}

.font-weight-semibold {
  font-weight: 600;
}
</style>
