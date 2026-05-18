import type { TmProject, TmProjectWorkflow, TmStatus, TmBoardConfig, BoardColumnConfig } from '@/types/apps/taskManager';

const rid = (v: unknown): string => {
  if (v == null) return '';
  if (typeof v === 'string') return v;
  if (typeof v === 'object' && v !== null && ('__dataId' in v || 'dataId' in v))
    return (v as { __dataId?: string; dataId?: string }).__dataId ?? (v as { dataId?: string }).dataId ?? '';
  return String(v);
};

/**
 * Havuz listesi / yeni proje varsayılan akışı için sıralama: ada göre.
 * Kanban kolon sırası proje workflow’undadır.
 */
export function sortTmStatusesByName(statuses: TmStatus[]): TmStatus[] {
  return [...statuses].sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
}

/** @deprecated sortTmStatusesByName kullanın */
export const sortTmStatusesByCategoryThenName = sortTmStatusesByName;

/** Havuzdaki durumlara göre sıralı varsayılan akış: yalnızca komşu kolona geçiş. */
export function buildDefaultWorkflow(statuses: TmStatus[]): TmProjectWorkflow {
  const sorted = sortTmStatusesByName(statuses);
  const ids = sorted.map((s) => s.__dataId).filter(Boolean);
  const transitions: Record<string, string[]> = {};
  for (let i = 0; i < ids.length; i++) {
    const cur = ids[i];
    if (i < ids.length - 1) transitions[cur] = [ids[i + 1]];
    else transitions[cur] = [];
  }
  return {
    statusIds: ids,
    initialStatusId: ids[0] ?? '',
    closedStatusId: ids.length ? ids[ids.length - 1] : '',
    transitions,
  };
}

