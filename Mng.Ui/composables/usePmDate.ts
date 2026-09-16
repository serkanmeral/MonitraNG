import { useAppI18n } from '@/composables/useAppI18n';
import { pmFormatDate } from '@/services/projectManagementService';

export function usePmDate() {
  const { locale } = useAppI18n();

  function formatPmDate(value?: string | null): string {
    return pmFormatDate(value, locale());
  }

  function formatPmDateOrDash(value?: string | null): string {
    return formatPmDate(value) || '—';
  }

  function formatPmDateRange(start?: string | null, finish?: string | null): string {
    const from = formatPmDate(start);
    const to = formatPmDate(finish);
    if (!from && !to) return '';
    return `${from || '—'} → ${to || '—'}`;
  }

  return { formatPmDate, formatPmDateOrDash, formatPmDateRange };
}
