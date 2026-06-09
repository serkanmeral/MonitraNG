import type { CreateWidgetDto, UpdateWidgetDto, Widget, WidgetCategory } from '@/stores/apps/widget';
import type {
  ParameterValue,
  PresentationKind,
  WidgetDefinitionManifest,
  WidgetDomain,
  WidgetTemplateManifest,
  WidgetTemplateRecord,
} from '@/types/apps/widgetManifest';
import {
  cloneTemplateToDefinition,
  extractPresentationConfigForDesigner,
  manifestToLegacyWidget,
  normalizeTemplateRecord,
  pickLocalized,
  resolveWidgetDefinitionManifest,
} from '@/utils/widgets/widgetManifestAdapter';
import {
  WIDGET_MODULES,
  widgetModulesForDesigner,
  resolveCategoryIdForTemplate,
  resolveDomainCategoryName,
} from '@/utils/widgets/widgetCategoryDomains';

export interface WidgetDesignerDraft {
  template: WidgetTemplateManifest;
  templateRecord?: WidgetTemplateRecord;
  name: string;
  title: string;
  description?: string;
  preset: string;
  parameters: Record<string, ParameterValue>;
  /** Stat/chart/table sunum alan eşlemesi (designer override) */
  presentationConfigOverrides: Record<string, unknown>;
  refreshIntervalSeconds: number | null;
  permissionGroups: string[];
  isActive: boolean;
  order: number;
}

export interface WidgetDesignerDomainOption {
  value: WidgetDomain | 'all';
  icon: string;
  labelKey: string;
}

export const WIDGET_DESIGNER_DOMAINS: WidgetDesignerDomainOption[] = [
  { value: 'all', icon: 'mdi-view-grid', labelKey: 'allModules' },
  ...WIDGET_MODULES.map((m) => ({
    value: m.domain as WidgetDomain,
    icon: m.icon,
    labelKey: m.domain,
  })),
];

export const PRESENTATION_KIND_ICONS: Record<PresentationKind, string> = {
  stat: 'mdi-numeric',
  chart: 'mdi-chart-line',
  table: 'mdi-table',
  list: 'mdi-format-list-bulleted',
  banner: 'mdi-bullhorn',
  gauge: 'mdi-gauge',
  map: 'mdi-map',
  embed: 'mdi-code-tags',
};

export const REFRESH_INTERVAL_OPTIONS: Array<{ value: number | null; labelKey: string }> = [
  { value: null, labelKey: 'refreshOff' },
  { value: 30, labelKey: 'refresh30s' },
  { value: 60, labelKey: 'refresh1m' },
  { value: 300, labelKey: 'refresh5m' },
];

export function resolveCategoryIdBySlug(slug: string, categories: WidgetCategory[]): string | undefined {
  return resolveCategoryIdForTemplate({ category: slug }, categories);
}

/** Şablondan @widget_categories relation id — domain öncelikli */
export function resolveCategoryIdFromTemplate(
  template: Pick<WidgetTemplateManifest, 'domain' | 'category'>,
  categories: WidgetCategory[],
): string | undefined {
  return resolveCategoryIdForTemplate(
    { domain: template.domain, category: template.category },
    categories,
  );
}

/** Eski teknik slug → domain ad (seed / liste uyumu) */
export { widgetModulesForDesigner, WIDGET_MODULES };

export function suggestWidgetName(templateId: string): string {
  const base = templateId.replace(/\./g, '-').replace(/[^a-zA-Z0-9-_]/g, '');
  const suffix = Date.now().toString(36).slice(-4);
  return `${base}-${suffix}`;
}

export function buildDefaultParameters(template: WidgetTemplateManifest): Record<string, ParameterValue> {
  const params: Record<string, ParameterValue> = {};
  for (const field of template.parametersSchema ?? []) {
    if (field.hidden || field.advanced) continue;
    if (field.default !== undefined) {
      params[field.name] = field.default;
    }
  }
  return params;
}

export function createDraftFromTemplate(record: WidgetTemplateRecord): WidgetDesignerDraft {
  const template = normalizeTemplateRecord(record);

  const title = pickLocalized(template.title);
  const bridged = manifestToLegacyWidget(template, { name: record.templateId });
  const presentationConfigOverrides = extractPresentationConfigForDesigner(
    (bridged.config ?? {}) as Record<string, unknown>,
  );
  return {
    template,
    templateRecord: record,
    name: suggestWidgetName(record.templateId),
    title,
    description: record.description ? pickLocalized(record.description) : undefined,
    preset: template.presentation.defaultPreset ?? template.presentation.allowedPresets?.[0] ?? 'stat-simple',
    parameters: buildDefaultParameters(template),
    presentationConfigOverrides,
    refreshIntervalSeconds: null,
    permissionGroups: template.permissions?.groups ?? [],
    isActive: true,
    order: 0,
  };
}

