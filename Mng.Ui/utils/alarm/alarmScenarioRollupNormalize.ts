import type { AlarmScenarioRollup } from '@/types/apps/alarm';
import type { ManifestDataBinding } from '@/types/apps/widgetManifest';

/** API / BFF yanıtından senaryo rollup satırları (camelCase + PascalCase). */
export function normalizeScenarioRollupRow(row: unknown): AlarmScenarioRollup | null {
  if (!row || typeof row !== 'object') return null;
  const r = row as Record<string, unknown>;
  const matchKey = String(r.matchKey ?? r.MatchKey ?? '').trim();
  if (!matchKey) return null;
  const maxSev = r.maxSeverity ?? r.MaxSeverity;
  const lastSeen = r.lastSeenAt ?? r.LastSeenAt;
  return {
    matchKey,
    openCount: Number(r.openCount ?? r.OpenCount ?? 0),
    totalInRange: Number(r.totalInRange ?? r.TotalInRange ?? 0),
    maxSeverity: maxSev != null && maxSev !== '' ? Number(maxSev) : null,
    lastSeenAt: lastSeen != null && lastSeen !== '' ? String(lastSeen) : null,
  };
}

export function extractScenarioRollupFromSnapshot(raw: unknown): AlarmScenarioRollup[] {
  if (!raw || typeof raw !== 'object') return [];
  const obj = raw as Record<string, unknown>;
  const rows = obj.scenarioRollup ?? obj.ScenarioRollup;
  if (!Array.isArray(rows)) return [];
  return rows
    .map(normalizeScenarioRollupRow)
    .filter((r): r is AlarmScenarioRollup => r != null);
}

/** Manifest binding bu widget için scenarioRollup satırları mı bekliyor? */
export function bindingWantsScenarioRollup(binding: ManifestDataBinding): boolean {
  if (binding.fieldMap?.rows === 'scenarioRollup') return true;
  const ref = binding.serviceRef ?? '';
  if (ref.includes('scenario-rollup')) return true;
  return false;
}

export function scenarioRollupWidgetResponse(raw: unknown): { data: AlarmScenarioRollup[]; total: number } {
  const data = extractScenarioRollupFromSnapshot(raw);
  return { data, total: data.length };
}

/** Widget data yanıtından rollup satırları (stat şekli sızıntısına karşı). */
export function coerceWidgetDataToScenarioRollups(data: unknown): AlarmScenarioRollup[] {
  if (!Array.isArray(data)) return [];
  const normalized = data
    .map(normalizeScenarioRollupRow)
    .filter((r): r is AlarmScenarioRollup => r != null);
  if (normalized.length > 0) return normalized;
  return [];
}
