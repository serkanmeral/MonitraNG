import type { DiDocxPlaceholder, DiTemplateParameter } from '@/types/apps/documentIntelligence';

export function placeholderToken(key: string): string {
  return `{{${key}}}`;
}

export function humanizePlaceholderKey(key: string): string {
  const spaced = key
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/_/g, ' ')
    .trim();
  if (!spaced) return key;
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

export function isPlaceholderDefined(
  key: string,
  parameters: DiTemplateParameter[]
): boolean {
  return parameters.some((p) => p.key.toLowerCase() === key.toLowerCase());
}

export function missingPlaceholderDefinitions(
  placeholders: DiDocxPlaceholder[],
  parameters: DiTemplateParameter[]
): DiDocxPlaceholder[] {
  return placeholders.filter((ph) => !isPlaceholderDefined(ph.key, parameters));
}

export function undefinedDocPlaceholders(
  placeholders: DiDocxPlaceholder[],
  parameters: DiTemplateParameter[]
): DiTemplateParameter[] {
  const docKeys = new Set(placeholders.map((p) => p.key.toLowerCase()));
  return parameters.filter((p) => !docKeys.has(p.key.toLowerCase()));
}

export function buildParameterFromPlaceholder(
  ph: DiDocxPlaceholder,
  mode: 'manual' | 'incremental' = 'manual',
  incrementalFormat = 'ODK-COC-{yy}-{0:D3}'
): DiTemplateParameter {
  return {
    key: ph.key,
    label: humanizePlaceholderKey(ph.key),
    dataType: 'text',
    valueSourceMode: mode,
    incremental:
      mode === 'incremental'
        ? {
            format: incrementalFormat,
            startValue: 1,
            incrementStep: 1,
            scopeKey: ph.key,
            resetPolicy: 'yearly',
          }
        : null,
    sourceBinding: {
      regionKind: 'placeholder',
      paragraphIndex: 0,
      originalText: ph.token,
    },
  };
}

export function importMissingPlaceholders(
  placeholders: DiDocxPlaceholder[],
  parameters: DiTemplateParameter[]
): DiTemplateParameter[] {
  const next = [...parameters];
  for (const ph of placeholders) {
    if (isPlaceholderDefined(ph.key, next)) continue;
    next.push(buildParameterFromPlaceholder(ph));
  }
  return next;
}
