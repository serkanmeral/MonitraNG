import type { Widget, DataSourceConfigData } from '@/stores/apps/widget';
import type {
  DataBindingConfig,
  ManifestDataBinding,
  ParameterValue,
  SurfaceContext,
  WidgetDefinitionManifest,
  WidgetTemplateManifest,
  WidgetTemplateRecord,
  LocalizedString,
} from '@/types/apps/widgetManifest';

export type { WidgetDefinitionManifest };
import {
  applyFieldMapToPresentationConfig,
  extractPresentationConfigForDesigner,
  mergePresentationConfigOverrides,
  responseShapeFromKind,
} from '@/utils/widgets/widgetFieldMappingBridge';
import { getPresentationPreset, legacyTypeFromKind, resolvePresetConfig } from '@/utils/widgets/presentationPresets';
import { normalizeManifestBinding } from '@/utils/widgets/widgetManifestServiceRefs';

export const LEGACY_CUSTOM_TEMPLATE_ID = 'legacy.custom';

export interface WidgetManifestFields {
  manifestVersion?: string;
  templateId?: string;
  templateVersion?: string;
  manifest?: WidgetDefinitionManifest | WidgetTemplateManifest;
}

export type WidgetLike = Widget & WidgetManifestFields;

const QUERY_REF_PATTERN = /^@([^/]+)\/queries\/([^/]+)$/;

export function pickLocalized(value: LocalizedString | undefined, locale = 'tr'): string {
  if (value == null) return '';
  if (typeof value === 'string') return value;
  return value[locale] ?? value.tr ?? value.en ?? Object.values(value).find((v) => typeof v === 'string') ?? '';
}

export function isLegacyWidget(widget: WidgetLike): boolean {
  if (widget.manifest) return false;
  if (widget.templateId && widget.templateId !== LEGACY_CUSTOM_TEMPLATE_ID) return false;
  return true;
}

/** @widgets.config.manifest veya widget.manifest */
export function resolveWidgetDefinitionManifest(widget: WidgetLike): WidgetDefinitionManifest | null {
  const config = (widget.config ?? {}) as Record<string, unknown>;
  const fromConfig = config.manifest as WidgetDefinitionManifest | undefined;
  if (fromConfig?.templateId) return fromConfig;
  const fromRoot = widget.manifest as WidgetDefinitionManifest | undefined;
  if (fromRoot?.templateId) return fromRoot;
  return null;
}

export function normalizeTemplateRecord(record: WidgetTemplateRecord): WidgetTemplateManifest {
  if (record.manifest?.templateId) {
    return record.manifest;
  }
  return {
    manifestVersion: '1.0',
    templateId: record.templateId,
    templateVersion: record.templateVersion,
    domain: record.domain,
    category: typeof record.category === 'string' ? record.category : '',
    title: record.title,
    description: record.description,
    tags: record.tags,
    presentation: record.manifest?.presentation ?? {
      kind: 'stat',
      defaultPreset: 'stat-simple',
      allowedPresets: ['stat-simple'],
    },
    dataBinding: record.manifest?.dataBinding ?? {},
    parametersSchema: record.manifest?.parametersSchema ?? [],
    interactions: record.manifest?.interactions,
    permissions: record.manifest?.permissions,
    export: record.manifest?.export,
  };
}

export function resolveParameterRefs(
  parameters: Record<string, ParameterValue> | undefined,
  context: SurfaceContext = {}
): Record<string, unknown> {
  if (!parameters) return {};
  const resolved: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(parameters)) {
    resolved[key] = resolveParameterValue(value, context);
  }
  return resolved;
}

export function resolveParameterValue(value: ParameterValue | undefined, context: SurfaceContext = {}): unknown {
  if (value == null) return value;
  if (typeof value === 'object' && '$ref' in value) {
    return resolveContextRef(value.$ref, context);
  }
  return value;
}

export function resolveContextRef(ref: string, context: SurfaceContext): unknown {
  if (!ref.startsWith('$')) return ref;
  const path = ref.slice(1).split('.');
  if (path[0] === 'timeRange') {
    const tr = context.timeRange ?? {};
    if (path[1] === 'hours' && tr.hours == null && tr.preset) {
      return durationPresetToHours(tr.preset);
    }
    return (tr as Record<string, unknown>)[path[1] ?? ''];
  }
  if (path[0] === 'variables') {
    return context.variables?.[path[1] ?? ''];
  }
  if (path[0] === 'locale') {
    return context.locale ?? 'tr';
  }
  return undefined;
}

export function durationPresetToHours(preset: string): number {
  const map: Record<string, number> = {
    '20m': 1,
    '1h': 1,
    '6h': 6,
    '24h': 24,
    '7d': 168,
    '30d': 720,
  };
  return map[preset] ?? 24;
}

