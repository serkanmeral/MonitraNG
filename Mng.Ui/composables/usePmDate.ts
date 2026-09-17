import { pmFormatDate, pmFormatDateTime } from '@/services/projectManagementService';

export function usePmDate() {
  const { locale } = useAppI18n();

  function formatPmDate(value?: string | null): string {
    return pmFormatDate(value, locale());
  }

  function formatPmDateTime(value?: string | null): string {
    return pmFormatDateTime(value, locale());
  }

  function formatPmDateOrDash(value?: string | null): string {
    return formatPmDate(value) || '—';
  }

  function formatPmDateTimeOrDash(value?: string | null): string {
    return formatPmDateTime(value) || '—';
  }

  function formatPmDateRange(start?: string | null, finish?: string | null): string {
    const from = formatPmDate(start);
    const to = formatPmDate(finish);
    if (!from && !to) return '';
    return `${from || '—'} → ${to || '—'}`;
  }

  return { formatPmDate, formatPmDateTime, formatPmDateOrDash, formatPmDateTimeOrDash, formatPmDateRange };
}
