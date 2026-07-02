import type { AfListColumnFormat } from '@/utils/afListColumnFormat';
import { isActiveListColumnFormat } from '@/utils/afListColumnFormat';
import type { OdakHubListColumnConfig } from '@/utils/odakSiparisHubListConfig';

export const ODAK_LIST_PRIMARY_COLOR: AfListColumnFormat = {
  type: 'color',
  textColor: 'primary',
};

export const ODAK_LIST_INTEGER_NUMBER: AfListColumnFormat = {
  type: 'number',
  decimalPlaces: 0,
  thousandSeparator: true,
};

export const ODAK_LIST_DECIMAL_NUMBER: AfListColumnFormat = {
  type: 'number',
  decimalPlaces: 2,
  thousandSeparator: true,
};

export const ODAK_LIST_CURRENCY_TRY: AfListColumnFormat = {
  type: 'currency',
  decimalPlaces: 2,
  thousandSeparator: true,
  currencySymbol: '₺',
};

export const ODAK_PACKAGE_STATUS_FORMAT: AfListColumnFormat = {
  type: 'conditional-color',
  conditions: [
    { operator: 'eq', value: 'Açık', textColor: 'success' },
    { operator: 'eq', value: 'Kapalı', textColor: 'secondary' },
  ],
  defaultTextColor: 'info',
};

export const ODAK_LINE_REMAINING_QTY_FORMAT: AfListColumnFormat = {
  type: 'conditional-color',
  conditions: [
    { operator: 'eq', value: '0', textColor: 'success' },
    { operator: 'gt', value: 0, textColor: 'warning' },
  ],
};

export const ODAK_SHIPMENT_STATUS_FORMAT: AfListColumnFormat = {
  type: 'conditional-color',
  conditions: [
    { operator: 'eq', value: 'Planlandı', textColor: 'info' },
    { operator: 'eq', value: 'Tamamlandı', textColor: 'success' },
    { operator: 'eq', value: 'İptal', textColor: 'error' },
  ],
};

export const ODAK_SHIPMENT_QCF_STATUS_FORMAT: AfListColumnFormat = {
  type: 'conditional-color',
  conditions: [
    { operator: 'eq', value: 'Bekliyor', textColor: 'warning' },
    { operator: 'eq', value: 'Tamamlandı', textColor: 'success' },
    { operator: 'eq', value: 'Yok', textColor: 'secondary' },
  ],
};

/** Hub list sütunları — yalnızca henüz biçim tanımı olmayanlara varsayılan uygular. */
export function applyDefaultColumnFormats(
  columns: OdakHubListColumnConfig[],
  formatByField: Record<string, AfListColumnFormat>
): OdakHubListColumnConfig[] {
  return columns.map((col) => {
    if (isActiveListColumnFormat(col.format) || col.format?.type === 'none') {
      return col;
    }
    const preset = formatByField[col.fieldName];
    if (!preset) return col;
    return {
      ...col,
      format: {
        ...preset,
        conditions: preset.conditions?.map((c) => ({ ...c })) ?? [],
      },
    };
  });
}

export const ODAK_PACKAGE_LIST_DEFAULT_FORMATS: Record<string, AfListColumnFormat> = {
  packageNo: ODAK_LIST_PRIMARY_COLOR,
  status: ODAK_PACKAGE_STATUS_FORMAT,
  partCount: ODAK_LIST_INTEGER_NUMBER,
  stockCount: ODAK_LIST_INTEGER_NUMBER,
  shippedCount: ODAK_LIST_INTEGER_NUMBER,
  lineCount: ODAK_LIST_INTEGER_NUMBER,
};

export const ODAK_LINE_LIST_DEFAULT_FORMATS: Record<string, AfListColumnFormat> = {
  lineNo: ODAK_LIST_PRIMARY_COLOR,
  quantity: ODAK_LIST_INTEGER_NUMBER,
  shippedQuantity: ODAK_LIST_INTEGER_NUMBER,
  remainingQuantity: ODAK_LINE_REMAINING_QTY_FORMAT,
  // unitCost/totalCost — satır currency alanı lineListCellRaw içinde formatlanır
  unitCost: { type: 'none' },
  totalCost: { type: 'none' },
};

export const ODAK_SHIPMENT_LIST_DEFAULT_FORMATS: Record<string, AfListColumnFormat> = {
  waybillNo: ODAK_LIST_PRIMARY_COLOR,
  status: ODAK_SHIPMENT_STATUS_FORMAT,
  orderQty: ODAK_LIST_INTEGER_NUMBER,
  lineQty: ODAK_LIST_INTEGER_NUMBER,
  remainingQty: ODAK_LINE_REMAINING_QTY_FORMAT,
  qcfStatus: ODAK_SHIPMENT_QCF_STATUS_FORMAT,
};

export function withDefaultListColumnFormats(
  config: { columns: OdakHubListColumnConfig[] },
  formatByField: Record<string, AfListColumnFormat>
) {
  return {
    ...config,
    columns: applyDefaultColumnFormats(config.columns, formatByField),
  };
}
