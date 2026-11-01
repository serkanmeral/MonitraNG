import { fetchFromDataGateway } from '@/services/apiService';
import type { DashboardLayout } from '@/stores/apps/dashboard';

export const PERSONNEL_DASHBOARD_SLUG = 'personel';
const BOARD_VERSION = 4;
const CATEGORY_NAME = 'Personel';

interface WidgetSeed {
  name: string;
  title: string;
  description: string;
  type: 'card' | 'table';
  order: number;
  serviceRef: string;
  icon?: string;
  color?: string;
  columns?: Array<{ key: string; title: string }>;
  rowClick?: { path: string; paramMap?: Record<string, string> };
}

const WIDGETS: WidgetSeed[] = [
  {
    name: 'personel-points',
    title: 'İş puanı',
    description: 'Aralıkta kapanan işlerin puan toplamı',
    type: 'card',
    order: 10,
    serviceRef: 'mngui:personnel/story-points',
    icon: 'mdi-star-circle-outline',
    color: 'primary',
  },
  {
    name: 'personel-work-open',
    title: 'Açık işler',
    description: 'Üzerindeki açık yük. Aralık bu listeyi daraltmaz.',
    type: 'table',
    order: 20,
    serviceRef: 'mngui:personnel/work-open',
    columns: [
      { key: 'key', title: 'Kod' },
      { key: 'title', title: 'İş' },
    ],
    rowClick: { path: '/apps/operation-core/work-items/{id}/profile' },
  },
  {
    name: 'personel-work-closed',
    title: 'Kapanan işler',
    description: 'Seçilen aralıkta kapanan işler',
    type: 'table',
    order: 30,
    serviceRef: 'mngui:personnel/work-closed',
    columns: [
      { key: 'key', title: 'Kod' },
      { key: 'title', title: 'İş' },
      { key: 'points', title: 'Puan' },
      { key: 'closedAt', title: 'Kapandı' },
    ],
    rowClick: { path: '/apps/operation-core/work-items/{id}/profile' },
  },
  {
    name: 'personel-meetings',
    title: 'Toplantılar',
    description: 'Aralıkta katıldığı toplantılar',
    type: 'table',
    order: 40,
    serviceRef: 'mngui:personnel/meetings',
    columns: [
      { key: 'when', title: 'Zaman' },
      { key: 'title', title: 'Toplantı' },
      { key: 'status', title: 'Durum' },
      { key: 'presence', title: 'Katılım' },
    ],
    rowClick: { path: '/apps/meetings', paramMap: { open: '$row.id' } },
  },
  {
    name: 'personel-documents',
    title: 'Belgeler',
    description: 'Aralıkta kütüphaneye eklediği belgeler',
    type: 'table',
    order: 50,
    serviceRef: 'mngui:personnel/documents',
    columns: [
      { key: 'when', title: 'Zaman' },
      { key: 'title', title: 'Belge' },
      { key: 'kind', title: 'Tür' },
    ],
    rowClick: { path: '/apps/document-intelligence/r/{id}' },
  },
];

export async function ensurePersonnelDashboard(): Promise<string> {
  const categoryId = await ensureCategory();
  const widgetIds = new Map<string, string>();
  for (const seed of WIDGETS) {
    widgetIds.set(seed.name, await ensureWidget(seed, categoryId));
  }

  const existing = await findDashboard();
  const layout = buildLayout(widgetIds);
  if (!existing) {
    const created = await fetchFromDataGateway('/api/v1/data/@dashboards', 'POST', {
      name: 'personel-hareket',
      title: 'Personel hareketi',
      description: 'Bir personelin seçilen aralıktaki toplantı, iş ve belge izi',
      slug: PERSONNEL_DASHBOARD_SLUG,
      layout,
      isDefault: false,
      isActive: true,
      order: 20,
      permissions: { edit: { groups: ['admins'] } },
    });
    return recordId(created);
  }

  const currentVersion = Number((existing.layout as { meta?: { personnelBoardVersion?: number } } | undefined)?.meta?.personnelBoardVersion ?? 0);
  if (currentVersion !== BOARD_VERSION) {
    for (const seed of WIDGETS) {
      const id = widgetIds.get(seed.name);
      if (!id) continue;
      await fetchFromDataGateway(`/api/v1/data/@widgets/${encodeURIComponent(id)}`, 'PUT', widgetBody(seed, categoryId));
    }
    await fetchFromDataGateway(`/api/v1/data/@dashboards/${encodeURIComponent(recordId(existing))}`, 'PUT', {
      name: 'personel-hareket',
      title: 'Personel hareketi',
      slug: PERSONNEL_DASHBOARD_SLUG,
      layout,
      isActive: true,
    });
  }
  return recordId(existing);
}