export function parseQueryRef(queryRef: string): { dataset: string; queryName: string } | null {
  const match = QUERY_REF_PATTERN.exec(queryRef.trim());
  if (!match) return null;
  const dataset = match[1].startsWith('@') ? match[1].slice(1) : match[1];
  return { dataset, queryName: match[2] };
}

export function buildManifestDataBinding(
  binding: DataBindingConfig,
  parameters: Record<string, ParameterValue> | undefined,
  context: SurfaceContext,
  options?: {
    presentationKind?: import('@/types/apps/widgetManifest').PresentationKind;
    templateId?: string;
  },
): ManifestDataBinding {
  const mergedParams = {
    ...resolveParameterRefs(binding.defaultParameters, context),
    ...resolveParameterRefs(parameters, context),
  };
  const mapping = (binding.advanced?.mapping as Record<string, string> | undefined) ?? undefined;
  const responseShape = options?.presentationKind ? responseShapeFromKind(options.presentationKind) : undefined;

  if (binding.serviceRef?.includes(':static/')) {
    return { kind: 'static', parameters: mergedParams, fieldMap: binding.fieldMap, mapping, responseShape };
  }
  if (binding.serviceRef) {
    return normalizeManifestBinding(
      {
        kind: 'serviceRef',
        serviceRef: binding.serviceRef,
        parameters: mergedParams,
        fieldMap: binding.fieldMap,
        mapping,
        responseShape,
      },
      { templateId: options?.templateId },
    );
  }
  if (binding.queryRef) {
    return {
      kind: 'queryRef',
      queryRef: binding.queryRef,
      parameters: mergedParams,
      fieldMap: binding.fieldMap,
      mapping,
      responseShape,
    };
  }
  return { kind: 'static', parameters: mergedParams, fieldMap: binding.fieldMap, mapping, responseShape };
}

function dataBindingToLegacyDataSource(
  binding: DataBindingConfig,
  parameters: Record<string, ParameterValue> | undefined,
  context: SurfaceContext,
  presentationKind?: import('@/types/apps/widgetManifest').PresentationKind,
): DataSourceConfigData {
  const manifestBinding = buildManifestDataBinding(binding, parameters, context, { presentationKind });

  if (manifestBinding.kind === 'queryRef' && manifestBinding.queryRef) {
    const parsed = parseQueryRef(manifestBinding.queryRef);
    if (!parsed) {
      throw new Error(`Geçersiz queryRef: ${manifestBinding.queryRef}`);
    }
    const mapping = buildFieldMapMapping(binding.fieldMap, manifestBinding.mapping);
    return {
      type: 'data',
      dataset: parsed.dataset,
      getMethod: 'predefined',
      predefined: {
        queryName: parsed.queryName,
        parameters: manifestBinding.parameters,
      },
      ...(mapping ? { mapping } : {}),
    };
  }

  if (manifestBinding.kind === 'serviceRef') {
    return {
      type: 'data',
      dataset: '__manifest_service__',
      getMethod: 'default',
      default: {},
      mapping: {
        items: binding.fieldMap?.rows ?? 'items',
        total: binding.fieldMap?.total ?? 'total',
        value: binding.fieldMap?.value ?? 'value',
      },
    };
  }

  return {
    type: 'data',
    dataset: '__manifest_static__',
    getMethod: 'default',
    default: { limit: 0 },
  };
}

function buildFieldMapMapping(
  fieldMap?: Record<string, string>,
  advancedMapping?: Record<string, string>
): DataSourceConfigData['mapping'] | undefined {
  if (!fieldMap && !advancedMapping) return undefined;
  const mapping: Record<string, string> = {};
  if (fieldMap?.rows) mapping.items = fieldMap.rows;
  if (fieldMap?.total) mapping.total = fieldMap.total;
  if (fieldMap?.value) mapping.value = fieldMap.value;
  if (advancedMapping) {
    for (const [target, source] of Object.entries(advancedMapping)) {
      mapping[target] = source;
    }
  }
  return Object.keys(mapping).length ? mapping : undefined;
}

