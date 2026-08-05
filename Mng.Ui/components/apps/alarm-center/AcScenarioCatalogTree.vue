<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';
import type { ScenarioCatalogItem } from '@/types/apps/scenario';
import {
  PRODUCT_ROOT_ID,
  USER_ROOT_ID,
  childFoldersOf,
  createScenarioCatalogFolder,
  deleteScenarioCatalogFolder,
  loadScenarioCatalogFolders,
  placeScenarioInFolder,
  renameScenarioCatalogFolder,
  rootIdFor,
  saveScenarioCatalogFolders,
  toggleScenarioCatalogExpanded,
  type ScenarioCatalogFolder,
  type ScenarioCatalogFolderState,
  type ScenarioCatalogRoot,
} from '@/utils/alarm/scenarioCatalogFolders';

type CatalogRow =
  | {
    kind: 'group';
    id: string;
    root: ScenarioCatalogRoot;
    title: string;
    depth: number;
    count: number;
    expandable: true;
  }
  | {
    kind: 'item';
    id: string;
    root: ScenarioCatalogRoot;
    item: ScenarioCatalogItem;
    depth: number;
  };

const props = defineProps<{
  productItems: ScenarioCatalogItem[];
  userItems: ScenarioCatalogItem[];
  activeScenarioId: string | null;
  catalogLoaded: boolean;
}>();

const emit = defineEmits<{
  open: [item: ScenarioCatalogItem];
  clone: [item: ScenarioCatalogItem];
  'create-scenario': [payload: { folderId: string | null; root: ScenarioCatalogRoot }];
}>();

const { t } = useAppI18n();
const auth = useAuthStore();

const folderState = ref<ScenarioCatalogFolderState>(
  loadScenarioCatalogFolders(auth.domainName || 'odak'),
);
const selectedFolderId = ref<string>(USER_ROOT_ID);
const categoryDialog = ref(false);
const renameDialog = ref(false);
const categoryName = ref('');
const categoryParentId = ref<string>(USER_ROOT_ID);
const categoryRoot = ref<ScenarioCatalogRoot>('user');
const renameTargetId = ref<string | null>(null);
const folderError = ref('');

watch(
  () => auth.domainName,
  (domain) => {
    folderState.value = loadScenarioCatalogFolders(domain || 'odak');
  },
);

watch(
  folderState,
  (state) => {
    saveScenarioCatalogFolders(auth.domainName || 'odak', state);
  },
  { deep: true },
);

function isExpanded(id: string): boolean {
  return folderState.value.expandedIds.includes(id);
}

function itemsFor(root: ScenarioCatalogRoot): ScenarioCatalogItem[] {
  return root === 'product' ? props.productItems : props.userItems;
}

function itemsInFolder(
  root: ScenarioCatalogRoot,
  folderId: string | null,
): ScenarioCatalogItem[] {
  const items = itemsFor(root);
  return items.filter((item) => {
    const placement = folderState.value.placements[item.scenarioId];
    if (!folderId) {
      if (!placement) return true;
      const folder = folderState.value.folders.find(f => f.id === placement);
      return !folder || folder.root !== root;
    }
    return placement === folderId;
  });
}

function countInSubtree(root: ScenarioCatalogRoot, folderId: string | null): number {
  let total = itemsInFolder(root, folderId).length;
  for (const child of childFoldersOf(folderState.value, root, folderId)) {
    total += countInSubtree(root, child.id);
  }
  return total;
}

function appendFolderRows(
  rows: CatalogRow[],
  root: ScenarioCatalogRoot,
  parentId: string | null,
  depth: number,
) {
  for (const folder of childFoldersOf(folderState.value, root, parentId)) {
    rows.push({
      kind: 'group',
      id: folder.id,
      root,
      title: folder.name,
      depth,
      count: countInSubtree(root, folder.id),
      expandable: true,
    });
    if (!isExpanded(folder.id)) continue;
    appendFolderRows(rows, root, folder.id, depth + 1);
    for (const item of itemsInFolder(root, folder.id)) {
      rows.push({
        kind: 'item',
        id: item.scenarioId,
        root,
        item,
        depth: depth + 1,
      });
    }
  }
}

