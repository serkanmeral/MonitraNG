import type { OcDashboardWidgetDef } from '@/types/apps/operationCore';
import type { WidgetTemplateRecord } from '@/types/apps/widgetManifest';
import {
  normalizeTemplateRecord,
  parseQueryRef,
  pickLocalized,
} from '@/utils/widgets/widgetManifestAdapter';

const PRESENTATION_TO_OC_TYPE: Record<string, OcDashboardWidgetDef['type']> = {
  stat: 'summaryCard',
  chart: 'chart',
  table: 'list',
  list: 'list',
};

function resolveDefaultParamValue(ref: string, workspaceId: string): string {
  if (ref === '$variables.workspaceId') return workspaceId || '{{workspaceId}}';
  if (ref === '$variables.currentUserId') return '{{currentUser}}';
  if (ref === '$timeRange.to' || ref === '$timeRange.from') return '{{asOf}}';
  return '';
}

/**
 * MO dashboard editörü için @widget_templates kaydını OcDashboardWidgetDef'e çevirir.
 */
export function ocTemplateToWidgetDef(
  record: WidgetTemplateRecord,
  options: { workspaceId: string; key?: string },
): OcDashboardWidgetDef | null {
  const manifest = normalizeTemplateRecord(record);
  if (manifest.domain !== 'operation-core') return null;

  const queryRef = manifest.dataBinding.queryRef;
  if (!queryRef) return null;

  const parsed = parseQueryRef(queryRef);
  if (!parsed) return null;

  const ocType = PRESENTATION_TO_OC_TYPE[manifest.presentation.kind];
  if (!ocType) return null;

  const parameters: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(manifest.dataBinding.defaultParameters ?? {})) {
    if (value && typeof value === 'object' && '$ref' in value) {
      parameters[key] = resolveDefaultParamValue(String((value as { $ref: string }).$ref), options.workspaceId);
    } else {
      parameters[key] = value;
    }
  }

  const presetConfig = manifest.presentation.config ?? {};
  const pageSize = typeof presetConfig.pageSize === 'number' ? presetConfig.pageSize : null;

  const def: OcDashboardWidgetDef = {
    key: options.key ?? record.templateId.replace(/\./g, '_'),
    type: ocType,
    title: pickLocalized(manifest.title),
    dataset: parsed.dataset,
    queryKey: parsed.queryName,
    parameters: Object.keys(parameters).length ? parameters : null,
    take: pageSize,
  };

  if (ocType === 'chart') {
    def.chartType = (presetConfig.type as string | undefined) ?? 'donut';
    def.groupBy = 'stateId';
    const fieldMap = manifest.dataBinding.fieldMap;
    if (fieldMap?.series) {
      const seriesField = fieldMap.series;
      if (['stateId', 'priorityId', 'typeId', 'assignee'].includes(seriesField)) {
        def.groupBy = seriesField;
      }
    }
  }

  if (ocType === 'summaryCard') {
    if (typeof presetConfig.color === 'string') def.accentColor = presetConfig.color;
    if (typeof presetConfig.icon === 'string') def.icon = presetConfig.icon;
  }

  return def;
}

export function suggestOcWidgetKeyFromTemplate(templateId: string, existingKeys: string[]): string {
  let base = templateId.replace(/\./g, '_');
  if (!existingKeys.includes(base)) return base;
  let i = 2;
  while (existingKeys.includes(`${base}_${i}`)) i += 1;
  return `${base}_${i}`;
}
