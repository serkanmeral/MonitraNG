import DOMPurify from 'dompurify';

/** Yorum / richtext alanları — TipTap StarterKit ile uyumlu izin listesi. */
export const OC_RICH_TEXT_ALLOWED_TAGS = [
  'p',
  'br',
  'strong',
  'b',
  'em',
  'i',
  's',
  'strike',
  'del',
  'ul',
  'ol',
  'li',
  'a',
  'code',
  'pre',
  'blockquote',
  'span',
] as const;

export const OC_RICH_TEXT_ALLOWED_ATTR = ['href', 'target', 'rel'] as const;

/** Yorum / açıklama editörü — hafif emoji paleti (OcCommentComposer ile aynı). */
export const OC_RICH_TEXT_EMOJIS = [
  '😀', '😃', '😄', '😁', '😆', '😅', '😂', '🤣', '😊', '🙂',
  '🙃', '😉', '😍', '😘', '😎', '🤩', '🥳', '🤔', '🤨', '😐',
  '😴', '😢', '😭', '😤', '😡', '👍', '👎', '👏', '🙏', '💪',
  '🔥', '✅', '❌', '⚠️', '🎉', '💡', '📌', '📎', '❤️', '🚀',
] as const;

const HTML_TAG_RE = /<[a-z][\s\S]*>/i;

/**
 * Eski workspace kayıtları düz metin saklar; TipTap/DOMPurify için minimal HTML'e çevirir.
 */
export function normalizeOcRichTextHtml(html: string | null | undefined): string {
  if (html == null) return '';
  const trimmed = html.trim();
  if (!trimmed) return '';
  if (HTML_TAG_RE.test(trimmed)) return trimmed;

  const escaped = trimmed
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
  const withBreaks = escaped.replace(/\r\n|\r|\n/g, '<br>');
  return `<p>${withBreaks}</p>`;
}

/** HTML gövdesini XSS'e karşı sanitize eder (client-only). */
export function sanitizeOcRichHtml(html: string | null | undefined): string {
  const normalized = normalizeOcRichTextHtml(html);
  if (!normalized) return '';
  if (!import.meta.client) return normalized;
  return DOMPurify.sanitize(normalized, {
    ALLOWED_TAGS: [...OC_RICH_TEXT_ALLOWED_TAGS],
    ALLOWED_ATTR: [...OC_RICH_TEXT_ALLOWED_ATTR],
  });
}

const EMPTY_HTML_RE = /^(<p>\s*(<br\s*\/?>)?\s*<\/p>|<br\s*\/?>|\s)*$/i;

/** Zorunlu alan / form validasyonu — boş TipTap HTML'i. */
export function isOcRichTextEmpty(html: string | null | undefined): boolean {
  if (html == null) return true;
  const trimmed = html.trim();
  if (!trimmed) return true;
  if (!HTML_TAG_RE.test(trimmed)) return false;
  if (EMPTY_HTML_RE.test(trimmed.replace(/\s/g, ''))) return true;
  const text = trimmed
    .replace(/<[^>]+>/g, ' ')
    .replace(/&nbsp;/gi, ' ')
    .replace(/\s+/g, ' ')
    .trim();
  return text.length === 0;
}

/** Readonly özet (tek satır liste vb.) — düz metin. */
export function ocRichTextToPlainText(html: string | null | undefined): string {
  if (!html) return '';
  return html
    .replace(/<[^>]+>/g, ' ')
    .replace(/&nbsp;/gi, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}
