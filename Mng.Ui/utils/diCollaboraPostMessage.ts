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
  values: Record<string, unknown> = {},
) {
  if (!iframe?.contentWindow) return;
  iframe.contentWindow.postMessage(
    JSON.stringify({
      MessageId: messageId,
      SendTime: Date.now(),
      Values: values,
    }),
    resolveCollaboraTargetOrigin(editorUrl),
  );
}

export function applyCollaboraUiCustomizations(
  iframe: HTMLIFrameElement | null,
  editorUrl: string | null,
) {
  postCollaboraMessage(iframe, editorUrl, 'Host_PostmessageReady');
  postCollaboraMessage(iframe, editorUrl, 'Hide_Menu_Item', { id: 'help' });
  postCollaboraMessage(iframe, editorUrl, 'Hide_Button', { id: 'document-name' });
}

export type CollaboraHostMessageHandlers = {
  onDocumentLoaded?: () => void;
  onModifiedStatus?: (modified: boolean) => void;
  onSaveComplete?: () => void;
};

export function requestCollaboraSave(
  iframe: HTMLIFrameElement | null,
  editorUrl: string | null,
  options?: { dontTerminateEdit?: boolean },
) {
  postCollaboraMessage(iframe, editorUrl, 'Action_Save', {
    Notify: true,
    DontSaveIfUnmodified: true,
    DontTerminateEdit: options?.dontTerminateEdit ?? false,
  });
}

function readMessageId(payload: Record<string, unknown>): string {
  const raw = payload.MessageId ?? payload.messageId;
  return typeof raw === 'string' ? raw : '';
}

function readModifiedFlag(values: Record<string, unknown> | undefined): boolean | null {
  if (!values) return null;
  const raw = values.Modified ?? values.modified;
  if (typeof raw === 'boolean') return raw;
  if (raw === 'true') return true;
  if (raw === 'false') return false;
  return null;
}

export function handleCollaboraHostMessage(
  event: MessageEvent,
  editorUrl: string | null,
  iframe: HTMLIFrameElement | null,
  handlers?: CollaboraHostMessageHandlers,
): boolean {
  const expectedOrigin = resolveCollaboraTargetOrigin(editorUrl);
  if (expectedOrigin !== '*' && event.origin !== expectedOrigin) return false;

  let payload: Record<string, unknown>;
  try {
    payload =
      typeof event.data === 'string'
        ? (JSON.parse(event.data) as Record<string, unknown>)
        : (event.data as Record<string, unknown>);
  } catch {
    return false;
  }

  if (!payload || typeof payload !== 'object') return false;

  const messageId = readMessageId(payload);
  if (!messageId) return false;

  if (messageId === 'App_LoadingStatus') {
    const values = payload.Values as Record<string, unknown> | undefined;
    if (values?.Status === 'Document_Loaded') {
      applyCollaboraUiCustomizations(iframe, editorUrl);
      handlers?.onDocumentLoaded?.();
    }
    return true;
  }

  if (messageId === 'Doc_ModifiedStatus') {
    const values = payload.Values as Record<string, unknown> | undefined;
    const modified = readModifiedFlag(values);
    if (modified !== null) handlers?.onModifiedStatus?.(modified);
    return true;
  }

  if (messageId === 'Action_Save_Resp' || messageId === 'UI_Save') {
    handlers?.onSaveComplete?.();
    return true;
  }

  return true;
}