export function manifestToLegacyWidget(
  source: WidgetDefinitionManifest | WidgetTemplateManifest,
  options: {
    name?: string;
    categoryId?: string;
    parameters?: Record<string, ParameterValue>;
    context?: SurfaceContext;
    isActive?: boolean;
    presentationConfigOverrides?: Record<string, unknown>;
  } = {},
): WidgetLike {
  const presetId =
    ('preset' in source.presentation ? source.presentation.preset : undefined) ??
    source.presentation.defaultPreset ??
    'stat-simple';
  const preset = getPresentationPreset(presetId);
  const kind = source.presentation.kind;
  const legacyType = preset?.legacyType ?? legacyTypeFromKind(kind);
  let config = resolvePresetConfig(presetId, source.presentation.config, null) as Record<string, unknown>;
  config = applyFieldMapToPresentationConfig(kind, source.dataBinding.fieldMap, config, source.templateId);
  if (options.presentationConfigOverrides) {
    config = mergePresentationConfigOverrides(config, options.presentationConfigOverrides);
  }
  const manifestBinding = buildManifestDataBinding(
    source.dataBinding,
    options.parameters ?? ('parameters' in source ? source.parameters : undefined),
    options.context ?? {},
    { presentationKind: kind, templateId: source.templateId },
  );

  const widget: WidgetLike = {
    name: options.name ?? ('name' in source ? source.name : source.templateId),
    title: pickLocalized(source.title, options.context?.locale),
    description: source.description ? pickLocalized(source.description, options.context?.locale) : undefined,
    category: options.categoryId ?? source.category,
    type: legacyType,
    dataSource: dataBindingToLegacyDataSource(
      source.dataBinding,
      options.parameters ?? ('parameters' in source ? source.parameters : undefined),
      options.context ?? {},
      kind,
    ),
    config: {
      ...config,
      manifestBinding,
      templateId: source.templateId,
      templateVersion: source.templateVersion,
      manifestVersion: source.manifestVersion,
      presentationPreset: presetId,
      presentationKind: kind,
    },
    isActive: options.isActive ?? ('isActive' in source ? source.isActive : true),
    permissions: source.permissions?.groups ? { groups: source.permissions.groups } : undefined,
    manifestVersion: source.manifestVersion,
    templateId: source.templateId,
    templateVersion: source.templateVersion,
    manifest: source as WidgetDefinitionManifest,
  };

  if (manifestBinding.kind === 'static' && kind === 'banner') {
    applyBannerStaticConfig(widget, source);
  }

  return widget;
}

function applyBannerStaticConfig(widget: WidgetLike, source: WidgetDefinitionManifest | WidgetTemplateManifest) {
  const presentationConfig = source.presentation.config ?? {};
  const title = presentationConfig.title;
  const message = presentationConfig.message ?? presentationConfig.content;
  widget.config = {
    ...(widget.config as Record<string, unknown>),
    type: presentationConfig.color === 'warning' ? 'warning' : 'info',
    title: typeof title === 'string' ? title : pickLocalized(title as LocalizedString),
    content: typeof message === 'string' ? message : pickLocalized(message as LocalizedString),
    icon: presentationConfig.icon ?? 'mdi-information',
    action: presentationConfig.route
      ? {
          enabled: true,
          label: 'Git',
          icon: 'mdi-open-in-new',
        }
      : undefined,
    route: presentationConfig.route,
  };
  widget.dataSource = {
    type: 'data',
    dataset: '__manifest_static__',
    getMethod: 'default',
    default: { limit: 0 },
  };
}

export function resolveManifestBindingForFetch(
  widget: WidgetLike,
  context: SurfaceContext = {},
): ManifestDataBinding | null {
  const config = (widget.config ?? {}) as Record<string, unknown>;
  const manifest =
    widget.manifest ??
    (config.manifest as WidgetDefinitionManifest | undefined);
  const templateId =
    manifest?.templateId ??
    (config.templateId as string | undefined) ??
    widget.templateId;

  if (manifest?.dataBinding) {
    const parameters =
      manifest.parameters ??
      (config.parameters as Record<string, ParameterValue> | undefined);
    return buildManifestDataBinding(manifest.dataBinding, parameters, context, {
      presentationKind: manifest.presentation?.kind,
      templateId,
    });
  }

  const existing = config.manifestBinding as ManifestDataBinding | undefined;
  if (existing?.kind) {
    const kind = manifest?.presentation?.kind;
    const merged =
      !existing.responseShape && kind
        ? { ...existing, responseShape: responseShapeFromKind(kind) }
        : existing;
    return normalizeManifestBinding(merged, { templateId });
  }
  return null;
}

/** Legacy @widgets kaydını runtime'a hazırlar — manifest varsa legacy shape'e çevirir. */
export function adaptWidgetForRuntime(widget: WidgetLike, context: SurfaceContext = {}): WidgetLike {
  if (isLegacyWidget(widget)) {
    return widget;
  }

  const manifest =
    widget.manifest ??
    ((widget.config as Record<string, unknown> | undefined)?.manifest as WidgetDefinitionManifest | undefined);

  if (!manifest) {
    return {
      ...widget,
      templateId: widget.templateId ?? LEGACY_CUSTOM_TEMPLATE_ID,
    };
  }

  return manifestToLegacyWidget(manifest, {
    name: widget.name,
    categoryId: typeof widget.category === 'string' ? widget.category : widget.category?.__dataId,
    parameters: manifest.parameters,
    context,
    isActive: widget.isActive,
    presentationConfigOverrides: extractPersistedPresentationOverrides(widget.config as Record<string, unknown>),
  });
}

