<script setup lang="ts">

import { watch, computed } from 'vue';

import type { TmFieldDefinition, TmIssueCreateLayout } from '@/types/apps/taskManager';

import {
  columnIdForNewIssueRow,
  defaultExtraValue,
  defaultSectionKeyForColumnId,
  fieldColSpanFor,
  sectionColSpanFor,
  type IssueFormModel,
  type NewIssueFormRow,
} from '@/utils/taskManagerNewIssueForm';

import { effectiveFieldCardinality, parseTmFieldOptionsJson } from '@/utils/taskManagerFieldDefinitions';
import TmIssueDescriptionEditor from '@/components/apps/task-manager/TmIssueDescriptionEditor.client.vue';



function isMulti(def: TmFieldDefinition): boolean {

  return effectiveFieldCardinality(def) === 'multi';

}



const props = defineProps<{
  rows: NewIssueFormRow[];

  /** Havuz alanları için varsayılan bölüm (extra) tespiti */

  fieldDefinitions: TmFieldDefinition[];

  issueTypeItems: { title: string; value: string }[];

  priorityItems: { title: string; value: string }[];

  labelItems: { title: string; value: string }[];

  userItems: { title: string; value: string }[];

  /** Salt okunur önizleme (proje düzenleme layout ekranı vb.) */

  previewMode?: boolean;

  /** Proje `issueCreateLayout` (columnSections ile dinamik bölüm) */

  issueCreateLayout?: TmIssueCreateLayout | null;
}>();

const preview = computed(() => !!props.previewMode);



const form = defineModel<IssueFormModel>({ required: true });



const nuxtApp = useNuxtApp();

function mt(key: string, fallback: string): string {

  try {

    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;

    if (i18n?.global?.t) return i18n.global.t(key) || fallback;

    if (i18n?.t) return i18n.t(key) || fallback;

  } catch (_) {}

  return fallback;

}



/** Bölüm anahtarı: issueCreateLayout.columnSections veya varsayılan. */

function rowSectionKey(row: NewIssueFormRow): string {

  const colId = columnIdForNewIssueRow(row);

  const layout = props.issueCreateLayout;

  const fromLayout = layout?.columnSections?.[colId];

  if (fromLayout != null && String(fromLayout).trim() !== '') {

    return String(fromLayout).trim();

  }

  return defaultSectionKeyForColumnId(colId, props.fieldDefinitions);

}



function sectionTitleForKey(key: string): string {

  const custom = props.issueCreateLayout?.sectionTitles?.[key];

  if (custom?.trim()) return custom.trim();

  const known: Record<string, { k: string; fb: string }> = {

    core: { k: 'taskManager.newIssueSectionCore', fb: 'Temel bilgiler' },

    assignment: { k: 'taskManager.newIssueSectionAssignment', fb: 'Atama ve tarih' },

    labels: { k: 'taskManager.newIssueSectionLabels', fb: 'Etiketler' },

    extra: { k: 'taskManager.newIssueSectionExtra', fb: 'Ek alanlar' },

  };

  if (known[key]) return mt(known[key].k, known[key].fb);

  return key;

}



/** Sırayı koruyarak ardışık aynı bölümü tek grupta birleştirir */

const rowSections = computed(() => {

  const list = props.rows;

  if (!list.length) return [] as { id: string; title: string; rows: NewIssueFormRow[] }[];

  const out: { id: string; title: string; rows: NewIssueFormRow[] }[] = [];

  for (const row of list) {

    const id = rowSectionKey(row);

    const last = out[out.length - 1];

    if (last && last.id === id) last.rows.push(row);

    else out.push({ id, title: sectionTitleForKey(id), rows: [row] });

  }

  return out;

});



const orderedRowSections = computed(() => {

  const secs = rowSections.value;

  const order = props.issueCreateLayout?.sectionOrder?.filter(Boolean) ?? [];

  if (!order.length) return secs;

  const byId = new Map(secs.map((s) => [s.id, s]));

  const used = new Set<string>();

  const out: { id: string; title: string; rows: NewIssueFormRow[] }[] = [];

  for (const id of order) {

    const s = byId.get(id);

    if (s) {

      out.push(s);

      used.add(id);

    }

  }

  for (const s of secs) {

    if (!used.has(s.id)) out.push(s);

  }

  return out;

});



function sectionGridSpanFor(sectionId: string): number {

  return sectionColSpanFor(sectionId, props.issueCreateLayout);

}



function extraKind(def: TmFieldDefinition): 'text' | 'number' | 'bool' | 'date' | 'datetime' | 'persons' | 'tags' {

  const ft = (def.fieldType || '').toLowerCase();

  if (ft === 'number') return 'number';

  if (ft === 'bool' || ft === 'boolean') return 'bool';

  if (ft === 'date') return 'date';

  if (ft === 'datetime') return 'datetime';

  if (ft === 'persons' || ft === 'person' || ft === 'group') return 'persons';

  if (ft === 'tags' || ft === 'relation' || ft === 'array') return 'tags';

  return 'text';

}



