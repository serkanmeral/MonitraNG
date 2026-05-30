import { formatDistanceStrict, formatDistanceToNowStrict } from 'date-fns';
import { enUS, tr } from 'date-fns/locale';
import type { OcColumnFormat } from '@/types/apps/operationCore';

const EMPTY = '—';

function dfLocale(locale?: string) {
  return locale?.toLowerCase().startsWith('tr') ? tr : enUS;
}

function intlLocale(locale?: string): string {
  return locale?.toLowerCase().startsWith('tr') ? 'tr-TR' : 'en-US';
}

// Intl formatter'ları kurmak pahalı; liste hücrelerinde (50 satır × sütun) yüzlerce kez
// çağrılıyor. Locale/currency anahtarlı memoize — çıktı birebir aynı.
const dateFmtCache = new Map<string, Intl.DateTimeFormat>();
const numberFmtCache = new Map<string, Intl.NumberFormat>();
const moneyFmtCache = new Map<string, Intl.NumberFormat>();

function getDateFormatter(loc: string): Intl.DateTimeFormat {
  let fmt = dateFmtCache.get(loc);
  if (!fmt) {
    fmt = new Intl.DateTimeFormat(loc, { dateStyle: 'medium', timeStyle: 'short' });
    dateFmtCache.set(loc, fmt);
  }
  return fmt;
}

function getNumberFormatter(loc: string): Intl.NumberFormat {
  let fmt = numberFmtCache.get(loc);
  if (!fmt) {
    fmt = new Intl.NumberFormat(loc);
    numberFmtCache.set(loc, fmt);
  }
  return fmt;
}

function getMoneyFormatter(loc: string, currency: string): Intl.NumberFormat {
  const key = `${loc}|${currency}`;
  let fmt = moneyFmtCache.get(key);
  if (!fmt) {
    fmt = new Intl.NumberFormat(loc, { style: 'currency', currency, maximumFractionDigits: 2 });
    moneyFmtCache.set(key, fmt);
  }
  return fmt;
}

function toDate(value: unknown): Date | null {
  if (value === null || value === undefined || value === '') return null;
  if (value instanceof Date) return Number.isNaN(value.getTime()) ? null : value;
  const d = new Date(String(value));
  return Number.isNaN(d.getTime()) ? null : d;
}

function toNumber(value: unknown): number | null {
  if (value === null || value === undefined || value === '') return null;
  const n = typeof value === 'number' ? value : Number(String(value).replace(/\s/g, '').replace(',', '.'));
  return Number.isFinite(n) ? n : null;
}

export interface OcFormatOptions {
  /** i18n yerel kodu (örn. 'tr', 'en'). */
  locale?: string;
  /** relativeTime için bitiş anchor'ı (kapalı item'da closedAt). Yoksa "şimdi". */
  anchorEnd?: string | Date | null;
  /** money formatı için para birimi (varsayılan TRY). */
  currency?: string;
}

/**
 * Liste hücresi değerini verilen formata göre okunabilir metne çevirir.
 * Tüm sütun tipleri (sistem + pool + computed) için ortak katman.
 */
export function formatCellValue(
  value: unknown,
  format: OcColumnFormat | null | undefined,
  opts: OcFormatOptions = {}
): string {
  if (value === null || value === undefined || value === '') return EMPTY;

  switch (format) {
    case 'date': {
      const d = toDate(value);
      if (!d) return EMPTY;
      return getDateFormatter(intlLocale(opts.locale)).format(d);
    }
    case 'relativeTime': {
      const start = toDate(value);
      if (!start) return EMPTY;
      const end = opts.anchorEnd ? toDate(opts.anchorEnd) : null;
      if (end) {
        // Kapalı item: oluşturma → kapanış (çözüm süresi), ek-sonek yok.
        return formatDistanceStrict(start, end, { locale: dfLocale(opts.locale) });
      }
      return formatDistanceToNowStrict(start, { addSuffix: true, locale: dfLocale(opts.locale) });
    }
    case 'number': {
      const n = toNumber(value);
      if (n === null) return EMPTY;
      return getNumberFormatter(intlLocale(opts.locale)).format(n);
    }
    case 'money': {
      const n = toNumber(value);
      if (n === null) return EMPTY;
      return getMoneyFormatter(intlLocale(opts.locale), opts.currency || 'TRY').format(n);
    }
    case 'text':
    default:
      return String(value);
  }
}
