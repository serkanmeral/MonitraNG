<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { VueDraggableNext } from 'vue-draggable-next';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OpFormLayoutSection } from '@/types/apps/operationCore';
import { gridColSelectItems, normalizeOcGridCol } from '@/utils/ocFormLayout';

const props = defineProps<{
  sections: OpFormLayoutSection[];
  fieldCols: Record<string, number>;
  layoutFieldItems: { value: string; title: string }[];
}>();

const emit = defineEmits<{
  'update:sections': [OpFormLayoutSection[]];
  'update:fieldCols': [Record<string, number>];
}>();

const { t } = useAppI18n();

type SectionDragItem = { id: string };
type FieldDragItem = { id: string };

const sectionDragItems = ref<SectionDragItem[]>([]);
const fieldDragBySection = ref<Record<string, FieldDragItem[]>>({});

const colSelectItems = computed(() =>
  gridColSelectItems((cols, full) =>
    full
      ? t('operationCore.workspaceDefinitions.forms.gridColFull')
      : t('operationCore.workspaceDefinitions.forms.gridColSpan', { cols })
  )
);

function labelForField(key: string): string {
  return props.layoutFieldItems.find((i) => i.value === key)?.title ?? key;
}

function syncDragFromSections() {
  sectionDragItems.value = props.sections.map((s) => ({ id: s.key }));
  const nextFields: Record<string, FieldDragItem[]> = {};
  for (const s of props.sections) {
    nextFields[s.key] = s.fields.map((id) => ({ id }));
  }
  fieldDragBySection.value = nextFields;
}

watch(() => props.sections, syncDragFromSections, { immediate: true, deep: true });

function emitSections(next: OpFormLayoutSection[]) {
  emit('update:sections', next);
}

function emitFieldCols(patch: Record<string, number>) {
  emit('update:fieldCols', patch);
}

function onSectionDragEnd() {
  const order = sectionDragItems.value.map((x) => x.id);
  const byKey = new Map(props.sections.map((s) => [s.key, s]));
  const next = order.map((key) => byKey.get(key)).filter((s): s is OpFormLayoutSection => !!s);
  emitSections(next);
}

function onFieldDragEnd(sectionKey: string) {
  const order = (fieldDragBySection.value[sectionKey] ?? []).map((x) => x.id);
  const next = props.sections.map((s) =>
    s.key === sectionKey ? { ...s, fields: order } : { ...s, fields: [...s.fields] }
  );
  emitSections(next);
}

function setSectionCol(sectionKey: string, value: unknown) {
  const cols = normalizeOcGridCol(value, 12);
  emitSections(
    props.sections.map((s) => (s.key === sectionKey ? { ...s, cols } : { ...s }))
  );
}

function setFieldCol(fieldKey: string, value: unknown) {
  const cols = normalizeOcGridCol(value, 12);
  emitFieldCols({ ...props.fieldCols, [fieldKey]: cols });
}

function patchSection(sectionKey: string, patch: Partial<OpFormLayoutSection>) {
  emitSections(props.sections.map((s) => (s.key === sectionKey ? { ...s, ...patch } : s)));
}

function addSection() {
  const n = props.sections.length + 1;
  let key = `section_${n}`;
  while (props.sections.some((s) => s.key === key)) key = `${key}_x`;
  emitSections([...props.sections, { key, title: '', cols: 12, fields: [] }]);
}

function removeSection(sectionKey: string) {
  if (props.sections.length <= 1) return;
  const nextCols = { ...props.fieldCols };
  for (const key of props.sections.find((s) => s.key === sectionKey)?.fields ?? []) {
    delete nextCols[key];
  }
  emitFieldCols(nextCols);
  emitSections(props.sections.filter((s) => s.key !== sectionKey));
}

function addFieldToSection(sectionKey: string, fieldKey: string | null) {
  if (!fieldKey) return;
  const next = props.sections.map((s) => {
    if (s.key !== sectionKey) return s;
    if (s.fields.includes(fieldKey)) return s;
    return { ...s, fields: [...s.fields, fieldKey] };
  });
  emitSections(next);
  if (props.fieldCols[fieldKey] == null) {
    emitFieldCols({
      ...props.fieldCols,
      [fieldKey]: fieldKey === 'description' ? 12 : 6,
    });
  }
}

function removeField(sectionKey: string, fieldKey: string) {
  const nextCols = { ...props.fieldCols };
  delete nextCols[fieldKey];
  emitFieldCols(nextCols);
  emitSections(
    props.sections.map((s) =>
      s.key === sectionKey ? { ...s, fields: s.fields.filter((f) => f !== fieldKey) } : s
    )
  );
}

function availableFieldsForSection(section: OpFormLayoutSection) {
  const used = new Set<string>();
  for (const s of props.sections) {
    for (const f of s.fields) used.add(f);
  }
  return props.layoutFieldItems.filter((i) => !used.has(i.value) || section.fields.includes(i.value));
}

function sectionByKey(key: string): OpFormLayoutSection | undefined {
  return props.sections.find((s) => s.key === key);
}

const fieldToAddBySection = ref<Record<string, string | null>>({});
</script>

