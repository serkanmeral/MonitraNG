import { onBeforeUnmount, onMounted } from 'vue';
import { diEndEditorSession, diEndEditorSessionKeepalive } from '@/services/documentIntelligenceService';
import { notifyEditorSessionChanged } from '@/utils/diEditorSessionBroadcast';

export interface DiEditorSessionCleanupOptions {
  /** Collabora Action_Save — sayfa kapanmadan önce WOPI PutFile tetikler. */
  requestSave?: () => Promise<void>;
}

/** Collabora editör oturumunu kapatır (D-E1 — sayfa/dialog/sekme kapanışı). */
export function useDiEditorSessionCleanup(options?: DiEditorSessionCleanupOptions) {
  let accessToken: string | null = null;
  let released = false;

  async function flushCollaboraSave() {
    try {
      await options?.requestSave?.();
    } catch {
      // Best-effort; oturum kapatımını engelleme.
    }
  }

  function trackEditorAccessToken(value: string | null | undefined) {
    released = false;
    accessToken = value?.trim() || null;
    if (accessToken) notifyEditorSessionChanged();
  }

  function releaseEditorSessionKeepalive() {
    if (released) return;
    const token = accessToken?.trim();
    if (!token) return;
    released = true;
    accessToken = null;
    void flushCollaboraSave();
    diEndEditorSessionKeepalive(token);
    notifyEditorSessionChanged();
  }

  async function releaseEditorSession() {
    if (released) return;
    const token = accessToken?.trim();
    if (!token) return;
    await flushCollaboraSave();
    released = true;
    accessToken = null;
    try {
      await diEndEditorSession(token);
    } catch {
      // Kapanışta best-effort; kullanıcıyı rahatsız etme.
    } finally {
      notifyEditorSessionChanged();
    }
  }

  onMounted(() => {
    if (!import.meta.client) return;
    // Sekme kapanırken async fetch tamamlanmayabilir — keepalive POST kullan.
    window.addEventListener('pagehide', releaseEditorSessionKeepalive);
  });

  onBeforeUnmount(() => {
    if (import.meta.client) {
      window.removeEventListener('pagehide', releaseEditorSessionKeepalive);
    }
    void releaseEditorSession();
  });

  return { trackEditorAccessToken, releaseEditorSession, releaseEditorSessionKeepalive };
}