function relationDataset(def: TmFieldDefinition): string | null {

  return parseTmFieldOptionsJson(def.optionsJson ?? null)?.relationDataset ?? null;

}



watch(

  () => props.rows,

  (rows) => {

    for (const r of rows) {

      if (r.kind !== 'extra') continue;

      const k = r.definition.key;

      if (form.value.extra[k] === undefined) {

        form.value.extra = { ...form.value.extra, [k]: defaultExtraValue(r.definition) };

      }

    }

  },

  { immediate: true, deep: true }

);



function builtinLabel(key: string): string {

  const map: Record<string, string> = {

    title: mt('taskManager.issueTitle', 'Başlık'),

    description: mt('taskManager.description', 'Açıklama'),

    issueTypeId: mt('taskManager.issueType', 'Görev tipi'),

    priorityId: mt('taskManager.priority', 'Öncelik'),

    assignee: mt('taskManager.assignee', 'Atanan'),

    dueDate: mt('taskManager.dueDate', 'Bitiş tarihi'),

    labels: mt('taskManager.labels', 'Etiketler'),

    storyPoints: mt('taskManager.storyPoints', 'Story point'),

  };

  return map[key] ?? key;

}

const descriptionFieldLabel = computed(() => {
  const def = props.fieldDefinitions.find((f) => f.key === 'description');
  if (def?.label?.trim()) return def.label.trim();
  return mt('taskManager.description', 'Açıklama');
});

const fieldDensity = 'compact' as const;

function gridSpanFor(row: NewIssueFormRow): number {

  return fieldColSpanFor(columnIdForNewIssueRow(row), props.issueCreateLayout);

}

</script>



