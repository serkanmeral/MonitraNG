import type {
  SecEventFilterCatalogState,
  SecEventFilterCategory,
  SecEventSavedFilter,
} from '@/types/apps/secEventFilterCatalog';

/** Locked system category ids (stable across upgrades). */
export const SEC_FILTER_CAT_SYSTEM = 'cat-system';
export const SEC_FILTER_CAT_RDP = 'cat-rdp';
export const SEC_FILTER_CAT_HOST = 'cat-host';
export const SEC_FILTER_CAT_IDENTITY = 'cat-identity';
export const SEC_FILTER_CAT_USER = 'cat-user';

/**
 * Default filter catalog seed (plan-locked).
 * RDP filters use product + event.code (reliable before action normalizer deploy).
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
      id: SEC_FILTER_CAT_HOST,
      parentId: SEC_FILTER_CAT_SYSTEM,
      name: 'Host',
      sortOrder: 1,
      isSystem: true,
    },
    {
      id: SEC_FILTER_CAT_IDENTITY,
      parentId: SEC_FILTER_CAT_SYSTEM,
      name: 'Kimlik',
      sortOrder: 2,
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