<template>
  <div class="oc-form-layout-editor">
    <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-3">
      <p class="text-caption text-medium-emphasis mb-0">
        {{ t('operationCore.workspaceDefinitions.forms.layoutEditorHint') }}
      </p>
      <v-btn size="small" variant="tonal" rounded="lg" class="text-none" @click="addSection">
        <v-icon icon="mdi-plus" start />
        {{ t('operationCore.workspaceDefinitions.forms.addSection') }}
      </v-btn>
    </div>

    <VueDraggableNext
      :list="sectionDragItems"
      handle=".oc-layout-drag-handle"
      item-key="id"
      class="d-flex flex-column gap-3"
      @end="onSectionDragEnd"
    >
      <v-card
        v-for="el in sectionDragItems"
        :key="el.id"
        variant="outlined"
        rounded="lg"
        class="oc-form-layout-section-card"
      >
        <v-card-text v-if="sectionByKey(el.id)" class="pb-2">
          <div class="d-flex align-center flex-wrap ga-2 mb-3">
              <v-icon
                class="oc-layout-drag-handle cursor-grab flex-shrink-0"
                icon="mdi-drag"
                size="small"
                color="medium-emphasis"
              />
              <v-text-field
                :model-value="sectionByKey(el.id)!.key"
                :label="t('operationCore.workspaceDefinitions.forms.fieldSectionKey')"
                density="compact"
                variant="outlined"
                hide-details
                readonly
                class="flex-grow-1"
                style="min-width: 120px; max-width: 200px"
              />
              <v-text-field
                :model-value="sectionByKey(el.id)!.title ?? ''"
                :label="t('operationCore.workspaceDefinitions.forms.fieldSectionTitle')"
                density="compact"
                variant="outlined"
                hide-details
                class="flex-grow-1"
                style="min-width: 160px"
                @update:model-value="(v) => patchSection(el.id, { title: String(v ?? '') })"
              />
              <v-select
                :model-value="sectionByKey(el.id)!.cols ?? 12"
                :items="colSelectItems"
                item-title="title"
                item-value="value"
                density="compact"
                variant="outlined"
                hide-details
                style="max-width: 150px; min-width: 130px"
                :label="t('operationCore.workspaceDefinitions.forms.sectionColWidth')"
                @update:model-value="(v) => setSectionCol(el.id, v)"
              />
              <v-btn
                v-if="sections.length > 1"
                icon
                variant="text"
                size="small"
                color="error"
                :title="t('operationCore.workspaceDefinitions.forms.removeSection')"
                @click="removeSection(el.id)"
              >
                <v-icon icon="mdi-delete-outline" />
              </v-btn>
            </div>

            <div class="text-caption text-medium-emphasis mb-2">
              {{ t('operationCore.workspaceDefinitions.forms.fieldsInSection') }}
            </div>

            <VueDraggableNext
              v-if="(fieldDragBySection[el.id] ?? []).length"
              :list="fieldDragBySection[el.id]!"
              handle=".oc-layout-field-drag"
              item-key="id"
              class="d-flex flex-column rounded-lg border mb-2"
              @end="() => onFieldDragEnd(el.id)"
            >
              <div
                v-for="fieldEl in fieldDragBySection[el.id]!"
                :key="fieldEl.id"
                class="d-flex align-center flex-wrap ga-2 px-3 py-2 oc-form-layout-field-row"
              >
                <v-icon
                  class="oc-layout-field-drag cursor-grab flex-shrink-0"
                  icon="mdi-drag"
                  size="x-small"
                  color="medium-emphasis"
                />
                <span class="text-body-2 flex-grow-1" style="min-width: 100px">
                  {{ labelForField(fieldEl.id) }}
                </span>
                <v-chip size="x-small" variant="outlined" class="text-mono d-none d-sm-inline-flex">
                  {{ fieldEl.id }}
                </v-chip>
                <v-select
                  :model-value="fieldCols[fieldEl.id] ?? (fieldEl.id === 'description' ? 12 : 6)"
                  :items="colSelectItems"
                  item-title="title"
                  item-value="value"
                  density="compact"
                  variant="outlined"
                  hide-details
                  style="max-width: 140px; min-width: 120px"
                  :label="t('operationCore.workspaceDefinitions.forms.fieldColWidth')"
                  @update:model-value="(v) => setFieldCol(fieldEl.id, v)"
                />
                <v-btn
                  icon
                  variant="text"
                  size="x-small"
                  color="error"
                  @click="removeField(el.id, fieldEl.id)"
                >
                  <v-icon icon="mdi-close" />
                </v-btn>
              </div>
            </VueDraggableNext>

            <v-alert v-else type="info" variant="tonal" density="compact" class="mb-2">
              {{ t('operationCore.workspaceDefinitions.forms.noFieldsInSection') }}
            </v-alert>

            <v-select
              :model-value="fieldToAddBySection[el.id] ?? null"
              :items="
                availableFieldsForSection(sectionByKey(el.id)!).filter(
                  (i) => !sectionByKey(el.id)!.fields.includes(i.value)
                )
              "
              item-title="title"
              item-value="value"
              :label="t('operationCore.workspaceDefinitions.forms.addFieldToSection')"
              density="compact"
              clearable
              hide-details
              @update:model-value="
                (v) => {
                  addFieldToSection(el.id, v);
                  fieldToAddBySection[el.id] = null;
                }
              "
            />
        </v-card-text>
      </v-card>
    </VueDraggableNext>
  </div>
</template>

<style scoped>
.oc-form-layout-field-row + .oc-form-layout-field-row {
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
