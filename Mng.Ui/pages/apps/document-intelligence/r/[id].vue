<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DiMarkdownViewer from '@/components/apps/document-intelligence/DiMarkdownViewer.vue';
import DiFilePreviewDialog from '@/components/apps/document-intelligence/DiFilePreviewDialog.vue';
import DiResourceEditorDialog from '@/components/apps/document-intelligence/DiResourceEditorDialog.vue';
import DiLinkedWorkItemsPanel from '@/components/apps/document-intelligence/DiLinkedWorkItemsPanel.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { isDiDocxEditable, isDiPreviewable } from '@/utils/diFilePreview';
import { DI_HOME_PATH, buildDiFolderUrl } from '@/utils/diResourceLink';
import {
  diErrorStatus,
  diExtractMessage,
  diFetchFileBlob,
  diGetBreadcrumb,
  diGetById,
  diGetMarkdownContent,
} from '@/services/documentIntelligenceService';
import type { DiBreadcrumb, DiResource } from '@/types/apps/documentIntelligence';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const route = useRoute();

const resourceId = computed(() => String(route.params.id ?? '').trim());

const loading = ref(true);
const errorMessage = ref('');
const resource = ref<DiResource | null>(null);
const folderPath = ref<DiBreadcrumb[]>([]);
const downloadedFallback = ref(false);

const editorOpen = ref(false);
const previewOpen = ref(false);

const markdownContent = ref('');
const markdownLoading = ref(false);
const showMarkdown = ref(false);

const snackbar = ref(false);
const snackbarText = ref('');

function resourceLabel(r: DiResource | null): string {
  if (!r) return '…';
  return r.type === 'markdown' ? r.title || r.name : r.name;
}

const breadcrumbs = computed(() => [
  { title: t('documentIntelligence.menuTitle'), to: DI_HOME_PATH },
  ...folderPath.value.map((b) => ({
    title: b.name,
    to: buildDiFolderUrl(b.id),
  })),
  { title: resourceLabel(resource.value), disabled: true },
]);

async function goToParentFolder() {
  await navigateTo(buildDiFolderUrl(resource.value?.parentId ?? null));
}

async function downloadFile(r: DiResource) {
  if (!r.filePath) {
    errorMessage.value = t('documentIntelligence.errors.download');
    return;
  }
  try {
    const blob = await diFetchFileBlob(r.filePath);
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = r.fileName || r.name || 'dosya';
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
    downloadedFallback.value = true;
  } catch (e: unknown) {
    errorMessage.value = diExtractMessage(e, t('documentIntelligence.errors.download'));
  }
}

async function openLoadedResource(r: DiResource) {
  downloadedFallback.value = false;

  if (r.type === 'folder') {
    await navigateTo(buildDiFolderUrl(r.id), { replace: true });
    return;
  }

  if (r.type === 'markdown') {
    showMarkdown.value = true;
    markdownLoading.value = true;
    try {
      const c = await diGetMarkdownContent(r.id);
      markdownContent.value = c.content;
    } catch (e: unknown) {
      errorMessage.value = diExtractMessage(e, t('documentIntelligence.errors.docLoad'));
    } finally {
      markdownLoading.value = false;
    }
    return;
  }

  if (isDiDocxEditable(r)) {
    editorOpen.value = true;
    return;
  }

  if (isDiPreviewable(r)) {
    previewOpen.value = true;
    return;
  }

  await downloadFile(r);
}

async function loadResource() {
  const id = resourceId.value;
  if (!id) {
    await navigateTo(DI_HOME_PATH, { replace: true });
    return;
  }

  loading.value = true;
  errorMessage.value = '';
  showMarkdown.value = false;
  editorOpen.value = false;
  previewOpen.value = false;
  downloadedFallback.value = false;
  resource.value = null;
  markdownContent.value = '';

  try {
    const r = await diGetById(id);
    resource.value = r;
    folderPath.value = r.parentId ? await diGetBreadcrumb(r.parentId) : [];
    await openLoadedResource(r);
  } catch (e: unknown) {
    if (diErrorStatus(e) === 404) {
      errorMessage.value = t('documentIntelligence.errors.resourceNotFound');
    } else {
      errorMessage.value = diExtractMessage(e, t('documentIntelligence.errors.docLoad'));
    }
  } finally {
    loading.value = false;
  }
}

