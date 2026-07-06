<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import DiResourceTree from '@/components/apps/document-intelligence/DiResourceTree.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { diGetBootstrap, diGetRecent, diSearch } from '@/services/documentIntelligenceService';
import {
  DI_RESOURCE_TYPE,
  type DiResource,
  type DiTreeNode,
} from '@/types/apps/documentIntelligence';
import { diPageResourceIcon, diPageResourceLabel } from '@/utils/diPageResource';

const props = withDefaults(
  defineProps<{
    modelValue: boolean;
    /** Yalnızca markdown seçilebilir (varsayılan: markdown + dosya). */
    markdownOnly?: boolean;
    excludeResourceIds?: string[];
  }>(),
  { markdownOnly: false, excludeResourceIds: () => [] }
);

const emit = defineEmits<{
  'update:modelValue': [boolean];
  pick: [DiResource];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const selectedFolderId = ref<string | null>(null);
const tree = ref<DiTreeNode[]>([]);
const children = ref<DiResource[]>([]);
const treeLoading = ref(false);
const childrenLoading = ref(false);
const errorLocal = ref<string | null>(null);
const selectedResourceId = ref<string | null>(null);
const searchQuery = ref('');
const searchLoading = ref(false);
const searchResults = ref<DiResource[]>([]);
const recentPages = ref<DiResource[]>([]);

const excludedSet = computed(() => {
  const ids = new Set<string>();
  for (const id of props.excludeResourceIds ?? []) {
    const v = id?.trim().toLowerCase();
    if (v) ids.add(v);
  }
  return ids;
});

function isPickable(r: DiResource): boolean {
  if (excludedSet.value.has(r.id.trim().toLowerCase())) return false;
  if (props.markdownOnly) return r.type === DI_RESOURCE_TYPE.markdown;
  return r.type === DI_RESOURCE_TYPE.markdown || r.type === DI_RESOURCE_TYPE.file;
}

const pickableChildren = computed(() => children.value.filter(isPickable));

const displayedResources = computed(() => {
  const q = searchQuery.value.trim();
  if (q.length >= 2) return searchResults.value.filter(isPickable);
  return pickableChildren.value;
});

const selectedResource = computed(
  () => displayedResources.value.find((r) => r.id === selectedResourceId.value) ?? null
);

function resourceLabel(r: DiResource): string {
  return diPageResourceLabel(r);
}

async function loadRecent() {
  try {
    const res = await diGetRecent(8);
    recentPages.value = res.items.filter(isPickable);
  } catch {
    recentPages.value = [];
  }
}

async function runSearch() {
  const q = searchQuery.value.trim();
  if (q.length < 2) {
    searchResults.value = [];
    return;
  }
  searchLoading.value = true;
  errorLocal.value = null;
  try {
    const res = await diSearch(q, 0, 30);
    searchResults.value = res.items.filter(isPickable);
    selectedResourceId.value = null;
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'documentIntelligence.internalLink.searchError');
    searchResults.value = [];
  } finally {
    searchLoading.value = false;
  }
}

async function loadTree() {
  treeLoading.value = true;
  childrenLoading.value = true;
  errorLocal.value = null;
  try {
    const boot = await diGetBootstrap(null);
    tree.value = boot.tree;
    children.value = boot.children.items;
    selectedFolderId.value = null;
    selectedResourceId.value = null;
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'documentIntelligence.internalLink.loadTreeError');
  } finally {
    treeLoading.value = false;
    childrenLoading.value = false;
  }
}

async function selectFolder(folderId: string | null) {
  selectedFolderId.value = folderId;
  selectedResourceId.value = null;
  searchQuery.value = '';
  searchResults.value = [];
  childrenLoading.value = true;
  errorLocal.value = null;
  try {
    const boot = await diGetBootstrap(folderId);
    children.value = boot.children.items;
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'documentIntelligence.internalLink.loadTreeError');
    children.value = [];
  } finally {
    childrenLoading.value = false;
  }
}

function pickRecent(r: DiResource) {
  emit('pick', r);
  open.value = false;
}

function confirmPick() {
  const r = selectedResource.value;
  if (!r) return;
  emit('pick', r);
  open.value = false;
}

let searchDebounce: ReturnType<typeof setTimeout> | null = null;
watch(searchQuery, () => {
  if (searchDebounce) clearTimeout(searchDebounce);
  searchDebounce = setTimeout(() => void runSearch(), 300);
});

