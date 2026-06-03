/** Vuetify tema renkleri — özet kart aksanı (MO/DG config ile uyumlu). */
export const OC_SUMMARY_CARD_ACCENTS = [
  'primary',
  'secondary',
  'success',
  'info',
  'warning',
  'error',
] as const;

export type OcSummaryCardAccent = (typeof OC_SUMMARY_CARD_ACCENTS)[number];

/** Sık kullanılan MDI ikonları (özet kart rozeti). */
export const OC_SUMMARY_CARD_ICONS = [
  'mdi-counter',
  'mdi-folder-open-outline',
  'mdi-check-circle-outline',
  'mdi-account-check-outline',
  'mdi-alarm-light-outline',
  'mdi-progress-clock',
  'mdi-chart-line',
  'mdi-clipboard-list-outline',
  'mdi-flag-outline',
  'mdi-lightning-bolt-outline',
] as const;

export function readDashboardWidgetStyle(raw: Record<string, unknown>): {
  accentColor: string | null;
  icon: string | null;
} {
  let accentColor = pickStr(raw.accentColor ?? raw.AccentColor);
  let icon = pickStr(raw.icon ?? raw.Icon);
  const cfg = raw.config ?? raw.Config;
  if (cfg && typeof cfg === 'object') {
    const c = cfg as Record<string, unknown>;
    accentColor = accentColor ?? pickStr(c.accentColor ?? c.AccentColor);
    icon = icon ?? pickStr(c.icon ?? c.Icon);
  }
  return { accentColor, icon };
}

export function buildSummaryCardConfig(
  accentColor: string | null,
  icon: string | null
): Record<string, unknown> | undefined {
  const accent = accentColor?.trim() || null;
  const ic = icon?.trim() || null;
  if (!accent && !ic) return undefined;
  return { accentColor: accent, icon: ic };
}

/** Liste widget atanan avatarı — kişi id'sinden kararlı tema rengi (ek API yok). */
export function assigneeAvatarAccent(personId: string): OcSummaryCardAccent {
  let hash = 0;
  for (let i = 0; i < personId.length; i++) hash = (hash * 31 + personId.charCodeAt(i)) >>> 0;
  return OC_SUMMARY_CARD_ACCENTS[hash % OC_SUMMARY_CARD_ACCENTS.length];
}

function pickStr(v: unknown): string | null {
  if (v == null) return null;
  const s = String(v).trim();
  return s || null;
}