function onEditorOpenChange(open: boolean) {
  editorOpen.value = open;
  if (!open) void goToParentFolder();
}

function onPreviewOpenChange(open: boolean) {
  previewOpen.value = open;
  if (!open) void goToParentFolder();
}

async function copyShareLink() {
  try {
    await navigator.clipboard.writeText(window.location.href);
    snackbarText.value = t('documentIntelligence.linkCopied');
    snackbar.value = true;
  } catch {
    snackbarText.value = t('documentIntelligence.linkCopyFailed');
    snackbar.value = true;
  }
}

onMounted(() => {
  void loadResource();
});

watch(resourceId, () => {
  void loadResource();
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="resourceLabel(resource)" :breadcrumbs="breadcrumbs" />

    <v-card elevation="10" rounded="lg" class="pa-4">
      <div class="d-flex align-center flex-wrap ga-2 mb-4">
        <v-btn
          variant="text"
          size="small"
          class="text-none"
          prepend-icon="mdi-arrow-left"
          @click="goToParentFolder"
        >
          {{ t('documentIntelligence.backToFolder') }}
        </v-btn>
        <v-spacer />
        <v-btn
          v-if="resource && !loading && !errorMessage"
          variant="tonal"
          size="small"
          class="text-none"
          prepend-icon="mdi-link-variant"
          @click="copyShareLink"
        >
          {{ t('documentIntelligence.copyLink') }}
        </v-btn>
        <v-btn variant="tonal" size="small" class="text-none" :to="DI_HOME_PATH">
          {{ t('documentIntelligence.menuTitle') }}
        </v-btn>
      </div>

      <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

      <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-4">
        {{ errorMessage }}
        <div class="mt-3">
          <v-btn variant="flat" color="primary" size="small" class="text-none" :to="DI_HOME_PATH">
            {{ t('documentIntelligence.menuTitle') }}
          </v-btn>
        </div>
      </v-alert>

      <template v-else-if="showMarkdown && resource">
        <div class="d-flex align-center mb-3">
          <v-icon icon="mdi-language-markdown-outline" color="primary" class="mr-2" />
          <h3 class="text-h5 font-weight-bold">{{ resourceLabel(resource) }}</h3>
        </div>
        <v-progress-linear v-if="markdownLoading" indeterminate color="primary" class="mb-2" />
        <DiMarkdownViewer
          v-else
          :content="markdownContent"
          :empty-label="t('documentIntelligence.emptyDoc')"
        />
        <DiLinkedWorkItemsPanel :resource-id="resource.id" class="mt-4" />
      </template>

      <v-alert
        v-else-if="downloadedFallback && resource"
        type="info"
        variant="tonal"
      >
        {{ t('documentIntelligence.previewUnavailable') }}
        <div class="mt-2">
          <v-btn
            variant="flat"
            color="primary"
            size="small"
            class="text-none"
            prepend-icon="mdi-download"
            @click="downloadFile(resource)"
          >
            {{ t('documentIntelligence.download') }}
          </v-btn>
        </div>
      </v-alert>
    </v-card>

    <DiResourceEditorDialog
      :model-value="editorOpen"
      :resource="resource"
      @update:model-value="onEditorOpenChange"
    />

    <DiFilePreviewDialog
      :model-value="previewOpen"
      :resource="resource"
      @update:model-value="onPreviewOpenChange"
    />

    <v-snackbar v-model="snackbar" location="top right" :timeout="2500">
      {{ snackbarText }}
    </v-snackbar>
  </div>
</template>