watch(open, (v) => {
  if (!v) {
    selectedResourceId.value = null;
    errorLocal.value = null;
    searchQuery.value = '';
    searchResults.value = [];
    return;
  }
  void loadTree();
  void loadRecent();
});
</script>

<template>
  <v-dialog v-model="open" max-width="720" scrollable>
    <v-card rounded="lg">
      <v-card-title class="text-h6 font-weight-bold">
        {{ t('documentIntelligence.internalLink.pickTitle') }}
      </v-card-title>
      <v-card-subtitle class="text-wrap pb-2">
        {{ t('documentIntelligence.internalLink.pickHint') }}
      </v-card-subtitle>
      <v-divider />
      <v-card-text class="pa-4">
        <v-text-field
          v-model="searchQuery"
          :label="t('documentIntelligence.internalLink.searchLabel')"
          :placeholder="t('documentIntelligence.internalLink.searchPlaceholder')"
          variant="outlined"
          density="compact"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          class="mb-3"
        />

        <div v-if="!searchQuery.trim() && recentPages.length" class="mb-3">
          <div class="text-caption text-medium-emphasis mb-2">
            {{ t('documentIntelligence.internalLink.recentPages') }}
          </div>
          <div class="d-flex flex-wrap ga-2">
            <v-chip
              v-for="r in recentPages"
              :key="r.id"
              size="small"
              variant="tonal"
              prepend-icon="mdi-book-open-page-variant-outline"
              @click="pickRecent(r)"
            >
              {{ resourceLabel(r) }}
            </v-chip>
          </div>
        </div>

        <v-alert v-if="errorLocal" type="error" variant="tonal" density="compact" class="mb-3 rounded-lg">
          {{ errorLocal }}
        </v-alert>

        <div v-if="searchQuery.trim().length >= 2" class="flex-grow-1">
          <v-progress-linear v-if="searchLoading" indeterminate color="primary" class="mb-2" />
          <div v-if="!searchLoading && !displayedResources.length" class="text-body-2 text-medium-emphasis py-4">
            {{ t('documentIntelligence.internalLink.searchEmpty') }}
          </div>
          <v-list v-else density="compact" class="py-0">
            <v-list-item
              v-for="r in displayedResources"
              :key="r.id"
              :active="selectedResourceId === r.id"
              rounded="lg"
              @click="selectedResourceId = r.id"
              @dblclick="confirmPick"
            >
              <template #prepend>
                <v-icon :icon="diPageResourceIcon(r)" size="20" />
              </template>
              <v-list-item-title>{{ resourceLabel(r) }}</v-list-item-title>
            </v-list-item>
          </v-list>
        </div>

        <div v-else class="d-flex ga-3 di-pick-layout">
          <div class="di-pick-tree flex-shrink-0">
            <v-progress-linear v-if="treeLoading" indeterminate color="primary" class="mb-2" />
            <DiResourceTree
              :nodes="tree"
              :selected-id="selectedFolderId"
              :root-label="t('documentIntelligence.allDocuments')"
              :empty-label="t('documentIntelligence.noFolders')"
              @select="selectFolder"
            />
          </div>
          <div class="flex-grow-1">
            <v-progress-linear v-if="childrenLoading" indeterminate color="primary" class="mb-2" />
            <div v-if="!childrenLoading && !displayedResources.length" class="text-body-2 text-medium-emphasis py-4">
              {{ t('documentIntelligence.internalLink.noPickable') }}
            </div>
            <v-list v-else density="compact" class="py-0">
              <v-list-item
                v-for="r in displayedResources"
                :key="r.id"
                :active="selectedResourceId === r.id"
                rounded="lg"
                @click="selectedResourceId = r.id"
                @dblclick="confirmPick"
              >
                <template #prepend>
                  <v-icon
                    :icon="r.type === 'markdown' ? diPageResourceIcon(r) : 'mdi-file-outline'"
                    size="20"
                  />
                </template>
                <v-list-item-title>{{ resourceLabel(r) }}</v-list-item-title>
                <v-list-item-subtitle class="text-caption">{{ r.type }}</v-list-item-subtitle>
              </v-list-item>
            </v-list>
          </div>
        </div>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" class="text-none" @click="open = false">
          {{ t('documentIntelligence.cancel') }}
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          class="text-none"
          :disabled="!selectedResource"
          @click="confirmPick"
        >
          {{ t('documentIntelligence.internalLink.pickConfirm') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.di-pick-layout {
  min-height: 280px;
}
.di-pick-tree {
  width: 220px;
  max-height: 320px;
  overflow: auto;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 8px;
  padding: 8px;
}
</style>
