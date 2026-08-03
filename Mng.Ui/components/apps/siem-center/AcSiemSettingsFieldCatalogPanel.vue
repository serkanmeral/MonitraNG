<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  deleteSecEventCustomField,
  fetchSecEventTargetFields,
  upsertSecEventCustomField,
} from '@/services/secEventParseRuleCatalogService';
import type { SecEventTargetFieldDefinition } from '@/types/apps/secEventParseRules';

const { t } = useAppI18n();

const loading = ref(false);
const saving = ref(false);
const error = ref<string | null>(null);
const flash = ref<string | null>(null);
const version = ref('');
const fields = ref<SecEventTargetFieldDefinition[]>([]);
const search = ref('');
const groupFilter = ref<string | null>(null);
const kindFilter = ref<'all' | 'core' | 'custom'>('all');
const listPage = ref(1);
const listItemsPerPage = ref(10);
const LIST_PAGE_SIZE_OPTIONS = [
  { value: 10, title: '10' },
  { value: 25, title: '25' },
  { value: 50, title: '50' },
  { value: -1, title: 'All' },
];

const newSlug = ref('');
const newLabel = ref('');
const newDescription = ref('');
const deleteTarget = ref<SecEventTargetFieldDefinition | null>(null);

const groups = computed(() => {
  const set = new Set(fields.value.map((f) => f.group).filter(Boolean));
  return [...set].sort((a, b) => a.localeCompare(b));
});

const groupItems = computed(() => [
  { title: t('siemCenter.settings.fields.allGroups'), value: null as string | null },
  ...groups.value.map((g) => ({
    title: groupLabel(g),
    value: g as string | null,
  })),
]);

const kindFilterItems = computed(() => [
  { value: 'all', title: t('siemCenter.settings.fields.filterAll') },
  { value: 'core', title: t('siemCenter.settings.fields.filterCore') },
  { value: 'custom', title: t('siemCenter.settings.fields.filterCustom') },
]);

interface FieldTableRow extends SecEventTargetFieldDefinition {
  _nameSort: string;
  _groupLabel: string;
  _extractSort: string;
  _flagsSort: string;
}

function groupLabel(group: string): string {
  const key = `siemCenter.settings.fields.groups.${group}`;
  const translated = t(key);
  return translated === key ? group : translated;
}

const listHeaders = computed(() => [
  { title: t('siemCenter.settings.fields.colName'), key: '_nameSort', sortable: true },
  { title: t('siemCenter.settings.fields.colGroup'), key: '_groupLabel', sortable: true },
  { title: t('siemCenter.settings.fields.colType'), key: 'valueType', sortable: true },
  { title: t('siemCenter.settings.fields.colExtract'), key: '_extractSort', sortable: true },
  { title: t('siemCenter.settings.fields.colQueryOps'), key: 'queryOperators', sortable: false },
  { title: t('siemCenter.settings.fields.colFlags'), key: '_flagsSort', sortable: true },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
]);

const filteredRows = computed((): FieldTableRow[] => {
  const q = search.value.trim().toLowerCase();
  return fields.value
    .filter((f) => {
      if (groupFilter.value && f.group !== groupFilter.value) return false;
      if (kindFilter.value === 'custom' && !f.isCustom) return false;
      if (kindFilter.value === 'core' && f.isCustom) return false;
      if (!q) return true;
      const hay = [
        f.name,
        f.label,
        f.group,
        f.valueType,
        f.description ?? '',
        ...(f.extractTypes || []),
        ...(f.queryOperators || []),
      ]
        .join(' ')
        .toLowerCase();
      return hay.includes(q);
    })
    .map((f) => ({
      ...f,
      _nameSort: f.label || f.name,
      _groupLabel: groupLabel(f.group),
      _extractSort: (f.extractTypes || []).join(', '),
      _flagsSort: [
        f.isCustom ? 'custom' : '',
        f.queryable ? 'queryable' : '',
        f.wizardSelectable ? 'wizard' : '',
      ]
        .filter(Boolean)
        .join(' '),
    }));
});

watch([search, groupFilter, kindFilter], () => {
  listPage.value = 1;
});

