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
      return new Intl.DateTimeFormat(intlLocale(opts.locale), {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(d);
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
      return new Intl.NumberFormat(intlLocale(opts.locale)).format(n);
    }
    case 'money': {
      const n = toNumber(value);
      if (n === null) return EMPTY;
      return new Intl.NumberFormat(intlLocale(opts.locale), {
        style: 'currency',
        currency: opts.currency || 'TRY',
        maximumFractionDigits: 2,
      }).format(n);
    }
    case 'text':
    default:
      return String(value);
  }
}
