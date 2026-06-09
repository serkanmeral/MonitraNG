export type OcProfileNavFrom = 'board' | 'workspace';

export function buildWorkItemProfilePath(
  workItemId: string,
  opts: {
    boardId?: string | null;
    workspaceId?: string | null;
    from?: OcProfileNavFrom;
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
  return `/apps/operation-core/work-items/${encodeURIComponent(id)}/profile?${qs.toString()}`;
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
