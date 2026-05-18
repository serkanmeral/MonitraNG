import { defineStore } from 'pinia';
import { fetchFromDataGateway } from '@/services/apiService';

/** Dashboard layout row (row-based) */
export interface LayoutRow {
  cols: LayoutCol[];
  align?: 'start' | 'center' | 'end' | 'baseline' | 'stretch';
  justify?: 'start' | 'center' | 'end' | 'space-between' | 'space-around' | 'space-evenly';
  noGutters?: boolean;
  dense?: boolean;
}

/** Dashboard'da widget instance için override (sadece bu widget için) */
export interface WidgetConfigOverrides {
  timeRangeMinutes?: number | null;
  limit?: number;
  refreshIntervalSeconds?: number;
}

/** Dashboard layout column */
export interface LayoutCol {
  widgetId?: string;
  /** Widget ayarları override (dashboard görünümünde değiştirilebilir) */
  widgetOverrides?: WidgetConfigOverrides;
  rows?: LayoutRow[];
  span?: number;
  spanSm?: number;
  spanMd?: number;
  spanLg?: number;
  spanXl?: number;
  alignSelf?: string;
  order?: number;
}

/** Dashboard layout */
export interface DashboardLayout {
  type: 'rows';
  rows: LayoutRow[];
}

/** Dashboard permissions */
export interface DashboardPermissions {
  view?: { groups: string[]; users?: string[] };
  edit?: { groups: string[]; users?: string[] };
}

/** Dashboard entity */
export interface Dashboard {
  __dataId?: string;
  dataId?: string;
  name: string;
  title: string;
  description?: string;
  slug?: string;
  layout: DashboardLayout;
  permissions?: DashboardPermissions;
  isDefault?: boolean;
  isActive: boolean;
  order?: number;
  createInfo?: {
    createdAt: string | Date;
    userInfo?: { uid: string; userName: string; domain: string };
  };
  lastUpdateInfo?: {
    updatedAt: string | Date;
    userInfo?: { uid: string; userName: string; domain: string };
  } | null;
}

/** Create dashboard DTO */
export interface CreateDashboardDto {
  name: string;
  title: string;
  description?: string;
  slug?: string;
  layout: DashboardLayout;
  permissions?: DashboardPermissions;
  isDefault?: boolean;
  isActive: boolean;
  order?: number;
}

/** Update dashboard DTO */
export interface UpdateDashboardDto {
  name?: string;
  title?: string;
  description?: string;
  slug?: string;
  layout?: DashboardLayout;
  permissions?: DashboardPermissions;
  isDefault?: boolean;
  isActive?: boolean;
  order?: number;
}

/** Fetch list params */
export interface FetchDashboardsParams {
  skip?: number;
  limit?: number;
  sort?: string;
  filter?: string;
  search?: string;
}

interface DashboardState {
  dashboards: Dashboard[];
  currentDashboard: Dashboard | null;
  loading: boolean;
  error: string | null;
  totalCount: number;
}

const DATASET = '@dashboards';

function mapToDashboard(item: any): Dashboard {
  return {
    __dataId: item.__dataId ?? item.DataId ?? item.dataId ?? '',
    dataId: item.__dataId ?? item.DataId ?? item.dataId ?? '',
    name: item.name ?? item.Name ?? '',
    title: item.title ?? item.Title ?? '',
    description: item.description ?? item.Description,
    slug: item.slug ?? item.Slug,
    layout: item.layout ?? item.Layout ?? { type: 'rows', rows: [] },
    permissions: item.permissions ?? item.Permissions,
    isDefault: item.isDefault ?? item.IsDefault ?? false,
    isActive: item.isActive ?? item.IsActive ?? true,
    order: item.order ?? item.Order ?? 0,
    createInfo: item.createInfo ?? item.CreateInfo,
    lastUpdateInfo: item.lastUpdateInfo ?? item.LastUpdateInfo ?? null,
  };
}

/** Default empty layout (single row, single full-width column) */
export function defaultLayout(): DashboardLayout {
  return {
    type: 'rows',
    rows: [
      {
        cols: [{ span: 12, widgetId: '' }],
      },
    ],
  };
}

