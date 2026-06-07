import { computed, ref } from 'vue';

export interface AppToastPayload {
  title: string;
  message: string;
  notificationType?: string | null;
  deepLink?: string | null;
  severity?: string | number | null;
}

interface ToastQueueItem extends AppToastPayload {
  id: string;
  color: string;
  timeout: number;
}

const queue = ref<ToastQueueItem[]>([]);
const active = ref<ToastQueueItem | null>(null);
const visible = ref(false);

function resolveToastStyle(payload: AppToastPayload): { color: string; timeout: number } {
  const severityRaw = payload.severity != null ? String(payload.severity).trim().toLowerCase() : '';
  switch (severityRaw) {
    case 'error':
      return { color: 'error', timeout: 12_000 };
    case 'warning':
      return { color: 'warning', timeout: 8_000 };
    case 'success':
      return { color: 'success', timeout: 6_000 };
    case 'info':
      return { color: 'info', timeout: 6_000 };
  }

  const severityNum = typeof payload.severity === 'number'
    ? payload.severity
    : payload.severity != null && payload.severity !== ''
      ? Number(payload.severity)
      : NaN;

  if (!Number.isNaN(severityNum) && severityNum >= 8) {
    return { color: 'error', timeout: 12_000 };
  }

  switch (payload.notificationType) {
    case 'CommentMention':
      return { color: 'primary', timeout: 6_000 };
    case 'WorkItemAssigned':
      return { color: 'success', timeout: 6_000 };
    default:
      return { color: 'info', timeout: 6_000 };
  }
}

function showNext() {
  if (active.value || queue.value.length === 0) return;
  active.value = queue.value.shift() ?? null;
  visible.value = !!active.value;
}

function dismiss() {
  visible.value = false;
  active.value = null;
  showNext();
}

export function useAppToast() {
  function push(payload: AppToastPayload) {
    const title = payload.title?.trim();
    const message = payload.message?.trim();
    if (!title && !message) return;

    const style = resolveToastStyle(payload);
    queue.value.push({
      id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
      title: title || message || '',
      message: message || title || '',
      notificationType: payload.notificationType,
      deepLink: payload.deepLink,
      severity: payload.severity,
      color: style.color,
      timeout: style.timeout,
    });

    if (!active.value) showNext();
  }

  async function onAction() {
    const link = active.value?.deepLink?.trim();
    dismiss();
    if (link) await navigateTo(link);
  }

  return {
    active: computed(() => active.value),
    visible,
    push,
    dismiss,
    onAction,
  };
}
