/** HTML → düz metin (tablo hücresi, geçmiş özeti vb.) */
export function stripHtmlToPlainText(html: string | null | undefined): string {
  if (html == null) return '';
  const s = String(html).trim();
  if (!s) return '';
  if (typeof document !== 'undefined') {
    const d = document.createElement('div');
    d.innerHTML = s;
    const t = (d.textContent || d.innerText || '').replace(/\s+/g, ' ').trim();
    return t;
  }
  return s
    .replace(/<script[\s\S]*?>[\s\S]*?<\/script>/gi, '')
    .replace(/<style[\s\S]*?>[\s\S]*?<\/style>/gi, '')
    .replace(/<[^>]+>/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}
