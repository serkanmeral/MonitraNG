/**
 * Build Telegram deep-link for MonitraNG account binding.
 * Payload: link_{domainId}_{userId} (Telegram start param ≤ 64 chars).
 */
export function buildTelegramBindUrl(
  botUsername: string,
  domainId: string,
  userId: string
): string | null {
  const bot = (botUsername || '').trim().replace(/^@/, '');
  const domain = (domainId || '').trim();
  const user = (userId || '').trim();
  if (!bot || !domain || !user) return null;
  const payload = `link_${domain}_${user}`;
  if (payload.length > 64) return null;
  return `https://t.me/${bot}?start=${encodeURIComponent(payload)}`;
}
