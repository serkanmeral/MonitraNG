/**
 * Durum rengi — Vuetify tema anahtarları (dark/light ile uyumlu).
 * Eski kurulumlarda #hex değerleri kalabilir; UI’da geri uyumluluk için ayrı gösterilir.
 */
export const TM_STATUS_THEME_COLORS = ['primary', 'secondary', 'success', 'info', 'warning', 'error'] as const;

export type TmStatusThemeColor = (typeof TM_STATUS_THEME_COLORS)[number];

export function isTmStatusThemeColor(s: string | null | undefined): s is TmStatusThemeColor {
  if (!s?.trim()) return false;
  return (TM_STATUS_THEME_COLORS as readonly string[]).includes(s.trim());
}

export function isLegacyHexStatusColor(s: string | null | undefined): boolean {
  return !!s?.trim().startsWith('#');
}
