/** Cross-tab editör oturumu değişikliği bildirimi (BroadcastChannel). */
const CHANNEL_NAME = 'di-editor-sessions';

export type DiEditorSessionBroadcastMessage = {
  type: 'changed';
  at: number;
};

export function notifyEditorSessionChanged(): void {
  if (typeof BroadcastChannel === 'undefined') return;
  try {
    const channel = new BroadcastChannel(CHANNEL_NAME);
    const message: DiEditorSessionBroadcastMessage = { type: 'changed', at: Date.now() };
    channel.postMessage(message);
    channel.close();
  } catch {
    // Best-effort — tek sekme ortamında sessizce devam.
  }
}

export function subscribeEditorSessionChanges(onChange: () => void): () => void {
  if (typeof BroadcastChannel === 'undefined') return () => undefined;

  let channel: BroadcastChannel | null = null;
  try {
    channel = new BroadcastChannel(CHANNEL_NAME);
    channel.onmessage = () => onChange();
  } catch {
    return () => undefined;
  }

  return () => {
    channel?.close();
    channel = null;
  };
}
