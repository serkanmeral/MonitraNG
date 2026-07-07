import { onBeforeUnmount, ref, watch, type Ref } from 'vue';
import { diGetById } from '@/services/documentIntelligenceService';

/** Yedek poll — postMessage kaçırılırsa sürüm senkronu (2 sn). */
const FALLBACK_POLL_INTERVAL_MS = 2000;
const SAVE_CHECK_ATTEMPTS = 12;
const SAVE_CHECK_DELAY_MS = 200;

export function useDiEditorVersionWatch(options: {
  resourceId: Ref<string | null | undefined>;
  initialVersion: Ref<number>;
  enabled: Ref<boolean>;
  readOnly: Ref<boolean>;
  onVersionSaved?: (newVersion: number) => void;
}) {
  const currentVersion = ref(options.initialVersion.value);
  let timer: ReturnType<typeof setInterval> | null = null;
  let saveCheckPromise: Promise<boolean> | null = null;

  function notifyVersionBump(serverVersion: number) {
    const previous = currentVersion.value;
    currentVersion.value = serverVersion;
    if (
      !options.readOnly.value
      && serverVersion > previous
      && previous > 0
    ) {
      options.onVersionSaved?.(serverVersion);
      return true;
    }
    return false;
  }

  async function fetchServerVersion(bypassEnabled = false): Promise<number | null> {
    const id = options.resourceId.value?.trim();
    if (!id || (!bypassEnabled && !options.enabled.value)) return null;

    try {
      const resource = await diGetById(id);
      const serverVersion = resource.currentVersionNumber ?? 0;
      return serverVersion > 0 ? serverVersion : null;
    } catch {
      return null;
    }
  }

  async function checkVersionOnce(bypassEnabled = false): Promise<boolean> {
    const serverVersion = await fetchServerVersion(bypassEnabled);
    if (serverVersion == null) return false;

    if (serverVersion > currentVersion.value) {
      return notifyVersionBump(serverVersion);
    }

    currentVersion.value = serverVersion;
    return false;
  }

  async function poll() {
    await checkVersionOnce();
  }

  /** Collabora kaydı sonrası — WOPI PutFile bitene kadar kısa aralıklarla dener. Eşzamanlı çağrılar aynı promise'i paylaşır. */
  async function checkVersionAfterSave(): Promise<boolean> {
    if (options.readOnly.value) return false;
    if (saveCheckPromise) return saveCheckPromise;

    saveCheckPromise = (async (): Promise<boolean> => {
      try {
        for (let attempt = 0; attempt < SAVE_CHECK_ATTEMPTS; attempt++) {
          if (await checkVersionOnce(true)) return true;
          if (attempt < SAVE_CHECK_ATTEMPTS - 1) {
            await new Promise((resolve) => setTimeout(resolve, SAVE_CHECK_DELAY_MS));
          }
        }
        return false;
      } finally {
        saveCheckPromise = null;
      }
    })();

    return saveCheckPromise;
  }

  function stopPoll() {
    if (timer) {
      clearInterval(timer);
      timer = null;
    }
  }

  function startPoll() {
    stopPoll();
    const id = options.resourceId.value?.trim();
    if (!options.enabled.value || !id) return;

    void poll();
    timer = setInterval(() => void poll(), FALLBACK_POLL_INTERVAL_MS);
  }

  watch(
    () => [options.enabled.value, options.resourceId.value, options.initialVersion.value] as const,
    ([enabled, id, initial]) => {
      if (initial > 0) currentVersion.value = initial;
      if (enabled && id?.trim()) startPoll();
      else stopPoll();
    },
    { immediate: true },
  );

  onBeforeUnmount(stopPoll);

  return { currentVersion, refreshVersion: poll, checkVersionAfterSave };
}
