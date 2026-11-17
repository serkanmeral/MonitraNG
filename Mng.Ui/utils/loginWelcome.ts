import DOMPurify from 'dompurify';
import {
  isOcRichTextEmpty,
  normalizeOcRichTextHtml,
  OC_RICH_TEXT_ALLOWED_ATTR,
  OC_RICH_TEXT_ALLOWED_TAGS,
} from '@/utils/ocRichText';

export const LOGIN_WELCOME_DATASET = 'ui_karsilama_mesajlari';

const IMAGE_EXTENSIONS = new Set(['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp', 'svg']);
const WELCOME_CLASS = /^welcome-[a-z0-9-]+$/;

/** Welcome copy may use welcome-* classes. Other classes and event attributes are removed. */
export function sanitizeLoginWelcomeHtml(html: string | null | undefined): string {
  const normalized = normalizeOcRichTextHtml(html);
  if (!normalized) return '';
  if (!import.meta.client) return normalized;

  const keepWelcomeClass = (_node: Element, data: { attrName: string; attrValue: string; keepAttr?: boolean }) => {
    if (data.attrName !== 'class') return;
    const kept = data.attrValue.split(/\s+/).filter((token) => WELCOME_CLASS.test(token));
    if (!kept.length) {
      data.keepAttr = false;
      return;
    }
    data.attrValue = kept.join(' ');
  };

  DOMPurify.addHook('uponSanitizeAttribute', keepWelcomeClass);
  try {
    return DOMPurify.sanitize(normalized, {
      ALLOWED_TAGS: [...OC_RICH_TEXT_ALLOWED_TAGS],
      ALLOWED_ATTR: [...OC_RICH_TEXT_ALLOWED_ATTR, 'class'],
    });
  } finally {
    DOMPurify.removeHook('uponSanitizeAttribute', keepWelcomeClass);
  }
}

export interface LoginWelcomeMessage {
  id: string;
  title: string;
  html: string;
  imagePath: string | null;
  imageExtension: string | null;
}

export interface LoginWelcomeGroupLookup {
  /** Group id → group name. Used when a message stores ids instead of names. */
  nameById: Map<string, string>;
}

function text(value: unknown): string {
  if (typeof value === 'string') return value.trim();
  if (typeof value === 'number' && Number.isFinite(value)) return String(value);
  return '';
}

