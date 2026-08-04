import type {
  SecEventFilterCatalogState,
  SecEventFilterCategory,
  SecEventSavedFilter,
} from '@/types/apps/secEventFilterCatalog';

/** Locked system category ids (stable across upgrades). */
export const SEC_FILTER_CAT_SYSTEM = 'cat-system';
export const SEC_FILTER_CAT_RDP = 'cat-rdp';
export const SEC_FILTER_CAT_FIREWALL = 'cat-firewall';
export const SEC_FILTER_CAT_HOST = 'cat-host';
export const SEC_FILTER_CAT_IDENTITY = 'cat-identity';
export const SEC_FILTER_CAT_USER = 'cat-user';

/** High-signal destinations often probed / lateral-moved (SSH, SMB, RDP). */
const FW_CRITICAL_DST_PORTS = '22,445,3389';

/**
 * Default filter catalog seed (plan-locked).
 * RDP filters use product + event.code (reliable before action normalizer deploy).
 * Firewall filters use source.type (+ fortigate product) and event.action.
 */
export function createSecEventFilterCatalogSeed(): SecEventFilterCatalogState {
  const categories: SecEventFilterCategory[] = [
    {
      id: SEC_FILTER_CAT_SYSTEM,
      parentId: null,
      name: 'Sistem',
      sortOrder: 0,
      isSystem: true,
    },
    {
      id: SEC_FILTER_CAT_RDP,
      parentId: SEC_FILTER_CAT_SYSTEM,
      name: 'RDP',
      sortOrder: 0,
      isSystem: true,
    },
    {
      id: SEC_FILTER_CAT_FIREWALL,
      parentId: SEC_FILTER_CAT_SYSTEM,
      name: 'Firewall',
      sortOrder: 1,
      isSystem: true,
    },
    {
      id: SEC_FILTER_CAT_HOST,
      parentId: SEC_FILTER_CAT_SYSTEM,
      name: 'Host',
      sortOrder: 2,
      isSystem: true,
    },
    {
      id: SEC_FILTER_CAT_IDENTITY,
      parentId: SEC_FILTER_CAT_SYSTEM,
      name: 'Kimlik',
      sortOrder: 3,
      isSystem: true,
    },
    {
      id: SEC_FILTER_CAT_USER,
      parentId: null,
      name: 'Benim',
      sortOrder: 10,
      isSystem: false,
    },
  ];

  const filters: SecEventSavedFilter[] = [
    {
      id: 'flt-rdp-sessions',
      categoryId: SEC_FILTER_CAT_RDP,
      name: 'Oturum hareketleri',
      description: 'RDP logon / logoff / disconnect / reconnect (event 21–25)',
      isSystem: true,
      scope: { product: 'rdp-session' },
      fields: [{ field: 'event.code', op: 'in', value: '21,23,24,25' }],
    },
    {
      id: 'flt-rdp-disconnect-reconnect',
      categoryId: SEC_FILTER_CAT_RDP,
      name: 'Disconnect / Reconnect',
      description: 'RDP disconnect (24) ve reconnect (25)',
      isSystem: true,
      scope: { product: 'rdp-session' },
      fields: [{ field: 'event.code', op: 'in', value: '24,25' }],
    },
    {
      id: 'flt-rdp-logon',
      categoryId: SEC_FILTER_CAT_RDP,
      name: 'Logon',
      description: 'RDP session logon (21)',
      isSystem: true,
      scope: { product: 'rdp-session' },
      fields: [{ field: 'event.code', op: 'eq', value: '21' }],
    },
    {
      id: 'flt-fw-all',
      categoryId: SEC_FILTER_CAT_FIREWALL,
      name: 'Tüm firewall',
      description: 'source.type = firewall (tüm markalar)',
      isSystem: true,
      scope: { type: 'firewall' },
      fields: [],
    },
    {
      id: 'flt-fw-denied',
      categoryId: SEC_FILTER_CAT_FIREWALL,
      name: 'Engellenen trafik',
      description: 'denied_flow — engellenen bağlantılar',
      isSystem: true,
      scope: { type: 'firewall' },
      fields: [{ field: 'event.action', op: 'eq', value: 'denied_flow' }],
    },
    {
      id: 'flt-fw-allowed',
      categoryId: SEC_FILTER_CAT_FIREWALL,
      name: 'İzin verilen trafik',
      description: 'allowed_flow — hacim yüksek olabilir',
      isSystem: true,
      scope: { type: 'firewall' },
      fields: [{ field: 'event.action', op: 'eq', value: 'allowed_flow' }],
    },
    {
      id: 'flt-fw-rule-change',
      categoryId: SEC_FILTER_CAT_FIREWALL,
      name: 'Kural değişikliği',
      description: 'rule_change — policy / config değişiklikleri',
      isSystem: true,
      scope: { type: 'firewall' },
      fields: [{ field: 'event.action', op: 'eq', value: 'rule_change' }],
    },
    {
      id: 'flt-fw-denied-critical-ports',
      categoryId: SEC_FILTER_CAT_FIREWALL,
      name: 'Engellenen · kritik portlar',
      description: `denied_flow + dstPort in (${FW_CRITICAL_DST_PORTS}) — SSH / SMB / RDP`,
      isSystem: true,
      scope: { type: 'firewall' },
      fields: [
        { field: 'event.action', op: 'eq', value: 'denied_flow' },
        { field: 'network.dstPort', op: 'in', value: FW_CRITICAL_DST_PORTS },
      ],
    },
    {
      id: 'flt-fw-fgt-all',
      categoryId: SEC_FILTER_CAT_FIREWALL,
      name: 'FortiGate (tümü)',
      description: 'source.type = firewall · product = fortigate',
      isSystem: true,
      scope: { type: 'firewall', product: 'fortigate' },
      fields: [],
    },
    {
      id: 'flt-fw-fgt-denied',
      categoryId: SEC_FILTER_CAT_FIREWALL,
      name: 'FortiGate · Engellenen',
      description: 'FortiGate denied_flow',
      isSystem: true,
      scope: { type: 'firewall', product: 'fortigate' },
      fields: [{ field: 'event.action', op: 'eq', value: 'denied_flow' }],
    },
    {
      id: 'flt-fw-fgt-allowed',
      categoryId: SEC_FILTER_CAT_FIREWALL,
      name: 'FortiGate · İzin verilen',
      description: 'FortiGate allowed_flow — hacim yüksek olabilir',
      isSystem: true,
      scope: { type: 'firewall', product: 'fortigate' },
      fields: [{ field: 'event.action', op: 'eq', value: 'allowed_flow' }],
    },
    {
      id: 'flt-fw-fgt-rule-change',
      categoryId: SEC_FILTER_CAT_FIREWALL,
      name: 'FortiGate · Kural değişikliği',
      description: 'FortiGate rule_change',
      isSystem: true,
      scope: { type: 'firewall', product: 'fortigate' },
      fields: [{ field: 'event.action', op: 'eq', value: 'rule_change' }],
    },
    {
      id: 'flt-fw-fgt-denied-critical-ports',
      categoryId: SEC_FILTER_CAT_FIREWALL,
      name: 'FortiGate · Engellenen · kritik portlar',
      description: `FortiGate denied_flow + dstPort in (${FW_CRITICAL_DST_PORTS})`,
      isSystem: true,
      scope: { type: 'firewall', product: 'fortigate' },
      fields: [
        { field: 'event.action', op: 'eq', value: 'denied_flow' },
        { field: 'network.dstPort', op: 'in', value: FW_CRITICAL_DST_PORTS },
      ],
    },
    {
      id: 'flt-host-windows-eventlog',
      categoryId: SEC_FILTER_CAT_HOST,
      name: 'Windows Event Log',
      description: 'source.type = windows-eventlog',
      isSystem: true,
      scope: { type: 'windows-eventlog' },
      fields: [],
    },
    {
      id: 'flt-host-agent-metrics',
      categoryId: SEC_FILTER_CAT_HOST,
      name: 'Agent metrikleri',
      description: 'MngLogs agent host/process metrics',
      isSystem: true,
      scope: { type: 'metric', product: 'mnglogs-agent' },
      fields: [],
    },
    {
      id: 'flt-identity-failed',
      categoryId: SEC_FILTER_CAT_IDENTITY,
      name: 'Başarısız oturum',
      description: 'outcome=failure (kimlik / oturum başarısızları)',
      isSystem: true,
      scope: {},
      fields: [{ field: 'event.outcome', op: 'eq', value: 'failure' }],
    },
  ];

  return { categories, filters };
}
