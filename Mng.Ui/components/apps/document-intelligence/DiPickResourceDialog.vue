<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import DiResourceTree from '@/components/apps/document-intelligence/DiResourceTree.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { diGetBootstrap } from '@/services/documentIntelligenceService';
import {
  DI_RESOURCE_TYPE,
  type DiResource,
  type DiTreeNode,
} from '@/types/apps/documentIntelligence';

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

const excludedSet = computed(() => {
  const ids = new Set<string>();
  for (const id of props.excludeResourceIds ?? []) {
    const v = id?.trim().toLowerCase();
    if (v) ids.add(v);
  }
  return ids;
});

const pickableChildren = computed(() =>
  children.value.filter((r) => {
    if (excludedSet.value.has(r.id.trim().toLowerCase())) return false;
    if (props.markdownOnly) return r.type === DI_RESOURCE_TYPE.markdown;
    return r.type === DI_RESOURCE_TYPE.markdown || r.type === DI_RESOURCE_TYPE.file;
  })
);

const selectedResource = computed(
  () => pickableChildren.value.find((r) => r.id === selectedResourceId.value) ?? null
);

function resourceLabel(r: DiResource): string {
  return r.type === DI_RESOURCE_TYPE.markdown ? r.title || r.name : r.name;
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

function confirmPick() {
  const r = selectedResource.value;
  if (!r) return;
  emit('pick', r);
  open.value = false;
}

watch(open, (v) => {
  if (!v) {
    selectedResourceId.value = null;
    errorLocal.value = null;
    return;
  }
  void loadTree();
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
        <v-alert v-if="errorLocal" type="error" variant="tonal" density="compact" class="mb-3 rounded-lg">
          {{ errorLocal }}
        </v-alert>

        <div class="d-flex ga-3 di-pick-layout">
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
            <div v-if="!childrenLoading && !pickableChildren.length" class="text-body-2 text-medium-emphasis py-4">
              {{ t('documentIntelligence.internalLink.noPickable') }}
            </div>
            <v-list v-else density="compact" class="py-0">
              <v-list-item
                v-for="r in pickableChildren"
                :key="r.id"
                :active="selectedResourceId === r.id"
                rounded="lg"
                @click="selectedResourceId = r.id"
                @dblclick="confirmPick"
              >
                <template #prepend>
                  <v-icon
                    :icon="r.type === 'markdown' ? 'mdi-language-markdown-outline' : 'mdi-file-outline'"
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
