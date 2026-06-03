import type { OpBoard } from '@/types/apps/operationCore';
import { boardListColumnKeys, deriveBoardListColumns } from '@/utils/ocBoardListColumns';

/** op_boards kaydı için DG PUT gövdesi (board dialog ile aynı şema). */
export function buildBoardDgPayload(board: OpBoard, poolFieldKeys: string[] = []): Record<string, unknown> {
  const columns = (board.columns ?? [])
    .filter((c) => c.stateId)
    .map((c) => ({
      stateId: c.stateId,
      title: c.title?.trim() || null,
      queryKey: c.queryKey?.trim() || 'wi_board_column',
      defaultTransitionKey: c.defaultTransitionKey?.trim() || null,
    }));

  const listColumns = deriveBoardListColumns(board.listColumns, board.visibleFields, poolFieldKeys);
  const visibleKeys = boardListColumnKeys(listColumns.filter((c) => !c.computed));
  const defaultSort =
    board.defaultSort?.field && listColumns.some((c) => c.key === board.defaultSort?.field && c.sortable)
      ? { field: board.defaultSort.field, direction: board.defaultSort.direction }
      : null;

  return {
    name: board.name.trim(),
    workspaceId: board.workspaceId,
    viewType: board.viewType || 'list',
    defaultFormId: board.defaultFormId || null,
    defaultDashboardId: board.defaultDashboardId || null,
    defaultStateFlowId: board.defaultStateFlowId || null,
    defaultProfileId: board.defaultProfileId || null,
    defaultTypeId: board.defaultTypeId || null,
    defaultPriorityId: board.defaultPriorityId || null,
    defaultStateId: board.defaultStateId || null,
    visibleFields: visibleKeys,
    viewGroups: board.viewGroups?.length ? board.viewGroups : null,
    editGroups: board.editGroups?.length ? board.editGroups : null,
    config: { columns, listColumns, defaultSort },
  };
}
