<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import DiCollaboraEditor from '@/components/apps/document-intelligence/DiCollaboraEditor.vue';
import DiEditorLockDialog from '@/components/apps/document-intelligence/DiEditorLockDialog.vue';
import DiFileVersionHistoryDialog from '@/components/apps/document-intelligence/DiFileVersionHistoryDialog.vue';
import DiSaveVersionNoteDialog from '@/components/apps/document-intelligence/DiSaveVersionNoteDialog.vue';
import DiEditorCloseConfirmDialog from '@/components/apps/document-intelligence/DiEditorCloseConfirmDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import { useDiEditorSessionCleanup } from '@/composables/useDiEditorSessionCleanup';
import { useDiEditorLockGate } from '@/composables/useDiEditorLockGate';
import { useDiEditorVersionWatch } from '@/composables/useDiEditorVersionWatch';
import { useDiEditorCloseGuard } from '@/composables/useDiEditorCloseGuard';
import {
  diGetById,
  diGetResourceEditorSession,
  diUpdateFileVersionChangeNote,
} from '@/services/documentIntelligenceService';
import { DI_HOME_PATH } from '@/utils/diResourceLink';
import { diPageResourceLabel } from '@/utils/diPageResource';
import { isDiOfficeEditable } from '@/utils/diFilePreview';
import type { DiResource, DiResourceEditorOpenOptions } from '@/types/apps/documentIntelligence';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const { push } = useAppToast();
const panelError = usePanelErrorNotify('errors.dg.generic');
const route = useRoute();
const { trackEditorAccessToken, releaseEditorSession } = useDiEditorSessionCleanup();
const {
  dialogOpen: lockDialogOpen,
  lockStatus,
  gateResourceEditor,
  onDialogChoose,
  onDialogUpdate,
} = useDiEditorLockGate();

const resourceId = computed(() => String(route.params.id ?? '').trim());
const readOnlyQuery = computed(
  () => route.query.readOnly === '1' || route.query.readOnly === 'true',
);
const bypassLockQuery = computed(
  () => route.query.bypassLock === '1' || route.query.bypassLock === 'true',
);
const hasOpenIntent = computed(() => readOnlyQuery.value || bypassLockQuery.value);

const resource = ref<DiResource | null>(null);
const editorUrl = ref<string | null>(null);
const editorReadOnly = ref(false);
const lockEnforced = ref(false);
const loading = ref(true);
const error = ref<string | null>(null);

const historyDialog = ref(false);
const versionNoteDialog = ref(false);
const pendingVersionNote = ref<number | null>(null);
const savingVersionNote = ref(false);
const pendingCloseAfterNote = ref(false);
let lastNotifiedVersion = 0;

const collaboraEditorRef = ref<InstanceType<typeof DiCollaboraEditor> | null>(null);

async function closeTabConfirmed() {
  await releaseEditorSession();
  window.close();
}

const initialVersion = computed(() => resource.value?.currentVersionNumber ?? 0);
const editorActive = computed(() => Boolean(editorUrl.value) && !loading.value && !error.value);

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
    if (resource.value) {
      resource.value = { ...resource.value, currentVersionNumber: newVersion };
    }
  },
});

async function finishCloseAfterSave() {
  pendingCloseAfterNote.value = true;
  const versionBumped = await checkVersionAfterSave();
  if (versionBumped || versionNoteDialog.value || pendingVersionNote.value) {
    return;
  }
  pendingCloseAfterNote.value = false;
  await closeTabConfirmed();
}

function finishPendingCloseIfNeeded() {
  if (!pendingCloseAfterNote.value) return;
  pendingCloseAfterNote.value = false;
  void closeTabConfirmed();
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
  onForceClose: () => void closeTabConfirmed(),
  onAfterCloseSave: finishCloseAfterSave,
});

const title = computed(() => {
  if (resource.value) return diPageResourceLabel(resource.value);
  return t('documentIntelligence.openInEditor');
});

async function loadEditor(options?: DiResourceEditorOpenOptions) {
  const id = resourceId.value;
  if (!id) {
    await navigateTo(DI_HOME_PATH, { replace: true });
    return;
  }

  await releaseEditorSession();
  loading.value = true;
  error.value = null;
  editorUrl.value = null;
  resource.value = null;
  lockEnforced.value = false;

  try {
    const r = await diGetById(id);
    if (!isDiOfficeEditable(r)) {
      error.value = t('documentIntelligence.errors.editorSession');
      return;
    }
    resource.value = r;
    const session = await diGetResourceEditorSession(id, r.fileName ?? r.name, options);
    editorReadOnly.value = session.readOnly;
    lockEnforced.value = Boolean(session.lockEnforced);
    editorUrl.value = session.editorUrl || null;
    trackEditorAccessToken(session.accessToken);
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.errors.editorSession');
  } finally {
    loading.value = false;
  }
}

async function initEditor() {
  const id = resourceId.value;
  if (!id) {
    await navigateTo(DI_HOME_PATH, { replace: true });
    return;
  }

  if (hasOpenIntent.value) {
    await loadEditor({
      readOnly: readOnlyQuery.value ? true : undefined,
      bypassLock: bypassLockQuery.value,
    });
    return;
  }

  const gate = await gateResourceEditor(id);
  if (!gate.proceed) {
    window.close();
    return;
  }
  await loadEditor(gate.options);
}

async function closeTab() {
  requestClose();
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
  resource.value = restored;
  currentVersion.value = restored.currentVersionNumber ?? currentVersion.value;
  lastNotifiedVersion = restored.currentVersionNumber ?? lastNotifiedVersion;
  await refreshVersion();
}

function onCollaboraDocumentSaved() {
  void checkVersionAfterSave();
}

onMounted(() => {
  void initEditor();
});
</script>

<template>
  <div class="di-editor-page d-flex flex-column h-100">
    <v-toolbar density="comfortable" color="surface" class="border-b flex-grow-0">
      <v-icon icon="mdi-file-document-edit-outline" class="ml-3 mr-2" color="primary" />
      <div class="px-1 py-1 flex-grow-1 min-width-0">
        <div class="text-subtitle-1 font-weight-bold text-truncate">
          {{ title }}
        </div>
        <div class="text-caption text-medium-emphasis">
          {{ t('documentIntelligence.editorNewTabHint') }}
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
      <v-btn variant="text" class="text-none mr-2" @click="closeTab">
        {{ t('documentIntelligence.close') }}
      </v-btn>
    </v-toolbar>

    <div class="flex-grow-1 min-height-0">
      <DiCollaboraEditor
        ref="collaboraEditorRef"
        :editor-url="editorUrl"
        :loading="loading"
        :error="error"
        :title="null"
        @document-saved="onCollaboraDocumentSaved"
      />
    </div>

    <DiEditorLockDialog
      :model-value="lockDialogOpen"
      :status="lockStatus"
      @update:model-value="onDialogUpdate"
      @choose="onDialogChoose"
    />

    <DiFileVersionHistoryDialog
      v-model="historyDialog"
      :resource="resource"
      :can-restore="resource?.permissions.canEdit ?? false"
      @restored="onVersionRestored"
    />

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
  </div>
</template>

<style scoped>
.di-editor-page {
  min-height: 100vh;
}

.border-b {
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}

.di-editor-page :deep(.di-collabora-editor) {
  border: 0;
  border-radius: 0;
  height: 100%;
}

.di-editor-page :deep(.di-collabora-editor__frame-wrap) {
  height: calc(100vh - 64px);
  max-height: none;
  min-height: 0;
}
</style>
