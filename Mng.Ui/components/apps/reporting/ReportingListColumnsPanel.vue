<script setup lang="ts">
import { computed, ref } from 'vue';
import { VueDraggableNext } from 'vue-draggable-next';
import ReportingListColumnFormatDialog from '@/components/apps/reporting/ReportingListColumnFormatDialog.vue';
import ReportingListColumnRelationDisplayDialog from '@/components/apps/reporting/ReportingListColumnRelationDisplayDialog.vue';
import ReportingListColumnReportLinkDialog from '@/components/apps/reporting/ReportingListColumnReportLinkDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { isActiveListColumnFormat, type AfListColumnFormat } from '@/utils/afListColumnFormat';
import type { OdakHubListColumnConfig, OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import type { ReportingColumnLink } from '@/utils/reportingColumnLink';
import { reportingColumnListKey, reportingFieldLabel } from '@/utils/reportingListConfig';
import type { FieldDefinition } from '@/stores/apps/dataset';

const props = withDefaults(
  defineProps<{
    listConfig: OdakHubListConfig;
    fields: FieldDefinition[];
    disabled?: boolean;
    domainKey?: string;
    /** Child list: parentField mapping in report link dialog */
    allowParentField?: boolean;
  }>(),
  {
    domainKey: '',
    allowParentField: false,
  }
);

const { t } = useAppI18n();

const formatDialogOpen = ref(false);
const formatColumn = ref<OdakHubListColumnConfig | null>(null);
const relationDialogOpen = ref(false);
const relationColumn = ref<OdakHubListColumnConfig | null>(null);
const reportLinkDialogOpen = ref(false);
const reportLinkColumn = ref<OdakHubListColumnConfig | null>(null);

const fieldMap = computed(() => new Map(props.fields.map((f) => [f.name, f])));

function sourceFieldForColumn(col: OdakHubListColumnConfig): FieldDefinition | undefined {
  const root = col.fieldName.includes('.') ? col.fieldName.split('.')[0] : col.fieldName;
  return fieldMap.value.get(root);
}

function isRelationColumn(col: OdakHubListColumnConfig): boolean {
  return sourceFieldForColumn(col)?.fieldType === 'relation';
}

function columnLabel(col: OdakHubListColumnConfig): string {
  if (col.title?.trim()) return col.title.trim();
  return reportingFieldLabel(sourceFieldForColumn(col), col.fieldName);
}

function relationDisplaySummary(col: OdakHubListColumnConfig): string {
  if (!isRelationColumn(col)) return '';
  if (col.relationDisplayField?.trim()) {
    return t('reporting.columns.relationDisplaySummary', { field: col.relationDisplayField });
  }
  return t('reporting.columns.relationDisplayMissing');
}

const conditionFields = computed(() =>
  props.listConfig.columns.map((c) => ({
    value: reportingColumnListKey(c),
    title: columnLabel(c),
  }))
);

function formatSummary(element: OdakHubListColumnConfig): string {
  if (!isActiveListColumnFormat(element.format)) {
    return t('reporting.columns.noFormat');
  }
  const typeKey = element.format?.type ?? 'none';
  const labelKey =
    typeKey === 'text-transform'
      ? 'textTransform'
      : typeKey === 'conditional-color'
        ? 'conditionalColor'
        : typeKey;
  return t(`reporting.columns.formatTypes.${labelKey}`);
}

function openRelationDialog(element: OdakHubListColumnConfig) {
  relationColumn.value = element;
  relationDialogOpen.value = true;
}

function onRelationDisplaySave(relationDisplayField: string | undefined) {
  if (!relationColumn.value) return;
  relationColumn.value.relationDisplayField = relationDisplayField;
  if (relationDisplayField) {
    relationColumn.value.sortable = false;
  }
  relationColumn.value = null;
}

function openFormatDialog(element: OdakHubListColumnConfig) {
  formatColumn.value = element;
  formatDialogOpen.value = true;
}

function onFormatSave(format: AfListColumnFormat | undefined) {
  if (!formatColumn.value) return;
  formatColumn.value.format = format;
  formatColumn.value = null;
}

function openReportLinkDialog(element: OdakHubListColumnConfig) {
  reportLinkColumn.value = element;
  reportLinkDialogOpen.value = true;
}

function onReportLinkSave(link: ReportingColumnLink | undefined) {
  if (!reportLinkColumn.value) return;
  if (link) reportLinkColumn.value.reportLink = link;
  else delete reportLinkColumn.value.reportLink;
  reportLinkColumn.value = null;
}

function reportLinkSummary(element: OdakHubListColumnConfig): string {
  if (!element.reportLink?.targetReportId) return '';
  return t('reporting.columns.reportLinkSummary', { id: element.reportLink.targetReportId });
}

function reorderColumns() {
  props.listConfig.columns.forEach((c, idx) => {
    c.order = idx + 1;
  });
}

function resetDefaults() {
  emit('reset');
}

const emit = defineEmits<{ reset: [] }>();
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-2">
      <span class="text-subtitle-2">{{ t('reporting.columns.title') }}</span>
      <v-btn size="x-small" variant="text" :disabled="disabled" @click="resetDefaults">
        {{ t('reporting.columns.reset') }}
      </v-btn>
    </div>
    <p class="text-caption text-medium-emphasis mb-3">
      {{ t('reporting.columns.hint') }}
    </p>

    <VueDraggableNext
      v-if="listConfig.columns.length"
      :list="listConfig.columns"
      item-key="fieldName"
      handle=".drag-handle"
      class="d-flex flex-column"
      :disabled="disabled"
      @end="reorderColumns"
    >
      <v-card
        v-for="element in listConfig.columns"
        :key="reportingColumnListKey(element)"
        variant="outlined"
        class="mb-2 pa-2"
        density="compact"
      >
        <div class="d-flex flex-wrap align-center ga-2">
          <v-icon
            class="drag-handle"
            :class="disabled ? '' : 'cursor-grab'"
            icon="mdi-drag"
            size="small"
          />
          <div class="d-flex flex-column flex-grow-1" style="min-width: 120px">
            <span class="text-body-2 font-weight-medium">{{ columnLabel(element) }}</span>
            <span class="text-caption text-medium-emphasis">{{ formatSummary(element) }}</span>
            <span v-if="isRelationColumn(element)" class="text-caption" :class="element.relationDisplayField ? 'text-primary' : 'text-warning'">
              {{ relationDisplaySummary(element) }}
            </span>
            <span v-if="element.reportLink" class="text-caption text-primary">
              {{ reportLinkSummary(element) }}
            </span>
          </div>
          <v-btn
            v-if="isRelationColumn(element)"
            variant="tonal"
            size="x-small"
            :color="element.relationDisplayField ? 'primary' : 'warning'"
            :disabled="disabled"
            @click="openRelationDialog(element)"
          >
            <v-icon start size="16">mdi-link-variant</v-icon>
            {{ t('reporting.columns.relationDisplay') }}
          </v-btn>
          <v-btn
            variant="tonal"
            size="x-small"
            :color="element.reportLink ? 'primary' : undefined"
            :disabled="disabled"
            @click="openReportLinkDialog(element)"
          >
            <v-icon start size="16">mdi-file-chart-outline</v-icon>
            {{ t('reporting.columns.reportLink') }}
          </v-btn>
          <v-btn
            variant="tonal"
            size="x-small"
            :color="isActiveListColumnFormat(element.format) ? 'primary' : undefined"
            :disabled="disabled"
            @click="openFormatDialog(element)"
          >
            <v-icon start size="16">mdi-format-paint</v-icon>
            {{ t('reporting.columns.format') }}
          </v-btn>
          <v-switch
            v-model="element.visible"
            :disabled="disabled"
            :label="t('reporting.columns.visible')"
            hide-details
            density="compact"
            color="primary"
          />
          <v-switch
            v-model="element.sortable"
            :disabled="disabled"
            :label="t('reporting.columns.sortable')"
            hide-details
            density="compact"
            color="primary"
          />
          <v-switch
            v-model="element.filterable"
            :disabled="disabled"
            :label="t('reporting.columns.filterable')"
            hide-details
            density="compact"
            color="primary"
          />
        </div>
      </v-card>
    </VueDraggableNext>

    <v-alert v-else type="warning" variant="tonal" density="compact">
      {{ t('reporting.columns.empty') }}
    </v-alert>

    <ReportingListColumnFormatDialog
      v-model="formatDialogOpen"
      :column="formatColumn"
      :column-label="formatColumn ? columnLabel(formatColumn) : ''"
      :condition-fields="conditionFields"
      @save="onFormatSave"
    />

    <ReportingListColumnRelationDisplayDialog
      v-model="relationDialogOpen"
      :column="relationColumn"
      :source-field="relationColumn ? sourceFieldForColumn(relationColumn) ?? null : null"
      :schema-fields="fields"
      @save="onRelationDisplaySave"
    />

    <ReportingListColumnReportLinkDialog
      v-model="reportLinkDialogOpen"
      :column="reportLinkColumn"
      :column-label="reportLinkColumn ? columnLabel(reportLinkColumn) : ''"
      :domain-key="domainKey"
      :allow-parent-field="allowParentField"
      @save="onReportLinkSave"
    />
  </div>
</template>

<style scoped>
.cursor-grab {
  cursor: grab;
}
</style>
