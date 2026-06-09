<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import WidgetTemplateCatalog from '@/components/widgets/designer/WidgetTemplateCatalog.vue';
import type { OcDashboardWidgetDef, OpState, OpPriority } from '@/types/apps/operationCore';
import type { WidgetTemplateRecord } from '@/types/apps/widgetManifest';
import {
  ocTemplateToWidgetDef,
  suggestOcWidgetKeyFromTemplate,
} from '@/utils/widgets/ocWidgetTemplateAdapter';
import {
  OC_SUMMARY_CARD_ACCENTS,
  OC_SUMMARY_CARD_ICONS,
} from '@/utils/ocDashboardWidgetStyle';

const props = defineProps<{
  modelValue: boolean;
  widget: OcDashboardWidgetDef | null;
  existingKeys: string[];
  workspaceId: string;
  states: OpState[];
  priorities: OpPriority[];
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  save: [widget: OcDashboardWidgetDef];
}>();

const { t } = useAppI18n();

const DEFAULT_DATASET = 'op_work_items';

// Desteklenen named query'ler + onerilen parametre anahtarlari (DG/MO OcQueries ile uyumlu).
const NAMED_QUERIES: { key: string; params: string[]; supportsChart: boolean }[] = [
  { key: 'wi_by_workspace_and_state', params: ['workspaceId', 'stateId'], supportsChart: true },
  { key: 'wi_board_column', params: ['boardId', 'columnId'], supportsChart: true },
  { key: 'wi_assigned_to_user', params: ['assignee'], supportsChart: true },
  { key: 'wi_sla_response_breach', params: ['workspaceId', 'asOf'], supportsChart: false },
  { key: 'wi_sla_resolve_breach', params: ['workspaceId', 'asOf'], supportsChart: false },
];

const WIDGET_TYPES = ['summaryCard', 'list', 'chart'];
const CHART_TYPES = ['donut', 'pie', 'bar', 'line'];
const GROUP_BY_FIELDS = ['stateId', 'priorityId', 'typeId', 'assignee'];
const TOKENS = ['{{workspaceId}}', '{{currentUser}}', '{{asOf}}', '{{boardId}}'];

interface ParamEntry {
  key: string;
  value: string;
}

const form = ref<{
  key: string;
  type: string;
  title: string;
  queryKey: string;
  take: string;
  chartType: string;
  groupBy: string;
  accentColor: string;
  icon: string;
}>({
  key: '',
  type: 'summaryCard',
  title: '',
  queryKey: '',
  take: '',
  chartType: 'donut',
  groupBy: 'stateId',
  accentColor: '',
  icon: '',
});
const paramEntries = ref<ParamEntry[]>([]);
const errorLocal = ref<string | null>(null);
const editorTab = ref<'template' | 'custom'>('template');

const isChart = computed(() => form.value.type === 'chart');
const isSummary = computed(() => form.value.type === 'summaryCard');

const accentItems = computed(() =>
  OC_SUMMARY_CARD_ACCENTS.map((v) => ({
    title: t(`operationCore.definitions.themeColor.${v}`),
    value: v,
  }))
);

const iconItems = computed(() =>
  OC_SUMMARY_CARD_ICONS.map((v) => ({ title: v.replace('mdi-', ''), value: v }))
);

const stateItems = computed(() =>
  props.states.map((s) => ({ title: s.name, value: s.__dataId }))
);
const priorityItems = computed(() =>
  props.priorities.map((p) => ({ title: p.name, value: p.__dataId }))
);

const open = computed({
  get: () => props.modelValue,
  set: (v) => emit('update:modelValue', v),
});

function paramsToEntries(params?: Record<string, unknown> | null): ParamEntry[] {
  if (!params) return [];
  return Object.entries(params).map(([key, value]) => ({
    key,
    value: value == null ? '' : String(value),
  }));
}