function calendarDay(value: unknown): string | null {
  if (value == null || value === '') return null;
  const date = new Date(String(value));
  if (Number.isNaN(date.getTime())) return null;
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

export function localCalendarDay(date = new Date()): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

function filePath(value: unknown): string | null {
  if (typeof value === 'string') return value.trim() || null;
  if (value && typeof value === 'object' && typeof (value as { path?: unknown }).path === 'string') {
    const path = (value as { path: string }).path.trim();
    return path || null;
  }
  return null;
}

function fileExtension(value: unknown, path: string | null): string | null {
  if (value && typeof value === 'object') {
    const raw = text((value as { file_ext?: unknown }).file_ext);
    if (raw) return raw.replace(/^\./, '').toLowerCase();
  }
  if (!path) return null;
  const leaf = path.split('/').pop() || '';
  const dot = leaf.lastIndexOf('.');
  if (dot < 0 || dot === leaf.length - 1) return null;
  return leaf.slice(dot + 1).toLowerCase();
}

export function isLoginWelcomeImageExtension(extension: string | null | undefined): boolean {
  if (!extension) return false;
  return IMAGE_EXTENSIONS.has(extension.replace(/^\./, '').toLowerCase());
}

function groupTokens(value: unknown): string[] {
  const raw = Array.isArray(value) ? value : value == null || value === '' ? [] : [value];
  const tokens: string[] = [];
  for (const item of raw) {
    if (typeof item === 'string' || typeof item === 'number') {
      const token = text(item);
      if (token) tokens.push(token);
      continue;
    }
    if (!item || typeof item !== 'object') continue;
    const row = item as Record<string, unknown>;
    const name = text(row.name ?? row.Name);
    const id = text(row.groupId ?? row.GroupId ?? row.id ?? row.__dataId);
    if (name) tokens.push(name);
    else if (id) tokens.push(id);
  }
  return tokens;
}

function createdMillis(row: Record<string, unknown>): number {
  const info = row.__createInfo ?? row.__CreateInfo;
  if (info && typeof info === 'object') {
    const raw = (info as { createdAt?: unknown; CreatedAt?: unknown }).createdAt
      ?? (info as { CreatedAt?: unknown }).CreatedAt;
    const time = new Date(String(raw ?? '')).getTime();
    if (!Number.isNaN(time)) return time;
  }
  return Number.MAX_SAFE_INTEGER;
}

function orderKey(row: Record<string, unknown>): number {
  const raw = row.sira;
  if (typeof raw === 'number' && Number.isFinite(raw)) return raw;
  if (typeof raw === 'string' && raw.trim() !== '' && Number.isFinite(Number(raw))) return Number(raw);
  return Number.POSITIVE_INFINITY;
}

function visibleToUser(
  tokens: string[],
  userGroups: string[],
  lookup: LoginWelcomeGroupLookup | null,
): boolean {
  if (tokens.length === 0) return true;
  const names = new Set(userGroups.map((group) => group.trim().toLowerCase()).filter(Boolean));
  if (names.size === 0) return false;
  return tokens.some((token) => {
    const direct = token.trim().toLowerCase();
    if (names.has(direct)) return true;
    const resolved = lookup?.nameById.get(token)?.trim().toLowerCase();
    return !!resolved && names.has(resolved);
  });
}

function asRows(raw: unknown): Record<string, unknown>[] {
  if (Array.isArray(raw)) {
    return raw.filter((row): row is Record<string, unknown> => !!row && typeof row === 'object');
  }
  if (!raw || typeof raw !== 'object') return [];
  const obj = raw as Record<string, unknown>;
  const nested = obj.data ?? obj.items ?? obj.Data ?? obj.Items;
  if (!Array.isArray(nested)) return [];
  return nested.filter((row): row is Record<string, unknown> => !!row && typeof row === 'object');
}

export function selectLoginWelcomeMessages(
  raw: unknown,
  userGroups: string[],
  today = localCalendarDay(),
  lookup: LoginWelcomeGroupLookup | null = null,
): LoginWelcomeMessage[] {
  const rows = asRows(raw)
    .filter((row) => {
      const start = calendarDay(row.baslangic);
      const end = calendarDay(row.bitis);
      if (!start || !end || end < start) return false;
      if (today < start || today > end) return false;
      if (!visibleToUser(groupTokens(row.gruplar), userGroups, lookup)) return false;
      const html = text(row.html);
      const path = filePath(row.gorsel);
      return !isOcRichTextEmpty(html) || !!path;
    })
    .sort((left, right) => {
      const order = orderKey(left) - orderKey(right);
      if (order !== 0) return order;
      const start = (calendarDay(left.baslangic) || '').localeCompare(calendarDay(right.baslangic) || '');
      if (start !== 0) return start;
      return createdMillis(left) - createdMillis(right);
    });

  return rows.map((row) => {
    const path = filePath(row.gorsel);
    const html = sanitizeLoginWelcomeHtml(text(row.html));
    return {
      id: text(row.__dataId ?? row.dataId) || `${calendarDay(row.baslangic)}-${orderKey(row)}`,
      title: text(row.baslik) || 'Karşılama',
      html: isOcRichTextEmpty(html) ? '' : html,
      imagePath: path,
      imageExtension: fileExtension(row.gorsel, path),
    };
  });
}

export function messageNeedsGroupLookup(raw: unknown, userGroups: string[]): boolean {
  const names = new Set(userGroups.map((group) => group.trim().toLowerCase()).filter(Boolean));
  return asRows(raw).some((row) =>
    groupTokens(row.gruplar).some((token) => !names.has(token.trim().toLowerCase())),
  );
}
