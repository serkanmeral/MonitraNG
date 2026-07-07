import { ref, type Ref } from 'vue';
import type DiCollaboraEditor from '@/components/apps/document-intelligence/DiCollaboraEditor.vue';

type CollaboraEditorRef = Ref<InstanceType<typeof DiCollaboraEditor> | null>;

export function useDiEditorCloseGuard(options: {
  collaboraRef: CollaboraEditorRef;
  readOnly: Ref<boolean>;
  onForceClose: () => void;
  /** Kaydet ile kapat — kayıt sonrası sürüm notu vb. tamamlanınca kapatma burada yapılır. */
  onAfterCloseSave?: () => void | Promise<void>;
}) {
  const closeConfirmOpen = ref(false);
  const closeConfirmSaving = ref(false);

  function canCloseWithoutPrompt(): boolean {
    if (options.readOnly.value) return true;
    if (options.collaboraRef.value?.isModified()) return false;
    return true;
  }

  function requestClose() {
    if (canCloseWithoutPrompt()) {
      options.onForceClose();
      return;
    }
    closeConfirmOpen.value = true;
  }

  function cancelCloseConfirm() {
    closeConfirmOpen.value = false;
  }

  async function confirmCloseSave() {
    closeConfirmSaving.value = true;
    try {
      await options.collaboraRef.value?.requestSave();
      closeConfirmOpen.value = false;
      if (options.onAfterCloseSave) {
        await options.onAfterCloseSave();
      } else {
        options.onForceClose();
      }
    } finally {
      closeConfirmSaving.value = false;
    }
  }

  function confirmCloseDiscard() {
    closeConfirmOpen.value = false;
    options.onForceClose();
  }

  return {
    closeConfirmOpen,
    closeConfirmSaving,
    requestClose,
    cancelCloseConfirm,
    confirmCloseSave,
    confirmCloseDiscard,
  };
}