function seedSuggestedParams() {
  const def = NAMED_QUERIES.find((q) => q.key === form.value.queryKey);
  if (!def) return;
  const existing = new Map(paramEntries.value.map((e) => [e.key, e.value]));
  const next: ParamEntry[] = def.params.map((k) => ({
    key: k,
    value: existing.get(k) ?? defaultParamValue(k),
  }));
  // Onerilmeyen ama mevcut ozel parametreleri koru.
  for (const e of paramEntries.value) {
    if (!def.params.includes(e.key)) next.push(e);
  }
  paramEntries.value = next;
}

function defaultParamValue(key: string): string {
  if (key === 'workspaceId') return props.workspaceId || '{{workspaceId}}';
  if (key === 'assignee') return '{{currentUser}}';
  if (key === 'asOf') return '{{asOf}}';
  return '';
}

watch(
  () => props.modelValue,
  (isOpen) => {
    if (!isOpen) return;
    errorLocal.value = null;
    editorTab.value = props.widget ? 'custom' : 'template';
    const w = props.widget;
    form.value = {
      key: w?.key ?? '',
      type: w?.type ?? 'summaryCard',
      title: w?.title ?? '',
      queryKey: w?.queryKey ?? '',
      take: w?.take != null ? String(w.take) : '',
      chartType: w?.chartType ?? 'donut',
      groupBy: w?.groupBy ?? 'stateId',
      accentColor: w?.accentColor ?? '',
      icon: w?.icon ?? '',
    };
    paramEntries.value = paramsToEntries(w?.parameters);
    if (!paramEntries.value.length && form.value.queryKey) seedSuggestedParams();
  }
);

function onQueryChange() {
  seedSuggestedParams();
}

function addParam() {
  paramEntries.value.push({ key: '', value: '' });
}

function removeParam(idx: number) {
  paramEntries.value.splice(idx, 1);
}

function appendToken(idx: number, token: string) {
  const e = paramEntries.value[idx];
  if (e) e.value = token;
}

function smartInputKind(key: string): 'state' | 'priority' | 'token' | 'text' {
  if (key === 'stateId' && stateItems.value.length) return 'state';
  if (key === 'priorityId' && priorityItems.value.length) return 'priority';
  if (key === 'assignee' || key === 'asOf' || key === 'workspaceId') return 'token';
  return 'text';
}

function tokenFor(key: string): string | null {
  if (key === 'assignee') return '{{currentUser}}';
  if (key === 'asOf') return '{{asOf}}';
  if (key === 'workspaceId') return '{{workspaceId}}';
  return null;
}

function onTemplateSelect(record: WidgetTemplateRecord) {
  errorLocal.value = null;
  const key = suggestOcWidgetKeyFromTemplate(record.templateId, props.existingKeys);
  const def = ocTemplateToWidgetDef(record, { workspaceId: props.workspaceId, key });
  if (!def) {
    errorLocal.value = t('operationCore.dashboards.editor.widget.templateUnsupported');
    return;
  }
  form.value = {
    key: def.key,
    type: def.type,
    title: def.title ?? '',
    queryKey: def.queryKey ?? '',
    take: def.take != null ? String(def.take) : '',
    chartType: def.chartType ?? 'donut',
    groupBy: def.groupBy ?? 'stateId',
    accentColor: def.accentColor ?? '',
    icon: def.icon ?? '',
  };
  paramEntries.value = paramsToEntries(def.parameters);
  editorTab.value = 'custom';
}

