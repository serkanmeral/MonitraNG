const STORAGE_KEY = 'pdks:return';

export function rememberPassageReturn(fullPath: string) {
  const target = passageReturnPath(fullPath);
  if (!target || !import.meta.client) return;
  sessionStorage.setItem(STORAGE_KEY, target);
}

export function takePassageReturn(rawQuery: unknown): string {
  const fromQuery = passageReturnPath(firstText(rawQuery));
  const fromStore = import.meta.client ? sessionStorage.getItem(STORAGE_KEY) || '' : '';
  const target = fromQuery || passageReturnPath(fromStore);
  if (target && import.meta.client) sessionStorage.removeItem(STORAGE_KEY);
  return target;
}

export function passageReturnPath(value: string): string {
  let text = value.trim();
  if (!text || text.includes('\\')) return '';
  if (text.includes('%')) {
    try {
      text = decodeURIComponent(text);
    } catch {
      return '';
    }
  }
  if (text.includes('://')) {
    try {
      const url = new URL(text);
      text = `${url.pathname}${url.search}`;
    } catch {
      return '';
    }
  }
  if (text === '/gecis' || text.startsWith('/gecis?') || text.startsWith('/gecis/')) return text;
  return '';
}

function firstText(value: unknown): string {
  if (Array.isArray(value)) return String(value.find((item) => typeof item === 'string' && item) || '');
  return typeof value === 'string' ? value : '';
}
