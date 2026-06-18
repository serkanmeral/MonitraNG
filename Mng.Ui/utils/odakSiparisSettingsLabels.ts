/**
 * Odak Sipariş hub ayarları — etiketler her zaman Türkçe (tr.json).
 * UI dili EN olsa bile ayar ekranlarında iş alanı adları Türkçe gösterilir.
 */
import trLocale from '@/utils/locales/tr.json';

type TrOdak = {
  odakSiparis?: {
    packages?: {
      settings?: {
        fieldPolicyFields?: Record<string, string>;
        formatting?: { types?: Record<string, string> };
      };
    };
    lines?: {
      fields?: Record<string, string>;
      columns?: Record<string, string>;
    };
    shipments?: {
      fields?: Record<string, string>;
      columns?: Record<string, string>;
    };
  };
};

const trOdak = trLocale as TrOdak;

export function odakPackageSettingsFieldLabelTr(fieldName: string): string {
  return trOdak.odakSiparis?.packages?.settings?.fieldPolicyFields?.[fieldName] ?? fieldName;
}

export function odakLineSettingsFieldLabelTr(fieldName: string): string {
  const fields = trOdak.odakSiparis?.lines?.fields;
  const columns = trOdak.odakSiparis?.lines?.columns;
  return fields?.[fieldName] ?? columns?.[fieldName] ?? fieldName;
}

export function odakShipmentSettingsFieldLabelTr(fieldName: string): string {
  const fields = trOdak.odakSiparis?.shipments?.fields;
  const columns = trOdak.odakSiparis?.shipments?.columns;
  if (fieldName === 'lineQty') {
    return columns?.lineQty ?? 'Toplam Miktar';
  }
  return fields?.[fieldName] ?? columns?.[fieldName] ?? fieldName;
}

export function odakPackageSettingsFormatTypeLabelTr(typeKey: string): string {
  return trOdak.odakSiparis?.packages?.settings?.formatting?.types?.[typeKey] ?? typeKey;
}
