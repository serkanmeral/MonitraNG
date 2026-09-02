<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  pmApplyProjectPack,
  pmDetachProjectPack,
  pmGetProjectPacks,
  pmPreviewProjectPack,
} from '@/services/projectManagementService';
import type {
  PmApplyPackResult,
  PmJobPack,
  PmPackPreview,
  PmProjectPackInstall,
} from '@/types/apps/projectManagement';
import {
  applyJobPackDocuments,
  detachJobPackDocuments,
  previewJobPackFolders,
  type PmPackFolderPreview,
} from '@/utils/pmJobPack';

const props = defineProps<{
  projectId: string;
  projectCode: string;
}>();

const emit = defineEmits<{
  changed: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const loading = ref(false);
const busyCode = ref<string | null>(null);
const catalog = ref<PmJobPack[]>([]);
const installed = ref<PmProjectPackInstall[]>([]);
const previewCode = ref<string | null>(null);
const preview = ref<PmPackPreview | null>(null);
const previewLoading = ref(false);
const detachPack = ref<PmJobPack | null>(null);
const detachPreview = ref<PmPackPreview | null>(null);
const detachFolders = ref<PmPackFolderPreview | null>(null);
const detachLoading = ref(false);

const previewPack = computed(() => catalog.value.find((row) => row.code === previewCode.value) || null);

function installOf(code: string) {
  return installed.value.find((row) => row.packCode === code) || null;
}

function applyModeOf(code: string): 'skip' | 'update' {
  return installOf(code)?.outdated ? 'update' : 'skip';
}

function statusLabel(pack: PmJobPack) {
  const row = installOf(pack.code);
  if (!row) return t('projectManagement.packCatalog.available');
  if (row.outdated) return t('projectManagement.packCatalog.outdated');
  return t('projectManagement.packCatalog.installed');
}

function statusColor(pack: PmJobPack) {
  const row = installOf(pack.code);
  if (!row) return 'default';
  if (row.outdated) return 'warning';
  return 'success';
}

function actionColor(action: string) {
  if (action === 'create') return 'success';
  if (action === 'update') return 'warning';
  if (action === 'remove') return 'error';
  return 'default';
}

function actionLabel(action: string) {
  const key = `projectManagement.packCatalog.action.${action}`;
  const label = t(key);
  return label === key ? action : label;
}

function remainingPacks(exceptCode: string): PmJobPack[] {
  const codes = new Set(
    installed.value.filter((row) => row.packCode !== exceptCode).map((row) => row.packCode),
  );
  return catalog.value.filter((pack) => codes.has(pack.code));
}

async function load() {
  loading.value = true;
  try {
    const next = await pmGetProjectPacks(props.projectId);
    catalog.value = next.catalog || [];
    installed.value = next.installed || [];
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    loading.value = false;
  }
}

function notifyResult(messageKey: string, result: PmApplyPackResult) {
  toast.push({
    title: t('projectManagement.notify.successTitle'),
    message: t(messageKey, {
      created: result.created ?? 0,
      skipped: result.skipped ?? 0,
      updated: result.updated ?? 0,
      removed: result.removed ?? 0,
      kept: result.kept ?? 0,
    }),
    severity: 'success',
  });
}

async function apply(pack: PmJobPack) {
  busyCode.value = pack.code;
  try {
    const result = await pmApplyProjectPack(props.projectId, pack.code, applyModeOf(pack.code));
    try {
      await applyJobPackDocuments(props.projectId, props.projectCode, pack);
    } catch (error) {
      panelError(error, 'projectManagement.errors.packDocsFailed');
    }
    notifyResult('projectManagement.notify.packApplied', result);
    await load();
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    busyCode.value = null;
  }
}

async function executeDetach() {
  const pack = detachPack.value;
  if (!pack) return;
  busyCode.value = pack.code;
  try {
    const result = await pmDetachProjectPack(props.projectId, pack.code);
    try {
      const folders = await detachJobPackDocuments(props.projectCode, pack, remainingPacks(pack.code));
      if (folders.removed > 0 || folders.kept > 0) {
        toast.push({
          title: t('projectManagement.notify.successTitle'),
          message: t('projectManagement.notify.packFoldersDetached', {
            removed: folders.removed,
            kept: folders.kept,
          }),
          severity: 'success',
        });
      }
    } catch (error) {
      panelError(error, 'projectManagement.errors.packDocsDetachFailed');
    }
    notifyResult('projectManagement.notify.packDetached', result);
    detachPack.value = null;
    detachPreview.value = null;
    detachFolders.value = null;
    await load();
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.deleteFailed');
  } finally {
    busyCode.value = null;
  }
}

async function loadApplyPreview(code: string) {
  previewLoading.value = true;
  preview.value = null;
  try {
    preview.value = await pmPreviewProjectPack(props.projectId, code, 'apply', applyModeOf(code));
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
    previewCode.value = null;
  } finally {
    previewLoading.value = false;
  }
}

async function openDetach(pack: PmJobPack) {
  detachPack.value = pack;
  detachLoading.value = true;
  detachPreview.value = null;
  detachFolders.value = null;
  try {
    detachPreview.value = await pmPreviewProjectPack(props.projectId, pack.code, 'detach');
    try {
      detachFolders.value = await previewJobPackFolders(props.projectCode, pack, remainingPacks(pack.code));
    } catch (error) {
      panelError(error, 'projectManagement.errors.packDocsDetachFailed');
    }
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
    detachPack.value = null;
  } finally {
    detachLoading.value = false;
  }
}

function onPreviewDialog(open: boolean) {
  if (!open) previewCode.value = null;
}

function onDetachDialog(open: boolean) {
  if (!open) {
    detachPack.value = null;
    detachPreview.value = null;
    detachFolders.value = null;
  }
}

watch(previewCode, (code) => {
  if (!code) {
    preview.value = null;
    return;
  }
  void loadApplyPreview(code);
});

onMounted(() => {
  void load();
});
</script>

<template>
  <div>
    <p class="text-body-2 text-medium-emphasis mb-4">{{ t('projectManagement.packCatalog.hint') }}</p>
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />
    <div class="d-flex flex-wrap ga-4">
      <v-card
        v-for="pack in catalog"
        :key="pack.code"
        variant="outlined"
        class="pack-card"
      >
        <v-card-title class="d-flex align-center justify-space-between">
          <span>{{ pack.name }}</span>
          <v-chip size="small" :color="statusColor(pack)" variant="tonal">
            {{ statusLabel(pack) }}
          </v-chip>
        </v-card-title>
        <v-card-subtitle>v{{ pack.version || '1.0.0' }} · {{ pack.code }}</v-card-subtitle>
        <v-card-text>
          <div class="text-body-2 mb-3">{{ pack.description }}</div>
          <div class="text-caption text-medium-emphasis mb-1">{{ t('projectManagement.packCatalog.folders') }}</div>
          <div class="d-flex flex-wrap ga-1 mb-3">
            <v-chip v-for="folder in pack.folders" :key="folder" size="x-small" variant="tonal">{{ folder }}</v-chip>
          </div>
          <div class="text-caption text-medium-emphasis mb-1">{{ t('projectManagement.packCatalog.kinds') }}</div>
          <div class="d-flex flex-wrap ga-1">
            <v-chip v-for="kind in pack.kinds" :key="kind" size="x-small" variant="outlined">{{ kind }}</v-chip>
          </div>
          <div v-if="installOf(pack.code)" class="text-caption text-medium-emphasis mt-3">
            {{ t('projectManagement.packCatalog.installedVersion') }}: {{ installOf(pack.code)?.version }}
          </div>
        </v-card-text>
        <v-card-actions>
          <v-btn size="small" variant="text" @click="previewCode = pack.code">
            {{ t('projectManagement.packCatalog.preview') }}
          </v-btn>
          <v-spacer />
          <v-btn
            size="small"
            color="primary"
            :loading="busyCode === pack.code"
            @click="apply(pack)"
          >
            {{ installOf(pack.code)?.outdated
              ? t('projectManagement.packCatalog.update')
              : installOf(pack.code)
                ? t('projectManagement.packCatalog.reapply')
                : t('projectManagement.packCatalog.install') }}
          </v-btn>
          <v-btn
            v-if="installOf(pack.code)"
            size="small"
            variant="text"
            color="error"
            :loading="busyCode === pack.code"
            @click="openDetach(pack)"
          >
            {{ t('projectManagement.packCatalog.detach') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </div>

    <v-dialog :model-value="Boolean(previewPack)" max-width="640" @update:model-value="onPreviewDialog">
      <v-card v-if="previewPack" rounded="lg">
        <v-card-title>{{ t('projectManagement.packCatalog.previewTitle', { name: previewPack.name }) }}</v-card-title>
        <v-card-text>
          <div class="text-body-2 mb-3">{{ previewPack.description }}</div>
          <v-progress-linear v-if="previewLoading" indeterminate color="primary" class="mb-3" />
          <div v-else-if="preview" class="text-caption text-medium-emphasis mb-3">
            {{ t('projectManagement.packCatalog.applySummary', {
              create: preview.createCount,
              skip: preview.skipCount,
              update: preview.updateCount,
            }) }}
            <div class="mt-2">
              <v-chip
                v-if="preview.workspaceAction === 'create'"
                size="x-small"
                color="success"
                variant="tonal"
              >
                {{ t('projectManagement.packCatalog.workspaceCreate', { name: preview.workspaceName || '' }) }}
              </v-chip>
              <v-chip
                v-else
                size="x-small"
                color="default"
                variant="tonal"
              >
                {{ t('projectManagement.packCatalog.workspaceSkip') }}
              </v-chip>
            </div>
          </div>
          <div v-if="preview && preview.items.length" class="preview-list">
            <div v-for="(row, idx) in preview.items" :key="`${row.path}-${idx}`" class="d-flex align-center py-1">
              <span class="text-body-2 flex-grow-1">{{ row.path }}</span>
              <span class="text-caption text-medium-emphasis mr-2">{{ row.kind }}</span>
              <v-chip size="x-small" :color="actionColor(row.action)" variant="tonal">
                {{ actionLabel(row.action) }}
              </v-chip>
            </div>
          </div>
          <div v-else-if="!previewLoading" class="text-body-2 text-medium-emphasis">
            {{ t('projectManagement.packCatalog.previewEmpty') }}
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="previewCode = null">{{ t('projectManagement.cancel') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="Boolean(detachPack)" max-width="640" @update:model-value="onDetachDialog">
      <v-card v-if="detachPack" rounded="lg">
        <v-card-title>{{ t('projectManagement.packCatalog.detachTitle', { name: detachPack.name }) }}</v-card-title>
        <v-card-text>
          <v-progress-linear v-if="detachLoading" indeterminate color="primary" class="mb-3" />
          <div v-else-if="detachPreview" class="text-caption text-medium-emphasis mb-3">
            {{ t('projectManagement.packCatalog.detachSummary', {
              remove: detachPreview.removeCount,
              keep: detachPreview.keepCount,
            }) }}
          </div>
          <div v-if="detachFolders && detachFolders.items.length" class="text-caption text-medium-emphasis mb-3">
            {{ t('projectManagement.packCatalog.detachFolderSummary', {
              remove: detachFolders.removeCount,
              keep: detachFolders.keepCount,
            }) }}
            <div class="d-flex flex-wrap ga-1 mt-2">
              <v-chip
                v-for="row in detachFolders.items"
                :key="row.name"
                size="x-small"
                :color="actionColor(row.action)"
                variant="tonal"
              >
                {{ row.name }} · {{ actionLabel(row.action) }}
              </v-chip>
            </div>
          </div>
          <div v-if="detachPreview && detachPreview.items.length" class="preview-list">
            <div v-for="(row, idx) in detachPreview.items" :key="`${row.path}-${idx}`" class="d-flex align-center py-1">
              <span class="text-body-2 flex-grow-1">{{ row.path }}</span>
              <span class="text-caption text-medium-emphasis mr-2">{{ row.kind }}</span>
              <v-chip size="x-small" :color="actionColor(row.action)" variant="tonal">
                {{ actionLabel(row.action) }}
              </v-chip>
            </div>
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="onDetachDialog(false)">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn
            color="error"
            :loading="busyCode === detachPack.code"
            :disabled="detachLoading"
            @click="executeDetach"
          >
            {{ t('projectManagement.packCatalog.detach') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.pack-card {
  width: min(100%, 360px);
}
.preview-list {
  max-height: 360px;
  overflow: auto;
}
</style>
