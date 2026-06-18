/** Shared list column formatting (Automated Forms listConfig.format). */

export type AfListColumnFormatType =
  | 'none'
  | 'regex'
  | 'number'
  | 'date'
  | 'currency'
  | 'text-transform'
  | 'color'
  | 'conditional-color';

export interface AfListColumnFormatCondition {
  field?: string;
  operator?: 'eq' | 'ne' | 'gt' | 'gte' | 'lt' | 'lte' | 'contains' | 'startsWith' | 'endsWith' | 'in' | 'notIn';
  value?: unknown;
  textColor?: string;
  customTextColor?: string;
  backgroundColor?: string;
  customBackgroundColor?: string;
}

export interface AfListColumnFormat {
  type?: AfListColumnFormatType;
  pattern?: string;
  replacement?: string;
  decimalPlaces?: number;
  thousandSeparator?: boolean;
  currencySymbol?: string;
  dateFormat?: string;
  showTime?: boolean;
  timeFormat?: 'HH:mm' | 'HH:mm:ss';
  textTransform?: 'uppercase' | 'lowercase' | 'capitalize';
  textColor?: string;
  customTextColor?: string;
  backgroundColor?: string;
  customBackgroundColor?: string;
  conditions?: AfListColumnFormatCondition[];
  defaultTextColor?: string;
  customDefaultTextColor?: string;
  defaultBackgroundColor?: string;
  customDefaultBackgroundColor?: string;
}

export function emptyAfListColumnFormat(): AfListColumnFormat {
  return { type: 'none', conditions: [] };
}

export function cloneAfListColumnFormat(source?: AfListColumnFormat | null): AfListColumnFormat {
  if (!source) return emptyAfListColumnFormat();
  return {
    ...source,
    conditions: source.conditions?.map((c) => ({ ...c })) ?? [],
  };
}

export function formatTypeLabelKey(type: AfListColumnFormatType | undefined): string {
  if (!type || type === 'none') return 'none';
  if (type === 'text-transform') return 'textTransform';
  if (type === 'conditional-color') return 'conditionalColor';
  return type;
}

export function isActiveListColumnFormat(format?: AfListColumnFormat | null): boolean {
  return Boolean(format?.type && format.type !== 'none');
}

const VUETIFY_THEME_COLORS: Record<string, string> = {
  primary: 'rgb(var(--v-theme-primary))',
  secondary: 'rgb(var(--v-theme-secondary))',
  success: 'rgb(var(--v-theme-success))',
  error: 'rgb(var(--v-theme-error))',
  warning: 'rgb(var(--v-theme-warning))',
  info: 'rgb(var(--v-theme-info))',
};

function resolveColorToken(token: string | undefined, custom?: string): string | undefined {
  if (!token) return undefined;
  if (token === 'custom' && custom) return custom;
  if (token.startsWith('#') || token.startsWith('rgb')) return token;
  return VUETIFY_THEME_COLORS[token] ?? token;
}

export function evaluateListFormatCondition(
  value: unknown,
  condition: AfListColumnFormatCondition,
  currentFieldName: string,
  rowData?: Record<string, unknown>
): boolean {
  if (!condition?.operator) return false;

  let compareValue = value;
  if (condition.field && condition.field !== currentFieldName && rowData) {
    compareValue = rowData[condition.field];
  }

  const conditionValue = condition.value;
  const operator = condition.operator;

  try {
    switch (operator) {
      case 'eq':
        return String(compareValue) === String(conditionValue);
      case 'ne':
        return String(compareValue) !== String(conditionValue);
      case 'gt':
        return Number(compareValue) > Number(conditionValue);
      case 'gte':
        return Number(compareValue) >= Number(conditionValue);
      case 'lt':
        return Number(compareValue) < Number(conditionValue);
      case 'lte':
        return Number(compareValue) <= Number(conditionValue);
      case 'contains':
        return String(compareValue).toLowerCase().includes(String(conditionValue).toLowerCase());
      case 'startsWith':
        return String(compareValue).toLowerCase().startsWith(String(conditionValue).toLowerCase());
      case 'endsWith':
        return String(compareValue).toLowerCase().endsWith(String(conditionValue).toLowerCase());
      case 'in':
        if (Array.isArray(conditionValue)) return conditionValue.includes(compareValue);
        return String(conditionValue)
          .split(',')
          .map((v) => v.trim())
          .includes(String(compareValue));
      case 'notIn':
        if (Array.isArray(conditionValue)) return !conditionValue.includes(compareValue);
        return !String(conditionValue)
          .split(',')
          .map((v) => v.trim())
          .includes(String(compareValue));
      default:
        return false;
    }
  } catch {
    return false;
  }
}

