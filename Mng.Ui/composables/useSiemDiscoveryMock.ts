import type {
  SiemCoverageStatus,
  SiemDiscoveryBranch,
  SiemDiscoveryFacet,
  SiemDiscoveryHost,
  SiemDiscoveryKpi,
  SiemDiscoveryLegendItem,
} from '@/types/apps/siemDiscovery';

const hosts: SiemDiscoveryHost[] = [
  {
    id: 'h1',
    hostname: 'DC01',
    ip: '10.20.1.10',
    osHint: 'Windows Server',
    coverage: 'managedOnline',
  },
  {
    id: 'h2',
    hostname: 'FS01',
    ip: '10.20.1.20',
    osHint: 'Windows Server',
    coverage: 'managedOnline',
  },
  {
    id: 'h3',
    hostname: 'TERMINAL-pilot',
    ip: '10.20.2.55',
    osHint: 'Windows 11',
    coverage: 'managedOnline',
  },
  {
    id: 'h4',
    hostname: 'ACC-PC-12',
    ip: '10.20.2.88',
    osHint: 'Windows 10',
    coverage: 'managedOffline',
  },
  {
    id: 'h5',
    hostname: 'LAB-LINUX-01',
    ip: '10.20.3.14',
    osHint: 'Ubuntu',
    coverage: 'discoveredUnmanaged',
  },
  {
    id: 'h6',
    hostname: 'PRINT-02',
    ip: '10.20.3.40',
    osHint: 'Unknown',
    coverage: 'unknown',
  },
  {
    id: 'h7',
    hostname: 'WH-PC-03',
    ip: '10.20.4.22',
    osHint: 'Windows 11',
    coverage: 'discoveredUnmanaged',
  },
  {
    id: 'h8',
    hostname: 'AP-GUEST-CLIENT',
    ip: '10.20.5.9',
    osHint: 'Unknown',
    coverage: 'unknown',
  },
];

const byId = Object.fromEntries(hosts.map((h) => [h.id, h]));

function pick(...ids: string[]): SiemDiscoveryHost[] {
  return ids.map((id) => byId[id]).filter(Boolean);
}

const facetTrees: Record<SiemDiscoveryFacet, SiemDiscoveryBranch[]> = {
  vlan: [
    {
      id: 'vlan-10',
      label: 'VLAN 10 · Servers',
      detail: '10.20.1.0/24',
      hosts: pick('h1', 'h2'),
    },
    {
      id: 'vlan-20',
      label: 'VLAN 20 · Office',
      detail: '10.20.2.0/24',
      hosts: pick('h3', 'h4'),
    },
    {
      id: 'vlan-30',
      label: 'VLAN 30 · Lab',
      detail: '10.20.3.0/24',
      hosts: pick('h5', 'h6'),
    },
    {
      id: 'vlan-40',
      label: 'VLAN 40 · Warehouse',
      detail: '10.20.4.0/24',
      hosts: pick('h7'),
    },
    {
      id: 'vlan-50',
      label: 'VLAN 50 · Guest Wi‑Fi',
      detail: '10.20.5.0/24',
      hosts: pick('h8'),
    },
  ],
  dhcp: [
    {
      id: 'dhcp-corp',
      label: 'DHCP · CORP-SCOPE',
      detail: '10.20.1.0–10.20.2.254',
      hosts: pick('h1', 'h2', 'h3', 'h4'),
    },
    {
      id: 'dhcp-lab',
      label: 'DHCP · LAB-SCOPE',
      detail: '10.20.3.0/24',
      hosts: pick('h5', 'h6'),
    },
    {
      id: 'dhcp-wh',
      label: 'DHCP · WH-SCOPE',
      detail: '10.20.4.0/24',
      hosts: pick('h7'),
    },
    {
      id: 'dhcp-guest',
      label: 'DHCP · GUEST',
      detail: '10.20.5.0/24',
      hosts: pick('h8'),
    },
  ],
  ap: [
    {
      id: 'ap-lobby',
      label: 'AP · Lobby-01',
      detail: 'SSID: ODAK-CORP',
      hosts: pick('h3', 'h4'),
    },
    {
      id: 'ap-lab',
      label: 'AP · Lab-Hall',
      detail: 'SSID: ODAK-LAB',
      hosts: pick('h5'),
    },
    {
      id: 'ap-wh',
      label: 'AP · Warehouse',
      detail: 'SSID: ODAK-WH',
      hosts: pick('h7'),
    },
    {
      id: 'ap-guest',
      label: 'AP · Guest',
      detail: 'SSID: ODAK-GUEST',
      hosts: pick('h8'),
    },
    {
      id: 'ap-wired',
      label: 'Wired / no AP',
      detail: 'Switch uplinks',
      hosts: pick('h1', 'h2', 'h6'),
    },
  ],
  subnet: [
    {
      id: 'sn-1',
      label: '10.20.1.0/24',
      detail: 'Servers',
      hosts: pick('h1', 'h2'),
    },
    {
      id: 'sn-2',
      label: '10.20.2.0/24',
      detail: 'Office',
      hosts: pick('h3', 'h4'),
    },
    {
      id: 'sn-3',
      label: '10.20.3.0/24',
      detail: 'Lab',
      hosts: pick('h5', 'h6'),
    },
    {
      id: 'sn-4',
      label: '10.20.4.0/24',
      detail: 'Warehouse',
      hosts: pick('h7'),
    },
    {
      id: 'sn-5',
      label: '10.20.5.0/24',
      detail: 'Guest',
      hosts: pick('h8'),
    },
  ],
};