async function load() {
  loading.value = true;
  error.value = null;
  try {
    const res = await fetchSecEventTargetFields();
    fields.value = res.fields ?? [];
    version.value = res.version || '';
  } catch (e) {
    fields.value = [];
    version.value = '';
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

async function addCustom() {
  flash.value = null;
  error.value = null;
  saving.value = true;
  try {
    await upsertSecEventCustomField({
      name: newSlug.value.trim(),
      label: newLabel.value.trim() || null,
      description: newDescription.value.trim() || null,
      valueType: 'keyword',
    });
    flash.value = t('siemCenter.settings.fields.customSaved');
    newSlug.value = '';
    newLabel.value = '';
    newDescription.value = '';
    await load();
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  saving.value = true;
  error.value = null;
  try {
    await deleteSecEventCustomField(deleteTarget.value.name);
    flash.value = t('siemCenter.settings.fields.customDeleted');
    deleteTarget.value = null;
    await load();
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

onMounted(() => {
  void load();
});
</script>

<template>
  <div class="siem-settings-fields">
    <p class="text-body-2 text-medium-emphasis mb-2">
      {{ t('siemCenter.settings.fields.manageHint') }}
    </p>
    <p class="text-caption text-medium-emphasis mb-4">
      {{ t('siemCenter.settings.fields.manageHintDetail') }}
    </p>

    <v-sheet border rounded class="pa-3 mb-4">
      <div class="text-subtitle-2 mb-2">{{ t('siemCenter.settings.fields.addCustomTitle') }}</div>
      <p class="text-caption text-medium-emphasis mb-3">
        {{ t('siemCenter.settings.fields.addCustomHint') }}
      </p>
      <div class="d-flex flex-wrap ga-2 align-start">
        <v-text-field
          v-model="newSlug"
          density="compact"
          hide-details="auto"
          :label="t('siemCenter.settings.fields.customSlug')"
          :hint="t('siemCenter.settings.fields.customSlugHint')"
          persistent-hint
          style="max-width: 220px"
        />
        <v-text-field
          v-model="newLabel"
          density="compact"
          hide-details
          :label="t('siemCenter.settings.fields.customLabel')"
          style="max-width: 200px"
        />
        <v-text-field
          v-model="newDescription"
          density="compact"
          hide-details
          :label="t('siemCenter.settings.fields.customDescription')"
          class="flex-grow-1"
          style="min-width: 200px"
        />
        <v-btn
          color="primary"
          variant="tonal"
          :loading="saving"
          :disabled="!newSlug.trim()"
          @click="addCustom"
        >
          {{ t('siemCenter.settings.fields.addCustom') }}
        </v-btn>
      </div>
    </v-sheet>

    <div class="d-flex flex-wrap align-center ga-2 mb-3">
      <v-text-field
        v-model="search"
        density="compact"
        hide-details
        clearable
        prepend-inner-icon="mdi-magnify"
        :label="t('siemCenter.settings.fields.search')"
        style="min-width: 14rem; max-width: 22rem"
      />
      <v-select
        v-model="groupFilter"
        :items="groupItems"
        item-title="title"
        item-value="value"
        density="compact"
        hide-details
        :label="t('siemCenter.settings.fields.group')"
        style="max-width: 11rem"
      />
      <v-select
        v-model="kindFilter"
        :items="kindFilterItems"
        item-title="title"
        item-value="value"
        density="compact"
        hide-details
        :label="t('siemCenter.settings.fields.colKind')"
        style="max-width: 10rem"
      />
      <v-spacer />
      <v-chip v-if="version" size="small" variant="tonal">
        {{ t('siemCenter.settings.fields.version') }}: {{ version }}
      </v-chip>
      <span class="text-caption text-medium-emphasis">
        {{
          t('siemCenter.settings.fields.filterCount', {
            shown: filteredRows.length,
            total: fields.length,
          })
        }}
      </span>
      <v-btn
        size="small"
        variant="tonal"
        prepend-icon="mdi-refresh"
        :loading="loading"
        @click="load"
      >
        {{ t('siemCenter.settings.fields.refresh') }}
      </v-btn>
    </div>

    <v-alert v-if="flash" type="success" variant="tonal" density="compact" class="mb-3" closable>
      {{ flash }}
    </v-alert>
    <v-alert v-if="error" type="error" variant="tonal" class="mb-4">
      {{ error.startsWith('http') || error.includes(' ') ? error : t('siemCenter.settings.fields.loadError') }}
      <div class="text-caption mt-1">{{ error }}</div>
    </v-alert>

    <v-skeleton-loader v-if="loading && !fields.length" type="table" />

    <v-data-table
      v-else
      v-model:page="listPage"
      v-model:items-per-page="listItemsPerPage"
      :headers="listHeaders"
      :items="filteredRows"
      item-value="name"
      density="compact"
      class="mb-2 fields-catalog-table"
      :items-per-page-options="LIST_PAGE_SIZE_OPTIONS"
      :loading="loading"
      :no-data-text="
        fields.length
          ? t('siemCenter.settings.fields.emptyFilter')
          : t('siemCenter.settings.fields.empty')
      "
    >
      <template #item._nameSort="{ item }">
        <div class="text-body-2">{{ item.label || item.name }}</div>
        <div class="text-caption font-mono text-medium-emphasis">{{ item.name }}</div>
        <div v-if="item.description" class="text-caption text-medium-emphasis mt-1">
          {{ item.description }}
        </div>
      </template>
      <template #item._groupLabel="{ item }">
        <v-chip size="x-small" variant="tonal">{{ item._groupLabel }}</v-chip>
      </template>
      <template #item.valueType="{ item }">
        <code class="text-caption">{{ item.valueType }}</code>
      </template>
      <template #item._extractSort="{ item }">
        <div class="d-flex flex-wrap ga-1">
          <v-chip
            v-for="x in item.extractTypes"
            :key="item.name + '-e-' + x"
            size="x-small"
            variant="outlined"
          >
            {{ x }}
          </v-chip>
          <span v-if="!item.extractTypes.length" class="text-caption">—</span>
        </div>
      </template>
      <template #item.queryOperators="{ item }">
        <div class="d-flex flex-wrap ga-1">
          <v-chip
            v-for="op in item.queryOperators"
            :key="item.name + '-q-' + op"
            size="x-small"
            color="primary"
            variant="tonal"
          >
            {{ op }}
          </v-chip>
          <span v-if="!item.queryOperators.length" class="text-caption">—</span>
        </div>
      </template>
      <template #item._flagsSort="{ item }">
        <div class="d-flex flex-wrap ga-1">
          <v-chip
            v-if="item.isCustom"
            size="x-small"
            color="warning"
            variant="tonal"
          >
            {{ t('siemCenter.settings.fields.flagCustom') }}
          </v-chip>
          <v-chip
            v-if="item.queryable"
            size="x-small"
            color="success"
            variant="tonal"
          >
            {{ t('siemCenter.settings.fields.flagQueryable') }}
          </v-chip>
          <v-chip
            v-if="item.wizardSelectable"
            size="x-small"
            color="info"
            variant="tonal"
          >
            {{ t('siemCenter.settings.fields.flagWizard') }}
          </v-chip>
        </div>
      </template>
      <template #item.actions="{ item }">
        <v-btn
          v-if="item.isCustom"
          icon="mdi-delete-outline"
          size="small"
          variant="text"
          color="error"
          @click="deleteTarget = item"
        />
      </template>
    </v-data-table>

    <v-dialog :model-value="!!deleteTarget" max-width="420" @update:model-value="(v) => { if (!v) deleteTarget = null; }">
      <v-card v-if="deleteTarget">
        <v-card-title>{{ t('siemCenter.settings.fields.deleteTitle') }}</v-card-title>
        <v-card-text>
          {{ t('siemCenter.settings.fields.deleteBody', { name: deleteTarget.name }) }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteTarget = null">
            {{ t('siemCenter.settings.fields.cancel') }}
          </v-btn>
          <v-btn color="error" variant="flat" :loading="saving" @click="confirmDelete">
            {{ t('siemCenter.settings.fields.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.font-mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}
.fields-catalog-table :deep(td) {
  vertical-align: middle;
}
</style>