function save() {
  errorLocal.value = null;
  const key = form.value.key.trim();
  if (!key) {
    errorLocal.value = t('operationCore.dashboards.editor.widget.keyRequired');
    return;
  }
  if (!/^[a-z0-9_]+$/i.test(key)) {
    errorLocal.value = t('operationCore.dashboards.editor.widget.keyInvalid');
    return;
  }
  const dup = props.existingKeys.some(
    (k) => k === key && k !== props.widget?.key
  );
  if (dup) {
    errorLocal.value = t('operationCore.dashboards.editor.widget.keyDuplicate');
    return;
  }
  if (!form.value.queryKey) {
    errorLocal.value = t('operationCore.dashboards.editor.widget.queryRequired');
    return;
  }

  const parameters: Record<string, unknown> = {};
  for (const e of paramEntries.value) {
    const k = e.key.trim();
    if (!k) continue;
    parameters[k] = e.value.trim();
  }

  const takeNum = form.value.take.trim() ? Number(form.value.take) : null;

  const widget: OcDashboardWidgetDef = {
    key,
    type: form.value.type,
    title: form.value.title.trim() || null,
    dataset: DEFAULT_DATASET,
    queryKey: form.value.queryKey,
    parameters: Object.keys(parameters).length ? parameters : null,
    take: takeNum != null && !Number.isNaN(takeNum) ? takeNum : null,
    chartType: isChart.value ? form.value.chartType : null,
    groupBy: isChart.value ? form.value.groupBy : null,
    accentColor: isSummary.value ? form.value.accentColor.trim() || null : null,
    icon: isSummary.value ? form.value.icon.trim() || null : null,
  };

  emit('save', widget);
  open.value = false;
}
</script>