<template>

  <div class="tm-new-issue-fields d-flex flex-column gap-4" :class="{ 'tm-new-issue-fields--preview': preview }">

    <div

      v-if="(issueCreateLayout?.formHeading ?? '').trim()"

      class="tm-new-issue-form-header text-h6 font-weight-bold"

    >

      {{ (issueCreateLayout?.formHeading ?? '').trim() }}

    </div>

    <div

      v-if="(issueCreateLayout?.formIntro ?? '').trim()"

      class="tm-new-issue-form-intro text-body-2 text-medium-emphasis"

    >

      {{ (issueCreateLayout?.formIntro ?? '').trim() }}

    </div>

    <div class="tm-new-issue-sections-root tm-new-issue-section__fields--grid">

    <section

      v-for="sec in orderedRowSections"

      :key="sec.id"

      class="tm-new-issue-section"

      :style="{ gridColumn: `span ${sectionGridSpanFor(sec.id)}` }"

      :aria-label="orderedRowSections.length > 1 ? sec.title : undefined"

    >

      <div v-if="orderedRowSections.length > 1" class="tm-new-issue-section__title">

        {{ sec.title }}

      </div>

      <div class="tm-new-issue-section__fields tm-new-issue-section__fields--grid">

        <div

          v-for="row in sec.rows"

          :key="row.kind === 'builtin' ? row.key : row.definition.key"

          class="tm-new-issue-field-slot"

          :style="{ gridColumn: `span ${gridSpanFor(row)}` }"

        >

          <v-text-field

            v-if="row.kind === 'builtin' && row.key === 'title'"

            v-model="form.title"

            :label="builtinLabel('title')"

            :density="fieldDensity"

            variant="outlined"

            hide-details="auto"

            :readonly="preview"

            :autofocus="!preview"

          />

          <ClientOnly v-else-if="row.kind === 'builtin' && row.key === 'description'">
            <TmIssueDescriptionEditor v-model="form.description" :label="descriptionFieldLabel" :readonly="preview" />
            <template #fallback>
              <v-textarea
                v-model="form.description"
                :label="descriptionFieldLabel"
                rows="3"
                auto-grow
                :density="fieldDensity"
                variant="outlined"
                hide-details="auto"
                :readonly="preview"
              />
            </template>
          </ClientOnly>

          <v-select

            v-else-if="row.kind === 'builtin' && row.key === 'issueTypeId'"

            v-model="form.issueTypeId"

            :items="issueTypeItems"

            item-title="title"

            item-value="value"

            clearable

            :label="builtinLabel('issueTypeId')"

            :density="fieldDensity"

            variant="outlined"

            hide-details="auto"

            :disabled="preview"

          />

          <v-select

            v-else-if="row.kind === 'builtin' && row.key === 'priorityId'"

            v-model="form.priorityId"

            :items="priorityItems"

            item-title="title"

            item-value="value"

            clearable

            :label="builtinLabel('priorityId')"

            :density="fieldDensity"

            variant="outlined"

            hide-details="auto"

            :disabled="preview"

          />

          <v-select

            v-else-if="row.kind === 'builtin' && row.key === 'assignee'"

            v-model="form.assignee"

            :items="userItems"

            item-title="title"

            item-value="value"

            clearable

            :label="builtinLabel('assignee')"

            :density="fieldDensity"

            variant="outlined"

            hide-details="auto"

            :disabled="preview"

          />

          <v-text-field

            v-else-if="row.kind === 'builtin' && row.key === 'dueDate'"

            v-model="form.dueDate"

            type="date"

            :label="builtinLabel('dueDate')"

            :density="fieldDensity"

            variant="outlined"

            hide-details="auto"

            :readonly="preview"

          />

          <v-select

            v-else-if="row.kind === 'builtin' && row.key === 'labels'"

            v-model="form.labels"

            :items="labelItems"

            item-title="title"

            item-value="value"

            multiple

            chips

            closable-chips

            clearable

            :label="builtinLabel('labels')"

            :density="fieldDensity"

            variant="outlined"

            hide-details="auto"

            :disabled="preview"

          />

          <v-text-field

            v-else-if="row.kind === 'builtin' && row.key === 'storyPoints'"

            v-model.number="form.storyPoints"

            type="number"

            :label="builtinLabel('storyPoints')"

            :density="fieldDensity"

            variant="outlined"

            hide-details="auto"

            clearable

            :readonly="preview"

          />



          <template v-else-if="row.kind === 'extra'">

            <v-text-field

              v-if="extraKind(row.definition) === 'text'"

              v-model="form.extra[row.definition.key]"

              :label="row.definition.label"

              :density="fieldDensity"

              variant="outlined"

              hide-details="auto"

              :readonly="preview"

            />

            <v-text-field

              v-else-if="extraKind(row.definition) === 'number'"

              v-model.number="form.extra[row.definition.key]"

              type="number"

              :label="row.definition.label"

              :density="fieldDensity"

              variant="outlined"

              hide-details="auto"

              clearable

              :readonly="preview"

            />

            <v-checkbox

              v-else-if="extraKind(row.definition) === 'bool'"

              v-model="form.extra[row.definition.key]"

              :label="row.definition.label"

              density="compact"

              hide-details

              :disabled="preview"

            />

            <v-text-field

              v-else-if="extraKind(row.definition) === 'date'"

              v-model="form.extra[row.definition.key]"

              type="date"

              :label="row.definition.label"

              :density="fieldDensity"

              variant="outlined"

              hide-details="auto"

              :readonly="preview"

            />

            <v-text-field

              v-else-if="extraKind(row.definition) === 'datetime'"

              v-model="form.extra[row.definition.key]"

              type="datetime-local"

              :label="row.definition.label"

              :density="fieldDensity"

              variant="outlined"

              hide-details="auto"

              :readonly="preview"

            />

            <v-select

              v-else-if="extraKind(row.definition) === 'persons' && !isMulti(row.definition)"

              v-model="form.extra[row.definition.key]"

              :items="userItems"

              item-title="title"

              item-value="value"

              clearable

              :label="row.definition.label"

              :density="fieldDensity"

              variant="outlined"

              hide-details="auto"

              :disabled="preview"

            />

            <v-select

              v-else-if="extraKind(row.definition) === 'persons' && isMulti(row.definition)"

              v-model="form.extra[row.definition.key]"

              :items="userItems"

              item-title="title"

              item-value="value"

              multiple

              chips

              closable-chips

              clearable

              :label="row.definition.label"

              :density="fieldDensity"

              variant="outlined"

              hide-details="auto"

              :disabled="preview"

            />

            <v-select

              v-else-if="extraKind(row.definition) === 'tags' && relationDataset(row.definition) === 'tm_labels'"

              v-model="form.extra[row.definition.key]"

              :items="labelItems"

              item-title="title"

              item-value="value"

              :multiple="isMulti(row.definition)"

              chips

              closable-chips

              clearable

              :label="row.definition.label"

              :density="fieldDensity"

              variant="outlined"

              hide-details="auto"

              :disabled="preview"

            />

            <v-textarea

              v-else

              v-model="form.extra[row.definition.key]"

              :label="row.definition.label"

              rows="2"

              auto-grow

              :density="fieldDensity"

              variant="outlined"

              hide-details="auto"

              :readonly="preview"

            />

          </template>

        </div>

      </div>

    </section>

    </div>

  </div>

</template>

<style scoped>

.tm-new-issue-section__fields--grid {

  display: grid;

  grid-template-columns: repeat(12, minmax(0, 1fr));

  gap: 8px;

  align-items: start;

}

@media (max-width: 959px) {

  .tm-new-issue-section__fields--grid .tm-new-issue-field-slot {

    grid-column: 1 / -1 !important;

  }

  .tm-new-issue-sections-root > .tm-new-issue-section {

    grid-column: 1 / -1 !important;

  }

}

</style>

