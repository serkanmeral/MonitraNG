import type { OdakPackageRow } from '@/utils/odakSiparisConfig';
import {
  customerIdFromRow,
  customerLabelFromRow,
  formatOdakDate,
  formatOdakNumber,
  packageStatusLabel,
} from '@/utils/odakSiparisService';

export type OdakPackageSummaryRow = {
  label: string;
  value: string;
  link?: string;
  /** Müşteri önizleme drawer'ı */
  customerId?: string;
};

function optionalSummaryRow(
  t: (key: string) => string,
  labelKey: string,
  value: unknown
): OdakPackageSummaryRow | null {
  if (value == null || value === '') return null;
  return { label: t(labelKey), value: String(value) };
}

/** Detay sayfasi ve liste expanded-row ozeti icin ortak alan listesi. */
export function buildOdakPackageSummaryRows(
  pkg: OdakPackageRow,
  customerLabels: Record<string, string>,
  t: (key: string) => string
): OdakPackageSummaryRow[] {
  const customerId = customerIdFromRow(pkg);
  const customerLabel = customerLabelFromRow(pkg, customerLabels);

  const rows: OdakPackageSummaryRow[] = [
    { label: t('odakSiparis.detail.fields.packageNo'), value: pkg.packageNo ?? '—' },
    { label: t('odakSiparis.detail.fields.name'), value: pkg.name ?? '—' },
    { label: t('odakSiparis.detail.fields.status'), value: packageStatusLabel(pkg.status) },
    {
      label: t('odakSiparis.detail.fields.customer'),
      value: customerLabel,
      customerId: customerId || undefined,
    },
    { label: t('odakSiparis.detail.fields.partCount'), value: formatOdakNumber(pkg.partCount) },
    { label: t('odakSiparis.detail.fields.stockCount'), value: formatOdakNumber(pkg.stockCount) },
    { label: t('odakSiparis.detail.fields.shippedCount'), value: formatOdakNumber(pkg.shippedCount) },
    { label: t('odakSiparis.detail.fields.lineCount'), value: formatOdakNumber(pkg.lineCount) },
    { label: t('odakSiparis.detail.fields.beginDate'), value: formatOdakDate(pkg.beginDate) },
    { label: t('odakSiparis.detail.fields.deliveryDate'), value: formatOdakDate(pkg.deliveryDate) },
    { label: t('odakSiparis.detail.fields.deliveryAddress'), value: pkg.deliveryAddress ?? '—' },
    { label: t('odakSiparis.detail.fields.paymentDetail'), value: pkg.paymentDetail ?? '—' },
    { label: t('odakSiparis.detail.fields.notes'), value: pkg.notes ?? '—' },
  ];

  const legacyRows = [
    optionalSummaryRow(t, 'odakSiparis.detail.fields.customerContact', pkg.legacyContactId),
    optionalSummaryRow(t, 'odakSiparis.detail.fields.packageResponsible', pkg.legacyResponsibleId),
    optionalSummaryRow(t, 'odakSiparis.detail.fields.designResponsible', pkg.legacyDesignResponsibleId),
    optionalSummaryRow(t, 'odakSiparis.detail.fields.manufactureResponsible', pkg.legacyManufactureResponsibleId),
  ].filter(Boolean) as OdakPackageSummaryRow[];
  rows.push(...legacyRows);

  const createdAt = pkg.__createdAt ?? pkg.legacyCreatedAt;
  const updatedAt = pkg.__updatedAt ?? pkg.legacyUpdatedAt;
  const createdBy = pkg.__createdBy ?? pkg.legacyCreatedBy;
  const updatedBy = pkg.__updatedBy ?? pkg.legacyUpdatedBy;

  if (createdAt) {
    rows.push({ label: t('odakSiparis.detail.fields.createdAt'), value: formatOdakDate(createdAt) });
  }
  if (updatedAt) {
    rows.push({ label: t('odakSiparis.detail.fields.updatedAt'), value: formatOdakDate(updatedAt) });
  }
  if (createdBy) {
    rows.push({ label: t('odakSiparis.detail.fields.createdBy'), value: String(createdBy) });
  }
  if (updatedBy) {
    rows.push({ label: t('odakSiparis.detail.fields.updatedBy'), value: String(updatedBy) });
  }
  if (pkg.workItemKey) {
    rows.push({ label: t('odakSiparis.detail.fields.workItemKey'), value: pkg.workItemKey });
  }

  return rows;
}
