<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import DiResourceTree from '@/components/apps/document-intelligence/DiResourceTree.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  diCreateDatasetRowLink,
  diCreateFileResource,
  diCreateFolder,
  diCreateResourceLink,
  diGetBootstrap,
  diGetById,
  diGetTreeChildren,
} from '@/services/documentIntelligenceService';
import {
  DI_WORK_ITEM_LINK_RELATION_TYPES,
  DI_RESOURCE_TYPE,
  type DiLinkRelationType,
  type DiResource,
  type DiTreeNode,
} from '@/types/apps/documentIntelligence';

const props = defineProps<{
  modelValue: boolean;
  workItemId?: string;
  datasetName?: string;
  dataId?: string;
  excludedResourceIds?: string[];
  relationTypes?: string[];
  /** Formun belge kökü. Doluysa ağaç bu klasörden başlar. */
  rootFolderId?: string | null;
  allowUpload?: boolean;
  allowCreateFolder?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  linked: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const relationType = ref<DiLinkRelationType>('reference');
const selectedFolderId = ref<string | null>(null);
const tree = ref<DiTreeNode[]>([]);
const children = ref<DiResource[]>([]);
const treeLoading = ref(false);
const childrenLoading = ref(false);
const submitting = ref(false);
const errorLocal = ref<string | null>(null);
const selectedResourceId = ref<string | null>(null);
const scopeLabel = ref('');
const folderName = ref('');
const creatingFolder = ref(false);
const uploading = ref(false);
const fileInput = ref<HTMLInputElement | null>(null);

const scopeRootId = computed(() => props.rootFolderId?.trim() || null);
const treeRootLabel = computed(() => scopeLabel.value || t('documentIntelligence.allDocuments'));
const currentParentId = computed(() => selectedFolderId.value || scopeRootId.value);

const relationItems = computed(() =>
  (props.relationTypes?.length ? props.relationTypes : DI_WORK_ITEM_LINK_RELATION_TYPES).map((value) => ({
    value,
    title: t(`operationCore.profile.documents.relationTypes.${value}`),
  }))
);

const excludedSet = computed(() => {
  const ids = new Set<string>();
  for (const id of props.excludedResourceIds ?? []) {
    const v = id?.trim().toLowerCase();
    if (v) ids.add(v);
  }
  return ids;
});

const linkableChildren = computed(() =>
  children.value.filter(
    (r) =>
      (r.type === DI_RESOURCE_TYPE.markdown || r.type === DI_RESOURCE_TYPE.file) &&
      !excludedSet.value.has(r.id.trim().toLowerCase())
  )
);

const selectedResource = computed(
  () => linkableChildren.value.find((r) => r.id === selectedResourceId.value) ?? null
);

function resourceLabel(r: DiResource): string {
  return r.type === DI_RESOURCE_TYPE.markdown ? r.title || r.name : r.name;
}

async function loadFolderFiles(folderId: string | null) {
  const boot = await diGetBootstrap(folderId);
  children.value = boot.children.items;
}

async function loadTree() {
  treeLoading.value = true;
  childrenLoading.value = true;
  errorLocal.value = null;
  try {
    const scope = scopeRootId.value;
    if (scope) {
      const folder = await diGetById(scope);
      scopeLabel.value = folder.name || folder.title || '';
      tree.value = await diGetTreeChildren(scope);
      await loadFolderFiles(scope);
    } else {
      scopeLabel.value = '';
      const boot = await diGetBootstrap(null);
      tree.value = boot.treeRoots.length ? boot.treeRoots : boot.tree;
      children.value = boot.children.items;
    }
    selectedFolderId.value = null;
    selectedResourceId.value = null;
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.profile.documents.loadTreeError');
  } finally {
    treeLoading.value = false;
    childrenLoading.value = false;
  }
}

async function selectFolder(folderId: string | null) {
  const target = folderId || scopeRootId.value;
  selectedFolderId.value = folderId;
  selectedResourceId.value = null;
  childrenLoading.value = true;
  errorLocal.value = null;
  try {
    await loadFolderFiles(target);
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.profile.documents.loadTreeError');
    children.value = [];
  } finally {
    childrenLoading.value = false;
  }
}

function loadChildren(parentId: string) {
  return diGetTreeChildren(parentId);
}

async function createFolder() {
  const name = folderName.value.trim();
  if (!name || props.allowCreateFolder === false) return;
  creatingFolder.value = true;
  errorLocal.value = null;
  try {
    const created = await diCreateFolder({
      name,
      parentId: currentParentId.value,
    });
    folderName.value = '';
    await loadTree();
    if (created.id) await selectFolder(created.id);
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.profile.documents.linkError');
  } finally {
    creatingFolder.value = false;
  }
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const value = String(reader.result || '');
      const comma = value.indexOf(',');
      resolve(comma >= 0 ? value.slice(comma + 1) : value);
    };
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}

