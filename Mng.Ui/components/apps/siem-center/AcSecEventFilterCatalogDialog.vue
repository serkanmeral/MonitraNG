<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type {
  SecEventFilterCatalogState,
  SecEventFilterTreeNode,
  SecEventSavedFilter,
} from '@/types/apps/secEventFilterCatalog';
import {
  buildSecEventFilterTree,
  createUserCategory,
  defaultUserCategoryId,
  deleteUserFilter,
  findFilterById,
  loadSecEventFilterCatalog,
  upsertUserFilter,
} from '@/services/secEventFilterCatalogService';
import {
  cloneFilterAsUserCopy,
  createEmptyActiveFilter,
} from '@/utils/secEventFilterQueryMap';
import AcSecEventFilterCatalogTree from '@/components/apps/siem-center/AcSecEventFilterCatalogTree.vue';
import AcSecEventFilterEditor from '@/components/apps/siem-center/AcSecEventFilterEditor.vue';

const props = defineProps<{
  modelValue: boolean;
  /** Currently applied filter (for prefill). */
  initialFilter?: SecEventSavedFilter | null;
  initialFilterId?: string | null;
  hostOptions: string[];
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  apply: [payload: { filter: SecEventSavedFilter; filterId: string | null }];
}>();

const { t } = useAppI18n();

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const catalog = ref<SecEventFilterCatalogState>(loadSecEventFilterCatalog());
const treeSearch = ref('');
const selectedTreeId = ref<string | null>(null);
const selectedFilterId = ref<string | null>(null);
const draft = ref<SecEventSavedFilter>(createEmptyActiveFilter());
const baseline = ref('');

const saveAsOpen = ref(false);
const saveAsName = ref('');
const newCategoryOpen = ref(false);
const newCategoryName = ref('');

const treeNodes = computed(() => buildSecEventFilterTree(catalog.value));
const dirty = computed(() => serialize(draft.value) !== baseline.value);

function serialize(f: SecEventSavedFilter): string {
  return JSON.stringify({ scope: f.scope, fields: f.fields });
}

function cloneDraft(filter: SecEventSavedFilter, filterId: string | null) {
  draft.value = {
    ...filter,
    scope: {
      type: filter.scope?.type ?? null,
      product: filter.scope?.product ?? null,
      hosts: [...(filter.scope?.hosts ?? [])],
    },
    fields: filter.fields.map((x) => ({ ...x })),
  };
  selectedFilterId.value = filterId;
  baseline.value = serialize(draft.value);
  selectedTreeId.value = filterId ? `filter:${filterId}` : null;
}

watch(
  () => props.modelValue,
  (isOpen) => {
    if (!isOpen) return;
    catalog.value = loadSecEventFilterCatalog();
    treeSearch.value = '';
    if (props.initialFilterId) {
      const found = findFilterById(catalog.value, props.initialFilterId);
      if (found) {
        cloneDraft(found, found.id);
        return;
      }
    }
    if (props.initialFilter) {
      cloneDraft(props.initialFilter, props.initialFilterId ?? null);
      return;
    }
    cloneDraft(createEmptyActiveFilter(), null);
  },
);

function onTreeSelect(node: SecEventFilterTreeNode) {
  selectedTreeId.value = node.id;
  if (node.kind === 'filter' && node.filterId) {
    const found = findFilterById(catalog.value, node.filterId);
    if (found) cloneDraft(found, found.id);
  }
}

function onApply() {
  baseline.value = serialize(draft.value);
  emit('apply', {
    filter: {
      ...draft.value,
      scope: {
        type: draft.value.scope?.type ?? null,
        product: draft.value.scope?.product ?? null,
        hosts: [...(draft.value.scope?.hosts ?? [])],
      },
      fields: draft.value.fields.map((x) => ({ ...x })),
    },
    filterId: selectedFilterId.value,
  });
  open.value = false;
}

function onClear() {
  cloneDraft(createEmptyActiveFilter(), null);
}

function onSave() {
  const current = selectedFilterId.value
    ? findFilterById(catalog.value, selectedFilterId.value)
    : null;
  if (current?.isSystem || !current) {
    openSaveAs();
    return;
  }
  const updated: SecEventSavedFilter = {
    ...current,
    scope: draft.value.scope,
    fields: draft.value.fields,
  };
  catalog.value = upsertUserFilter(catalog.value, updated);
  cloneDraft(updated, updated.id);
}

function openSaveAs() {
  const base = draft.value.name?.trim() || t('siemCenter.events.filterCatalog.newFilterName');
  if (selectedFilterId.value) {
    const cur = findFilterById(catalog.value, selectedFilterId.value);
    if (cur?.isSystem) {
      saveAsName.value = `${base} (${t('siemCenter.events.filterCatalog.copySuffix')})`;
    } else {
      saveAsName.value = base;
    }
  } else {
    saveAsName.value = base;
  }
  saveAsOpen.value = true;
}

function confirmSaveAs() {
  const name = saveAsName.value.trim() || t('siemCenter.events.filterCatalog.newFilterName');
  const categoryId = defaultUserCategoryId(catalog.value);
  const copy = cloneFilterAsUserCopy(
    { ...draft.value, name, isSystem: false },
    categoryId,
    name,
  );
  catalog.value = upsertUserFilter(catalog.value, copy);
  cloneDraft(copy, copy.id);
  saveAsOpen.value = false;
}