function definitionToTemplateManifest(
  definition: WidgetDefinitionManifest,
  templateRecord?: WidgetTemplateRecord | null,
): WidgetTemplateManifest {
  const shell = templateRecord ? normalizeTemplateRecord(templateRecord) : null;
  return {
    manifestVersion: definition.manifestVersion ?? '1.0',
    templateId: definition.templateId,
    templateVersion: definition.templateVersion,
    domain: definition.domain,
    category: definition.category,
    title: definition.title,
    description: definition.description,
    tags: definition.tags,
    presentation: definition.presentation,
    dataBinding: definition.dataBinding,
    parametersSchema: shell?.manifest?.parametersSchema ?? definition.parametersSchema ?? [],
    interactions: definition.interactions,
    permissions: definition.permissions,
    export: definition.export,
  };
}

/** Mevcut @widgets kaydından Designer draft */
export function createDraftFromWidget(
  widget: Widget,
  templateRecord?: WidgetTemplateRecord | null,
): WidgetDesignerDraft {
  const config = (widget.config ?? {}) as Record<string, unknown>;
  let definition = resolveWidgetDefinitionManifest(widget);

  if (!definition && templateRecord) {
    const template = normalizeTemplateRecord(templateRecord);
    definition = cloneTemplateToDefinition(template, {
      name: widget.name,
      title: { tr: widget.title, en: widget.title },
      preset: (config.presentationPreset as string) ?? template.presentation.defaultPreset ?? 'stat-simple',
      parameters: (config.parameters as Record<string, ParameterValue>) ?? {},
      categoryId: typeof widget.category === 'string' ? widget.category : widget.category?.__dataId,
    });
    definition.isActive = widget.isActive ?? true;
    definition.order = widget.order ?? 0;
  }

  if (!definition) {
    throw new Error('Manifest widget tanımı bulunamadı');
  }

  const template = definitionToTemplateManifest(definition, templateRecord);
  const overridesRaw = config.presentationConfigOverrides as Record<string, unknown> | undefined;
  const presentationConfigOverrides =
    overridesRaw && typeof overridesRaw === 'object'
      ? overridesRaw
      : extractPresentationConfigForDesigner(config);

  return {
    template,
    templateRecord: templateRecord ?? undefined,
    name: widget.name,
    title: widget.title,
    description: widget.description,
    preset:
      (config.presentationPreset as string) ??
      definition.presentation.preset ??
      template.presentation.defaultPreset ??
      'stat-simple',
    parameters: { ...(definition.parameters ?? {}) },
    presentationConfigOverrides,
    refreshIntervalSeconds:
      typeof config.refreshIntervalSeconds === 'number' ? config.refreshIntervalSeconds : null,
    permissionGroups: widget.permissions?.groups ?? definition.permissions?.groups ?? [],
    isActive: widget.isActive ?? definition.isActive ?? true,
    order: widget.order ?? definition.order ?? 0,
  };
}

function buildManifestConfigFromDraft(
  draft: WidgetDesignerDraft,
  categoryId: string,
): { definition: WidgetDefinitionManifest; legacy: ReturnType<typeof manifestToLegacyWidget> } {
  const definition = cloneTemplateToDefinition(draft.template, {
    name: draft.name,
    title: { tr: draft.title, en: draft.title },
    preset: draft.preset,
    parameters: draft.parameters,
    categoryId,
  });
  definition.isActive = draft.isActive;
  definition.order = draft.order;

  const legacy = manifestToLegacyWidget(definition, {
    name: draft.name,
    categoryId,
    parameters: draft.parameters,
    isActive: draft.isActive,
    presentationConfigOverrides: draft.presentationConfigOverrides,
  });

  return { definition, legacy };
}

export function draftToCreateWidgetDto(draft: WidgetDesignerDraft, categoryId: string): CreateWidgetDto {
  const { definition, legacy } = buildManifestConfigFromDraft(draft, categoryId);

  const config: Record<string, unknown> = {
    ...(legacy.config as Record<string, unknown>),
    manifest: definition,
    presentationConfigOverrides: draft.presentationConfigOverrides,
    refreshIntervalSeconds: draft.refreshIntervalSeconds ?? undefined,
  };

  return {
    name: draft.name,
    title: draft.title,
    description:
      draft.description ??
      (draft.template.description ? pickLocalized(draft.template.description) : undefined),
    category: categoryId,
    type: legacy.type,
    dataSource: legacy.dataSource,
    config,
    isActive: draft.isActive,
    order: draft.order,
    permissions: draft.permissionGroups.length ? { groups: draft.permissionGroups } : undefined,
  };
}

export function draftToUpdateWidgetDto(draft: WidgetDesignerDraft, categoryId: string): UpdateWidgetDto {
  const { definition, legacy } = buildManifestConfigFromDraft(draft, categoryId);

  const config: Record<string, unknown> = {
    ...(legacy.config as Record<string, unknown>),
    manifest: definition,
    presentationConfigOverrides: draft.presentationConfigOverrides,
    refreshIntervalSeconds: draft.refreshIntervalSeconds ?? undefined,
  };

  return {
    name: draft.name,
    title: draft.title,
    description:
      draft.description ??
      (draft.template.description ? pickLocalized(draft.template.description) : undefined),
    category: categoryId,
    type: legacy.type,
    dataSource: legacy.dataSource,
    config,
    isActive: draft.isActive,
    order: draft.order,
    permissions: draft.permissionGroups.length ? { groups: draft.permissionGroups } : undefined,
  };
}
