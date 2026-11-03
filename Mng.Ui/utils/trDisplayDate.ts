const DATE_ONLY = /^(\d{4})-(\d{2})-(\d{2})$/;
const ISO_DATE_TIME = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:?\d{2})?$/;

const dateOptions: Intl.DateTimeFormatOptions = {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
};

const dateTimeOptions: Intl.DateTimeFormatOptions = {
  ...dateOptions,
  hour: '2-digit',
  minute: '2-digit',
};

function formatLocal(date: Date, withTime: boolean): string {
  return withTime
    ? date.toLocaleString('tr-TR', dateTimeOptions)
    : date.toLocaleDateString('tr-TR', dateOptions);
}

/** ISO tarih veya tarih-saat değerini Türkçe gösterim biçimine çevirir. Tanınmayan değerde null. */
export function formatTrDisplayDate(value: unknown): string | null {
  if (value instanceof Date) {
    if (Number.isNaN(value.getTime())) return null;
    const withTime = value.getHours() !== 0 || value.getMinutes() !== 0;
    return formatLocal(value, withTime);
  }
  if (typeof value !== 'string') return null;
  const text = value.trim();
  const dateOnly = DATE_ONLY.exec(text);
  if (dateOnly) {
    const date = new Date(Number(dateOnly[1]), Number(dateOnly[2]) - 1, Number(dateOnly[3]));
    if (Number.isNaN(date.getTime())) return null;
    return formatLocal(date, false);
  }
  if (!ISO_DATE_TIME.test(text)) return null;
  // Date-only values saved as UTC midnight should stay on that calendar day, without a clock.
  if (/T00:00:00(?:\.0+)?Z$/.test(text)) {
    const [year, month, day] = text.slice(0, 10).split('-').map(Number);
    const date = new Date(year, month - 1, day);
    if (Number.isNaN(date.getTime())) return null;
    return formatLocal(date, false);
  }
  const date = new Date(text);
  if (Number.isNaN(date.getTime())) return null;
  const withTime = date.getHours() !== 0 || date.getMinutes() !== 0;
  return formatLocal(date, withTime);
}
