<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { diSearchTreeFolders } from '@/services/documentIntelligenceService';
import type { DiTreeNode } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  modelValue: string | null;
  /** Taşımada kendi alt ağacını hariç tut. */
  excludeSubtreeId?: string | null;
  loadChildren: (parentId: string | null) => Promise<DiTreeNode[]>;
  isLoading?: (parentId: string | null) => boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: string | null];
}>();

const { t } = useAppI18n();

type VisibleRow = {
  id: string;
  name: string;
  depth: number;
  hasChildren: boolean;
  node: DiTreeNode;
};

const rootNodes = ref<DiTreeNode[]>([]);
const expandedIds = ref<Set<string>>(new Set());
const loadingIds = ref<Set<string>>(new Set());
const searchQuery = ref('');
const searchResults = ref<DiTreeNode[]>([]);
const searchLoading = ref(false);

let searchTimer: ReturnType<typeof setTimeout> | null = null;

function isExcluded(id: string): boolean {
  return Boolean(props.excludeSubtreeId && id === props.excludeSubtreeId);
}

function isDescendantOfExcluded(node: DiTreeNode, nodes: DiTreeNode[]): boolean {
  if (!props.excludeSubtreeId) return false;
  let parentId = node.parentId;
  while (parentId) {
    if (parentId === props.excludeSubtreeId) return true;
    const parent = findNodeById(nodes, parentId);
    if (!parent) break;
    parentId = parent.parentId;
  }
  return false;
}

function findNodeById(nodes: DiTreeNode[], id: string): DiTreeNode | null {
  for (const node of nodes) {
    if (node.id === id) return node;
    const nested = findNodeById(node.children ?? [], id);
    if (nested) return nested;
  }
  return null;
}

function flattenVisible(nodes: DiTreeNode[], depth: number, out: VisibleRow[]) {
  for (const node of nodes) {
    if (isExcluded(node.id) || isDescendantOfExcluded(node, rootNodes.value)) continue;
    out.push({
      id: node.id,
      name: node.name,
      depth,
      hasChildren: node.hasChildren || (node.children?.length ?? 0) > 0,
      node,
    });
    if (expandedIds.value.has(node.id) && node.children?.length) {
      flattenVisible(node.children, depth + 1, out);
    }
  }
}

const visibleRows = computed(() => {
  const out: VisibleRow[] = [];
  flattenVisible(rootNodes.value, 0, out);
  return out;
});

const searchActive = computed(() => searchQuery.value.trim().length >= 2);

const searchRows = computed(() =>
  searchResults.value
    .filter((node) => !isExcluded(node.id))
    .map((node) => ({
      id: node.id,
      name: node.name,
      depth: 0,
      hasChildren: node.hasChildren,
      node,
    })),
);

const rowsToRender = computed(() => (searchActive.value ? searchRows.value : visibleRows.value));

async function ensureRoots() {
  if (rootNodes.value.length) return;
  if (props.isLoading?.(null) || loadingIds.value.has('__root__')) return;
  loadingIds.value.add('__root__');
  try {
    rootNodes.value = await props.loadChildren(null);
  } finally {
    loadingIds.value.delete('__root__');
  }
}

async function runFolderSearch(q: string) {
  const trimmed = q.trim();
  if (trimmed.length < 2) {
    searchResults.value = [];
    return;
  }
  searchLoading.value = true;
  try {
    searchResults.value = await diSearchTreeFolders(trimmed, 50);
  } catch {
    searchResults.value = [];
  } finally {
    searchLoading.value = false;
  }
}

watch(
  () => props.excludeSubtreeId,
  () => {
    if (props.excludeSubtreeId && props.modelValue === props.excludeSubtreeId) {
      emit('update:modelValue', null);
    }
  },
);

watch(searchQuery, (q) => {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => runFolderSearch(q), 300);
});

defineExpose({ ensureRoots, reload: ensureRoots });

async function toggleExpand(row: VisibleRow) {
  if (!row.hasChildren) return;
  const next = new Set(expandedIds.value);
  if (next.has(row.id)) {
    next.delete(row.id);
    expandedIds.value = next;
    return;
  }

  if (!(row.node.children?.length)) {
    loadingIds.value.add(row.id);
    try {
      row.node.children = await props.loadChildren(row.id);
    } finally {
      loadingIds.value.delete(row.id);
    }
  }

  next.add(row.id);
  expandedIds.value = next;
}

function select(id: string | null) {
  emit('update:modelValue', id);
}

function rowLoading(id: string | null): boolean {
  if (id === null) return props.isLoading?.(null) ?? loadingIds.value.has('__root__');
  return props.isLoading?.(id) ?? loadingIds.value.has(id);
}

ensureRoots();
</script>

<template>
  <div>
    <v-text-field
      v-model="searchQuery"
      :label="t('documentIntelligence.folderPicker.searchPlaceholder')"
      variant="outlined"
      density="compact"
      hide-details
      clearable
      prepend-inner-icon="mdi-magnify"
      class="mb-2"
    />

    <v-list density="compact" class="di-folder-picker-list border rounded-lg">
      <v-list-item
        :active="modelValue === null"
        prepend-icon="mdi-folder-home-outline"
        :title="t('documentIntelligence.allDocuments')"
        @click="select(null)"
      />

      <template v-if="searchLoading">
        <v-list-item>
          <v-progress-linear indeterminate color="primary" height="2" />
        </v-list-item>
      </template>

      <template v-else-if="searchActive && !rowsToRender.length">
        <v-list-item>
          <v-list-item-title class="text-caption text-medium-emphasis">
            {{ t('documentIntelligence.folderPicker.searchEmpty') }}
          </v-list-item-title>
        </v-list-item>
      </template>

      <template v-else-if="!searchActive && rowLoading(null) && !rootNodes.length">
        <v-list-item>
          <v-progress-linear indeterminate color="primary" height="2" />
        </v-list-item>
      </template>

      <v-list-item
        v-for="row in rowsToRender"
        :key="row.id"
        :active="modelValue === row.id"
        @click="select(row.id)"
      >
        <template #prepend>
          <span :style="{ display: 'inline-block', width: row.depth * 14 + 'px' }" />
          <v-btn
            v-if="!searchActive && row.hasChildren"
            icon
            size="x-small"
            variant="text"
            class="mr-1"
            :loading="rowLoading(row.id)"
            @click.stop="toggleExpand(row)"
          >
            <v-icon size="16">
              {{ expandedIds.has(row.id) ? 'mdi-chevron-down' : 'mdi-chevron-right' }}
            </v-icon>
          </v-btn>
          <span v-else-if="!searchActive" class="di-folder-picker-spacer mr-1" />
          <v-icon size="18" class="mr-2">mdi-folder-outline</v-icon>
        </template>
        <v-list-item-title>{{ row.name }}</v-list-item-title>
      </v-list-item>
    </v-list>
  </div>
</template>

<style scoped>
.di-folder-picker-list {
  max-height: 280px;
  overflow: auto;
}
.di-folder-picker-spacer {
  display: inline-block;
  width: 28px;
}
</style>
