import type { SurfaceContext, LocalizedString } from '@/types/apps/widgetManifest';
import { resolveContextRef, pickLocalized } from '@/utils/widgets/widgetManifestAdapter';

export interface CrossFilterInteraction {
  variable: string;
  field: string;
  clearValue?: string | number | boolean | null;
}

export interface ChartZoomInteraction {
  enabled?: boolean;
  xField?: string;
}

export interface DrillDownConfig {
  type?: 'route' | 'external';
  label?: LocalizedString;
  path: string;
  paramMap?: Record<string, string>;
  openInNewTab?: boolean;
}

export interface WidgetActionConfig {
  id: string;
  label: LocalizedString;
  icon?: string;
  type: 'route' | 'workflow' | 'api';
  path?: string;
  workflowId?: string;
  parameterMap?: Record<string, string>;
  requiredGroups?: string[];
}

export interface WidgetInteractions {
  drillDown?: DrillDownConfig | DrillDownConfig[];
  rowClick?: DrillDownConfig;
  crossFilter?: CrossFilterInteraction;
  chartZoom?: ChartZoomInteraction;
  actions?: WidgetActionConfig[];
}

export function getWidgetInteractions(widget: { config?: object | null }): WidgetInteractions | null {
  const config = (widget.config ?? {}) as Record<string, unknown>;
  const manifest = config.manifest as { interactions?: WidgetInteractions } | undefined;
  const direct = config.interactions as WidgetInteractions | undefined;
  return manifest?.interactions ?? direct ?? null;
}

export function resolveDrillDownConfig(interactions: WidgetInteractions | null): DrillDownConfig | null {
  if (!interactions) return null;
  if (interactions.rowClick?.path) return interactions.rowClick;
  const drill = interactions.drillDown;
  if (Array.isArray(drill)) return drill.find((d) => d.path) ?? null;
  if (drill?.path) return drill;
  return null;
}

export function resolveRowField(row: Record<string, unknown>, fieldRef: string): unknown {
  const path = fieldRef.startsWith('$row.') ? fieldRef.slice(5) : fieldRef;
  if (!path) return undefined;
  const keys = path.split('.');
  let value: unknown = row;
  for (const key of keys) {
    if (value == null || typeof value !== 'object') return undefined;
    value = (value as Record<string, unknown>)[key];
    if (value === undefined && key in (row as Record<string, unknown>)) {
      value = (row as Record<string, unknown>)[key];
    }
  }
  return value;
}

export function resolveInteractionRef(
  ref: string,
  row: Record<string, unknown>,
  context: SurfaceContext = {},
): unknown {
  if (ref.startsWith('$row.')) return resolveRowField(row, ref);
  if (ref.startsWith('$')) return resolveContextRef(ref, context);
  return ref;
}

export function resolveParamMap(
  paramMap: Record<string, string> | undefined,
  row: Record<string, unknown>,
  context: SurfaceContext = {},
): Record<string, string> {
  const query: Record<string, string> = {};
  if (!paramMap) return query;
  for (const [key, ref] of Object.entries(paramMap)) {
    const val = resolveInteractionRef(ref, row, context);
    if (val === undefined || val === null || val === '') continue;
    query[key] = String(val);
  }
  return query;
}

export function applyCrossFilterToVariables(
  interaction: CrossFilterInteraction,
  row: Record<string, unknown>,
  current: SurfaceContext['variables'] = {},
): SurfaceContext['variables'] {
  const raw = resolveRowField(row, interaction.field);
  const next = { ...(current ?? {}) };
  const normalized =
    raw === undefined || raw === null || raw === ''
      ? interaction.clearValue ?? null
      : (raw as string | number | boolean);
  next[interaction.variable] = normalized;
  return next;
}

export function isChartZoomEnabled(
  widget: { config?: object | null; type?: string },
  interactions: WidgetInteractions | null,
): boolean {
  if (widget.type !== 'chart') return false;
  if (interactions?.chartZoom?.enabled === false) return false;
  const config = (widget.config ?? {}) as Record<string, unknown>;
  const xField =
    interactions?.chartZoom?.xField ??
    (config.xAxis as { field?: string } | undefined)?.field ??
    'timestamp';
  return xField === 'timestamp' || xField === 'hour' || xField === 'hourStart' || xField === 'bucketStart';
}

export function hasDrillDown(interactions: WidgetInteractions | null): boolean {
  return resolveDrillDownConfig(interactions) != null;
}

export function actionLabel(action: WidgetActionConfig, locale = 'tr'): string {
  return pickLocalized(action.label, locale);
}

export function resolveActionParams(
  parameterMap: Record<string, string> | undefined,
  row: Record<string, unknown>,
  context: SurfaceContext = {},
): Record<string, unknown> {
  const params: Record<string, unknown> = {};
  if (!parameterMap) return params;
  for (const [key, ref] of Object.entries(parameterMap)) {
    params[key] = resolveInteractionRef(ref, row, context);
  }
  return params;
}