function extractPersistedPresentationOverrides(config: Record<string, unknown> | undefined): Record<string, unknown> | undefined {
  if (!config) return undefined;
  if (config.presentationConfigOverrides && typeof config.presentationConfigOverrides === 'object') {
    return config.presentationConfigOverrides as Record<string, unknown>;
  }
  return undefined;
}

/** @deprecated — widgetFieldMappingBridge.ALARM_RECENT_TABLE_COLUMNS kullanın */
export { ALARM_RECENT_TABLE_COLUMNS } from '@/utils/widgets/widgetFieldMappingBridge';

export function hasManifestTableColumns(widget: WidgetLike | null | undefined): boolean {
  if (!widget?.config) return false;
  const cols = (widget.config as Record<string, unknown>).columns;
  return Array.isArray(cols) && cols.length > 0;
}

export {
  extractPresentationConfigForDesigner,
} from '@/utils/widgets/widgetFieldMappingBridge';

/** Template kaydından preview widget (designer katalog). */
export function templateRecordToPreviewWidget(
  record: WidgetTemplateRecord,
  context: SurfaceContext = {}
): WidgetLike {
  const manifest = normalizeTemplateRecord(record);
  const widget = manifestToLegacyWidget(manifest, {
    name: record.templateId,
    categoryId: record.category,
    context,
    isActive: record.isActive,
  });
  if (record.templateId === 'alarm.recent-table') {
    const cfg = widget.config as Record<string, unknown>;
    applyFieldMapToPresentationConfig('table', record.manifest?.dataBinding?.fieldMap, cfg, 'alarm.recent-table');
  }
  if (record.templateId === 'siem.recent-events-table') {
    const cfg = widget.config as Record<string, unknown>;
    applyFieldMapToPresentationConfig('table', record.manifest?.dataBinding?.fieldMap, cfg, 'siem.recent-events-table');
  }
  return widget;
}

/** Legacy dataSource.dataset — gerçek Odak dataset değil; manifest service/static binding. */
export const MANIFEST_PLACEHOLDER_DATASETS = ['__manifest_service__', '__manifest_static__'] as const;

export function isManifestPlaceholderDataset(name: string | null | undefined): boolean {
  if (!name) return false;
  return (MANIFEST_PLACEHOLDER_DATASETS as readonly string[]).includes(name);
}

export function isManifestStaticDataSource(dataSource: DataSourceConfigData | undefined): boolean {
  if (!dataSource?.dataset) return false;
  return dataSource.dataset === '__manifest_static__';
}

export function isManifestServiceDataSource(dataSource: DataSourceConfigData | undefined): boolean {
  if (!dataSource?.dataset) return false;
  return dataSource.dataset === '__manifest_service__';
}

/** Manifest tabanlı widget — Designer edit rotası */
export function isManifestWidget(widget: WidgetLike | null | undefined): boolean {
  if (!widget) return false;
  if (resolveWidgetDefinitionManifest(widget)) return true;
  if (isLegacyWidget(widget)) return false;
  return Boolean(
    widget.templateId &&
      widget.templateId !== LEGACY_CUSTOM_TEMPLATE_ID &&
      (isManifestServiceDataSource(widget.dataSource) || isManifestStaticDataSource(widget.dataSource)),
  );
}

export function shouldFetchWidgetData(widget: WidgetLike): boolean {
  if (widget.type === 'map') return false;
  if (widget.type === 'banner' && isManifestStaticDataSource(widget.dataSource)) return false;
  if (isManifestStaticDataSource(widget.dataSource)) return false;
  if (isManifestServiceDataSource(widget.dataSource)) return true;
  return Boolean(widget.dataSource && widget.dataSource.type === 'data' && widget.dataSource.dataset);
}

export function cloneTemplateToDefinition(
  template: WidgetTemplateManifest,
  overrides: {
    name: string;
    title?: LocalizedString;
    preset?: string;
    parameters?: Record<string, ParameterValue>;
    categoryId?: string;
  }
): WidgetDefinitionManifest {
  const preset = overrides.preset ?? template.presentation.defaultPreset ?? 'stat-simple';
  return {
    ...template,
    name: overrides.name,
    title: overrides.title ?? template.title,
    templateId: template.templateId,
    templateVersion: template.templateVersion,
    presentation: {
      ...template.presentation,
      preset,
    },
    parameters: overrides.parameters ?? {},
    parametersSchema: template.parametersSchema ?? [],
    isActive: true,
    category: overrides.categoryId ?? template.category,
  };
}
