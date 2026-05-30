import type { OpBoardColumnConfig, OpStateFlow } from '@/types/apps/operationCore';

/** Akış sırasına göre kanban kolonları öner (initial + geçiş hedefleri). */
export function suggestBoardColumnsFromFlow(
  flow: OpStateFlow,
  stateTitleById: Map<string, string>
): OpBoardColumnConfig[] {
  const order: string[] = [];
  const seen = new Set<string>();

  const initial = flow.initialStateId?.trim();
  if (initial && seen.add(initial)) {
    order.push(initial);
  }

  const transitions = [...flow.transitions].sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
  for (const tr of transitions) {
    const to = tr.toStateId?.trim();
    if (to && seen.add(to)) {
      order.push(to);
    }
  }

  return order.map((stateId) => {
    const outgoing = transitions.filter((tr) => tr.fromStateId === stateId);
    return {
      stateId,
      title: stateTitleById.get(stateId) ?? null,
      queryKey: 'wi_board_column',
      defaultTransitionKey:
        outgoing.length === 1 ? outgoing[0]!.transitionKey : null,
    };
  });
}

export function boardColumnStateIds(columns: OpBoardColumnConfig[]): Set<string> {
  return new Set(columns.map((c) => c.stateId).filter(Boolean));
}

export function moveBoardColumn(
  columns: OpBoardColumnConfig[],
  index: number,
  direction: -1 | 1
): OpBoardColumnConfig[] {
  const next = [...columns];
  const target = index + direction;
  if (target < 0 || target >= next.length) return next;
  const tmp = next[index]!;
  next[index] = next[target]!;
  next[target] = tmp;
  return next;
}

export function outgoingTransitionsForState(
  flow: OpStateFlow | null | undefined,
  stateId: string
): { value: string; title: string; subtitle?: string }[] {
  if (!flow || !stateId) return [];
  return flow.transitions
    .filter((tr) => tr.fromStateId === stateId)
    .map((tr) => {
      const toName = tr.toStateId;
      return {
        value: tr.transitionKey,
        title: tr.label?.trim() || tr.transitionKey,
        subtitle: toName ? `→ ${toName}` : undefined,
      };
    });
}

export function pickDefaultTransitionForState(
  flow: OpStateFlow | null | undefined,
  stateId: string
): string | null {
  const options = outgoingTransitionsForState(flow, stateId);
  return options.length === 1 ? options[0]!.value : null;
}
