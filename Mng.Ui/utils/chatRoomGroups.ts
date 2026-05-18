/**
 * Sohbet — Keycloak / JWT grup id’leri ve Keeper kullanıcı `groups[]` (çoğunlukla grup adı) ile çalışır.
 */

/** İlk geçiş sırasını koruyarak trim + tekil (büyük/küçük harf duyarsız). */
export function uniqueTrimmedPreserveOrder(xs: readonly string[]): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const x of xs) {
    const v = String(x ?? '').trim();
    if (!v) continue;
    const k = v.toLowerCase();
    if (seen.has(k)) continue;
    seen.add(k);
    out.push(v);
  }
  return out;
}

/** `displayNameCache` veya liste için kısa okunur etiket (path ise son segment). */
export function humanizeGroupDisplayToken(token: string): string {
  const t = String(token ?? '').trim();
  if (!t) return '';
  const parts = t.split(/[/\\]/).map((p) => p.trim()).filter(Boolean);
  const last = parts.length ? parts[parts.length - 1] : t;
  if (last && last !== t) return last;
  return t.length > 52 ? `${t.slice(0, 22)}…${t.slice(-10)}` : t;
}