<template>
  <v-dialog v-model="open" max-width="720" persistent scrollable>
    <v-card rounded="lg">
      <v-card-title class="text-h6">
        {{
          props.widget
            ? t('operationCore.dashboards.editor.widget.editTitle')
            : t('operationCore.dashboards.editor.widget.addTitle')
        }}
      </v-card-title>
      <v-divider />
      <v-card-text style="max-height: 70vh">
        <v-alert
          v-if="errorLocal"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-3"
          closable
          @click:close="errorLocal = null"
        >
          {{ errorLocal }}
        </v-alert>

        <v-tabs v-model="editorTab" density="compact" class="mb-3">
          <v-tab value="template">{{ t('operationCore.dashboards.editor.widget.tabTemplate') }}</v-tab>
          <v-tab value="custom">{{ t('operationCore.dashboards.editor.widget.tabCustom') }}</v-tab>
        </v-tabs>

        <div v-if="editorTab === 'template'" class="mb-2">
          <p class="text-body-2 text-medium-emphasis mb-3">
            {{ t('operationCore.dashboards.editor.widget.templateHint') }}
          </p>
          <WidgetTemplateCatalog
            domain-filter="operation-core"
            compact
            @select="onTemplateSelect"
          />
        </div>

        <template v-else>
        <div class="d-flex ga-3 flex-wrap mb-3">
          <v-text-field
            v-model="form.key"
            :label="t('operationCore.dashboards.editor.widget.key')"
            :hint="t('operationCore.dashboards.editor.widget.keyHint')"
            persistent-hint
            variant="outlined"
            density="comfortable"
            style="flex: 1; min-width: 200px"
          />
          <v-select
            v-model="form.type"
            :items="WIDGET_TYPES"
            :label="t('operationCore.dashboards.editor.widget.type')"
            variant="outlined"
            density="comfortable"
            style="flex: 0 0 180px"
          />
        </div>

        <v-text-field
          v-model="form.title"
          :label="t('operationCore.dashboards.editor.widget.titleField')"
          variant="outlined"
          density="comfortable"
          class="mb-3"
          hide-details
        />

        <div class="d-flex ga-3 flex-wrap mb-1">
          <v-select
            v-model="form.queryKey"
            :items="NAMED_QUERIES.map((q) => q.key)"
            :label="t('operationCore.dashboards.editor.widget.query')"
            variant="outlined"
            density="comfortable"
            style="flex: 1; min-width: 220px"
            @update:model-value="onQueryChange"
          />
          <v-text-field
            v-model="form.take"
            type="number"
            :label="t('operationCore.dashboards.editor.widget.take')"
            variant="outlined"
            density="comfortable"
            style="flex: 0 0 110px"
            hide-details
          />
        </div>

        <!-- Summary card görünüm -->
        <div v-if="isSummary" class="d-flex ga-3 flex-wrap mb-3 mt-2">
          <v-select
            v-model="form.accentColor"
            :items="accentItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.dashboards.editor.widget.accentColor')"
            variant="outlined"
            density="comfortable"
            clearable
            style="flex: 1; min-width: 160px"
            hide-details
          />
          <v-select
            v-model="form.icon"
            :items="iconItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.dashboards.editor.widget.icon')"
            variant="outlined"
            density="comfortable"
            clearable
            style="flex: 1; min-width: 200px"
            hide-details
          >
            <template #item="{ props: itemProps, item }">
              <v-list-item v-bind="itemProps" :prepend-icon="item.raw.value" />
            </template>
            <template #selection="{ item }">
              <v-icon v-if="item.raw.value" :icon="item.raw.value" size="18" class="me-2" />
              {{ item.title }}
            </template>
          </v-select>
        </div>

        <!-- Chart config -->
        <div v-if="isChart" class="d-flex ga-3 flex-wrap mb-3 mt-2">
          <v-select
            v-model="form.chartType"
            :items="CHART_TYPES"
            :label="t('operationCore.dashboards.editor.widget.chartType')"
            variant="outlined"
            density="comfortable"
            style="flex: 1; min-width: 160px"
            hide-details
          />
          <v-select
            v-model="form.groupBy"
            :items="GROUP_BY_FIELDS"
            :label="t('operationCore.dashboards.editor.widget.groupBy')"
            variant="outlined"
            density="comfortable"
            style="flex: 1; min-width: 160px"
            hide-details
          />
        </div>

        <!-- Parameters -->
        <div class="d-flex align-center justify-space-between mt-4 mb-2">
          <span class="text-subtitle-2 font-weight-medium">
            {{ t('operationCore.dashboards.editor.widget.parameters') }}
          </span>
          <v-btn size="small" variant="tonal" prepend-icon="mdi-plus" @click="addParam">
            {{ t('operationCore.dashboards.editor.widget.addParam') }}
          </v-btn>
        </div>

        <p class="text-caption text-medium-emphasis mb-2">
          {{ t('operationCore.dashboards.editor.widget.tokenHint') }}
        </p>

        <div v-if="!paramEntries.length" class="text-body-2 text-medium-emphasis mb-2">
          {{ t('operationCore.dashboards.editor.widget.noParams') }}
        </div>

        <div
          v-for="(entry, idx) in paramEntries"
          :key="idx"
          class="d-flex ga-2 align-start mb-2"
        >
          <v-text-field
            v-model="entry.key"
            :label="t('operationCore.dashboards.editor.widget.paramKey')"
            variant="outlined"
            density="compact"
            hide-details
            style="flex: 0 0 150px"
          />
          <v-select
            v-if="smartInputKind(entry.key) === 'state'"
            v-model="entry.value"
            :items="stateItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.dashboards.editor.widget.paramValue')"
            variant="outlined"
            density="compact"
            hide-details
            style="flex: 1"
          />
          <v-select
            v-else-if="smartInputKind(entry.key) === 'priority'"
            v-model="entry.value"
            :items="priorityItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.dashboards.editor.widget.paramValue')"
            variant="outlined"
            density="compact"
            hide-details
            style="flex: 1"
          />
          <div v-else class="d-flex flex-column" style="flex: 1">
            <v-text-field
              v-model="entry.value"
              :label="t('operationCore.dashboards.editor.widget.paramValue')"
              variant="outlined"
              density="compact"
              hide-details
            />
            <div v-if="tokenFor(entry.key)" class="mt-1">
              <v-chip
                size="x-small"
                variant="tonal"
                color="primary"
                class="cursor-pointer"
                @click="appendToken(idx, tokenFor(entry.key) || '')"
              >
                {{ tokenFor(entry.key) }}
              </v-chip>
            </div>
          </div>
          <v-btn
            icon="mdi-close"
            variant="text"
            size="small"
            color="error"
            @click="removeParam(idx)"
          />
        </div>
        </template>
      </v-card-text>
      <v-divider />
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" class="text-none" @click="open = false">
          {{ t('operationCore.dashboards.editor.cancel') }}
        </v-btn>
        <v-btn
          v-if="editorTab === 'custom'"
          color="primary"
          variant="flat"
          class="text-none"
          @click="save"
        >
          {{ t('operationCore.dashboards.editor.widget.saveWidget') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.cursor-pointer {
  cursor: pointer;
}
</style>
