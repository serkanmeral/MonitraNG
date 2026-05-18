<script setup lang="ts">
import { ref, watch, computed, withDefaults } from 'vue';
import { VueDraggableNext } from 'vue-draggable-next';
import type { TmFieldDefinition, TmIssueCreateLayout, TmProject } from '@/types/apps/taskManager';
import { boardTableColumnTitle } from '@/utils/boardTableColumns';
import {
  DEFAULT_ISSUE_CREATE_DIALOG_MAX_WIDTH,
  defaultNewIssueFormColumnIds,
  defaultSectionKeyForColumnId,
  emptyIssueForm,
  mergeIssueCreateLayoutColumnSections,
  naturalSectionOrderFromLayout,
  normalizeDialogMaxWidthPx,
  resolveNewIssueFormRows,
  type IssueFormModel,
} from '@/utils/taskManagerNewIssueForm';
import TmNewIssueFormFields from '@/components/apps/task-manager/TmNewIssueFormFields.vue';

const PRESET_SECTION_KEYS = ['core', 'assignment', 'labels', 'extra'] as const;

const props = withDefaults(
  defineProps<{
    /** Sıralanacak sütun kimlikleri (title, issueType, … veya havuz key) */
    modelValue: string[];
    project: TmProject | null;
    fieldDefinitions: TmFieldDefinition[];
    /** Önizleme select listeleri (workspace ile aynı kaynak) */
    previewIssueTypeItems: { title: string; value: string }[];
    previewPriorityItems: { title: string; value: string }[];
    previewLabelItems: { title: string; value: string }[];
    previewUserItems: { title: string; value: string }[];
    /** Sütun kimliği → bölüm anahtarı */
    columnSections: Record<string, string>;
    formHeading: string;
    formIntro: string;
    sectionTitles: Record<string, string>;
    fieldCols: Record<string, number>;
    /** Yeni görev modalı max-width (px) */
    dialogMaxWidth?: number;
    sectionOrder?: string[];
    sectionCols?: Record<string, number>;
  }>(),
  {
    dialogMaxWidth: DEFAULT_ISSUE_CREATE_DIALOG_MAX_WIDTH,
    sectionOrder: () => [],
    sectionCols: () => ({}),
  }
);

const emit = defineEmits<{
  'update:modelValue': [string[]];
  'update:columnSections': [Record<string, string>];
  'update:formHeading': [string];
  'update:formIntro': [string];
  'update:sectionTitles': [Record<string, string>];
  'update:fieldCols': [Record<string, number>];
  'update:dialogMaxWidth': [number];
  'update:sectionOrder': [string[]];
  'update:sectionCols': [Record<string, number>];
}>();

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

function normalizeSectionKey(s: string): string {
  const t = s
    .trim()
    .toLowerCase()
    .replace(/\s+/g, '-')
    .replace(/[^a-z0-9_-]/g, '');
  return t || 'core';
}

function extractComboValue(v: unknown): string | null {
  if (v == null || v === '') return null;
  if (typeof v === 'object' && v !== null && 'value' in v) {
    const x = (v as { value: unknown }).value;
    return x == null ? null : String(x);
  }
  return String(v);
}

/** vue-draggable-next için sarmalayıcı (item-key için) */
type RowItem = { id: string };
const items = ref<RowItem[]>([]);

watch(
  () => props.modelValue,
  (v) => {
    items.value = (v ?? []).map((id) => ({ id }));
  },
  { immediate: true, deep: true }
);

function emitOrder() {
  emit(
    'update:modelValue',
    items.value.map((x) => x.id)
  );
}

function titleFor(columnId: string): string {
  return boardTableColumnTitle(columnId, props.fieldDefinitions, mt);
}

function resetToDefault() {
  const rows = defaultNewIssueFormColumnIds(props.project, props.fieldDefinitions);
  items.value = rows.map((id) => ({ id }));
  emit('update:modelValue', rows);
  emit(
    'update:columnSections',
    mergeIssueCreateLayoutColumnSections(props.fieldDefinitions, rows, null, null)
  );
  emit('update:formHeading', '');
  emit('update:formIntro', '');
  emit('update:sectionTitles', {});
  emit('update:fieldCols', {});
  emit('update:dialogMaxWidth', DEFAULT_ISSUE_CREATE_DIALOG_MAX_WIDTH);
  emit('update:sectionOrder', []);
  emit('update:sectionCols', {});
}

