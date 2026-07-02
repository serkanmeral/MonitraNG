import type { OdakLineRow } from '@/utils/odakSiparisConfig';
import { formatQualityRequirementsSummary } from '@/utils/odakSiparisCustomerQualityReqService';
import { formatOdakCurrencyAmount, formatOdakDate, formatOdakNumber } from '@/utils/odakSiparisService';
import { productLabelFromRow } from '@/utils/odakSiparisLineService';

export type OdakLineSummaryRow = { label: string; value: string };

function optionalLineRow(
  t: (key: string) => string,
  labelKey: string,
  value: unknown
): OdakLineSummaryRow | null {
  if (value == null || value === '') return null;
  return { label: t(labelKey), value: String(value) };
}

function boolLabel(v: unknown): string {
  if (v === true) return 'Evet';
  if (v === false) return 'Hayır';
  return '—';
}

/** Expand satirinda — tabloda olmayan ek alanlar. */
export function buildOdakLineExpandSummaryRows(
  line: OdakLineRow,
  t: (key: string) => string
): OdakLineSummaryRow[] {
  const rows: OdakLineSummaryRow[] = [
    { label: t('odakSiparis.lines.fields.shippedQuantity'), value: formatOdakNumber(line.shippedQuantity) },
    { label: t('odakSiparis.lines.fields.productId'), value: productLabelFromRow(line.productId) },
  ];

  const extras = [
    optionalLineRow(t, 'odakSiparis.lines.fields.customerJobNo', line.customerJobNo),
    optionalLineRow(t, 'odakSiparis.lines.fields.poItemRevNo', line.poItemRevNo),
    optionalLineRow(t, 'odakSiparis.lines.fields.qualityRequirementIds', formatQualityRequirementsSummary(line)),
    optionalLineRow(t, 'odakSiparis.lines.fields.qualityReqs', line.qualityReqs),
    { label: t('odakSiparis.lines.fields.isFai'), value: boolLabel(line.isFai) },
    { label: t('odakSiparis.lines.fields.isFaiComplete'), value: boolLabel(line.isFaiComplete) },
    line.unitCost != null && line.unitCost !== ''
      ? {
          label: t('odakSiparis.lines.fields.unitCost'),
          value: formatOdakCurrencyAmount(line.unitCost, line.currency),
        }
      : null,
    line.totalCost != null && line.totalCost !== ''
      ? {
          label: t('odakSiparis.lines.fields.totalCost'),
          value: formatOdakCurrencyAmount(line.totalCost, line.currency),
        }
      : null,
    optionalLineRow(t, 'odakSiparis.lines.fields.currency', line.currency),
  ].filter(Boolean) as OdakLineSummaryRow[];

  rows.push(...extras);

  if (line.shipmentDate) {
    rows.push({
      label: t('odakSiparis.lines.columns.shipmentDate'),
      value: formatOdakDate(line.shipmentDate),
    });
  }

  return rows;
}
