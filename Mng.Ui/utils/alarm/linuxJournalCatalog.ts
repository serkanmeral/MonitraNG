import type { EventCatalogRow, EventCatalogSelection } from '@/utils/alarm/eventCatalog';

/** Linux journal package ≈ Windows channel. */
export function linuxCatalogValue(packageName: string, matchKey: string): string {
  return `linux::${packageName.trim()}::${matchKey.trim()}`;
}

export function isLinuxCatalogValue(value: string): boolean {
  return value.startsWith('linux::');
}

/**
 * Curated Linux journal catalog (agent BuiltinJournalPackages + parse-rule actions).
 * Not exhaustive — custom package/action can be added in the selector.
 */
const LINUX_CATALOG: Array<{
  packageName: string;
  packageLabel: string;
  matchKey: string;
  label: string;
}> = [
  // sshd
  { packageName: 'sshd', packageLabel: 'SSH (sshd)', matchKey: 'login_failed', label: 'Failed password' },
  { packageName: 'sshd', packageLabel: 'SSH (sshd)', matchKey: 'login_success', label: 'Accepted password' },
  // sudo
  { packageName: 'sudo', packageLabel: 'sudo', matchKey: 'privilege_denied', label: 'Command not allowed' },
  { packageName: 'sudo', packageLabel: 'sudo', matchKey: 'privilege_escalation', label: 'sudo COMMAND' },
  // unit-fail (service failures — action often unknown until parse rules expand)
  { packageName: 'unit-fail', packageLabel: 'Unit failure', matchKey: 'service_failed', label: 'Unit entered failed state' },
];

export function buildLinuxJournalCatalogRows(): EventCatalogRow[] {
  return LINUX_CATALOG.map((item) => {
    const value = linuxCatalogValue(item.packageName, item.matchKey);
    return {
      value,
      eventId: 0,
      channel: item.packageName,
      channelLabel: item.packageLabel,
      label: item.label,
      matchKey: item.matchKey,
    };
  }).sort((a, b) => {
    const pkg = a.channelLabel.localeCompare(b.channelLabel, undefined, { sensitivity: 'base' });
    if (pkg !== 0) return pkg;
    return a.matchKey.localeCompare(b.matchKey, undefined, { sensitivity: 'base' });
  });
}

export function linuxPackageItems(): { value: string; title: string }[] {
  const map = new Map<string, string>();
  for (const item of LINUX_CATALOG) {
    if (!map.has(item.packageName)) map.set(item.packageName, item.packageLabel);
  }
  return [...map.entries()]
    .map(([value, title]) => ({ value, title }))
    .sort((a, b) => a.title.localeCompare(b.title, undefined, { sensitivity: 'base' }));
}

export function createCustomLinuxEventSelection(input: {
  packageName: string;
  matchKey: string;
  label?: string;
}): EventCatalogSelection | null {
  const packageName = String(input.packageName ?? '').trim();
  const matchKey = String(input.matchKey ?? '').trim();
  if (!packageName || !matchKey) return null;
  const known = LINUX_CATALOG.find(
    item => item.packageName === packageName && item.matchKey === matchKey,
  );
  const packageLabel = known?.packageLabel
    || linuxPackageItems().find(item => item.value === packageName)?.title
    || packageName;
  const label = String(input.label ?? '').trim() || known?.label || matchKey;
  return {
    value: linuxCatalogValue(packageName, matchKey),
    eventId: 0,
    channel: packageName,
    channelLabel: packageLabel,
    label,
    matchKey,
  };
}