function columnSectionModelFor(columnId: string): string {
  const v = props.columnSections[columnId];
  if (v != null && String(v).trim() !== '') return String(v).trim();
  return defaultSectionKeyForColumnId(columnId, props.fieldDefinitions);
}

function onSectionCombo(columnId: string, v: unknown) {
  const raw = extractComboValue(v);
  if (raw == null || raw === '') {
    const next = { ...props.columnSections };
    delete next[columnId];
    emit('update:columnSections', next);
    return;
  }
  const key = normalizeSectionKey(raw);
  emit('update:columnSections', { ...props.columnSections, [columnId]: key });
}

function displayTitleForSectionKey(key: string): string {
  const custom = props.sectionTitles?.[key]?.trim();
  if (custom) return custom;
  const known: Record<string, { k: string; fb: string }> = {
    core: { k: 'taskManager.newIssueSectionCore', fb: 'Temel bilgiler' },
    assignment: { k: 'taskManager.newIssueSectionAssignment', fb: 'Atama ve tarih' },
    labels: { k: 'taskManager.newIssueSectionLabels', fb: 'Etiketler' },
    extra: { k: 'taskManager.newIssueSectionExtra', fb: 'Ek alanlar' },
  };
  if (known[key]) return mt(known[key].k, known[key].fb);
  return key;
}

const allSectionKeys = computed(() => {
  const s = new Set<string>([...PRESET_SECTION_KEYS]);
  for (const v of Object.values(props.columnSections || {})) {
    if (v != null && String(v).trim() !== '') s.add(String(v).trim());
  }
  for (const k of Object.keys(props.sectionTitles || {})) {
    if (k.trim()) s.add(k.trim());
  }
  return [...s].sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }));
});

const sectionComboItems = computed(() =>
  allSectionKeys.value.map((k) => ({
    title: `${displayTitleForSectionKey(k)} (${k})`,
    value: k,
  }))
);

function isPresetSection(key: string): boolean {
  return (PRESET_SECTION_KEYS as readonly string[]).includes(key);
}

function setSectionTitle(key: string, title: string) {
  const next = { ...props.sectionTitles };
  const t = String(title).trim();
  if (!t) delete next[key];
  else next[key] = t;
  emit('update:sectionTitles', next);
}

function deleteCustomSection(key: string) {
  if (isPresetSection(key)) return;
  const nextCs: Record<string, string> = { ...props.columnSections };
  let remapped = false;
  for (const col of Object.keys(nextCs)) {
    if (nextCs[col] === key) {
      nextCs[col] = 'core';
      remapped = true;
    }
  }
  const nextSt = { ...props.sectionTitles };
  delete nextSt[key];
  if (remapped) emit('update:columnSections', nextCs);
  emit('update:sectionTitles', nextSt);
  emit(
    'update:sectionOrder',
    (props.sectionOrder ?? []).filter((k) => k !== key)
  );
  const nextSecCols = { ...props.sectionCols };
  delete nextSecCols[key];
  emit('update:sectionCols', nextSecCols);
}

const fieldColSelectItems = computed(() =>
  [12, 6, 4, 3].map((n) => ({
    title: n === 12 ? mt('taskManager.editorIssueCreateColFull', 'Tam satır (12)') : String(n),
    value: n,
  }))
);

const DIALOG_WIDTH_PRESETS = [480, 560, 640, 720, 840, 960, 1200] as const;

const dialogWidthSelectItems = computed(() =>
  DIALOG_WIDTH_PRESETS.map((px) => ({
    title: `${px} px`,
    value: px,
  }))
);

function onDialogWidthChange(v: unknown) {
  emit('update:dialogMaxWidth', normalizeDialogMaxWidthPx(v));
}

function onFieldColChange(columnId: string, v: unknown) {
  const n = typeof v === 'number' ? v : Number(v);
  const next = { ...props.fieldCols };
  if (!Number.isFinite(n) || n >= 12) delete next[columnId];
  else next[columnId] = Math.round(n);
  emit('update:fieldCols', next);
}

