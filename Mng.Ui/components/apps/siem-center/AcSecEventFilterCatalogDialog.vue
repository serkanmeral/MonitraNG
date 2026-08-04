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
  deleteUserCategory,
  deleteUserFilter,
  findCategoryById,
  findFilterById,
  listUserCategories,
  loadSecEventFilterCatalog,
  moveUserFilter,
  renameUserCategory,
  renameUserFilter,
  upsertUserFilter,
} from '@/services/secEventFilterCatalogService';
import {
  cloneFilterAsUserCopy,
  createEmptyActiveFilter,
} from '@/utils/secEventFilterQueryMap';
import AcSecEventFilterCatalogTree from '@/components/apps/siem-center/AcSecEventFilterCatalogTree.vue';
import AcSecEventFilterEditor from '@/components/apps/siem-center/AcSecEventFilterEditor.vue';
import type { DiscoveryHostDto } from '@/services/siemDiscoveryService';

const props = defineProps<{
  modelValue: boolean;
  initialFilter?: SecEventSavedFilter | null;
  initialFilterId?: string | null;
  hostOptions: string[];
  discoveryHosts?: DiscoveryHostDto[];
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
const saveAsCategoryId = ref('');
const newCategoryOpen = ref(false);
const newCategoryName = ref('');

const renameOpen = ref(false);
const renameKind = ref<'category' | 'filter'>('filter');
const renameTargetId = ref('');
const renameValue = ref('');

const moveOpen = ref(false);
const moveFilterId = ref('');
const moveTargetCategoryId = ref('');

const deleteCategoryOpen = ref(false);
const deleteCategoryId = ref('');
const deleteCategoryName = ref('');

const treeNodes = computed(() => buildSecEventFilterTree(catalog.value));
const dirty = computed(() => serialize(draft.value) !== baseline.value);
const userCategoryItems = computed(() =>
  listUserCategories(catalog.value).map((c) => ({ title: c.name, value: c.id })),
);

function serialize(f: SecEventSavedFilter): string {
  return JSON.stringify({ scope: f.scope, fields: f.fields, name: f.name });
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

function categoryIdFromTreeNode(node: SecEventFilterTreeNode): string | null {
  if (node.kind !== 'category') return null;
  return node.id.startsWith('category:') ? node.id.slice('category:'.length) : node.id;
}

function onTreeAction(payload: { action: string; node: SecEventFilterTreeNode }) {
  const { action, node } = payload;
  if (node.isSystem) return;

  if (action === 'rename-category') {
    const id = categoryIdFromTreeNode(node);
    if (!id) return;
    renameKind.value = 'category';
    renameTargetId.value = id;
    renameValue.value = node.name;
    renameOpen.value = true;
    return;
  }

  if (action === 'delete-category') {
    const id = categoryIdFromTreeNode(node);
    if (!id) return;
    deleteCategoryId.value = id;
    deleteCategoryName.value = node.name;
    deleteCategoryOpen.value = true;
    return;
  }

  if (action === 'rename-filter' && node.filterId) {
    renameKind.value = 'filter';
    renameTargetId.value = node.filterId;
    renameValue.value = node.name;
    renameOpen.value = true;
    return;
  }

  if (action === 'move-filter' && node.filterId) {
    const filter = findFilterById(catalog.value, node.filterId);
    if (!filter || filter.isSystem) return;
    moveFilterId.value = node.filterId;
    moveTargetCategoryId.value =
      filter.categoryId && !findCategoryById(catalog.value, filter.categoryId)?.isSystem
        ? filter.categoryId
        : defaultUserCategoryId(catalog.value);
    moveOpen.value = true;
    return;
  }

  if (action === 'delete-filter' && node.filterId) {
    catalog.value = deleteUserFilter(catalog.value, node.filterId);
    if (selectedFilterId.value === node.filterId) onClear();
  }
}

function confirmRename() {
  const name = renameValue.value.trim();
  if (!name) return;
  if (renameKind.value === 'category') {
    catalog.value = renameUserCategory(catalog.value, renameTargetId.value, name);
  } else {
    catalog.value = renameUserFilter(catalog.value, renameTargetId.value, name);
    if (selectedFilterId.value === renameTargetId.value) {
      draft.value = { ...draft.value, name };
      baseline.value = serialize(draft.value);
    }
  }
  renameOpen.value = false;
}

function confirmMoveFilter() {
  if (!moveFilterId.value || !moveTargetCategoryId.value) return;
  catalog.value = moveUserFilter(catalog.value, moveFilterId.value, moveTargetCategoryId.value);
  moveOpen.value = false;
}

function confirmDeleteCategory() {
  if (!deleteCategoryId.value) return;
  catalog.value = deleteUserCategory(catalog.value, deleteCategoryId.value);
  deleteCategoryOpen.value = false;
  // If selected filter was rehomed, keep selection; tree refresh is reactive
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
    name: draft.value.name?.trim() || current.name,
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
  saveAsCategoryId.value = defaultUserCategoryId(catalog.value);
  saveAsOpen.value = true;
}

function confirmSaveAs() {
  const name = saveAsName.value.trim() || t('siemCenter.events.filterCatalog.newFilterName');
  const categoryId = saveAsCategoryId.value || defaultUserCategoryId(catalog.value);
  const target = findCategoryById(catalog.value, categoryId);
  const safeCategoryId = target && !target.isSystem ? categoryId : defaultUserCategoryId(catalog.value);
  const copy = cloneFilterAsUserCopy(
    { ...draft.value, name, isSystem: false },
    safeCategoryId,
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
                @action="onTreeAction"
              />
            </div>
          </div>

          <div class="sec-filter-catalog-dialog__editor flex-grow-1 pa-4">
            <AcSecEventFilterEditor
              v-model="draft"
              :host-options="hostOptions"
              :discovery-hosts="discoveryHosts"
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
            class="mb-3"
            @keyup.enter="confirmSaveAs"
          />
          <v-select
            v-model="saveAsCategoryId"
            :items="userCategoryItems"
            item-title="title"
            item-value="value"
            :label="t('siemCenter.events.filterCatalog.targetCategory')"
            variant="outlined"
            density="compact"
            hide-details
          />
          <p class="text-caption text-medium-emphasis mb-0 mt-2">
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

    <v-dialog v-model="renameOpen" max-width="420" persistent>
      <v-card>
        <v-card-title>
          {{
            renameKind === 'category'
              ? t('siemCenter.events.filterCatalog.renameCategory')
              : t('siemCenter.events.filterCatalog.renameFilter')
          }}
        </v-card-title>
        <v-card-text>
          <v-text-field
            v-model="renameValue"
            :label="
              renameKind === 'category'
                ? t('siemCenter.events.filterCatalog.categoryName')
                : t('siemCenter.events.filterCatalog.filterName')
            "
            variant="outlined"
            density="compact"
            autofocus
            @keyup.enter="confirmRename"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="renameOpen = false">
            {{ t('siemCenter.events.filterCatalog.cancel') }}
          </v-btn>
          <v-btn color="primary" class="text-none" @click="confirmRename">
            {{ t('siemCenter.events.filterCatalog.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="moveOpen" max-width="420" persistent>
      <v-card>
        <v-card-title>{{ t('siemCenter.events.filterCatalog.moveFilter') }}</v-card-title>
        <v-card-text>
          <v-select
            v-model="moveTargetCategoryId"
            :items="userCategoryItems"
            item-title="title"
            item-value="value"
            :label="t('siemCenter.events.filterCatalog.targetCategory')"
            variant="outlined"
            density="compact"
            hide-details
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="moveOpen = false">
            {{ t('siemCenter.events.filterCatalog.cancel') }}
          </v-btn>
          <v-btn color="primary" class="text-none" @click="confirmMoveFilter">
            {{ t('siemCenter.events.filterCatalog.move') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteCategoryOpen" max-width="460" persistent>
      <v-card>
        <v-card-title>{{ t('siemCenter.events.filterCatalog.deleteCategory') }}</v-card-title>
        <v-card-text>
          <p class="text-body-2 mb-0">
            {{ t('siemCenter.events.filterCatalog.deleteCategoryHint', { name: deleteCategoryName }) }}
          </p>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteCategoryOpen = false">
            {{ t('siemCenter.events.filterCatalog.cancel') }}
          </v-btn>
          <v-btn color="error" class="text-none" @click="confirmDeleteCategory">
            {{ t('siemCenter.events.filterCatalog.deleteCategory') }}
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
