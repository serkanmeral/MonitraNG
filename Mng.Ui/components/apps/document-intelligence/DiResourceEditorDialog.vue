<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import { useDiEditorSessionCleanup } from '@/composables/useDiEditorSessionCleanup';
import { useDiEditorLockGate } from '@/composables/useDiEditorLockGate';
import { useDiEditorVersionWatch } from '@/composables/useDiEditorVersionWatch';
import { useDiEditorCloseGuard } from '@/composables/useDiEditorCloseGuard';
import DiCollaboraEditor from '@/components/apps/document-intelligence/DiCollaboraEditor.vue';
import DiEditorCloseConfirmDialog from '@/components/apps/document-intelligence/DiEditorCloseConfirmDialog.vue';
import DiEditorLockDialog from '@/components/apps/document-intelligence/DiEditorLockDialog.vue';
import DiFileVersionHistoryDialog from '@/components/apps/document-intelligence/DiFileVersionHistoryDialog.vue';
import DiSaveVersionNoteDialog from '@/components/apps/document-intelligence/DiSaveVersionNoteDialog.vue';
import {
  diGetResourceEditorSession,
  diUpdateFileVersionChangeNote,
} from '@/services/documentIntelligenceService';
import { isDiManagedDocument } from '@/utils/diFilePreview';
import type { DiResource, DiResourceEditorOpenOptions } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  modelValue: boolean;
  resource: DiResource | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  saved: [resource: DiResource];
}>();

const { t } = useAppI18n();
const { push } = useAppToast();
const panelError = usePanelErrorNotify('errors.dg.generic');
const { trackEditorAccessToken, releaseEditorSession } = useDiEditorSessionCleanup();
const {
  dialogOpen: lockDialogOpen,
  lockStatus,
  gateResourceEditor,
  onDialogChoose,
  onDialogUpdate,
} = useDiEditorLockGate();

const collaboraEditorRef = ref<InstanceType<typeof DiCollaboraEditor> | null>(null);
const loading = ref(false);
const error = ref<string | null>(null);
const editorUrl = ref<string | null>(null);
const editorReadOnly = ref(false);

function forceClose() {
  emit('update:modelValue', false);
}

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => {
    if (v) emit('update:modelValue', true);
    else requestClose();
  },
});

const lockEnforced = ref(false);
const viaTemplateFallback = ref(false);
const localResource = ref<DiResource | null>(null);

const historyDialog = ref(false);
const versionNoteDialog = ref(false);
const pendingVersionNote = ref<number | null>(null);
const savingVersionNote = ref(false);
const pendingCloseAfterNote = ref(false);
/** Aynı döküman için editör zaten yüklüyse yeniden gate/load yapma (iç içe dialog tetiklemesi). */
const editorReadyForResourceId = ref<string | null>(null);
let openingResourceId: string | null = null;
let lastNotifiedVersion = 0;

const resourceId = computed(() => props.resource?.id ?? localResource.value?.id ?? null);
const initialVersion = computed(
  () => localResource.value?.currentVersionNumber ?? props.resource?.currentVersionNumber ?? 0,
);
const editorActive = computed(() => open.value && Boolean(editorUrl.value) && !loading.value && !error.value);

const { currentVersion, refreshVersion, checkVersionAfterSave } = useDiEditorVersionWatch({
  resourceId,
  initialVersion,
  enabled: editorActive,
  readOnly: editorReadOnly,
  onVersionSaved: (newVersion) => {
    if (newVersion <= lastNotifiedVersion) return;
    lastNotifiedVersion = newVersion;
    push({
      title: t('documentIntelligence.notify.successTitle'),
      message: t('documentIntelligence.editorVersion.savedToast', { n: newVersion }),
      severity: 'success',
    });
    pendingVersionNote.value = newVersion;
    versionNoteDialog.value = true;
    if (localResource.value) {
      localResource.value = { ...localResource.value, currentVersionNumber: newVersion };
    }
    emit('saved', { ...(localResource.value ?? props.resource!), currentVersionNumber: newVersion });
  },
});

async function finishCloseAfterSave() {
  pendingCloseAfterNote.value = true;
  const versionBumped = await checkVersionAfterSave();
  if (versionBumped || versionNoteDialog.value || pendingVersionNote.value) {
    return;
  }
  pendingCloseAfterNote.value = false;
  forceClose();
}

