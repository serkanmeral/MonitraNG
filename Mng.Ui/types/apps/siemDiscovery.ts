/** SIEM discovery / coverage map — view model (mock-first). */

export type SiemCoverageStatus =
  | 'managedOnline'
  | 'managedOffline'
  | 'discoveredUnmanaged'
  | 'unknown';

export type SiemDiscoveryFacet = 'vlan' | 'dhcp' | 'ap' | 'subnet';

export interface SiemDiscoveryHost {
  id: string;
  hostname: string;
  ip: string;
  osHint?: string;
  coverage: SiemCoverageStatus;
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
