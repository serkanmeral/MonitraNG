/** Keeper K5 — fieldPolicies / capabilities (API-driven, no hardcoded field lists). */

export type ProvisioningSource = 'Local' | 'Directory' | string;

export interface UserFieldPolicyItem {
  editable: boolean;
  source: string;
}

export interface UserCapabilities {
  canChangePassword: boolean;
  canManageGroups: boolean;
  canDeactivate: boolean;
  canDelete: boolean;
}

export interface UserProvisioningFields {
  provisioningSource?: ProvisioningSource;
  directorySyncedAt?: string | Date | null;
  fieldPolicies?: Record<string, UserFieldPolicyItem>;
  capabilities?: UserCapabilities;
}

export function normalizeFieldPolicies(
  raw: Record<string, unknown> | undefined | null
): Record<string, UserFieldPolicyItem> | undefined {
  if (!raw || typeof raw !== 'object') return undefined;
  const out: Record<string, UserFieldPolicyItem> = {};
  for (const [key, val] of Object.entries(raw)) {
    const item = val as Record<string, unknown>;
    out[key] = {
      editable: Boolean(item.editable ?? item.Editable ?? false),
      source: String(item.source ?? item.Source ?? 'app'),
    };
  }
  return Object.keys(out).length ? out : undefined;
}

export function normalizeCapabilities(
  raw: Record<string, unknown> | undefined | null
): UserCapabilities | undefined {
  if (!raw || typeof raw !== 'object') return undefined;
  return {
    canChangePassword: Boolean(raw.canChangePassword ?? raw.CanChangePassword ?? true),
    canManageGroups: Boolean(raw.canManageGroups ?? raw.CanManageGroups ?? true),
    canDeactivate: Boolean(raw.canDeactivate ?? raw.CanDeactivate ?? true),
    canDelete: Boolean(raw.canDelete ?? raw.CanDelete ?? true),
  };
}

export function mapProvisioningFieldsFromApi(user: Record<string, unknown>): UserProvisioningFields {
  const src =
    (user.provisioningSource as string) ||
    (user.ProvisioningSource as string) ||
    'Local';
  const synced = (user.directorySyncedAt ?? user.DirectorySyncedAt) as string | Date | null | undefined;
  const policies = normalizeFieldPolicies(
    (user.fieldPolicies ?? user.FieldPolicies) as Record<string, unknown> | undefined
  );
  const caps = normalizeCapabilities(
    (user.capabilities ?? user.Capabilities) as Record<string, unknown> | undefined
  );
  return {
    provisioningSource: src,
    directorySyncedAt: synced ?? null,
    fieldPolicies: policies,
    capabilities: caps,
  };
}

import { isDirectoryProvisioningSource } from './provisioningSourceUi';

export function isDirectoryUser(
  user: { provisioningSource?: string } | null | undefined
): boolean {
  return isDirectoryProvisioningSource(user?.provisioningSource);
}

export function isFieldEditable(
  user: UserProvisioningFields | null | undefined,
  field: string,
  defaultWhenMissing = true
): boolean {
  const policies = user?.fieldPolicies;
  if (!policies) return defaultWhenMissing;
  const p =
    policies[field] ??
    policies[field.charAt(0).toLowerCase() + field.slice(1)];
  if (!p) return defaultWhenMissing;
  return p.editable;
}

export function userProvisioningSourceLabelKey(source?: string): string {
  return isDirectoryUser({ provisioningSource: source })
    ? 'users.source.directory'
    : 'users.source.local';
}

export function canDeleteUser(user: UserProvisioningFields | null | undefined): boolean {
  if (user?.capabilities && user.capabilities.canDelete === false) return false;
  if (isDirectoryUser(user)) return false;
  return true;
}