function confirmNewCategory() {
  const name = newCategoryName.value.trim();
  if (!name) return;
  catalog.value = createUserCategory(catalog.value, name);
  newCategoryName.value = '';
  newCategoryOpen.value = false;
}

function onDeleteSelectedFilter() {
  if (!selectedFilterId.value) return;
  const cur = findFilterById(catalog.value, selectedFilterId.value);
  if (!cur || cur.isSystem) return;
  catalog.value = deleteUserFilter(catalog.value, cur.id);
  onClear();
}

function onCancel() {
  open.value = false;
}
</script>

<template>
  <v-dialog v-model="open" max-width="1100" scrollable>
    <v-card class="sec-filter-catalog-dialog">
      <v-card-title class="d-flex align-center ga-2 flex-wrap">
        <v-icon icon="mdi-filter-plus" />
        <span>{{ t('siemCenter.events.filterCatalog.dialogTitle') }}</span>
        <v-spacer />
        <v-btn
          v-if="selectedFilterId && !findFilterById(catalog, selectedFilterId)?.isSystem"
          size="small"
          variant="text"
          color="error"
          class="text-none"
          @click="onDeleteSelectedFilter"
        >
          {{ t('siemCenter.events.filterCatalog.deleteFilter') }}
        </v-btn>
      </v-card-title>
      <v-card-subtitle>
        {{ t('siemCenter.events.filterCatalog.dialogSubtitle') }}
      </v-card-subtitle>

      <v-card-text class="pa-0">
        <div class="d-flex sec-filter-catalog-dialog__body">
          <div class="sec-filter-catalog-dialog__tree flex-shrink-0">
            <div class="px-3 py-2 border-b">
              <v-btn
                block
                size="small"
                variant="tonal"
                color="primary"
                class="text-none"
                prepend-icon="mdi-folder-plus-outline"
                @click="newCategoryOpen = true"
              >
                {{ t('siemCenter.events.filterCatalog.newCategory') }}
              </v-btn>
            </div>
            <div class="pa-2 sec-filter-catalog-dialog__tree-scroll">
              <AcSecEventFilterCatalogTree
                v-model:search="treeSearch"
                :nodes="treeNodes"
                :selected-id="selectedTreeId"
                @select="onTreeSelect"
              />
            </div>
          </div>

          <div class="sec-filter-catalog-dialog__editor flex-grow-1 pa-4">
            <AcSecEventFilterEditor
              v-model="draft"
              :host-options="hostOptions"
              :dirty="dirty"
              :selected-filter-id="selectedFilterId"
              @apply="onApply"
              @save="onSave"
              @save-as="openSaveAs"
              @clear="onClear"
            />
          </div>
        </div>
      </v-card-text>

      <v-card-actions class="px-4 py-3">
        <v-spacer />
        <v-btn variant="text" class="text-none" @click="onCancel">
          {{ t('siemCenter.events.filterCatalog.cancel') }}
        </v-btn>
        <v-btn color="primary" class="text-none" prepend-icon="mdi-check" @click="onApply">
          {{ t('siemCenter.events.filterCatalog.apply') }}
        </v-btn>
      </v-card-actions>
    </v-card>

    <v-dialog v-model="saveAsOpen" max-width="420" persistent>
      <v-card>
        <v-card-title>{{ t('siemCenter.events.filterCatalog.saveAsTitle') }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="saveAsName"
            :label="t('siemCenter.events.filterCatalog.filterName')"
            variant="outlined"
            density="compact"
            autofocus
            @keyup.enter="confirmSaveAs"
          />
          <p class="text-caption text-medium-emphasis mb-0">
            {{ t('siemCenter.events.filterCatalog.saveAsHint') }}
          </p>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="saveAsOpen = false">
            {{ t('siemCenter.events.filterCatalog.cancel') }}
          </v-btn>
          <v-btn color="primary" class="text-none" @click="confirmSaveAs">
            {{ t('siemCenter.events.filterCatalog.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="newCategoryOpen" max-width="420" persistent>
      <v-card>
        <v-card-title>{{ t('siemCenter.events.filterCatalog.newCategory') }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="newCategoryName"
            :label="t('siemCenter.events.filterCatalog.categoryName')"
            variant="outlined"
            density="compact"
            autofocus
            @keyup.enter="confirmNewCategory"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="newCategoryOpen = false">
            {{ t('siemCenter.events.filterCatalog.cancel') }}
          </v-btn>
          <v-btn color="primary" class="text-none" @click="confirmNewCategory">
            {{ t('siemCenter.events.filterCatalog.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-dialog>
</template>

<style scoped>
.sec-filter-catalog-dialog__body {
  min-height: 420px;
  max-height: min(70vh, 640px);
}

.sec-filter-catalog-dialog__tree {
  width: 280px;
  border-right: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  display: flex;
  flex-direction: column;
  max-height: min(70vh, 640px);
}

.sec-filter-catalog-dialog__tree-scroll {
  overflow: auto;
  flex: 1;
}

.sec-filter-catalog-dialog__editor {
  overflow: auto;
  max-height: min(70vh, 640px);
}

.border-b {
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

@media (max-width: 960px) {
  .sec-filter-catalog-dialog__body {
    flex-direction: column;
  }

  .sec-filter-catalog-dialog__tree {
    width: 100%;
    max-height: 220px;
    border-right: 0;
    border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  }
}
</style>
