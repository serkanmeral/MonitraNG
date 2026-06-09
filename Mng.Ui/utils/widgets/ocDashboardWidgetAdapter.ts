import type { Widget } from '@/stores/apps/widget';
import type { OcDashboardWidget, OcDashboardWidgetDef, OcBoardCatalogs, OcPersonDisplay } from '@/types/apps/operationCore';
import type { WidgetDataResponse } from '@/services/widgetDataService';
import type { WidgetLike } from '@/utils/widgets/widgetManifestAdapter';

const OC_WIDGET_TYPE_MAP: Record<string, Widget['type']> = {
  summaryCard: 'card',
  list: 'table',
  chart: 'chart',
};

const OC_CHART_TYPE_MAP: Record<string, string> = {
  donut: 'donut',
  pie: 'pie',
  bar: 'bar',
  line: 'line',
};

/**
 * MO op_dashboards.widgets[] tanımını legacy @widgets runtime modeline çevirir (Faz 4 köprü).
 * OcDashboardView henüz WidgetHost kullanmıyor; adapter test ve kademeli migrasyon için.
 */
export function ocDashboardWidgetDefToLegacyWidget(
  def: OcDashboardWidgetDef,
  options?: { workspaceId?: string },
): WidgetLike {
  const dataset = def.dataset?.trim() || 'op_work_items';
  const queryName = def.queryKey?.trim() ?? '';
  const widgetType = OC_WIDGET_TYPE_MAP[def.type] ?? 'card';

  const parameters: Record<string, unknown> = { ...(def.parameters ?? {}) };
  if (options?.workspaceId && !parameters.workspaceId) {
    parameters.workspaceId = options.workspaceId;
  }

  const config: Record<string, unknown> = {
    title: def.title ?? def.key,
    ocWidgetKey: def.key,
    ocWidgetType: def.type,
  };

  if (def.type === 'summaryCard') {
    if (def.accentColor) config.color = def.accentColor;
    if (def.icon) config.icon = def.icon;
    config.format = 'number';
  }

  if (def.type === 'chart') {
    config.type = OC_CHART_TYPE_MAP[def.chartType ?? 'donut'] ?? 'donut';
    if (def.groupBy) config.groupBy = def.groupBy;
    config.height = 280;
  }

  if (def.type === 'list' && def.take != null) {
    config.limit = def.take;
  }

  return {
    name: def.key,
    type: widgetType,
    category: '',
    isActive: true,
    config,
    dataSource: {
      type: 'data',
      dataset,
      getMethod: 'predefined',
      predefined: {
        queryName,
        parameters,
      },
      mapping: def.type === 'chart' && def.groupBy
        ? { groupBy: def.groupBy }
        : undefined,
    },
  };
}

export function ocDashboardWidgetDefsToLegacyWidgets(
  defs: OcDashboardWidgetDef[],
  options?: { workspaceId?: string },
): WidgetLike[] {
  return defs.map((d) => ocDashboardWidgetDefToLegacyWidget(d, options));
}

export function ocDashboardWidgetToLegacyWidget(
  widget: OcDashboardWidget,
  options?: { workspaceId?: string },
): WidgetLike {
  return ocDashboardWidgetDefToLegacyWidget(
    {
      key: widget.key,
      type: widget.widgetType,
      title: widget.title,
      dataset: widget.dataset,
      queryKey: widget.queryKey,
      parameters: widget.resolvedParameters ?? undefined,
      chartType: widget.chartType,
      groupBy: widget.groupBy,
      accentColor: widget.accentColor,
      icon: widget.icon,
      take: widget.execution?.take ?? undefined,
    },
    options,
  );
}

/** MO server-side execution sonucunu WidgetHost batch map formatına çevirir. */
export function ocExecutionToWidgetData(
  widget: OcDashboardWidget,
  options?: {
    catalogs?: OcBoardCatalogs;
    people?: Record<string, OcPersonDisplay>;
  },
): WidgetDataResponse | null {
  const exec = widget.execution;
  if (!exec?.success) return null;

  const kind = (widget.widgetType || '').toLowerCase();
  if (kind === 'summarycard') {
    return { data: [{ value: exec.total }], total: 1 };
  }
  if (kind === 'list') {
    return { data: exec.items ?? [], total: exec.total ?? exec.items?.length ?? 0 };
  }
  if (kind === 'chart' && exec.aggregation?.length) {
    const groupBy = (widget.groupBy || 'stateId').toLowerCase();
    const rows = exec.aggregation.map((b) => {
      const key = b.key ?? '';
      let label = key || '—';
      if (groupBy === 'assignee') {
        label = options?.people?.[key]?.name?.trim() || key || '—';
      } else if (groupBy === 'priorityid') {
        label = options?.catalogs?.priorities?.[key]?.name?.trim() || key || '—';
      } else if (groupBy === 'typeid') {
        label = options?.catalogs?.types?.[key]?.name?.trim() || key || '—';
      } else if (groupBy === 'stateid') {
        label = options?.catalogs?.states?.[key]?.name?.trim() || key || '—';
      }
      return {
        key,
        label,
        stateName: label,
        count: b.count,
        value: b.count,
      };
    });
    return { data: rows, total: rows.length };
  }
  return null;
}
