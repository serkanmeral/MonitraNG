export function resolveCollaboraTargetOrigin(editorUrl: string | null): string {
  if (!editorUrl) return '*';
  try {
    return new URL(editorUrl).origin;
  } catch {
    return '*';
  }
}

export function postCollaboraMessage(
  iframe: HTMLIFrameElement | null,
  editorUrl: string | null,
  messageId: string,
  values: Record<string, unknown> = {}
) {
  if (!iframe?.contentWindow) return;
  iframe.contentWindow.postMessage(
    JSON.stringify({
      MessageId: messageId,
      SendTime: Date.now(),
      Values: values,
    }),
    resolveCollaboraTargetOrigin(editorUrl)
  );
}

export function applyCollaboraUiCustomizations(
  iframe: HTMLIFrameElement | null,
  editorUrl: string | null
) {
  postCollaboraMessage(iframe, editorUrl, 'Host_PostmessageReady');
  postCollaboraMessage(iframe, editorUrl, 'Hide_Menu_Item', { id: 'help' });
  postCollaboraMessage(iframe, editorUrl, 'Hide_Button', { id: 'document-name' });
}

export function handleCollaboraHostMessage(
  event: MessageEvent,
  editorUrl: string | null,
  iframe: HTMLIFrameElement | null,
  onDocumentLoaded?: () => void
) {
  const expectedOrigin = resolveCollaboraTargetOrigin(editorUrl);
  if (expectedOrigin !== '*' && event.origin !== expectedOrigin) return;
  if (event.source !== iframe?.contentWindow) return;

  let payload: Record<string, unknown>;
  try {
    payload =
      typeof event.data === 'string'
        ? (JSON.parse(event.data) as Record<string, unknown>)
        : (event.data as Record<string, unknown>);
  } catch {
    return;
  }

  if (payload.MessageId !== 'App_LoadingStatus') return;
  const values = payload.Values as Record<string, unknown> | undefined;
  if (values?.Status !== 'Document_Loaded') return;

  applyCollaboraUiCustomizations(iframe, editorUrl);
  onDocumentLoaded?.();
}
