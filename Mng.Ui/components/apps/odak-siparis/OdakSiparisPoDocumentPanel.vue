<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  buildPoUploadPayload,
  downloadPoEntry,
  hasPendingPoUpload,
  hasStoredPoDocument,
  isPoDocumentDirty,
  listPoDocumentEntries,
  loadPackagePoState,
  resolvePoEntryPreviewBlobUrl,
  savePackagePoDocument,
  type PoDocumentEntry,
} from '@/utils/odakSiparisPoService';
import { DownloadIcon, EyeIcon, TrashIcon, UploadIcon, XIcon } from 'vue-tabler-icons';

const props = defineProps<{
  packageId: string;
  packageNo?: string;
}>();

const emit = defineEmits<{
  saved: [];
}>();

const { t } = useAppI18n();

const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const poDocument = ref<unknown>(null);
const savedPoDocument = ref<unknown>(null);
const poVersion = ref('');
const savedPoVersion = ref('');
const selectedKey = ref<string | null>(null);
const fileInput = ref<HTMLInputElement | null>(null);

const previewDialogOpen = ref(false);
const previewDialogLoading = ref(false);
const previewDialogError = ref('');
const previewDialogUrl = ref<string | null>(null);
const previewDialogObjectUrl = ref<string | null>(null);
const previewDialogFileName = ref('');

const fileEntries = computed(() => listPoDocumentEntries(poDocument.value, props.packageNo));

const selectedEntry = computed(
  () => fileEntries.value.find((e) => e.key === selectedKey.value) ?? fileEntries.value[0] ?? null
);

const hasStored = computed(() => hasStoredPoDocument(poDocument.value));
const hasPending = computed(() => hasPendingPoUpload(poDocument.value));

const dirty = computed(
  () =>
    isPoDocumentDirty(poDocument.value, savedPoDocument.value) ||
    poVersion.value.trim() !== savedPoVersion.value.trim()
);

function revokePreviewObjectUrl() {
  if (previewDialogObjectUrl.value) {
    URL.revokeObjectURL(previewDialogObjectUrl.value);
    previewDialogObjectUrl.value = null;
  }
}

function closePreviewDialog() {
  previewDialogOpen.value = false;
  previewDialogUrl.value = null;
  previewDialogError.value = '';
  previewDialogFileName.value = '';
  revokePreviewObjectUrl();
}

function syncSelection() {
  const entries = fileEntries.value;
  if (!entries.length) {
    selectedKey.value = null;
    return;
  }
  if (!selectedKey.value || !entries.some((e) => e.key === selectedKey.value)) {
    selectedKey.value = entries[0]!.key;
  }
}

