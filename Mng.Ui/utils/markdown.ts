import { marked } from 'marked';
import DOMPurify from 'dompurify';
import { getDataGatewayProxyUrlWithAuth } from '@/services/apiService';
import { parseDiFilePathFromMarkdownHref } from '@/utils/diResourceLink';

// Markdown -> güvenli HTML. Render client tarafında yapılır (DOMPurify DOM gerektirir).
// İçerik MngDocument'te ham markdown olarak saklanır; XSS'e karşı sanitize edilir.

marked.setOptions({
  gfm: true,
  breaks: true,
});

/** di-fp: gömülü görselleri render öncesi DG proxy URL'sine çevirir (DOMPurify bilinmeyen şemayı siler). */
function preprocessDiFileImageMarkdown(markdown: string): string {
  return markdown.replace(
    /!\[([^\]]*)\]\((di-fp:[^)\s]+)\)/gi,
    (full, alt, href) => {
      const filePath = parseDiFilePathFromMarkdownHref(href);
      if (!filePath) return full;
      const url = getDataGatewayProxyUrlWithAuth(
        `/api/v1/files/download?filePath=${encodeURIComponent(filePath)}`
      );
      return `![${alt}](${url})`;
    }
  );
}

const ALLOWED_TAGS = [
  'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
  'p', 'br', 'hr', 'span', 'div',
  'strong', 'b', 'em', 'i', 's', 'strike', 'del', 'mark', 'sub', 'sup',
  'ul', 'ol', 'li',
  'a', 'img',
  'code', 'pre', 'kbd', 'samp',
  'blockquote',
  'table', 'thead', 'tbody', 'tfoot', 'tr', 'th', 'td',
  'input', // task list checkbox'ları (disabled)
];

const ALLOWED_ATTR = [
  'href', 'target', 'rel', 'title',
  'src', 'alt', 'width', 'height',
  'class', 'align',
  'type', 'checked', 'disabled', // task list
  'colspan', 'rowspan',
];

/** Ham markdown'ı güvenli HTML'e çevirir. SSR'da boş döner (DOMPurify client gerektirir). */
export function renderMarkdown(markdown: string | null | undefined): string {
  if (!markdown) return '';
  if (!import.meta.client) return '';
  const prepared = preprocessDiFileImageMarkdown(markdown);
  const rawHtml = marked.parse(prepared, { async: false }) as string;
  const clean = DOMPurify.sanitize(rawHtml, {
    ALLOWED_TAGS,
    ALLOWED_ATTR,
    ALLOW_DATA_ATTR: false,
  });
  // Dış bağlantılar yeni sekmede + güvenli rel.
  return clean;
}

/** Markdown'dan düz metin özeti (arama/önizleme satırı için). */
export function markdownToPlainText(markdown: string | null | undefined, maxLength = 160): string {
  if (!markdown) return '';
  const text = markdown
    .replace(/```[\s\S]*?```/g, ' ')
    .replace(/`[^`]*`/g, ' ')
    .replace(/!\[[^\]]*\]\([^)]*\)/g, ' ')
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/[#>*_~-]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
  if (text.length <= maxLength) return text;
  return text.slice(0, maxLength).trimEnd() + '…';
}
