import { computed, ref } from 'vue';

export interface AppToastPayload {
  title: string;
  message: string;
  notificationType?: string | null;
  deepLink?: string | null;
  severity?: string | number | null;
}

export interface AppToastItem extends AppToastPayload {
  id: string;
  color: string;
  timeout: number;
}

const MAX_VISIBLE = 6;
const items = ref<AppToastItem[]>([]);
const timers = new Map<string, ReturnType<typeof setTimeout>>();

function resolveToastStyle(payload: AppToastPayload): { color: string; timeout: number } {
  const severityRaw = payload.severity != null ? String(payload.severity).trim().toLowerCase() : '';
  switch (severityRaw) {
    case 'error':
      return { color: 'error', timeout: 10_000 };
    case 'warning':
      return { color: 'warning', timeout: 6_000 };
    case 'success':
      return { color: 'success', timeout: 3_500 };
    case 'info':
      return { color: 'info', timeout: 4_000 };
  }

  const severityNum = typeof payload.severity === 'number'
    ? payload.severity
    : payload.severity != null && payload.severity !== ''
      ? Number(payload.severity)
      : NaN;

  if (!Number.isNaN(severityNum) && severityNum >= 8) {
    return { color: 'error', timeout: 10_000 };
  }

  switch (payload.notificationType) {
    case 'CommentMention':
      return { color: 'primary', timeout: 4_000 };
    case 'WorkItemAssigned':
      return { color: 'success', timeout: 3_500 };
    default:
      return { color: 'info', timeout: 4_000 };
  }
}

function clearTimer(id: string) {
  const timer = timers.get(id);
  if (timer != null) {
    clearTimeout(timer);
    timers.delete(id);
  }
}

function dismiss(id?: string) {
  if (!id) {
    for (const item of items.value) clearTimer(item.id);
    timers.clear();
    items.value = [];
    return;
  }
  clearTimer(id);
  items.value = items.value.filter(item => item.id !== id);
}

function scheduleDismiss(item: AppToastItem) {
  clearTimer(item.id);
  if (item.timeout <= 0) return;
  timers.set(
    item.id,
    setTimeout(() => dismiss(item.id), item.timeout),
  );
}

export function useAppToast() {
  function push(payload: AppToastPayload) {
    const title = payload.title?.trim();
    const message = payload.message?.trim();
    if (!title && !message) return;

    const style = resolveToastStyle(payload);
    const item: AppToastItem = {
      id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
      title: title || message || '',
      message: message || title || '',
      notificationType: payload.notificationType,
      deepLink: payload.deepLink,
      severity: payload.severity,
      color: style.color,
      timeout: style.timeout,
    };

    // Newest on top; drop oldest if stack is full.
    const overflow = items.value.slice(MAX_VISIBLE - 1);
    for (const old of overflow) clearTimer(old.id);
    items.value = [item, ...items.value].slice(0, MAX_VISIBLE);
    scheduleDismiss(item);
  }

  async function onAction(id: string) {
    const item = items.value.find(entry => entry.id === id);
    const link = item?.deepLink?.trim();
    dismiss(id);
    if (link) await navigateTo(link);
  }

  return {
    /** @deprecated Prefer `items` — kept for older callers that only read the top toast. */
    active: computed(() => items.value[0] ?? null),
    /** @deprecated Stacked toasts are always "visible" when `items` is non-empty. */
    visible: computed({
      get: () => items.value.length > 0,
      set: (value: boolean) => {
        if (!value) dismiss();
      },
    }),
    items: computed(() => items.value),
    push,
    dismiss,
    onAction,
  };
}
