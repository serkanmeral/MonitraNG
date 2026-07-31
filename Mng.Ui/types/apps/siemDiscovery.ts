/** SIEM discovery / coverage map — view model (mock-first). */

export type SiemCoverageStatus =
  | 'managedOnline'
  | 'managedOffline'
  | 'discoveredUnmanaged'
  | 'unknown';

export type SiemDiscoveryFacet = 'vlan' | 'dhcp' | 'ap' | 'subnet';

export interface SiemDiscoveryHostSession {
  user: string;
  sessionId?: number;
  state?: string;
  stationName?: string | null;
  clientProtocol?: string | null;
  logonAtUtc?: string | null;
  durationSeconds?: number | null;
}

/** Latest host.up enrichment from agent (when available). */
export interface SiemDiscoveryAgentInfo {
  primaryIp?: string | null;
  ipAddresses?: string[];
  consoleUser?: string | null;
  loggedOnUsers?: string[];
  bootTimeUtc?: string | null;
  uptimeSeconds?: number | null;
  agentVersion?: string | null;
  localUiPort?: number | null;
  localUiHost?: string | null;
  /** true when agent binds beyond loopback (0.0.0.0 / LAN IP) */
  localUiRemoteAccess?: boolean | null;
  sessions?: SiemDiscoveryHostSession[];
}

export interface SiemDiscoveryHost {
  id: string;
  hostname: string;
  ip: string;
  osHint?: string;
  coverage: SiemCoverageStatus;
  /** Optional detail fields for host modal */
  samAccountName?: string;
  sources?: string[];
  lastSeenFromAd?: string | null;
  /** Last host.up timestamp (ms), when coverage came from metrics */
  lastSeenAt?: number | null;
  /** Agent host.up snapshot (IP / user / uptime) */
  agent?: SiemDiscoveryAgentInfo | null;
}

export interface SiemDiscoveryBranch {
  id: string;
  label: string;
  detail?: string;
  hosts: SiemDiscoveryHost[];
}

export interface SiemDiscoveryKpi {
  key: string;
  labelKey: string;
  value: number;
  color: string;
  icon: string;
}

export interface SiemDiscoveryLegendItem {
  status: SiemCoverageStatus;
  labelKey: string;
  color: string;
  count: number;
}
