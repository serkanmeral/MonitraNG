import type { TmIssueHistoryEntry, TmIssueHistoryFieldChange } from '@/types/apps/taskManager';
import { stripHtmlToPlainText } from '@/utils/htmlPlainText';

function asRecord(v: unknown): Record<string, unknown> | null {
  if (v == null || typeof v !== 'object' || Array.isArray(v)) return null;
  return v as Record<string, unknown>;
}

/** DG / Mongo JSON: tarih string, sayı veya `{ $date: "..." }` */
export function historyIsoFromUnknown(v: unknown): string | null {
  if (v == null) return null;
  if (typeof v === 'string') {
    const t = v.trim();
    return t || null;
  }
  if (typeof v === 'number' && Number.isFinite(v)) {
    const d = new Date(v);
    return Number.isNaN(d.getTime()) ? null : d.toISOString();
  }
  if (typeof v === 'object' && v !== null) {
    const o = v as Record<string, unknown>;
    const inner = o.$date ?? o.date;
    if (typeof inner === 'string' && inner.trim()) return inner.trim();
    if (typeof inner === 'number' && Number.isFinite(inner)) {
      const d = new Date(inner);
      return Number.isNaN(d.getTime()) ? null : d.toISOString();
    }
  }
  return null;
}

function parseChangesFromUnknown(ch: unknown): TmIssueHistoryFieldChange[] {
  const changes: TmIssueHistoryFieldChange[] = [];
  if (Array.isArray(ch)) {
    for (const c of ch) {
      const co = asRecord(c);
      if (!co) continue;
      const field = String(co.field ?? co.fieldKey ?? co.key ?? '').trim();
      const label = co.label != null ? String(co.label).trim() : null;
      const oldValue = co.oldValue ?? co.old ?? co.previous;
      const newValue = co.newValue ?? co.new ?? co.next;
      if (field || label) {
        changes.push({
          field: field || undefined,
          label: label || undefined,
          oldValue,
          newValue,
        });
      }
    }
    return changes;
  }
  const obj = asRecord(ch);
  if (!obj) return changes;
  for (const [k, v] of Object.entries(obj)) {
    if (k.startsWith('__')) continue;
    const vo = asRecord(v);
    if (vo && ('old' in vo || 'new' in vo || 'oldValue' in vo || 'newValue' in vo)) {
      changes.push({
        field: k,
        oldValue: vo.oldValue ?? vo.old,
        newValue: vo.newValue ?? vo.new,
      });
    } else {
      // MngDataGateway `AddHistoryEntry`: `changes` = güncelleme gövdesi (alan → yeni değer)
      changes.push({
        field: k,
        oldValue: undefined,
        newValue: v,
      });
    }
  }
  return changes;
}

/**
 * DG `__history` alanını `TmIssueHistoryEntry[]` biçimine çevirir.
 * Kabul edilen şekiller: JSON dizi; tek nesne; `changes` / `fields` dizi veya alan→{old,new} haritası.
 */
export function parseIssueHistory(raw: unknown): TmIssueHistoryEntry[] {
  if (raw == null) return [];

  let root: unknown = raw;
  if (typeof raw === 'string') {
    const t = raw.trim();
    if (!t) return [];
    try {
      root = JSON.parse(t) as unknown;
    } catch {
      return [];
    }
  }

  let arr: unknown[] = [];
  if (Array.isArray(root)) arr = root;
  else if (root && typeof root === 'object') arr = [root];
  else return [];

  const out: TmIssueHistoryEntry[] = [];

  for (const item of arr) {
    const o = asRecord(item);
    if (!o) continue;

    const changedAt =
      historyIsoFromUnknown(o.timestamp ?? o.changedAt ?? o.at ?? o.date ?? o.createdAt) ||
      String(o.changedAt ?? o.at ?? o.date ?? o.createdAt ?? '').trim() ||
      null;

    const userInfo = asRecord(o.userInfo);
    const userId =
      String(o.changedBy ?? o.userId ?? o.authorId ?? o.actorId ?? userInfo?.uid ?? userInfo?.userId ?? '').trim() ||
      null;
    const userName =
      String(
        o.changedByName ?? o.userName ?? o.actorName ?? o.userEmail ?? userInfo?.userName ?? ''
      ).trim() || null;

    let changes = parseChangesFromUnknown(o.changes ?? o.fields ?? o.diff);

    if (!changes.length && (o.field || o.fieldKey)) {
      const field = String(o.field ?? o.fieldKey ?? '').trim();
      const label = o.label != null ? String(o.label).trim() : null;
      changes = [
        {
          field: field || undefined,
          label: label || undefined,
          oldValue: o.oldValue ?? o.old ?? o.previous,
          newValue: o.newValue ?? o.new ?? o.next,
        },
      ];
    }

    if (changedAt || userId || userName || changes.length) {
      out.push({ changedAt, userId, userName, changes });
    }
  }

  out.sort((a, b) => {
    const ta = a.changedAt ? Date.parse(a.changedAt) : 0;
    const tb = b.changedAt ? Date.parse(b.changedAt) : 0;
    return (Number.isFinite(tb) ? tb : 0) - (Number.isFinite(ta) ? ta : 0);
  });

  return out;
}

export function formatIssueHistoryValue(value: unknown, fieldKey?: string | null): string {
  if (value === null || value === undefined || value === '') return '—';
  if (typeof value === 'string') {
    if (fieldKey === 'description') {
      const plain = stripHtmlToPlainText(value).trim();
      return plain || '—';
    }
    return value;
  }
  if (typeof value === 'number' || typeof value === 'boolean') {
    return String(value);
  }
  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
}