const rows = computed<CatalogRow[]>(() => {
  const result: CatalogRow[] = [];
  const roots: Array<{ id: string; root: ScenarioCatalogRoot; title: string }> = [
    {
      id: PRODUCT_ROOT_ID,
      root: 'product',
      title: t('alarmCenter.scenarioStudio.catalog.templateTitle'),
    },
    {
      id: USER_ROOT_ID,
      root: 'user',
      title: t('alarmCenter.scenarioStudio.catalog.userTitle'),
    },
  ];

  for (const root of roots) {
    result.push({
      kind: 'group',
      id: root.id,
      root: root.root,
      title: root.title,
      depth: 0,
      count: itemsFor(root.root).length,
      expandable: true,
    });
    if (!isExpanded(root.id)) continue;
    appendFolderRows(result, root.root, null, 1);
    for (const item of itemsInFolder(root.root, null)) {
      result.push({
        kind: 'item',
        id: item.scenarioId,
        root: root.root,
        item,
        depth: 1,
      });
    }
  }
  return result;
});

const showEmptyHints = computed(() => ({
  product: props.catalogLoaded
    && !props.productItems.length
    && !childFoldersOf(folderState.value, 'product', null).length
    && isExpanded(PRODUCT_ROOT_ID),
  user: props.catalogLoaded
    && !props.userItems.length
    && !childFoldersOf(folderState.value, 'user', null).length
    && isExpanded(USER_ROOT_ID),
}));

function toggle(id: string) {
  folderState.value = toggleScenarioCatalogExpanded(folderState.value, id);
}

function selectFolder(id: string, root: ScenarioCatalogRoot) {
  selectedFolderId.value = id;
  categoryRoot.value = root;
}

function onGroupClick(row: Extract<CatalogRow, { kind: 'group' }>) {
  toggle(row.id);
  selectFolder(row.id, row.root);
}

function openCreateCategory(parentId: string, root: ScenarioCatalogRoot) {
  categoryParentId.value = parentId;
  categoryRoot.value = root;
  categoryName.value = '';
  folderError.value = '';
  categoryDialog.value = true;
  selectedFolderId.value = parentId;
}

function confirmCreateCategory() {
  folderError.value = '';
  if (!categoryName.value.trim()) {
    folderError.value = t('alarmCenter.scenarioStudio.catalog.categoryNameRequired');
    return;
  }
  const parentId = categoryParentId.value.startsWith('root:')
    ? null
    : categoryParentId.value;
  folderState.value = createScenarioCatalogFolder(folderState.value, {
    name: categoryName.value,
    root: categoryRoot.value,
    parentId,
  });
  categoryDialog.value = false;
}

function findFolder(id: string): ScenarioCatalogFolder | undefined {
  return folderState.value.folders.find(f => f.id === id);
}

function openRename(folderId: string) {
  const folder = findFolder(folderId);
  if (!folder) return;
  renameTargetId.value = folder.id;
  categoryName.value = folder.name;
  folderError.value = '';
  renameDialog.value = true;
}

function confirmRename() {
  if (!renameTargetId.value) return;
  folderError.value = '';
  if (!categoryName.value.trim()) {
    folderError.value = t('alarmCenter.scenarioStudio.catalog.categoryNameRequired');
    return;
  }
  folderState.value = renameScenarioCatalogFolder(
    folderState.value,
    renameTargetId.value,
    categoryName.value,
  );
  renameDialog.value = false;
}

function removeFolder(folderId: string) {
  const folder = findFolder(folderId);
  if (!folder) return;
  folderError.value = '';
  const next = deleteScenarioCatalogFolder(folderState.value, folder.id);
  if (!next) {
    folderError.value = t('alarmCenter.scenarioStudio.catalog.categoryDeleteBlocked');
    return;
  }
  folderState.value = next;
  if (selectedFolderId.value === folder.id) {
    selectedFolderId.value = rootIdFor(folder.root);
  }
}

function moveItem(item: ScenarioCatalogItem, folderId: string | null) {
  folderState.value = placeScenarioInFolder(
    folderState.value,
    item.scenarioId,
    folderId,
  );
}