type SectionOrderRow = { id: string };
const sectionOrderItems = ref<SectionOrderRow[]>([]);

function computeSectionKeyOrderForDrag(): string[] {
  const natural = naturalSectionOrderFromLayout(
    props.modelValue,
    props.columnSections,
    props.fieldDefinitions
  );
  const fromProp = props.sectionOrder?.length ? [...props.sectionOrder] : [];
  if (!fromProp.length) return natural;
  const naturalSet = new Set(natural);
  const ordered = fromProp.filter((k) => naturalSet.has(k));
  for (const k of natural) {
    if (!ordered.includes(k)) ordered.push(k);
  }
  return ordered;
}

watch(
  () => [props.modelValue, props.columnSections, props.sectionOrder],
  () => {
    sectionOrderItems.value = computeSectionKeyOrderForDrag().map((id) => ({ id }));
  },
  { immediate: true, deep: true }
);

function emitSectionOrderFromDrag() {
  emit(
    'update:sectionOrder',
    sectionOrderItems.value.map((x) => x.id)
  );
}

function onSectionBlockColChange(sectionId: string, v: unknown) {
  const n = typeof v === 'number' ? v : Number(v);
  const next = { ...props.sectionCols };
  if (!Number.isFinite(n) || n >= 12) delete next[sectionId];
  else next[sectionId] = Math.round(n);
  emit('update:sectionCols', next);
}

const previewIssueLayout = computed((): TmIssueCreateLayout | null => {
  if (!props.project) return null;
  const fh = props.formHeading?.trim();
  const fi = props.formIntro?.trim();
  const dw = normalizeDialogMaxWidthPx(props.dialogMaxWidth);
  return {
    rows: props.modelValue,
    columnSections: props.columnSections,
    sectionTitles: Object.keys(props.sectionTitles || {}).length ? props.sectionTitles : undefined,
    ...(fh ? { formHeading: fh } : {}),
    ...(fi ? { formIntro: fi } : {}),
    ...(Object.keys(props.fieldCols || {}).length ? { fieldCols: props.fieldCols } : {}),
    ...(dw !== DEFAULT_ISSUE_CREATE_DIALOG_MAX_WIDTH ? { dialogMaxWidth: dw } : {}),
    ...(props.sectionOrder?.length ? { sectionOrder: [...props.sectionOrder] } : {}),
    ...(Object.keys(props.sectionCols || {}).length ? { sectionCols: { ...props.sectionCols } } : {}),
  };
});

const previewPanelMaxWidthPx = computed(() => normalizeDialogMaxWidthPx(props.dialogMaxWidth));

const previewRows = computed(() => {
  if (!props.project) return [];
  return resolveNewIssueFormRows(props.project, props.fieldDefinitions, previewIssueLayout.value);
});

const previewForm = ref<IssueFormModel>(emptyIssueForm());

watch(
  () => [previewRows.value, props.modelValue],
  () => {
    const next = emptyIssueForm();
    next.title = mt('taskManager.editorIssueCreatePreviewSampleTitle', 'Örnek görev başlığı');
    previewForm.value = next;
  },
  { immediate: true, deep: true }
);
</script>