export const useDashboardStore = defineStore('dashboard', {
  state: (): DashboardState => ({
    dashboards: [],
    currentDashboard: null,
    loading: false,
    error: null,
    totalCount: 0,
  }),

  getters: {
    getById: (state) => (id: string) =>
      state.dashboards.find((d) => (d.__dataId ?? d.dataId) === id),

    getBySlug: (state) => (slug: string) =>
      state.dashboards.find(
        (d) => (d.slug ?? d.name) === slug
      ),

    activeDashboards: (state) => state.dashboards.filter((d) => d.isActive),
  },

  actions: {
    async fetchDashboards(params?: FetchDashboardsParams) {
      this.loading = true;
      this.error = null;

      try {
        const skip = params?.skip ?? 0;
        const limit = params?.limit ?? 50;
        const sort = params?.sort ?? 'order,name';
        const filter = params?.filter;
        const search = params?.search;

        const q = new URLSearchParams();
        q.set('skip', String(skip));
        q.set('limit', String(limit));
        if (sort) q.set('sort', sort);
        if (filter) q.set('filter', filter);
        if (search) q.set('search', search);

        const url = `/api/v1/data/${DATASET}?${q.toString()}`;
        const data = await fetchFromDataGateway(url, 'GET');

        const items = Array.isArray(data) ? data : [];
        const total = (data as any)?._totalCount ?? items.length;

        this.dashboards = items.map(mapToDashboard);
        this.totalCount = total;
      } catch (e: any) {
        this.error = e.message ?? 'Dashboard listesi yüklenirken hata oluştu';
        this.dashboards = [];
        this.totalCount = 0;
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async fetchDashboardById(id: string) {
      this.loading = true;
      this.error = null;

      try {
        const url = `/api/v1/data/${DATASET}/${encodeURIComponent(id)}`;
        const data = await fetchFromDataGateway(url, 'GET');

        const raw = Array.isArray(data) && data.length ? data[0] : data;
        if (!raw) throw new Error('Dashboard bulunamadı');

        const dashboard = mapToDashboard(raw);
        this.currentDashboard = dashboard;

        const idx = this.dashboards.findIndex((d) => (d.__dataId ?? d.dataId) === id);
        if (idx !== -1) this.dashboards[idx] = dashboard;

        return dashboard;
      } catch (e: any) {
        this.error = e.message ?? 'Dashboard yüklenirken hata oluştu';
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async fetchDashboardBySlug(slug: string) {
      this.loading = true;
      this.error = null;

      try {
        const f = `slug:eq:${slug}`;
        const q = new URLSearchParams({ filter: f, limit: '1' });
        const url = `/api/v1/data/${DATASET}?${q.toString()}`;
        const data = await fetchFromDataGateway(url, 'GET');

        const items = Array.isArray(data) ? data : [];
        const raw = items[0];
        if (!raw) throw new Error(`Slug ile dashboard bulunamadı: ${slug}`);

        const dashboard = mapToDashboard(raw);
        this.currentDashboard = dashboard;
        return dashboard;
      } catch (e: any) {
        this.error = e.message ?? 'Dashboard yüklenirken hata oluştu';
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async createDashboard(dto: CreateDashboardDto) {
      this.loading = true;
      this.error = null;

      try {
        const url = `/api/v1/data/${DATASET}`;
        const data = await fetchFromDataGateway(url, 'POST', dto);

        const raw = Array.isArray(data) && data.length ? data[0] : data;
        if (!raw) throw new Error('Dashboard oluşturulamadı');

        const dashboard = mapToDashboard(raw);
        this.dashboards.unshift(dashboard);
        this.totalCount += 1;
        this.currentDashboard = dashboard;

        return dashboard;
      } catch (e: any) {
        this.error = e.message ?? 'Dashboard oluşturulurken hata oluştu';
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async updateDashboard(id: string, dto: UpdateDashboardDto) {
      this.loading = true;
      this.error = null;

      try {
        const url = `/api/v1/data/${DATASET}/${encodeURIComponent(id)}`;
        const data = await fetchFromDataGateway(url, 'PUT', dto);

        const raw = Array.isArray(data) && data.length ? data[0] : data;
        if (!raw) throw new Error('Dashboard güncellenemedi');

        const dashboard = mapToDashboard(raw);
        const idx = this.dashboards.findIndex((d) => (d.__dataId ?? d.dataId) === id);
        if (idx !== -1) this.dashboards[idx] = dashboard;
        if (this.currentDashboard && (this.currentDashboard.__dataId ?? this.currentDashboard.dataId) === id) {
          this.currentDashboard = dashboard;
        }

        return dashboard;
      } catch (e: any) {
        this.error = e.message ?? 'Dashboard güncellenirken hata oluştu';
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async deleteDashboard(id: string) {
      this.loading = true;
      this.error = null;

      try {
        const url = `/api/v1/data/${DATASET}/${encodeURIComponent(id)}`;
        await fetchFromDataGateway(url, 'DELETE');

        this.dashboards = this.dashboards.filter((d) => (d.__dataId ?? d.dataId) !== id);
        this.totalCount = Math.max(0, this.totalCount - 1);
        if (this.currentDashboard && (this.currentDashboard.__dataId ?? this.currentDashboard.dataId) === id) {
          this.currentDashboard = null;
        }
      } catch (e: any) {
        this.error = e.message ?? 'Dashboard silinirken hata oluştu';
        throw e;
      } finally {
        this.loading = false;
      }
    },

    clearError() {
      this.error = null;
    },

    resetCurrent() {
      this.currentDashboard = null;
    },
  },
});