async function uploadFile(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  if (!file || props.allowUpload === false) return;
  uploading.value = true;
  errorLocal.value = null;
  try {
    const content = await fileToBase64(file);
    const dot = file.name.lastIndexOf('.');
    const created = await diCreateFileResource({
      parentId: currentParentId.value,
      name: file.name,
      originalFileName: file.name,
      content,
      mimeType: file.type || null,
      extension: dot >= 0 ? file.name.slice(dot + 1).toLowerCase() : null,
      size: file.size,
    });
    await selectFolder(selectedFolderId.value);
    if (created.id) selectedResourceId.value = created.id;
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.profile.documents.linkError');
  } finally {
    uploading.value = false;
  }
}

async function submit() {
  const resourceId = selectedResourceId.value?.trim();
  if (!resourceId) return;

  const datasetName = props.datasetName?.trim() ?? '';
  const dataId = props.dataId?.trim() ?? '';
  const workItemId = props.workItemId?.trim() ?? '';
  if (!dataId && !workItemId) return;

  submitting.value = true;
  errorLocal.value = null;
  try {
    if (datasetName && dataId) {
      await diCreateDatasetRowLink({
        resourceId,
        datasetName,
        dataId,
        relationType: relationType.value,
      });
    } else {
      await diCreateResourceLink({
        resourceId,
        targetModule: 'operationCore',
        targetType: 'workItem',
        targetId: workItemId,
        relationType: relationType.value,
      });
    }
    emit('linked');
    open.value = false;
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.profile.documents.linkError');
  } finally {
    submitting.value = false;
  }
}

function defaultRelation(): DiLinkRelationType {
  const first = props.relationTypes?.[0];
  return (first as DiLinkRelationType) || 'reference';
}

watch(open, (v) => {
  relationType.value = defaultRelation();
  if (!v) {
    selectedResourceId.value = null;
    errorLocal.value = null;
    return;
  }
  loadTree();
});
</script>

<template>
  <v-dialog v-model="open" max-width="720" scrollable>
    <v-card rounded="lg">
      <v-card-title class="text-h6 font-weight-bold">
        {{ t('operationCore.profile.documents.linkAction') }}
      </v-card-title>
      <v-divider />
      <v-card-text class="pa-4">
        <v-select
          v-model="relationType"
          :items="relationItems"
          item-title="title"
          item-value="value"
          :label="t('operationCore.profile.documents.relationType')"
          density="comfortable"
          variant="outlined"
          hide-details
          class="mb-4"
        />

        <v-alert v-if="errorLocal" type="error" variant="tonal" density="compact" class="mb-3 rounded-lg">
          {{ errorLocal }}
        </v-alert>

        <div class="d-flex ga-3 di-link-layout">
          <div class="di-link-tree flex-shrink-0">
            <v-progress-linear v-if="treeLoading" indeterminate color="primary" class="mb-2" />
            <DiResourceTree
              :nodes="tree"
              :selected-id="selectedFolderId"
              :root-label="treeRootLabel"
              :empty-label="t('documentIntelligence.noFolders')"
              :load-children="loadChildren"
              @select="selectFolder"
            />
          </div>
          <div class="flex-grow-1">
            <div v-if="allowUpload !== false || allowCreateFolder !== false" class="d-flex flex-wrap ga-2 mb-2">
              <v-text-field
                v-if="allowCreateFolder !== false"
                v-model="folderName"
                density="compact"
                variant="outlined"
                hide-details
                placeholder="Yeni klasör"
                class="folder-name"
                @keyup.enter="createFolder"
              />
              <v-btn
                v-if="allowCreateFolder !== false"
                size="small"
                variant="tonal"
                class="text-none"
                :loading="creatingFolder"
                :disabled="!folderName.trim()"
                @click="createFolder"
              >
                Klasör aç
              </v-btn>
              <v-btn
                v-if="allowUpload !== false"
                size="small"
                variant="tonal"
                class="text-none"
                :loading="uploading"
                @click="fileInput?.click()"
              >
                Dosya yükle
              </v-btn>
              <input
                ref="fileInput"
                type="file"
                class="d-none"
                @change="uploadFile"
              />
            </div>
            <v-progress-linear v-if="childrenLoading" indeterminate color="primary" class="mb-2" />
            <div v-if="!childrenLoading && !linkableChildren.length" class="text-body-2 text-medium-emphasis py-4">
              {{ t('operationCore.profile.documents.noLinkable') }}
            </div>
            <v-list v-else density="compact" class="py-0">
              <v-list-item
                v-for="r in linkableChildren"
                :key="r.id"
                :active="selectedResourceId === r.id"
                rounded="lg"
                @click="selectedResourceId = r.id"
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
          {{ t('operationCore.profile.documents.cancel') }}
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          class="text-none"
          :disabled="!selectedResource"
          :loading="submitting"
          @click="submit"
        >
          {{ t('operationCore.profile.documents.linkConfirm') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.di-link-layout {
  min-height: 280px;
}
.di-link-tree {
  width: 220px;
  max-height: 320px;
  overflow: auto;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 8px;
  padding: 8px;
}
.folder-name {
  max-width: 180px;
}
</style>