<template>
  <div class="project-issue-create-layout-editor">
    <p class="text-body-2 text-medium-emphasis mb-4">
      {{
        mt(
          'taskManager.editorIssueCreateHint',
          'Yeni görev penceresindeki alan sırası. Öncelikler / tipler / alanlar sekmesindeki seçimlere göre listelenen alanları sürükleyerek sıralayın.'
        )
      }}
    </p>
    <v-row>
      <v-col cols="12" lg="6">
        <v-card variant="outlined" class="rounded-lg pa-4 mb-4">
          <div class="text-subtitle-2 font-weight-medium mb-3">
            {{ mt('taskManager.editorIssueCreateFormHeaderBlock', 'Form üst metni') }}
          </div>
          <v-text-field
            :model-value="formHeading"
            density="comfortable"
            variant="outlined"
            hide-details="auto"
            class="mb-3"
            :label="mt('taskManager.editorIssueCreateFormHeading', 'Başlık (isteğe bağlı)')"
            @update:model-value="emit('update:formHeading', $event ?? '')"
          />
          <v-textarea
            :model-value="formIntro"
            density="comfortable"
            variant="outlined"
            rows="2"
            auto-grow
            hide-details="auto"
            :label="mt('taskManager.editorIssueCreateFormIntro', 'Üst açıklama (isteğe bağlı)')"
            @update:model-value="emit('update:formIntro', $event ?? '')"
          />
          <v-select
            class="mt-3"
            :model-value="normalizeDialogMaxWidthPx(dialogMaxWidth)"
            :items="dialogWidthSelectItems"
            item-title="title"
            item-value="value"
            density="comfortable"
            variant="outlined"
            hide-details="auto"
            :label="mt('taskManager.editorIssueCreateDialogWidth', 'Pencere genişliği')"
            :hint="mt('taskManager.editorIssueCreateDialogWidthHint', 'Yeni görev modalının genişliği (piksel).')"
            persistent-hint
            @update:model-value="onDialogWidthChange"
          />
        </v-card>

        <div class="d-flex flex-wrap gap-2 mb-4">
          <v-btn variant="tonal" size="small" rounded="lg" @click="resetToDefault">
            {{ mt('taskManager.editorIssueCreateReset', 'Varsayılan sıraya dön') }}
          </v-btn>
        </div>

        <v-expansion-panels variant="accordion" class="mb-4 rounded-lg tm-issue-section-titles-panel">
          <v-expansion-panel rounded="lg">
            <v-expansion-panel-title class="text-body-2">
              {{ mt('taskManager.editorIssueCreateSectionTitles', 'Bölüm görünen adları') }}
            </v-expansion-panel-title>
            <v-expansion-panel-text>
              <p class="text-caption text-medium-emphasis mb-3">
                {{ mt('taskManager.editorIssueCreateSectionTitlesHint', 'Özel bölüm anahtarları ve varsayılan bölümler için isteğe bağlı başlık.') }}
              </p>
              <div v-for="key in allSectionKeys" :key="key" class="d-flex flex-wrap align-center ga-2 mb-2">
                <v-chip size="small" variant="tonal" class="text-mono">{{ key }}</v-chip>
                <v-text-field
                  class="flex-grow-1"
                  style="min-width: 160px"
                  density="compact"
                  variant="outlined"
                  hide-details
                  :placeholder="displayTitleForSectionKey(key)"
                  :model-value="sectionTitles[key] ?? ''"
                  @update:model-value="(t) => setSectionTitle(key, String(t ?? ''))"
                />
                <v-btn
                  v-if="!isPresetSection(key)"
                  icon="mdi-delete-outline"
                  size="small"
                  variant="text"
                  color="error"
                  :title="mt('taskManager.editorIssueCreateDeleteSection', 'Bölümü kaldır (alanlar Temel bilgiler’e taşınır)')"
                  @click="deleteCustomSection(key)"
                />
              </div>
            </v-expansion-panel-text>
          </v-expansion-panel>
        </v-expansion-panels>

        <v-card variant="outlined" class="rounded-lg pa-3 mb-4">
          <div class="text-subtitle-2 font-weight-medium mb-2">
            {{ mt('taskManager.editorIssueCreateSectionLayout', 'Bölüm sırası ve genişlik') }}
          </div>
          <p class="text-caption text-medium-emphasis mb-3">
            {{
              mt(
                'taskManager.editorIssueCreateSectionLayoutHint',
                'Bölümleri sürükleyin. Yan yana göstermek için genişlik seçin (aynı satırda toplam 12).'
              )
            }}
          </p>
          <VueDraggableNext
            :list="sectionOrderItems"
            handle=".tm-section-layout-drag-handle"
            item-key="id"
            class="d-flex flex-column"
            @end="emitSectionOrderFromDrag"
          >
            <div
              v-for="el in sectionOrderItems"
              :key="el.id"
              class="d-flex align-center ga-2 flex-wrap px-2 py-2 tm-issue-layout-row"
              style="border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity))"
            >
              <v-icon
                class="tm-section-layout-drag-handle cursor-grab flex-shrink-0"
                icon="mdi-drag"
                size="small"
                color="medium-emphasis"
              />
              <span class="text-body-2 flex-grow-1" style="min-width: 100px">{{ displayTitleForSectionKey(el.id) }}</span>
              <v-chip size="x-small" variant="outlined" class="text-mono flex-shrink-0">{{ el.id }}</v-chip>
              <v-select
                :model-value="sectionCols[el.id] ?? 12"
                :items="fieldColSelectItems"
                item-title="title"
                item-value="value"
                density="compact"
                variant="outlined"
                hide-details
                class="flex-shrink-0"
                style="max-width: 140px; min-width: 120px"
                :label="mt('taskManager.editorIssueCreateSectionColWidth', 'Bölüm genişliği')"
                @update:model-value="(v) => onSectionBlockColChange(el.id, v)"
              />
            </div>
          </VueDraggableNext>
        </v-card>

        <v-card variant="outlined" class="rounded-lg pa-2">
          <VueDraggableNext
            :list="items"
            handle=".tm-issue-layout-drag-handle"
            item-key="id"
            class="d-flex flex-column"
            @end="emitOrder"
          >
            <div
              v-for="el in items"
              :key="el.id"
              class="d-flex align-center ga-2 flex-wrap px-2 py-2 tm-issue-layout-row"
              style="border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity))"
            >
              <v-icon class="tm-issue-layout-drag-handle cursor-grab flex-shrink-0" icon="mdi-drag" size="small" color="medium-emphasis" />
              <span class="text-body-2 flex-grow-1" style="min-width: 120px">{{ titleFor(el.id) }}</span>
              <v-combobox
                :model-value="columnSectionModelFor(el.id)"
                :items="sectionComboItems"
                item-title="title"
                item-value="value"
                density="compact"
                variant="outlined"
                hide-details
                clearable
                class="flex-shrink-0"
                style="max-width: 260px; min-width: 180px"
                :label="mt('taskManager.editorIssueCreateSectionLabel', 'Bölüm')"
                @update:model-value="(v) => onSectionCombo(el.id, v)"
              />
              <v-select
                :model-value="fieldCols[el.id] ?? 12"
                :items="fieldColSelectItems"
                item-title="title"
                item-value="value"
                density="compact"
                variant="outlined"
                hide-details
                class="flex-shrink-0"
                style="max-width: 140px; min-width: 120px"
                :label="mt('taskManager.editorIssueCreateColWidth', 'Genişlik')"
                @update:model-value="(v) => onFieldColChange(el.id, v)"
              />
              <v-chip size="x-small" variant="outlined" class="text-mono d-none d-sm-inline-flex">{{ el.id }}</v-chip>
            </div>
          </VueDraggableNext>
        </v-card>
      </v-col>
      <v-col cols="12" lg="6">
        <v-card variant="tonal" class="rounded-xl tm-issue-create-preview pa-4 h-100">
          <div class="text-subtitle-2 font-weight-medium mb-1">
            {{ mt('taskManager.editorIssueCreatePreviewTitle', 'Önizleme') }}
          </div>
          <p class="text-caption text-medium-emphasis mb-4">
            {{ mt('taskManager.editorIssueCreatePreviewHint', 'Yeni görev penceresinde alanlar bu sırayla görünür. Düzenleme yapılamaz.') }}
          </p>
          <div
            class="tm-issue-create-preview-inner tm-panel rounded-lg pa-4 mx-auto"
            :style="{ maxWidth: `${previewPanelMaxWidthPx}px` }"
          >
            <TmNewIssueFormFields
              v-model="previewForm"
              :rows="previewRows"
              :field-definitions="fieldDefinitions"
              :issue-type-items="previewIssueTypeItems"
              :priority-items="previewPriorityItems"
              :label-items="previewLabelItems"
              :user-items="previewUserItems"
              :issue-create-layout="previewIssueLayout"
              preview-mode
            />
          </div>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<style scoped>
.cursor-grab {
  cursor: grab;
}
.tm-issue-layout-row:last-child {
  border-bottom: none !important;
}
.tm-issue-create-preview-inner {
  max-height: min(70vh, 520px);
  overflow-y: auto;
}
.tm-issue-section-titles-panel :deep(.v-expansion-panel-title) {
  min-height: 48px;
}
</style>
