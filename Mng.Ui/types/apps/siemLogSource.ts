/** SIEM Discovery — network / syslog log sources (FortiGate P0). */

export type SiemLogSourceKind = 'firewall';

export type SiemLogSourceVendor = 'fortigate' | 'unknown';

/** Coverage for appliances that are monitored via syslog, not agents. */
export type SiemLogSourceCoverage = 'logOnline' | 'logSilent' | 'configuredMissing';

export interface SiemLogSourceSeed {
  id: string;
  kind: SiemLogSourceKind;
  vendor: SiemLogSourceVendor;
  /** source.product query value */
  product: string;
  displayName: string;
  /** Optional fixed sensor host (source.host); empty = discover from events. */
  sensorHost?: string | null;
  siteLabel?: string | null;
}

export interface SiemLogSource {
  id: string;
  kind: SiemLogSourceKind;
  vendor: SiemLogSourceVendor;
  product: string;
  displayName: string;
  /** Canonical source.host used in sec_events */
  sensorHost: string;
  sensorIp?: string | null;
  siteLabel?: string | null;
  coverage: SiemLogSourceCoverage;
  lastEventAt?: string | null;
  lastAction?: string | null;
  eventCount24h: number;
  fromSeed: boolean;
}

export interface SiemLogSourceKpi {
  id: string;
  labelKey: string;
  value: number;
  color: string;
  /** When set, clicking filters the list to this coverage. */
  coverage?: SiemLogSourceCoverage | 'all';
}

export interface SiemLogSourceActionBucket {
  action: string;
  count: number;
}

export interface SiemLogSourceRecentRow {
  id: string;
  timestamp: string;
  action: string;
  outcome?: string | null;
  srcIp?: string | null;
  dstIp?: string | null;
}

/** Thin triage summary for a log source (not a host analytics dashboard). */
export interface SiemLogSourceDetailSummary {
  eventCount1h: number;
  eventCount24h: number;
  topActions: SiemLogSourceActionBucket[];
  recent: SiemLogSourceRecentRow[];
}
