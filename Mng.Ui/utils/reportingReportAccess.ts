import type { OdakFieldPolicy, OdakFieldVisibilityPolicy } from '@/utils/odakSiparisFieldPolicies';
import { resolveOdakFieldAccess } from '@/utils/odakSiparisFieldPolicies';

const REPORT_ACCESS_FIELD_KEY = '__report';

/** Rapor görünürlüğü — politika yoksa herkese açık. */
export function canViewReportingReport(
  policies: OdakFieldVisibilityPolicy[],
  userGroups: string[]
): boolean {
  if (!policies.length) return true;
  const blob = {
    policiesByField: {
      [REPORT_ACCESS_FIELD_KEY]: policies as OdakFieldPolicy[],
    },
  };
  return resolveOdakFieldAccess(REPORT_ACCESS_FIELD_KEY, userGroups, {}, blob).visible;
}

export function filterVisibleReportingReports<T extends { visibilityPolicies: OdakFieldVisibilityPolicy[] }>(
  reports: T[],
  userGroups: string[]
): T[] {
  return reports.filter((r) => canViewReportingReport(r.visibilityPolicies, userGroups));
}