function finishPendingCloseIfNeeded() {
  if (!pendingCloseAfterNote.value) return;
  pendingCloseAfterNote.value = false;
  forceClose();
}

const {
  closeConfirmOpen,
  closeConfirmSaving,
  requestClose,
  cancelCloseConfirm,
  confirmCloseSave,
  confirmCloseDiscard,
} = useDiEditorCloseGuard({
  collaboraRef: collaboraEditorRef,
  readOnly: editorReadOnly,
  onForceClose: forceClose,
  onAfterCloseSave: finishCloseAfterSave,
});

const fileLabel = computed(
  () => localResource.value?.fileName || localResource.value?.name || props.resource?.fileName || props.resource?.name || '',
);

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '';
  try {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

const toolbarMeta = computed(() => {
  const r = localResource.value ?? props.resource;
  if (!r) return '';
  const parts: string[] = [];
  if (isDiManagedDocument(r) && r.documentNo) parts.push(r.documentNo);
  if (r.updatedBy || r.updatedAt) {
    const audit = [r.updatedBy, r.updatedAt ? formatDateTime(r.updatedAt) : null].filter(Boolean).join(' · ');
    if (audit) parts.push(audit);
  } else if (r.createdBy || r.createdAt) {
    const audit = [r.createdBy, r.createdAt ? formatDateTime(r.createdAt) : null].filter(Boolean).join(' · ');
    if (audit) parts.push(audit);
  }
  return parts.join(' · ');
});

const historyResource = computed(() => localResource.value ?? props.resource);

function reset(options?: { keepVersionNote?: boolean }) {
  editorUrl.value = null;
  editorReadOnly.value = false;
  lockEnforced.value = false;
  viaTemplateFallback.value = false;
  error.value = null;
  editorReadyForResourceId.value = null;
  if (!options?.keepVersionNote) {
    pendingVersionNote.value = null;
    versionNoteDialog.value = false;
    pendingCloseAfterNote.value = false;
  }
}

async function loadEditor(resource: DiResource, options?: DiResourceEditorOpenOptions) {
  await releaseEditorSession();
  reset();
  localResource.value = resource;
  loading.value = true;
  try {
    const session = await diGetResourceEditorSession(
      resource.id,
      resource.fileName ?? resource.name,
      options,
    );
    editorReadOnly.value = session.readOnly;
    lockEnforced.value = Boolean(session.lockEnforced);
    viaTemplateFallback.value = Boolean(session.viaTemplateFallback);
    editorUrl.value = session.editorUrl || null;
    trackEditorAccessToken(session.accessToken);
    editorReadyForResourceId.value = resource.id;
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.errors.editorSession');
  } finally {
    loading.value = false;
  }
}

async function tryOpenEditor(resource: DiResource) {
  const id = resource.id?.trim();
  if (!id) return;

  if (editorUrl.value && editorReadyForResourceId.value === id && !error.value) {
    return;
  }
  if (openingResourceId === id) return;

  openingResourceId = id;
  try {
    const gate = await gateResourceEditor(id);
    if (!gate.proceed) {
      forceClose();
      return;
    }
    await loadEditor(resource, gate.options);
  } finally {
    if (openingResourceId === id) openingResourceId = null;
  }
}

async function confirmVersionNote(changeNote: string) {
  const id = resourceId.value?.trim();
  const versionNumber = pendingVersionNote.value;
  if (!id || !versionNumber || !changeNote) {
    versionNoteDialog.value = false;
    finishPendingCloseIfNeeded();
    return;
  }

  savingVersionNote.value = true;
  try {
    await diUpdateFileVersionChangeNote(id, versionNumber, changeNote);
    push({
      title: t('documentIntelligence.notify.successTitle'),
      message: t('documentIntelligence.editorVersion.noteSaved'),
      severity: 'success',
    });
    versionNoteDialog.value = false;
    finishPendingCloseIfNeeded();
  } catch (e: unknown) {
    push({
      title: t('errors.dg.toastTitle'),
      message: panelError(e, 'documentIntelligence.errors.versionNoteSave'),
      severity: 'error',
    });
  } finally {
    savingVersionNote.value = false;
  }
}

function onVersionNoteSkip() {
  pendingVersionNote.value = null;
  finishPendingCloseIfNeeded();
}

function onVersionNoteDialogUpdate(open: boolean) {
  versionNoteDialog.value = open;
  if (!open && pendingCloseAfterNote.value) {
    pendingVersionNote.value = null;
    finishPendingCloseIfNeeded();
  }
}

async function onVersionRestored(restored: DiResource) {
  localResource.value = restored;
  currentVersion.value = restored.currentVersionNumber ?? currentVersion.value;
  lastNotifiedVersion = restored.currentVersionNumber ?? lastNotifiedVersion;
  emit('saved', restored);
  await refreshVersion();
}

function onCollaboraDocumentSaved() {
  void checkVersionAfterSave();
}

watch(
  () => props.modelValue,
  (isOpen, wasOpen) => {
    if (isOpen && !wasOpen && props.resource?.id) {
      void tryOpenEditor(props.resource);
      return;
    }
    if (!isOpen && wasOpen) {
      if (versionNoteDialog.value || historyDialog.value) {
        return;
      }
      lastNotifiedVersion = 0;
      void releaseEditorSession();
      reset();
      localResource.value = null;
    }
  },
);
</script>

<template>
  <v-dialog v-model="open" fullscreen transition="dialog-bottom-transition">
    <v-card rounded="0" class="d-flex flex-column h-100">
      <v-toolbar density="comfortable" color="surface" class="border-b">
        <div class="px-4 py-1 flex-grow-1 min-width-0">
          <div class="text-subtitle-1 font-weight-bold text-truncate">
            {{ fileLabel }}
          </div>
          <div v-if="toolbarMeta" class="text-caption text-medium-emphasis text-truncate">
            {{ toolbarMeta }}
          </div>
        </div>
        <v-chip v-if="currentVersion > 0" size="small" variant="tonal" color="primary" label class="ml-2">
          v{{ currentVersion }}
        </v-chip>
        <v-btn
          size="small"
          variant="text"
          class="text-none ml-1"
          prepend-icon="mdi-history"
          @click="historyDialog = true"
        >
          {{ t('documentIntelligence.history') }}
        </v-btn>
        <v-chip
          v-if="editorReadOnly"
          size="small"
          variant="tonal"
          color="warning"
          label
          class="ml-2"
        >
          {{
            lockEnforced
              ? t('documentIntelligence.editorLock.openReadOnly')
              : t('documentIntelligence.designer.editorReadOnlyHint')
          }}
        </v-chip>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" @click="requestClose" />
      </v-toolbar>

      <v-card-text class="flex-grow-1 pa-0 di-resource-editor-body">
        <v-alert
          v-if="viaTemplateFallback && !loading && !error"
          type="info"
          variant="tonal"
          density="compact"
          class="ma-3 mb-0 rounded-lg"
        >
          {{ t('documentIntelligence.editorTemplateFallbackHint') }}
        </v-alert>

        <DiCollaboraEditor
          ref="collaboraEditorRef"
          :editor-url="editorUrl"
          :loading="loading"
          :error="error"
          :title="null"
          @document-saved="onCollaboraDocumentSaved"
        />
      </v-card-text>
    </v-card>
  </v-dialog>

  <DiEditorLockDialog
    :model-value="lockDialogOpen"
    :status="lockStatus"
    @update:model-value="onDialogUpdate"
    @choose="onDialogChoose"
  />

  <DiFileVersionHistoryDialog
    v-model="historyDialog"
    :resource="historyResource"
    :can-restore="historyResource?.permissions.canEdit ?? false"
    @restored="onVersionRestored"
  />

  <!-- Tam ekran editör diyalogunun dışında — iç içe v-dialog modelValue flicker'ını önler -->
  <DiSaveVersionNoteDialog
    :model-value="versionNoteDialog"
    :version-number="pendingVersionNote"
    :loading="savingVersionNote"
    @update:model-value="onVersionNoteDialogUpdate"
    @confirm="confirmVersionNote"
    @skip="onVersionNoteSkip"
  />

  <DiEditorCloseConfirmDialog
    v-model="closeConfirmOpen"
    :saving="closeConfirmSaving"
    @save="confirmCloseSave"
    @discard="confirmCloseDiscard"
    @update:model-value="(v) => { if (!v) cancelCloseConfirm(); }"
  />
</template>

<style scoped>
.border-b {
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}

.di-resource-editor-body {
  min-height: 0;
}

.di-resource-editor-body :deep(.di-collabora-editor) {
  border: 0;
  border-radius: 0;
  height: 100%;
}

.di-resource-editor-body :deep(.di-collabora-editor__frame-wrap) {
  height: calc(100vh - 72px);
  max-height: none;
  min-height: 0;
}
</style>