const COVERAGE_COLORS: Record<SiemCoverageStatus, string> = {
  managedOnline: 'success',
  managedOffline: 'warning',
  discoveredUnmanaged: 'error',
  unknown: 'grey',
};

export function coverageColor(status: SiemCoverageStatus): string {
  return COVERAGE_COLORS[status] ?? 'grey';
}

export function buildLegend(allHosts: SiemDiscoveryHost[]): SiemDiscoveryLegendItem[] {
  const order: SiemCoverageStatus[] = [
    'managedOnline',
    'managedOffline',
    'discoveredUnmanaged',
    'unknown',
  ];
  return order.map((status) => ({
    status,
    labelKey: `siemCenter.discovery.coverage.${status}`,
    color: coverageColor(status),
    count: allHosts.filter((h) => h.coverage === status).length,
  }));
}

/** Coverage gap KPIs — same counts as legend; clickable filters in the map rail. */
export function buildCoverageKpis(allHosts: SiemDiscoveryHost[]): SiemDiscoveryKpi[] {
  const count = (status: SiemCoverageStatus) =>
    allHosts.filter((h) => h.coverage === status).length;

  return [
    {
      key: 'managedOnline',
      status: 'managedOnline',
      labelKey: 'siemCenter.discovery.coverage.managedOnline',
      value: count('managedOnline'),
      color: 'success',
      icon: 'mdi-shield-check',
    },
    {
      key: 'managedOffline',
      status: 'managedOffline',
      labelKey: 'siemCenter.discovery.coverage.managedOffline',
      value: count('managedOffline'),
      color: 'warning',
      icon: 'mdi-shield-alert',
    },
    {
      key: 'discoveredUnmanaged',
      status: 'discoveredUnmanaged',
      labelKey: 'siemCenter.discovery.coverage.discoveredUnmanaged',
      value: count('discoveredUnmanaged'),
      color: 'error',
      icon: 'mdi-shield-off-outline',
      emphasize: true,
    },
    {
      key: 'unknown',
      status: 'unknown',
      labelKey: 'siemCenter.discovery.coverage.unknown',
      value: count('unknown'),
      color: 'grey',
      icon: 'mdi-help-circle-outline',
    },
  ];
}

/** @deprecated Use buildCoverageKpis — kept for any stray imports. */
export function buildMockKpis(): SiemDiscoveryKpi[] {
  return buildCoverageKpis([]);
}

export function getMockBranches(facet: SiemDiscoveryFacet): SiemDiscoveryBranch[] {
  return facetTrees[facet] ?? [];
}

export function getMockHosts(): SiemDiscoveryHost[] {
  return hosts;
}

/** Primary grouping only — VLAN/DHCP/AP hidden until real data sources exist. */
export const DISCOVERY_FACETS: SiemDiscoveryFacet[] = ['subnet'];
