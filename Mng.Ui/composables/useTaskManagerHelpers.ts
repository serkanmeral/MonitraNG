import type { TmIssue } from '@/types/apps/taskManager';

/** Keeper / DG kullanıcı özeti (store ile uyumlu) */
export type AssigneeUserLookup = {
  firstName?: string;
  lastName?: string;
  username?: string;
} | null | undefined;

function isOidWrapper(v: unknown): v is { $oid: string } {
  return typeof v === 'object' && v !== null && typeof (v as { $oid?: unknown }).$oid === 'string';
}

/** persons / relation nesnesinden kimlik stringi (Mongo ObjectId, __dataId, Keeper id) */
export function assigneeUserId(assignee: unknown): string {
  if (assignee == null || assignee === '') return '';
  if (typeof assignee === 'string') return assignee;
  if (typeof assignee === 'object' && assignee !== null) {
    const o = assignee as Record<string, unknown>;
    const candidates: unknown[] = [
      o.id,
      o.userId,
      o.keycloakUserId,
      o.KeycloakUserId,
      o.__dataId,
      o.DataId,
      o.dataId,
      o._id,
    ];
    for (const c of candidates) {
      if (c == null || c === '') continue;
      if (typeof c === 'string') return c;
      if (isOidWrapper(c)) return c.$oid;
    }
  }
  return '';
}

/**
 * Atanan sütunu: API’de persons nesnesi (ad/soyad gömülü) veya yalnızca id.
 * Önce nesnedeki adları kullanır; yoksa id ile getUserById.
 */
export function assigneeDisplayLabel(assignee: unknown, getUserById?: (id: string) => AssigneeUserLookup): string {
  if (assignee == null || assignee === '') return '—';

  if (typeof assignee === 'string') {
    const id = assignee.trim();
    if (!id) return '—';
    const u = getUserById?.(id);
    if (u) {
      const n = `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim();
      if (n) return n;
      if (u.username) return u.username;
    }
    return id.length > 24 ? `${id.slice(0, 8)}…` : id;
  }

  if (typeof assignee === 'object' && assignee !== null) {
    const o = assignee as Record<string, unknown>;
    const fn = String(o.firstName ?? o.FirstName ?? o.first_name ?? '').trim();
    const ln = String(o.lastName ?? o.LastName ?? o.last_name ?? '').trim();
    const combined = `${fn} ${ln}`.trim();
    if (combined) return combined;

    const direct =
      (typeof o.displayName === 'string' && o.displayName.trim()) ||
      (typeof o.name === 'string' && o.name.trim()) ||
      (typeof o.username === 'string' && o.username.trim()) ||
      (typeof o.Username === 'string' && o.Username.trim()) ||
      (typeof o.email === 'string' && o.email.trim()) ||
      '';
    if (direct) return direct;

    const id = assigneeUserId(assignee);
    if (id && getUserById) {
      const u = getUserById(id);
      if (u) {
        const n = `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim();
        if (n) return n;
        if (u.username) return u.username;
      }
      return id.length > 24 ? `${id.slice(0, 8)}…` : id;
    }
    if (id) return id.length > 24 ? `${id.slice(0, 8)}…` : id;
  }

  return '—';
}

export function issueAssigneeLabel(issue: TmIssue, getUserById?: (id: string) => AssigneeUserLookup): string {
  return assigneeDisplayLabel(issue.assignee, getUserById);
}