export function applyListColumnFormatting(value: string, format?: AfListColumnFormat | null): string {
  if (!format?.type || format.type === 'none') return value;

  try {
    switch (format.type) {
      case 'regex':
        if (format.pattern && format.replacement !== undefined) {
          try {
            let regex: RegExp;
            if (format.pattern.startsWith('/') && format.pattern.lastIndexOf('/') > 0) {
              const lastSlash = format.pattern.lastIndexOf('/');
              const pattern = format.pattern.substring(1, lastSlash);
              const flags = format.pattern.substring(lastSlash + 1);
              regex = new RegExp(pattern, flags);
            } else {
              regex = new RegExp(format.pattern, 'g');
            }
            return value.replace(regex, format.replacement || '');
          } catch {
            return value;
          }
        }
        return value;

      case 'number': {
        const numValue = parseFloat(value);
        if (Number.isNaN(numValue)) return value;
        let formatted = numValue.toFixed(format.decimalPlaces ?? 2);
        if (format.thousandSeparator) {
          const parts = formatted.split('.');
          parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ',');
          formatted = parts.join('.');
        }
        return formatted;
      }

      case 'currency': {
        const currencyValue = parseFloat(value);
        if (Number.isNaN(currencyValue)) return value;
        let currencyFormatted = currencyValue.toFixed(format.decimalPlaces ?? 2);
        if (format.thousandSeparator) {
          const parts = currencyFormatted.split('.');
          parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ',');
          currencyFormatted = parts.join('.');
        }
        return `${format.currencySymbol || '₺'} ${currencyFormatted}`;
      }

      case 'date':
        if (!format.dateFormat) return value;
        try {
          const date = new Date(value);
          if (Number.isNaN(date.getTime())) return value;
          const day = String(date.getDate()).padStart(2, '0');
          const month = String(date.getMonth() + 1).padStart(2, '0');
          const year = date.getFullYear();
          let formatted = format.dateFormat
            .replace('DD', day)
            .replace('MM', month)
            .replace('YYYY', String(year));
          if (format.showTime) {
            const hours = String(date.getHours()).padStart(2, '0');
            const minutes = String(date.getMinutes()).padStart(2, '0');
            const seconds = String(date.getSeconds()).padStart(2, '0');
            const timeFormat = format.timeFormat || 'HH:mm';
            const timeString = timeFormat.replace('HH', hours).replace('mm', minutes).replace('ss', seconds);
            formatted = `${formatted} ${timeString}`;
          }
          return formatted;
        } catch {
          return value;
        }

      case 'text-transform':
        if (format.textTransform === 'uppercase') return value.toUpperCase();
        if (format.textTransform === 'lowercase') return value.toLowerCase();
        if (format.textTransform === 'capitalize') {
          return value.charAt(0).toUpperCase() + value.slice(1).toLowerCase();
        }
        return value;

      case 'color':
      case 'conditional-color':
        return value;

      default:
        return value;
    }
  } catch {
    return value;
  }
}

export function getListColumnCellStyle(
  value: unknown,
  fieldName: string,
  format?: AfListColumnFormat | null,
  rowData?: Record<string, unknown>
): Record<string, string> {
  if (!format?.type) return {};

  const style: Record<string, string> = {};

  if (format.type === 'color') {
    const color = resolveColorToken(format.textColor, format.customTextColor);
    const bg = resolveColorToken(format.backgroundColor, format.customBackgroundColor);
    if (color) style.color = color;
    if (bg) style.backgroundColor = bg;
    return style;
  }

  if (format.type === 'conditional-color' && format.conditions?.length) {
    for (const condition of format.conditions) {
      if (evaluateListFormatCondition(value, condition, fieldName, rowData)) {
        const color = resolveColorToken(condition.textColor, condition.customTextColor);
        const bg = resolveColorToken(condition.backgroundColor, condition.customBackgroundColor);
        if (color) style.color = color;
        if (bg) style.backgroundColor = bg;
        return style;
      }
    }
    const defaultColor = resolveColorToken(format.defaultTextColor, format.customDefaultTextColor);
    const defaultBg = resolveColorToken(format.defaultBackgroundColor, format.customDefaultBackgroundColor);
    if (defaultColor) style.color = defaultColor;
    if (defaultBg) style.backgroundColor = defaultBg;
  }

  return style;
}