async function reload() {
  if (!props.packageId) return;
  loading.value = true;
  errorMessage.value = '';
  try {
    const state = await loadPackagePoState(props.packageId);
    poDocument.value = state.poDocument;
    savedPoDocument.value = state.poDocument;
    poVersion.value = state.poVersion;
    savedPoVersion.value = state.poVersion;
    syncSelection();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

async function savePo() {
  if (!props.packageId || !dirty.value) return;
  saving.value = true;
  errorMessage.value = '';
  try {
    await savePackagePoDocument(props.packageId, poDocument.value, poVersion.value);
    await reload();
    emit('saved');
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

function openFilePicker() {
  if (loading.value || saving.value) return;
  fileInput.value?.click();
}

async function onFileSelected(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  if (!file) return;
  errorMessage.value = '';
  try {
    poDocument.value = await buildPoUploadPayload(file);
    syncSelection();
    if (selectedEntry.value) {
      await openPreviewModal(selectedEntry.value);
    }
  } catch (e: unknown) {
    errorMessage.value =
      e instanceof Error && e.message === 'PDF only'
        ? t('odakSiparis.po.errors.pdfOnly')
        : e instanceof Error && e.message === 'File too large'
          ? t('odakSiparis.po.errors.tooLarge', { max: 25 })
          : e instanceof Error
            ? e.message
            : String(e);
  }
}

function clearDocument() {
  poDocument.value = null;
  selectedKey.value = null;
  closePreviewDialog();
}

function selectEntry(entry: PoDocumentEntry) {
  selectedKey.value = entry.key;
}

async function openPreviewModal(entry: PoDocumentEntry) {
  selectEntry(entry);
  revokePreviewObjectUrl();
  previewDialogUrl.value = null;
  previewDialogError.value = '';
  previewDialogFileName.value = entry.fileName;
  previewDialogOpen.value = true;
  previewDialogLoading.value = true;
  try {
    const url = await resolvePoEntryPreviewBlobUrl(entry);
    previewDialogUrl.value = url;
    if (url.startsWith('blob:')) {
      previewDialogObjectUrl.value = url;
    }
  } catch (e: unknown) {
    previewDialogError.value = e instanceof Error ? e.message : String(e);
  } finally {
    previewDialogLoading.value = false;
  }
}

async function downloadEntry(entry: PoDocumentEntry) {
  errorMessage.value = '';
  try {
    await downloadPoEntry(entry);
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  }
}

watch(
  () => props.packageId,
  () => {
    void reload();
  },
  { immediate: true }
);

watch(fileEntries, () => {
  syncSelection();
});

onBeforeUnmount(() => {
  revokePreviewObjectUrl();
});
</script>

<template>
  <v-card variant="outlined" class="odak-po-panel h-100 d-flex flex-column">
    <v-card-title class="text-subtitle-2 py-2 px-3 d-flex align-center flex-wrap ga-2">
      <span>{{ t('odakSiparis.po.titleShort') }}</span>
      <v-chip v-if="hasStored && !hasPending" size="x-small" color="success" variant="tonal">
        {{ t('odakSiparis.po.hasDocument') }}
      </v-chip>
      <v-chip v-else-if="hasPending" size="x-small" color="warning" variant="tonal">
        {{ t('odakSiparis.po.pendingUpload') }}
      </v-chip>
      <v-spacer />
      <input
        ref="fileInput"
        type="file"
        accept=".pdf,application/pdf"
        class="d-none"
        @change="onFileSelected"
      />
      <v-btn size="small" variant="tonal" color="primary" :disabled="loading || saving" @click="openFilePicker">
        <UploadIcon size="16" class="mr-1" />
        {{ fileEntries.length ? t('odakSiparis.po.replaceFile') : t('odakSiparis.po.chooseFile') }}
      </v-btn>
    </v-card-title>
    <v-divider />

    <v-card-text class="px-3 py-2 flex-grow-1 d-flex flex-column ga-2">
      <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact">
        {{ errorMessage }}
      </v-alert>
      <v-progress-linear v-if="loading" indeterminate color="primary" />

      <div v-if="fileEntries.length" class="odak-po-files border rounded-md">
        <div class="odak-po-files__header text-caption font-weight-medium px-3 py-2">
          {{ t('odakSiparis.po.fileListTitle', { count: fileEntries.length }) }}
        </div>
        <v-list density="compact" class="py-0 bg-transparent">
          <v-list-item
            v-for="entry in fileEntries"
            :key="entry.key"
            :active="selectedEntry?.key === entry.key"
            color="primary"
            rounded="0"
            class="odak-po-files__row"
            @click="selectEntry(entry)"
          >
            <template #prepend>
              <v-icon icon="mdi-file-pdf-box" color="error" size="22" />
            </template>
            <v-list-item-title class="text-body-2 font-weight-medium text-wrap">
              {{ entry.fileName }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-caption">
              <span v-if="entry.isPending">{{ t('odakSiparis.po.pendingUpload') }}</span>
              <span v-else>{{ t('odakSiparis.po.storedFile') }}</span>
            </v-list-item-subtitle>
            <template #append>
              <div class="d-flex align-center ga-1" @click.stop>
                <v-btn
                  icon
                  size="x-small"
                  variant="text"
                  color="primary"
                  :title="t('odakSiparis.po.preview')"
                  @click="openPreviewModal(entry)"
                >
                  <EyeIcon size="18" />
                </v-btn>
                <v-btn
                  icon
                  size="x-small"
                  variant="text"
                  :title="t('odakSiparis.po.download')"
                  @click="downloadEntry(entry)"
                >
                  <DownloadIcon size="18" />
                </v-btn>
                <v-btn
                  icon
                  size="x-small"
                  variant="text"
                  color="error"
                  :title="t('odakSiparis.po.remove')"
                  @click="clearDocument"
                >
                  <TrashIcon size="18" />
                </v-btn>
              </div>
            </template>
          </v-list-item>
        </v-list>
      </div>

      <div v-else class="odak-po-empty border rounded-md pa-4 text-center">
        <v-icon icon="mdi-file-pdf-box" size="36" color="medium-emphasis" class="mb-2" />
        <p class="text-body-2 text-medium-emphasis mb-0">{{ t('odakSiparis.po.noDocument') }}</p>
        <p class="text-caption text-medium-emphasis mt-1 mb-0">{{ t('odakSiparis.po.hintShort') }}</p>
      </div>

      <v-text-field
        v-model="poVersion"
        :label="t('odakSiparis.packages.fields.poVersion')"
        variant="outlined"
        density="compact"
        hide-details
        :disabled="loading || saving"
      />

      <div class="d-flex justify-end">
        <v-btn
          color="primary"
          variant="flat"
          size="small"
          :loading="saving"
          :disabled="!dirty || loading"
          @click="savePo"
        >
          {{ t('odakSiparis.po.save') }}
        </v-btn>
      </div>
    </v-card-text>

    <!-- PDF onizleme modali -->
    <v-dialog
      v-model="previewDialogOpen"
      max-width="960"
      scrollable
      @after-leave="closePreviewDialog"
    >
      <v-card class="odak-po-preview-dialog">
        <v-card-title class="d-flex align-center py-2 px-3">
          <span class="text-subtitle-2">{{ t('odakSiparis.po.previewDialogTitle') }}</span>
          <span class="text-caption text-medium-emphasis ms-2 text-truncate flex-grow-1">
            {{ previewDialogFileName }}
          </span>
          <v-btn icon variant="text" size="small" @click="previewDialogOpen = false">
            <XIcon size="18" />
          </v-btn>
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-0 odak-po-preview-dialog__body">
          <div v-if="previewDialogLoading" class="d-flex justify-center align-center py-16">
            <v-progress-circular indeterminate color="primary" size="48" />
          </div>
          <v-alert
            v-else-if="previewDialogError"
            type="warning"
            variant="tonal"
            density="compact"
            class="ma-3"
          >
            {{ t('odakSiparis.po.previewFailed') }}
            <template v-if="selectedEntry">
              <v-btn
                size="x-small"
                variant="text"
                class="ms-1"
                @click="downloadEntry(selectedEntry)"
              >
                {{ t('odakSiparis.po.downloadInstead') }}
              </v-btn>
            </template>
          </v-alert>
          <iframe
            v-else-if="previewDialogUrl"
            :key="previewDialogUrl"
            :src="previewDialogUrl"
            class="odak-po-preview-dialog__iframe"
            :title="previewDialogFileName || 'PDF'"
          />
        </v-card-text>
      </v-card>
    </v-dialog>
  </v-card>
</template>

<style scoped>
.odak-po-panel {
  background: rgba(var(--v-theme-surface), 1);
  min-height: 0;
}

.odak-po-files {
  background: rgba(var(--v-theme-surface-variant), 0.35);
}

.odak-po-files__header {
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  color: rgba(var(--v-theme-on-surface), 0.85);
}

.odak-po-files__row {
  border-bottom: 1px solid rgba(var(--v-border-color), calc(var(--v-border-opacity) * 0.6));
}

.odak-po-files__row:last-child {
  border-bottom: none;
}

.odak-po-empty {
  background: rgba(var(--v-theme-on-surface), 0.03);
}

.odak-po-preview-dialog__body {
  min-height: 70vh;
  background: rgba(var(--v-theme-on-surface), 0.04);
}

.odak-po-preview-dialog__iframe {
  width: 100%;
  height: 78vh;
  min-height: 480px;
  border: 0;
  display: block;
  background: #525659;
}
</style>
