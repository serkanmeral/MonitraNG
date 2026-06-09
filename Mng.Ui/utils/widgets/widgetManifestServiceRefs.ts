import type { ManifestDataBinding } from '@/types/apps/widgetManifest';

/**
 * Eski seed / planlanmış serviceRef → runtime'da desteklenen canonical ref.
 * `mngreactor:sec-events/scenario-rollup` API'si yok; veri alarm snapshot içinde.
 */
const SERVICE_REF_ALIASES: Record<string, string> = {
  'mngreactor:sec-events/scenario-rollup': 'mngalarm:alarms/dashboard-snapshot',
};

export function normalizeManifestServiceRef(serviceRef: string): string {
  return SERVICE_REF_ALIASES[serviceRef.trim()] ?? serviceRef;
}

export function normalizeManifestBinding(
  binding: ManifestDataBinding,
  options: { templateId?: string } = {},
): ManifestDataBinding {
  const templateId = options.templateId;

  if (templateId === 'siem.scenario-cards') {
    return {
      ...binding,
      kind: binding.kind === 'queryRef' ? 'serviceRef' : binding.kind,
      serviceRef: 'mngalarm:alarms/dashboard-snapshot',
      fieldMap: { ...binding.fieldMap, rows: 'scenarioRollup' },
      responseShape: binding.responseShape ?? 'rows',
    };
  }

  if (binding.kind !== 'serviceRef' || !binding.serviceRef) {
    return binding;
  }

  let serviceRef = binding.serviceRef;
  let fieldMap = { ...binding.fieldMap };

  const canonical = normalizeManifestServiceRef(serviceRef);
  if (canonical !== serviceRef) {
    serviceRef = canonical;
    if (canonical === 'mngalarm:alarms/dashboard-snapshot') {
      fieldMap.rows = 'scenarioRollup';
    }
  }

  // Eski yanlış seed: dashboard-snapshot + rows:items (liste şablonu kalıntısı)
  if (
    serviceRef === 'mngalarm:alarms/dashboard-snapshot' &&
    fieldMap.rows === 'items' &&
    !fieldMap.value
  ) {
    fieldMap.rows = 'scenarioRollup';
  }

  const responseShape =
    fieldMap.rows === 'scenarioRollup' ? (binding.responseShape ?? 'rows') : binding.responseShape;

  const unchanged =
    serviceRef === binding.serviceRef &&
    fieldMap.rows === binding.fieldMap?.rows &&
    responseShape === binding.responseShape;
  if (unchanged) {
    return binding;
  }

  return {
    ...binding,
    serviceRef,
    fieldMap,
    responseShape,
  };
}
