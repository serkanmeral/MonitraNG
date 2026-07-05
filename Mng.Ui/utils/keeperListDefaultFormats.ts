import type { AfListColumnFormat } from '@/utils/afListColumnFormat';
import { applyDefaultColumnFormats } from '@/utils/odakSiparisHubListDefaultFormats';
import type { OdakHubListColumnConfig, OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';

/** TR + EN etiketleri — liste hücresi locale'e göre biçimlendirilir. */
export const KEEPER_ACTIVE_STATUS_FORMAT: AfListColumnFormat = {
  type: 'conditional-color',
  conditions: [
    { operator: 'eq', value: 'Aktif', textColor: 'success' },
    { operator: 'eq', value: 'Active', textColor: 'success' },
    { operator: 'eq', value: 'Pasif', textColor: 'error' },
    { operator: 'eq', value: 'Inactive', textColor: 'error' },
  ],
};

export const KEEPER_IN_APP_SCOPE_FORMAT: AfListColumnFormat = {
  type: 'conditional-color',
  conditions: [
    { operator: 'eq', value: 'Kapsamda', textColor: 'primary' },
    { operator: 'eq', value: 'In scope', textColor: 'primary' },
    { operator: 'eq', value: 'Saklı', textColor: 'warning' },
    { operator: 'eq', value: 'Hidden', textColor: 'warning' },
  ],
};

export const KEEPER_USER_LIST_DEFAULT_FORMATS: Record<string, AfListColumnFormat> = {
  isActive: KEEPER_ACTIVE_STATUS_FORMAT,
  includeInApplication: KEEPER_IN_APP_SCOPE_FORMAT,
};

export const KEEPER_GROUP_LIST_DEFAULT_FORMATS: Record<string, AfListColumnFormat> = {
  isActive: KEEPER_ACTIVE_STATUS_FORMAT,
  includeInApplication: KEEPER_IN_APP_SCOPE_FORMAT,
};

const STATUS_SCOPE_FIELDS = new Set(['isActive', 'includeInApplication']);

/** Durum / uygulama kapsamı varsayılan biçimini her yüklemede uygular (kullanıcı özelleştirmesi korunmaz). */
export function ensureKeeperStatusScopeColumnFormats(
  columns: OdakHubListColumnConfig[],
  formatByField: Record<string, AfListColumnFormat>
): OdakHubListColumnConfig[] {
  return columns.map((col) => {
    if (!STATUS_SCOPE_FIELDS.has(col.fieldName)) return col;
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

export function withKeeperDefaultListColumnFormats(
  config: OdakHubListConfig,
  formatByField: Record<string, AfListColumnFormat>
): OdakHubListConfig {
  const withDefaults = applyDefaultColumnFormats(config.columns, formatByField);
  return {
    ...config,
    columns: ensureKeeperStatusScopeColumnFormats(withDefaults, formatByField),
  };
}

export type KeeperListColumnConfig = OdakHubListColumnConfig;