function buildLayout(widgetIds: Map<string, string>): DashboardLayout {
  const col = (name: string, span: number) => ({ widgetId: widgetIds.get(name) ?? '', span, spanMd: span });
  return {
    type: 'rows',
    meta: {
      personnelBoardVersion: BOARD_VERSION,
      surfaceVariables: [
        { key: 'personId', kind: 'person', lockToSelf: true },
        { key: 'range', kind: 'dateRange' },
      ],
    },
    rows: [
      { cols: [col('personel-points', 4)] },
      { cols: [col('personel-work-open', 6), col('personel-work-closed', 6)] },
      { cols: [col('personel-meetings', 6), col('personel-documents', 6)] },
    ],
  };
}

function widgetBody(seed: WidgetSeed, categoryId: string) {
  const parameters = {
    personId: { $ref: '$variables.personId' },
    from: { $ref: '$timeRange.from' },
    to: { $ref: '$timeRange.to' },
  };
  const manifest = {
    manifestVersion: '1.0',
    templateId: seed.name,
    templateVersion: '1.0.0',
    domain: 'generic',
    category: CATEGORY_NAME,
    title: seed.title,
    description: seed.description,
    presentation: {
      kind: seed.type === 'card' ? 'stat' : 'table',
      defaultPreset: seed.type === 'card' ? 'stat-simple' : 'table-compact',
      config: seed.type === 'card'
        ? { format: 'number', icon: seed.icon, color: seed.color }
        : { dense: true, pageSize: 8, columns: seed.columns ?? [] },
    },
    dataBinding: {
      serviceRef: seed.serviceRef,
      defaultParameters: parameters,
      fieldMap: seed.type === 'card' ? { value: 'value' } : { rows: 'items' },
    },
    interactions: seed.rowClick ? { rowClick: { type: 'route', path: seed.rowClick.path, paramMap: seed.rowClick.paramMap } } : undefined,
  };
  return {
    name: seed.name,
    title: seed.title,
    description: seed.description,
    category: categoryId,
    type: seed.type,
    isActive: true,
    order: seed.order,
    dataSource: { type: 'data', dataset: '__manifest_service__', getMethod: 'default' },
    config: {
      ...(seed.type === 'card'
        ? { format: 'number', icon: seed.icon, color: seed.color, valueField: 'value' }
        : { dense: true, pageSize: 8, columns: seed.columns ?? [], density: 'compact' }),
      templateId: seed.name,
      manifest,
      interactions: manifest.interactions,
    },
  };
}

async function ensureCategory(): Promise<string> {
  const query = `filter=${encodeURIComponent(`name:eq:${CATEGORY_NAME}`)}&limit=1`;
  const found = (await listDataset('@widget_categories', query))[0];
  if (found) return recordId(found);
  try {
    const created = await fetchFromDataGateway('/api/v1/data/@widget_categories', 'POST', {
      name: CATEGORY_NAME,
      description: 'Personel hareketi panosu',
      icon: 'mdi-account-clock',
      color: 'primary',
      order: 80,
      isActive: true,
    });
    return recordId(created);
  } catch {
    const again = (await listDataset('@widget_categories', query))[0];
    if (again) return recordId(again);
    throw new Error('Personel kategorisi açılamadı');
  }
}

async function ensureWidget(seed: WidgetSeed, categoryId: string): Promise<string> {
  const rows = await listDataset('@widgets', `filter=${encodeURIComponent(`name:eq:${seed.name}`)}&limit=1`);
  const found = rows[0];
  if (found) return recordId(found);
  const created = await fetchFromDataGateway('/api/v1/data/@widgets', 'POST', widgetBody(seed, categoryId));
  return recordId(created);
}

async function findDashboard(): Promise<Record<string, unknown> | null> {
  const rows = await listDataset('@dashboards', `filter=${encodeURIComponent(`slug:eq:${PERSONNEL_DASHBOARD_SLUG}`)}&limit=1`);
  return rows[0] ?? null;
}

async function listDataset(dataset: string, query: string): Promise<Record<string, unknown>[]> {
  const raw = await fetchFromDataGateway(`/api/v1/data/${encodeURIComponent(dataset)}?${query}`, 'GET');
  if (Array.isArray(raw)) return raw as Record<string, unknown>[];
  if (raw && typeof raw === 'object') {
    const items = (raw as { items?: unknown; data?: unknown }).items ?? (raw as { data?: unknown }).data;
    if (Array.isArray(items)) return items as Record<string, unknown>[];
  }
  return [];
}

function recordId(raw: unknown): string {
  const row = Array.isArray(raw) ? raw[0] : raw;
  if (!row || typeof row !== 'object') return '';
  const record = row as Record<string, unknown>;
  const direct = text(record.__dataId ?? record.dataId ?? record.DataId);
  if (direct) return direct;
  for (const key of ['data', 'item', 'result']) {
    const nested = record[key];
    if (nested && typeof nested === 'object') {
      const id = recordId(nested);
      if (id) return id;
    }
  }
  return '';
}

function text(value: unknown): string {
  return typeof value === 'string' ? value.trim() : '';
}
