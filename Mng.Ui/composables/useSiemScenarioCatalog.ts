export interface SiemScenarioDef {
  id: string;
  matchKey: string;
  eventAction?: string;
}

/** U1–U9 SIEM senaryoları (MVP + post-MVP AD genişletme). */
export const SIEM_SCENARIO_CATALOG: SiemScenarioDef[] = [
  { id: 'U1', matchKey: 'login_failed', eventAction: 'login_failed' },
  { id: 'U2', matchKey: 'login_success_after_failures', eventAction: 'login_success' },
  { id: 'U3', matchKey: 'privileged_login_outside_window' },
  { id: 'U4', matchKey: 'denied_flow', eventAction: 'denied_flow' },
  { id: 'U5', matchKey: 'allowed_flow', eventAction: 'allowed_flow' },
  { id: 'U6', matchKey: 'rule_change', eventAction: 'rule_change' },
  { id: 'U7', matchKey: 'new_flow', eventAction: 'new_flow' },
  { id: 'U8', matchKey: 'group_member_added', eventAction: 'group_member_added' },
  { id: 'U9', matchKey: 'account_created', eventAction: 'account_created' },
];

export function scenarioEventsLink(def: SiemScenarioDef): string {
  const q = new URLSearchParams();
  if (def.eventAction) q.set('eventAction', def.eventAction);
  const s = q.toString();
  return s ? `/apps/siem-center/events?${s}` : '/apps/siem-center/events';
}
