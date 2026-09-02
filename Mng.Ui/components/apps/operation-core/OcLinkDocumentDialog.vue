<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import DiResourceTree from '@/components/apps/document-intelligence/DiResourceTree.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  diCreateResourceLink,
  diGetBootstrap,
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
  workItemId: string;
  excludedResourceIds?: string[];
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

const relationItems = computed(() =>
  DI_WORK_ITEM_LINK_RELATION_TYPES.map((value) => ({
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

async function loadTree() {
  treeLoading.value = true;
  childrenLoading.value = true;
  errorLocal.value = null;
  try {
    const boot = await diGetBootstrap(null);
    tree.value = boot.tree;
    children.value = boot.children.items;
    selectedFolderId.value = null;
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.profile.documents.loadTreeError');
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
    errorLocal.value = panelError(e, 'operationCore.profile.documents.loadTreeError');
    children.value = [];
  } finally {
    childrenLoading.value = false;
  }
}

async function submit() {
  const resourceId = selectedResourceId.value?.trim();
  const workItemId = props.workItemId.trim();
  if (!resourceId || !workItemId) return;

  submitting.value = true;
  errorLocal.value = null;
  try {
    await diCreateResourceLink({
      resourceId,
      targetModule: 'operationCore',
      targetType: 'workItem',
      targetId: workItemId,
      relationType: relationType.value,
    });
    emit('linked');
    open.value = false;
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.profile.documents.linkError');
  } finally {
    submitting.value = false;
  }
}

watch(open, (v) => {
  if (!v) {
    selectedResourceId.value = null;
    relationType.value = 'reference';
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
              :root-label="t('documentIntelligence.allDocuments')"
              :empty-label="t('documentIntelligence.noFolders')"
              @select="selectFolder"
            />
          </div>
          <div class="flex-grow-1">
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
</style>
