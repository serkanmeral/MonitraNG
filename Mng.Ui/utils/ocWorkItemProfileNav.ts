export type OcProfileNavFrom = 'board' | 'workspace';

const WORKSPACE_PANE_VIEWS = new Set(['library', 'sprints', 'calendar', 'gantt']);

export function buildWorkItemProfilePath(
  workItemId: string,
  opts: {
    boardId?: string | null;
    workspaceId?: string | null;
    from?: OcProfileNavFrom;
    view?: string | null;
  }
): string {
  const id = workItemId.trim();
  const qs = new URLSearchParams();
  const from = opts.from ?? (opts.workspaceId ? 'workspace' : 'board');
  qs.set('from', from);
  const boardId = opts.boardId?.trim();
  if (boardId) qs.set('boardId', boardId);
  const workspaceId = opts.workspaceId?.trim();
  if (workspaceId) qs.set('workspaceId', workspaceId);
  const view = opts.view?.trim();
  if (view && WORKSPACE_PANE_VIEWS.has(view)) qs.set('view', view);
  return `/apps/operation-core/work-items/${encodeURIComponent(id)}/profile?${qs.toString()}`;
}

/** Takvim, Gantt, kütüphane veya sprint sekmesinden açılan profilin geri adresi. */
export function resolveProfileBackToWorkspacePane(workspaceId: string, view: string): string | null {
  const id = workspaceId.trim();
  const pane = view.trim();
  if (!id || !WORKSPACE_PANE_VIEWS.has(pane)) return null;
  const qs = new URLSearchParams({ workspaceId: id, view: pane });
  return `/apps/operation-core/workspace?${qs.toString()}`;
}

/** Profil sayfası «geri» hedefi — workspace hub inline board veya tam sayfa board. */
export function resolveProfileBackToBoardPath(opts: {
  boardId: string;
  from?: string | null;
  workspaceId?: string | null;
  profileWorkspaceId?: string | null;
}): string {
  const boardId = opts.boardId.trim();
  const from = opts.from?.trim() ?? '';
  const workspaceId = (opts.workspaceId ?? opts.profileWorkspaceId ?? '').trim();

  if (from === 'workspace' && workspaceId) {
    const qs = new URLSearchParams({ workspaceId, boardId });
    return `/apps/operation-core/workspace?${qs.toString()}`;
  }

  return `/apps/operation-core/boards/${encodeURIComponent(boardId)}?view=list`;
}
