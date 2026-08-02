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
  /** Agent-reported OS family: windows | linux */
  platform?: string | null;
  /** Environment.MachineName from host.up (may differ from scan IP hostname). */
  machine?: string | null;
  localUiPort?: number | null;
  localUiHost?: string | null;
  /** true when agent binds beyond loopback (0.0.0.0 / LAN IP) */
  localUiRemoteAccess?: boolean | null;
  sessions?: SiemDiscoveryHostSession[];
}

export type SiemOsFamily = 'windows' | 'linux' | 'unknown';

export interface SiemDiscoveryHost {
  id: string;
  hostname: string;
  ip: string;
  /** Raw hint: windows|linux|unknown or AD OS string */
  osHint?: string;
  /** Normalized family for icons / filters */
  osFamily?: SiemOsFamily;
  /** TCP ports seen during network scan */
  openPorts?: number[];
  deviceRoleHint?: string;
  identityConfidence?: 'high' | 'medium' | 'low' | string;
  identitySummary?: string;
  httpTitle?: string;
  tlsCommonName?: string;
  sshBanner?: string;
  /** Matched prefix CIDR (LPM) */
  subnetCidr?: string;
  /** Site/subnet label from prefix table */
  siteLabel?: string;
  /** Optional operator-mapped VLAN name on the prefix row */
  vlanName?: string;
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
  /** When set, KPI toggles this coverage filter. */
  status?: SiemCoverageStatus;
  /** Visual emphasis (e.g. coverage gap / no agent). */
  emphasize?: boolean;
}

export interface SiemDiscoveryLegendItem {
  status: SiemCoverageStatus;
  labelKey: string;
  color: string;
  count: number;
}
