export interface OcWorkspaceViews {
  sprints: boolean;
  calendar: boolean;
  gantt: boolean;
}

const CLOSED: OcWorkspaceViews = { sprints: false, calendar: false, gantt: false };

/** Eksik anahtar kapalıdır. Yazılım paketi ilk kurulumda üçünü de açar. */
export function readOcWorkspaceViews(settings?: Record<string, unknown> | null): OcWorkspaceViews {
  const raw = settings?.views;
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return { ...CLOSED };
  const views = raw as Record<string, unknown>;
  return {
    sprints: views.sprints === true,
    calendar: views.calendar === true,
    gantt: views.gantt === true,
  };
}

export function mergeOcWorkspaceViews(
  settings: Record<string, unknown> | undefined,
  views: OcWorkspaceViews
): Record<string, unknown> {
  const base = { ...(settings ?? {}) };
  const current =
    base.views && typeof base.views === 'object' && !Array.isArray(base.views)
      ? { ...(base.views as Record<string, unknown>) }
      : {};
  base.views = {
    ...current,
    sprints: views.sprints,
    calendar: views.calendar,
    gantt: views.gantt,
  };
  return base;
}