function requestNewScenario(folderId: string, root: ScenarioCatalogRoot) {
  if (root !== 'user') return;
  selectFolder(folderId, root);
  if (!isExpanded(folderId) && folderId.startsWith('root:')) {
    toggle(folderId);
  } else if (!folderId.startsWith('root:') && !isExpanded(folderId)) {
    toggle(folderId);
  }
  emit('create-scenario', {
    folderId: folderId.startsWith('root:') ? null : folderId,
    root,
  });
}

function folderOptions(root: ScenarioCatalogRoot): { id: string | null; title: string }[] {
  const rootTitle = root === 'product'
    ? t('alarmCenter.scenarioStudio.catalog.templateTitle')
    : t('alarmCenter.scenarioStudio.catalog.userTitle');
  const options: { id: string | null; title: string }[] = [
    {
      id: null,
      title: t('alarmCenter.scenarioStudio.catalog.rootOf', { name: rootTitle }),
    },
  ];
  const walk = (parentId: string | null, depth: number) => {
    for (const folder of childFoldersOf(folderState.value, root, parentId)) {
      options.push({ id: folder.id, title: `${'— '.repeat(depth)}${folder.name}` });
      walk(folder.id, depth + 1);
    }
  };
  walk(null, 1);
  return options;
}

defineExpose({
  selectedFolderId,
  selectedRoot: categoryRoot,
});
</script>

