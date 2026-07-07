<script setup lang="ts">
import { ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import DiFileVersionPreviewDialog from '@/components/apps/document-intelligence/DiFileVersionPreviewDialog.vue';
import {
  diDownloadFileVersion,
  diGetFileVersions,
  diRestoreFileVersion,
} from '@/services/documentIntelligenceService';
import type { DiMarkdownVersion, DiResource } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  modelValue: boolean;
  resource: DiResource | null;
  canRestore?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  restored: [resource: DiResource];
}>();

const { t } = useAppI18n();
const { push } = useAppToast();
const panelError = usePanelErrorNotify('errors.dg.generic');

const versions = ref<DiMarkdownVersion[]>([]);
const versionsLoading = ref(false);
const downloadingVersion = ref<number | null>(null);
const restoringVersion = ref<number | null>(null);
const previewOpen = ref(false);
const previewVersionNumber = ref<number | null>(null);

function close() {
  emit('update:modelValue', false);
}

function formatDateTime(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('tr-TR', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function formatSize(size: number | null): string {
  if (size == null || size <= 0) return '';
  if (size < 1024) return `${size} B`;
  if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`;
  return `${(size / (1024 * 1024)).toFixed(1)} MB`;
}

function triggerBrowserDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}

async function loadVersions() {
  const id = props.resource?.id?.trim();
  if (!id) {
    versions.value = [];
    return;
  }
  versionsLoading.value = true;
  try {
    versions.value = await diGetFileVersions(id);
  } catch (e: unknown) {
    push({
      title: t('errors.dg.toastTitle'),
      message: panelError(e, 'documentIntelligence.errors.versionsLoad'),
      severity: 'error',
    });
    versions.value = [];
  } finally {
    versionsLoading.value = false;
  }
}

function openPreview(v: DiMarkdownVersion) {
  previewVersionNumber.value = v.versionNumber;
  previewOpen.value = true;
}

async function downloadVersion(v: DiMarkdownVersion) {
  const id = props.resource?.id?.trim();
  if (!id) return;
  downloadingVersion.value = v.versionNumber;
  try {
    const suggested = props.resource?.fileName || props.resource?.name || null;
    const { blob, fileName } = await diDownloadFileVersion(id, v.versionNumber, suggested);
    triggerBrowserDownload(blob, fileName);
  } catch (e: unknown) {
    push({
      title: t('errors.dg.toastTitle'),
      message: panelError(e, 'documentIntelligence.errors.versionLoad'),
      severity: 'error',
    });
  } finally {
    downloadingVersion.value = null;
  }
}

async function restoreVersion(v: DiMarkdownVersion) {
  const id = props.resource?.id?.trim();
  if (!id || v.isCurrent || !props.canRestore) return;
  restoringVersion.value = v.versionNumber;
  try {
    const restored = await diRestoreFileVersion(id, v.versionNumber);
    close();
    push({
      title: t('documentIntelligence.notify.successTitle'),
      message: t('documentIntelligence.versionRestored', { n: v.versionNumber }),
      severity: 'success',
    });
    emit('restored', restored);
  } catch (e: unknown) {
    push({
      title: t('errors.dg.toastTitle'),
      message: panelError(e, 'documentIntelligence.errors.versionRestore'),
      severity: 'error',
    });
  } finally {
    restoringVersion.value = null;
  }
}

watch(
  () => [props.modelValue, props.resource?.id] as const,
  ([open]) => {
    if (open) void loadVersions();
  }
);
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="640" scrollable @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center text-subtitle-1 font-weight-bold">
        <v-icon size="20" class="mr-2">mdi-history</v-icon>
        {{ t('documentIntelligence.versionHistory') }}
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="close" />
      </v-card-title>
      <v-divider />
      <v-card-text class="pa-0">
        <div v-if="versionsLoading" class="d-flex justify-center pa-6">
          <v-progress-circular indeterminate size="28" color="primary" />
        </div>
        <div v-else-if="!versions.length" class="text-medium-emphasis text-body-2 pa-4 text-center">
          {{ t('documentIntelligence.noVersions') }}
        </div>
        <v-list v-else density="compact" nav class="py-2">
          <v-list-item
            v-for="v in versions"
            :key="v.versionNumber"
            rounded="lg"
            class="mx-2 mb-1"
          >
            <template #prepend>
              <v-avatar size="28" :color="v.isCurrent ? 'primary' : 'grey-lighten-1'" class="text-caption">
                v{{ v.versionNumber }}
              </v-avatar>
            </template>
            <v-list-item-title class="text-body-2">
              {{ formatDateTime(v.createdAt) || ('v' + v.versionNumber) }}
              <v-chip v-if="v.isCurrent" size="x-small" color="primary" variant="tonal" class="ml-1">
                {{ t('documentIntelligence.currentVersion') }}
              </v-chip>
            </v-list-item-title>
            <v-list-item-subtitle class="text-caption">
              <span v-if="v.createdBy">{{ v.createdBy }}</span>
              <span v-if="v.changeNote"> · {{ v.changeNote }}</span>
              <span v-if="formatSize(v.size)"> · {{ formatSize(v.size) }}</span>
            </v-list-item-subtitle>
            <template #append>
              <v-btn
                size="x-small"
                variant="text"
                icon="mdi-file-eye-outline"
                :title="t('documentIntelligence.preview')"
                @click.stop="openPreview(v)"
              />
              <v-btn
                size="x-small"
                variant="text"
                icon="mdi-download"
                :loading="downloadingVersion === v.versionNumber"
                :title="t('documentIntelligence.download')"
                @click.stop="downloadVersion(v)"
              />
              <v-btn
                v-if="canRestore && !v.isCurrent"
                size="x-small"
                variant="tonal"
                color="primary"
                class="text-none ml-1"
                :loading="restoringVersion === v.versionNumber"
                @click.stop="restoreVersion(v)"
              >
                {{ t('documentIntelligence.restore') }}
              </v-btn>
            </template>
          </v-list-item>
        </v-list>
      </v-card-text>
    </v-card>
  </v-dialog>

  <DiFileVersionPreviewDialog
    v-model="previewOpen"
    :resource="resource"
    :version-number="previewVersionNumber"
  />
</template>
