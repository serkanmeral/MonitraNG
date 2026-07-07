import { ref } from 'vue';
import { diGetResourceEditorLockStatus } from '@/services/documentIntelligenceService';
import type {
  DiDocumentEditorLockStatus,
  DiEditorLockChoice,
  DiResourceEditorOpenOptions,
} from '@/types/apps/documentIntelligence';

function isDocumentLocked(status: DiDocumentEditorLockStatus): boolean {
  return status.isLocked || status.isLockedByOthers || status.isLockedBySelf;
}

export function useDiEditorLockGate() {
  const dialogOpen = ref(false);
  const lockStatus = ref<DiDocumentEditorLockStatus | null>(null);
  let pendingResolve: ((choice: DiEditorLockChoice) => void) | null = null;

  function resolvePrompt(choice: DiEditorLockChoice) {
    pendingResolve?.(choice);
    pendingResolve = null;
    lockStatus.value = null;
  }

  function resolveWithoutDialog(status: DiDocumentEditorLockStatus): DiEditorLockChoice {
    if (!isDocumentLocked(status)) return 'edit';
    if (!status.warnOnActiveEditor) {
      if (status.enforceExclusiveLock && !status.canBypassLock) return 'readOnly';
      return 'edit';
    }
    return 'cancel';
  }

  function prompt(status: DiDocumentEditorLockStatus): Promise<DiEditorLockChoice> {
    if (!isDocumentLocked(status)) return Promise.resolve('edit');

    if (status.warnOnActiveEditor) {
      lockStatus.value = status;
      dialogOpen.value = true;
      return new Promise((resolve) => {
        pendingResolve = resolve;
      });
    }

    return Promise.resolve(resolveWithoutDialog(status));
  }

  function onDialogChoose(choice: DiEditorLockChoice) {
    dialogOpen.value = false;
    resolvePrompt(choice);
  }

  function onDialogUpdate(open: boolean) {
    dialogOpen.value = open;
    if (!open && pendingResolve) resolvePrompt('cancel');
  }

  async function gateResourceEditor(
    resourceId: string,
  ): Promise<{ proceed: boolean; options?: DiResourceEditorOpenOptions }> {
    try {
      const status = await diGetResourceEditorLockStatus(resourceId);
      const choice = await prompt(status);
      if (choice === 'cancel') return { proceed: false };
      if (choice === 'readOnly') return { proceed: true, options: { readOnly: true } };
      if (choice === 'edit' && status.canBypassLock) {
        return { proceed: true, options: { bypassLock: true, readOnly: false } };
      }
      return { proceed: true, options: { readOnly: false } };
    } catch {
      return { proceed: true };
    }
  }

  return {
    dialogOpen,
    lockStatus,
    prompt,
    gateResourceEditor,
    onDialogChoose,
    onDialogUpdate,
  };
}
