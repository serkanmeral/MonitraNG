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
  if (fieldName === 'orderQty') {
    return columns?.orderQty ?? 'Sipariş miktarı (kalem)';
  }
  if (fieldName === 'lineQty') {
    return columns?.lineQty ?? 'Bu sevkte';
  }
  if (fieldName === 'remainingQty') {
    return columns?.remainingQty ?? 'Bu sevkte kalan';
  }
  return fields?.[fieldName] ?? columns?.[fieldName] ?? fieldName;
}

export function odakGlobalShipmentSettingsFieldLabelTr(fieldName: string): string {
  const globalCols = (trLocale as { odakSiparis?: { globalShipments?: { columns?: Record<string, string> } } })
    .odakSiparis?.globalShipments?.columns;
  if (fieldName === 'recordScope') return globalCols?.scope ?? 'Kapsam';
  if (fieldName === 'headerDescription') return globalCols?.content ?? 'İçerik özeti';
  if (fieldName === 'customerId') {
    return (trLocale as { odakSiparis?: { packages?: { columns?: Record<string, string> } } }).odakSiparis?.packages
      ?.columns?.customer ?? 'Müşteri';
  }
  if (fieldName === 'parentPackageId') {
    return (trLocale as { odakSiparis?: { globalShipments?: { filters?: Record<string, string> } } }).odakSiparis
      ?.globalShipments?.filters?.parentPackage ?? 'İş paketi';
  }
  return odakShipmentSettingsFieldLabelTr(fieldName);
}

/** Bildirim olay tipi etiketi — tr.json doğrudan (runtime i18n merge eksik kalmasın). */
export function odakNotificationEventLabelTr(eventType: string): string {
  const root = trLocale as {
    odakSiparis?: {
      packages?: { settings?: { notifications?: { eventTypes?: Record<string, string> } } };
      globalShipments?: { settings?: { notifications?: { eventTypes?: Record<string, string> } } };
    };
  };
  return (
    root.odakSiparis?.globalShipments?.settings?.notifications?.eventTypes?.[eventType] ??
    root.odakSiparis?.packages?.settings?.notifications?.eventTypes?.[eventType] ??
    eventType
  );
}

export function odakPackageSettingsFormatTypeLabelTr(typeKey: string): string {
  return trOdak.odakSiparis?.packages?.settings?.formatting?.types?.[typeKey] ?? typeKey;
}
