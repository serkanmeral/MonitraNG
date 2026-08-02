import type { SiemDiscoveryHost } from '@/types/apps/siemDiscovery';

export type SiemOsFamily = 'windows' | 'linux' | 'unknown';

/** Normalize scan hint / AD OS string / agent placeholder → windows|linux|unknown. */
export function resolveOsFamily(
  host: Pick<SiemDiscoveryHost, 'osHint' | 'osFamily' | 'openPorts' | 'sources'> | null | undefined,
): SiemOsFamily {
  if (!host) return 'unknown';
  if (host.osFamily === 'windows' || host.osFamily === 'linux' || host.osFamily === 'unknown') {
    return host.osFamily;
  }

  const hint = (host.osHint || '').trim().toLowerCase();
  if (hint === 'windows' || hint === 'linux' || hint === 'unknown') return hint as SiemOsFamily;

  if (
    hint.includes('windows')
    || hint.includes('win32')
    || hint.includes('win64')
    || hint.includes('server 20')
  ) {
    return 'windows';
  }
  if (
    hint.includes('linux')
    || hint.includes('ubuntu')
    || hint.includes('debian')
    || hint.includes('centos')
    || hint.includes('redhat')
    || hint.includes('rhel')
    || hint.includes('fedora')
    || hint.includes('suse')
    || hint.includes('alpine')
  ) {
    return 'linux';
  }

  // Port fallback when hint missing/unknown (same rules as Collector TcpPortProbe).
  const ports = host.openPorts ?? [];
  if (ports.length) {
    const has22 = ports.includes(22);
    const hasWin = ports.some((p) => p === 445 || p === 3389 || p === 5985);
    if (hasWin && !has22) return 'windows';
    if (has22 && !hasWin) return 'linux';
    if (hasWin && has22) {
      if (ports.includes(445) || ports.includes(3389) || ports.includes(5985)) return 'windows';
      return 'linux';
    }
  }

  return 'unknown';
}

export function osFamilyIcon(family: SiemOsFamily): string {
  if (family === 'windows') return 'mdi-microsoft-windows';
  if (family === 'linux') return 'mdi-linux';
  return 'mdi-help-rhombus-outline';
}