function asWorkflow(raw: unknown): Partial<TmProjectWorkflow> | null {
  if (raw == null || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const statusIds = Array.isArray(o.statusIds) ? o.statusIds.map((x) => rid(x)).filter(Boolean) : [];
  const initialStatusId = rid(o.initialStatusId);
  const closedStatusId = rid(o.closedStatusId);
  const transitions: Record<string, string[]> = {};
  if (o.transitions && typeof o.transitions === 'object') {
    for (const [k, v] of Object.entries(o.transitions as Record<string, unknown>)) {
      const key = rid(k);
      if (!key) continue;
      transitions[key] = Array.isArray(v)
        ? (v as unknown[]).map((x) => rid(x)).filter(Boolean)
        : [];
    }
  }
  return { statusIds, initialStatusId, closedStatusId, transitions };
}

/** Geçersiz id'leri atar; boş geçişleri tamamlamak için minimum doğrulama. */
export function normalizeWorkflow(partial: Partial<TmProjectWorkflow>, pool: TmStatus[]): TmProjectWorkflow {
  const poolIds = new Set(pool.map((s) => s.__dataId));
  let statusIds = (partial.statusIds ?? []).filter((id) => poolIds.has(id));
  if (statusIds.length === 0) {
    return buildDefaultWorkflow(pool);
  }
  const transitions: Record<string, string[]> = { ...partial.transitions };
  for (const id of statusIds) {
    if (!transitions[id]) transitions[id] = [];
    transitions[id] = [...new Set(transitions[id].filter((t) => statusIds.includes(t) && t !== id))];
  }
  for (const k of Object.keys(transitions)) {
    if (!statusIds.includes(k)) delete transitions[k];
  }
  let initialStatusId = partial.initialStatusId && statusIds.includes(partial.initialStatusId) ? partial.initialStatusId : statusIds[0];
  let closedStatusId =
    partial.closedStatusId && statusIds.includes(partial.closedStatusId) ? partial.closedStatusId : statusIds[statusIds.length - 1];
  if (initialStatusId === closedStatusId && statusIds.length > 1) {
    closedStatusId = statusIds[statusIds.length - 1];
    if (closedStatusId === initialStatusId) closedStatusId = statusIds.find((x) => x !== initialStatusId) ?? closedStatusId;
  }
  return {
    statusIds,
    initialStatusId,
    closedStatusId,
    transitions,
  };
}

export function parseProjectWorkflow(raw: unknown, pool: TmStatus[]): TmProjectWorkflow | null {
  const p = asWorkflow(raw);
  if (!p || !p.statusIds?.length) return null;
  return normalizeWorkflow(
    {
      statusIds: p.statusIds,
      initialStatusId: p.initialStatusId,
      closedStatusId: p.closedStatusId,
      transitions: p.transitions ?? {},
    },
    pool
  );
}

/** Kayıtlı workflow yoksa veya bozuksa havuzdan türetilmiş varsayılanı döner. */
export function getEffectiveWorkflow(project: TmProject | null | undefined, pool: TmStatus[]): TmProjectWorkflow {
  const parsed = project?.workflow != null ? parseProjectWorkflow(project.workflow, pool) : null;
  if (parsed && parsed.statusIds.length) return parsed;
  return buildDefaultWorkflow(pool);
}

export function isTransitionAllowed(
  wf: TmProjectWorkflow,
  fromStatusId: string,
  toStatusId: string
): boolean {
  if (!fromStatusId || !toStatusId || fromStatusId === toStatusId) return true;
  if (!wf.statusIds.includes(fromStatusId)) {
    return true;
  }
  const next = wf.transitions[fromStatusId];
  if (!next || next.length === 0) return false;
  return next.includes(toStatusId);
}

export function getAllowedTargetStatusIds(wf: TmProjectWorkflow, fromStatusId: string): string[] {
  if (!wf.statusIds.includes(fromStatusId)) return [...wf.statusIds];
  return wf.transitions[fromStatusId] ?? [];
}

/** Yeni görev için başlangıç durumu. */
export function getInitialStatusId(project: TmProject | null | undefined, pool: TmStatus[]): string | null {
  const wf = getEffectiveWorkflow(project, pool);
  return (wf.initialStatusId || wf.statusIds[0]) ?? null;
}

export function buildBoardConfigFromWorkflow(wf: TmProjectWorkflow, pool: TmStatus[]): TmBoardConfig {
  const byId = new Map(pool.map((s) => [s.__dataId, s]));
  const columns: BoardColumnConfig[] = wf.statusIds.map((statusId) => ({
    statusId,
    title: byId.get(statusId)?.name ?? statusId,
    wipLimit: null,
  }));
  return { columns };
}

/**
 * Board kolonları: önce workflow sırası; config başlıkları korunur.
 */
export function resolveKanbanColumns(
  board: TmBoard | null | undefined,
  project: TmProject | null | undefined,
  statuses: TmStatus[]
): { statusId: string; title: string }[] {
  const wf = getEffectiveWorkflow(project, statuses);
  const byId = new Map(statuses.map((s) => [s.__dataId, s]));
  const cfg = board?.config?.columns;
  const orderIdx = new Map(wf.statusIds.map((id, i) => [id, i]));
  if (cfg?.length) {
    const filtered = cfg.filter((c) => wf.statusIds.includes(c.statusId));
    filtered.sort((a, b) => (orderIdx.get(a.statusId) ?? 999) - (orderIdx.get(b.statusId) ?? 999));
    return filtered.map((c) => ({
      statusId: c.statusId,
      title: c.title || byId.get(c.statusId)?.name || c.statusId,
    }));
  }
  return wf.statusIds.map((statusId) => ({
    statusId,
    title: byId.get(statusId)?.name ?? statusId,
  }));
}

/** Workflow dışı kalan issue durumları için ek kolonlar (migration / eski veri). */
export function orphanStatusColumns(
  issueStatusIds: string[],
  wf: TmProjectWorkflow,
  statuses: TmStatus[]
): { statusId: string; title: string }[] {
  const inWf = new Set(wf.statusIds);
  const byId = new Map(statuses.map((s) => [s.__dataId, s]));
  const extra = new Set<string>();
  for (const sid of issueStatusIds) {
    if (sid && !inWf.has(sid)) extra.add(sid);
  }
  return [...extra].map((statusId) => ({
    statusId,
    title: (byId.get(statusId)?.name ?? statusId) + ' *',
  }));
}

/** Legacy / eksik alan: Kanban açık kabul edilir. */
export function projectUsesKanban(project: TmProject | null | undefined): boolean {
  if (!project) return true;
  return project.useKanban !== false;
}