<template>
  <div class="catalog-tree">
    <v-alert
      v-if="folderError"
      type="warning"
      variant="tonal"
      density="compact"
      class="ma-2"
      closable
      @click:close="folderError = ''"
    >
      {{ folderError }}
    </v-alert>

    <template v-for="row in rows" :key="`${row.kind}:${row.id}`">
      <div
        v-if="row.kind === 'group'"
        class="catalog-group"
        :class="{ selected: selectedFolderId === row.id }"
        :style="{ paddingLeft: `${8 + row.depth * 12}px` }"
      >
        <button
          type="button"
          class="catalog-group__toggle"
          @click="onGroupClick(row)"
        >
          <v-icon
            :icon="isExpanded(row.id) ? 'mdi-chevron-down' : 'mdi-chevron-right'"
            size="18"
          />
          <v-icon
            :icon="row.depth === 0
              ? (row.root === 'product' ? 'mdi-package-variant-closed' : 'mdi-folder-account-outline')
              : 'mdi-folder-outline'"
            size="16"
            class="ms-1"
          />
          <strong>{{ row.title }}</strong>
          <small>{{ row.count }}</small>
        </button>

        <v-menu location="bottom end">
          <template #activator="{ props: menuProps }">
            <v-btn
              v-bind="menuProps"
              icon="mdi-dots-vertical"
              size="x-small"
              variant="text"
              :title="t('alarmCenter.scenarioStudio.catalog.folderMenu')"
              @click.stop
            />
          </template>
          <v-list density="compact">
            <v-list-item
              v-if="row.root === 'user'"
              prepend-icon="mdi-plus"
              :title="t('alarmCenter.scenarioStudio.catalog.newScenario')"
              @click="requestNewScenario(row.id, row.root)"
            />
            <v-list-item
              prepend-icon="mdi-folder-plus-outline"
              :title="t('alarmCenter.scenarioStudio.catalog.newCategory')"
              @click="openCreateCategory(row.id, row.root)"
            />
            <template v-if="row.depth > 0">
              <v-divider class="my-1" />
              <v-list-item
                prepend-icon="mdi-pencil-outline"
                :title="t('alarmCenter.scenarioStudio.catalog.renameCategory')"
                @click="openRename(row.id)"
              />
              <v-list-item
                prepend-icon="mdi-delete-outline"
                :title="t('alarmCenter.scenarioStudio.catalog.deleteCategory')"
                @click="removeFolder(row.id)"
              />
            </template>
          </v-list>
        </v-menu>
      </div>

      <button
        v-else
        type="button"
        class="catalog-item"
        :class="{ active: activeScenarioId === row.item.scenarioId }"
        :style="{ paddingLeft: `${8 + row.depth * 12}px` }"
        @click="emit('open', row.item)"
      >
        <v-icon
          :icon="row.root === 'product' ? 'mdi-file-lock-outline' : 'mdi-vector-polyline-edit'"
          size="18"
        />
        <span>
          <strong>{{ row.item.name }}</strong>
          <small v-if="row.root === 'product'">
            {{ row.item.templateId || row.item.scenarioId }}
          </small>
          <small v-else>
            v{{ row.item.latestVersion }} · {{ row.item.latestStatus }}
          </small>
        </span>
        <v-menu location="bottom end">
          <template #activator="{ props: menuProps }">
            <v-btn
              v-bind="menuProps"
              icon="mdi-dots-vertical"
              size="x-small"
              variant="text"
              @click.stop
            />
          </template>
          <v-list density="compact">
            <v-list-item
              v-if="row.root === 'product'"
              prepend-icon="mdi-content-copy"
              :title="t('alarmCenter.scenarioStudio.catalog.clone')"
              @click="emit('clone', row.item)"
            />
            <v-list-subheader>
              {{ t('alarmCenter.scenarioStudio.catalog.moveTo') }}
            </v-list-subheader>
            <v-list-item
              v-for="opt in folderOptions(row.root)"
              :key="`${row.item.scenarioId}:${String(opt.id)}`"
              :title="opt.title"
              @click="moveItem(row.item, opt.id)"
            />
          </v-list>
        </v-menu>
      </button>
    </template>

    <div v-if="showEmptyHints.product" class="empty-state">
      {{ t('alarmCenter.scenarioStudio.catalog.templateEmpty') }}
    </div>
    <div v-if="showEmptyHints.user" class="empty-state">
      {{ t('alarmCenter.scenarioStudio.catalog.userEmpty') }}
    </div>

    <v-dialog v-model="categoryDialog" max-width="420">
      <v-card>
        <v-card-title>{{ t('alarmCenter.scenarioStudio.catalog.newCategory') }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="categoryName"
            :label="t('alarmCenter.scenarioStudio.catalog.categoryName')"
            density="compact"
            autofocus
            @keyup.enter="confirmCreateCategory"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="categoryDialog = false">
            {{ t('alarmCenter.flowLab.cancel') }}
          </v-btn>
          <v-btn color="primary" @click="confirmCreateCategory">
            {{ t('alarmCenter.flowLab.create') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="renameDialog" max-width="420">
      <v-card>
        <v-card-title>{{ t('alarmCenter.scenarioStudio.catalog.renameCategory') }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="categoryName"
            :label="t('alarmCenter.scenarioStudio.catalog.categoryName')"
            density="compact"
            autofocus
            @keyup.enter="confirmRename"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="renameDialog = false">
            {{ t('alarmCenter.flowLab.cancel') }}
          </v-btn>
          <v-btn color="primary" @click="confirmRename">
            {{ t('alarmCenter.scenarioStudio.catalog.rename') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.catalog-tree {
  min-height: 0;
}

.catalog-group {
  display: flex;
  align-items: center;
  gap: 2px;
  min-height: 34px;
  padding-right: 4px;
  border-radius: 7px;
}

.catalog-group.selected {
  background: rgba(var(--v-theme-primary), 0.08);
}

.catalog-group__toggle {
  flex: 1;
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 4px;
  border: 0;
  background: transparent;
  color: inherit;
  text-align: left;
  cursor: pointer;
}

.catalog-group__toggle strong {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.02em;
}

.catalog-group__toggle small {
  flex: 0 0 auto;
  font-size: 0.65rem;
  opacity: 0.55;
}

.catalog-item {
  width: 100%;
  display: grid;
  grid-template-columns: 22px 1fr auto;
  align-items: center;
  gap: 7px;
  padding: 8px;
  border: 0;
  border-radius: 7px;
  background: transparent;
  color: inherit;
  text-align: left;
  cursor: pointer;
}

.catalog-item:hover,
.catalog-item.active {
  background: rgba(var(--v-theme-primary), 0.1);
}

.catalog-item span {
  min-width: 0;
  display: grid;
}

.catalog-item strong,
.catalog-item small {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.catalog-item strong {
  font-size: 0.76rem;
}

.catalog-item small,
.empty-state {
  font-size: 0.65rem;
  color: rgba(var(--v-theme-on-surface), 0.55);
}

.empty-state {
  padding: 10px;
}
</style>
