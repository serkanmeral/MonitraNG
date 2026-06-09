import type { AlarmScenarioRollup } from '@/types/apps/alarm';
import { SIEM_SCENARIO_CATALOG, type SiemScenarioDef } from '@/composables/useSiemScenarioCatalog';

export interface ScenarioCard {
  def: SiemScenarioDef;
  lastSeenAt: string | null;
  severity: number | null;
  open: boolean;
  totalAlarms: number;
  openCount: number;
}

export type ScenarioStripState = 'open' | 'seen' | 'clean';

export function buildScenarioCardsFromRollup(rollups: AlarmScenarioRollup[]): ScenarioCard[] {
  const byKey = new Map(rollups.map((r) => [r.matchKey, r]));
  return SIEM_SCENARIO_CATALOG.map((def) => {
    const rollup = byKey.get(def.matchKey);
    return {
      def,
      lastSeenAt: rollup?.lastSeenAt ?? null,
      severity: rollup?.maxSeverity ?? null,
      open: (rollup?.openCount ?? 0) > 0,
      totalAlarms: rollup?.totalInRange ?? 0,
      openCount: rollup?.openCount ?? 0,
    };
  });
}

export function scenarioStripState(card: ScenarioCard): ScenarioStripState {
  if (card.open) return 'open';
  if (card.totalAlarms > 0 || card.lastSeenAt) return 'seen';
  return 'clean';
}
