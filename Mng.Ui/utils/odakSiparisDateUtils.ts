/** ISO datetime → `<input type="date">` value (YYYY-MM-DD). */
export function toDateInputValue(v: unknown): string {
  if (!v) return '';
  try {
    const d = new Date(String(v));
    if (Number.isNaN(d.getTime())) return '';
    return d.toISOString().slice(0, 10);
  } catch {
    return '';
  }
}

/** `<input type="date">` value → DG datetime (UTC end-of-day TR offset). */
export function fromDateInputValue(v: string): string | null {
  const trimmed = v.trim();
  if (!trimmed) return null;
  return `${trimmed}T21:00:00.0000000Z`;
}
