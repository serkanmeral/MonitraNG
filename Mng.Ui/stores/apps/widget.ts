import { defineStore } from 'pinia';
import { fetchFromDataGateway } from '@/services/apiService';

/** Widget Category Entity */
export interface WidgetCategory {
  __dataId?: string;
  dataId?: string;
  name: string;
  description?: string;
  icon?: string;
  color?: string;
  order?: number;
  isActive: boolean;
  createInfo?: {
    createdAt: string | Date;
    userInfo?: { uid: string; userName: string; domain: string };
  };
  lastUpdateInfo?: {
    updatedAt: string | Date;
    userInfo?: { uid: string; userName: string; domain: string };
  } | null;
}

/** Data Source Configuration (type: 'data') */
export interface DataSourceConfigData {
  type: 'data';
  dataset: string;
  getMethod: 'default' | 'query' | 'aggregate' | 'predefined';

  // getMethod = 'default'
  default?: {
    skip?: number;
    limit?: number;
    sort?: string;
    filter?: string;
    fields?: string;
    search?: string;
    format?: 'json' | 'csv';
    expand?: boolean;
    deep?: number;
    showHistory?: boolean;
  };

  // getMethod = 'query'
  query?: {
    match: object;
    skip?: number;
    limit?: number;
    sort?: string;
    fields?: string;
    expand?: boolean;
    deep?: number;
    showHistory?: boolean;
  };

  // getMethod = 'aggregate'
  aggregate?: {
    pipeline: object[];
  };

  // getMethod = 'predefined'
  predefined?: {
    queryName: string;
    parameters?: Record<string, any>;
  };

  mapping?: {
    items?: string;
    total?: string;
    [key: string]: any;
  };
}

/** Widget Permissions */
export interface WidgetPermissions {
  groups?: string[]; // Group names that can view this widget
}

/** Widget Entity */
export interface Widget {
  __dataId?: string;
  dataId?: string;
  name: string;
  title: string;
  description?: string;
  category: string | WidgetCategory; // Relation to @widget_categories
  type: 'card' | 'chart' | 'table' | 'banner' | 'map' | 'gauge';
  dataSource: DataSourceConfigData;
  layout?: object;
  style?: object;
  config?: object;
  isActive: boolean;
  order?: number;
  permissions?: WidgetPermissions;
  createInfo?: {
    createdAt: string | Date;
    userInfo?: { uid: string; userName: string; domain: string };
  };
  lastUpdateInfo?: {
    updatedAt: string | Date;
    userInfo?: { uid: string; userName: string; domain: string };
  } | null;
}

/** Create Widget DTO */
export interface CreateWidgetDto {
  name: string;
  title: string;
  description?: string;
  category: string; // Category __dataId
  type: 'card' | 'chart' | 'table' | 'banner' | 'map' | 'gauge';
  dataSource: DataSourceConfigData;
  layout?: object;
  style?: object;
  config?: object;
  isActive: boolean;
  order?: number;
  permissions?: WidgetPermissions;
}

/** Update Widget DTO */
export interface UpdateWidgetDto {
  name?: string;
  title?: string;
  description?: string;
  category?: string;
  type?: 'card' | 'chart' | 'table' | 'banner' | 'map' | 'gauge';
  dataSource?: DataSourceConfigData;
  layout?: object;
  style?: object;
  config?: object;
  isActive?: boolean;
  order?: number;
  permissions?: WidgetPermissions;
}

/** Fetch Widgets Params */
export interface FetchWidgetsParams {
  skip?: number;
  limit?: number;
  sort?: string;
  filter?: string;
  search?: string;
  category?: string; // Filter by category __dataId
  type?: 'card' | 'chart' | 'table' | 'banner' | 'map' | 'gauge'; // Filter by type
}

interface WidgetState {
  widgets: Widget[];
  currentWidget: Widget | null;
  categories: WidgetCategory[];
  currentCategory: WidgetCategory | null;
  loading: boolean;
  error: string | null;
  totalCount: number;
}

const WIDGETS_DATASET = '@widgets';
const CATEGORIES_DATASET = '@widget_categories';

function mapToWidgetCategory(item: any): WidgetCategory {
  return {
    __dataId: item.__dataId ?? item.DataId ?? item.dataId ?? '',
    dataId: item.__dataId ?? item.DataId ?? item.dataId ?? '',
    name: item.name ?? item.Name ?? '',
    description: item.description ?? item.Description,
    icon: item.icon ?? item.Icon,
    color: item.color ?? item.Color,
    order: item.order ?? item.Order ?? 0,
    isActive: item.isActive ?? item.IsActive ?? true,
    createInfo: item.createInfo ?? item.CreateInfo,
    lastUpdateInfo: item.lastUpdateInfo ?? item.LastUpdateInfo ?? null,
  };
}

function mapToWidget(item: any): Widget {
  // Category can be expanded (object) or just ID (string)
  let category: string | WidgetCategory = item.category ?? item.Category ?? '';
  if (typeof category === 'object' && category !== null) {
    category = mapToWidgetCategory(category);
  }

  return {
    __dataId: item.__dataId ?? item.DataId ?? item.dataId ?? '',
    dataId: item.__dataId ?? item.DataId ?? item.dataId ?? '',
    name: item.name ?? item.Name ?? '',
    title: item.title ?? item.Title ?? '',
    description: item.description ?? item.Description,
    category,
    type: (item.type ?? item.Type ?? 'card') as 'card' | 'chart' | 'table' | 'banner' | 'map' | 'gauge',
    dataSource: item.dataSource ?? item.DataSource ?? {
      type: 'data',
      dataset: '',
      getMethod: 'default',
    },
    layout: item.layout ?? item.Layout,
    style: item.style ?? item.Style,
    config: item.config ?? item.Config,
    isActive: item.isActive ?? item.IsActive ?? true,
    order: item.order ?? item.Order ?? 0,
    permissions: item.permissions ?? item.Permissions,
    createInfo: item.createInfo ?? item.CreateInfo,
    lastUpdateInfo: item.lastUpdateInfo ?? item.LastUpdateInfo ?? null,
  };
}

export const useWidgetStore = defineStore('widget', {
  state: (): WidgetState => ({
    widgets: [],
    currentWidget: null,
    categories: [],
    currentCategory: null,
    loading: false,
    error: null,
    totalCount: 0,
  }),

  getters: {
    getWidgetById: (state) => (id: string) =>
      state.widgets.find((w) => (w.__dataId ?? w.dataId) === id),

    getWidgetsByCategory: (state) => (categoryId: string) =>
      state.widgets.filter((w) => {
        const cat = typeof w.category === 'string' ? w.category : w.category.__dataId ?? w.category.dataId;
        return cat === categoryId;
      }),

    getWidgetsByType: (state) => (type: 'card' | 'chart' | 'table' | 'banner' | 'map' | 'gauge') =>
      state.widgets.filter((w) => w.type === type),

    activeWidgets: (state) => state.widgets.filter((w) => w.isActive),

    getCategoryById: (state) => (id: string) =>
      state.categories.find((c) => (c.__dataId ?? c.dataId) === id),

    activeCategories: (state) => state.categories.filter((c) => c.isActive),
  },

  actions: {
    // ========== Widget Categories ==========

    async fetchWidgetCategories(params?: { skip?: number; limit?: number; sort?: string; filter?: string }) {
      this.loading = true;
      this.error = null;

      try {
        const skip = params?.skip ?? 0;
        const limit = params?.limit ?? 100;
        const sort = params?.sort ?? 'order,name';
        const filter = params?.filter;

        const q = new URLSearchParams();
        q.set('skip', String(skip));
        q.set('limit', String(limit));
        if (sort) q.set('sort', sort);
        if (filter) q.set('filter', filter);

        const url = `/api/v1/data/${CATEGORIES_DATASET}?${q.toString()}`;
        const data = await fetchFromDataGateway(url, 'GET');

        const items = Array.isArray(data) ? data : [];
        this.categories = items.map(mapToWidgetCategory);
      } catch (e: any) {
        this.error = e.message ?? 'Widget kategorileri yüklenirken hata oluştu';
        this.categories = [];
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async fetchWidgetCategoryById(id: string) {
      this.loading = true;
      this.error = null;

      try {
        const url = `/api/v1/data/${CATEGORIES_DATASET}/${encodeURIComponent(id)}`;
        const data = await fetchFromDataGateway(url, 'GET');

        const raw = Array.isArray(data) && data.length ? data[0] : data;
        if (!raw) throw new Error('Widget kategorisi bulunamadı');

        const category = mapToWidgetCategory(raw);
        this.currentCategory = category;

        const idx = this.categories.findIndex((c) => (c.__dataId ?? c.dataId) === id);
        if (idx !== -1) this.categories[idx] = category;

        return category;
      } catch (e: any) {
        this.error = e.message ?? 'Widget kategorisi yüklenirken hata oluştu';
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async createWidgetCategory(dto: {
      name: string;
      description?: string;
      icon?: string;
      color?: string;
      order?: number;
      isActive?: boolean;
    }) {
      this.loading = true;
      this.error = null;

      try {
        const url = `/api/v1/data/${CATEGORIES_DATASET}`;
        const data = await fetchFromDataGateway(url, 'POST', dto);

        const raw = Array.isArray(data) && data.length ? data[0] : data;
        if (!raw) throw new Error('Widget kategorisi oluşturulamadı');

        const category = mapToWidgetCategory(raw);
        this.categories.push(category);
        return category;
      } catch (e: any) {
        this.error = e.message ?? 'Widget kategorisi oluşturulurken hata oluştu';
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async ensureMonitoringCategory(): Promise<string> {
      if (this.categories.length === 0) {
        await this.fetchWidgetCategories();
      }
      const existing = this.categories.find((c) => (c.name ?? '').toLowerCase() === 'monitoring');
      if (existing) {
        return existing.__dataId ?? existing.dataId ?? '';
      }
      const created = await this.createWidgetCategory({
        name: 'Monitoring',
        description: 'Monitoring widget\'ları - asset, collectible ve metrik bazlı izleme',
        icon: 'mdi-monitor-dashboard',
        color: 'success',
        order: 100,
        isActive: true,
      });
      return created.__dataId ?? created.dataId ?? '';
    },

    // ========== Widgets ==========

    async fetchWidgets(params?: FetchWidgetsParams) {
      this.loading = true;
      this.error = null;

      try {
        const skip = params?.skip ?? 0;
        const limit = params?.limit ?? 50;
        const sort = params?.sort ?? 'order,name';
        let filter = params?.filter;
        const search = params?.search;

        // Build filter string
        const filters: string[] = [];
        if (filter) filters.push(filter);
        if (params?.category) filters.push(`category:eq:${params.category}`);
        if (params?.type) filters.push(`type:eq:${params.type}`);
        if (filters.length > 0) filter = filters.join(',');

        const q = new URLSearchParams();
        q.set('skip', String(skip));
        q.set('limit', String(limit));
        if (sort) q.set('sort', sort);
        if (filter) q.set('filter', filter);
        if (search) q.set('search', search);

        const url = `/api/v1/data/${WIDGETS_DATASET}?${q.toString()}`;
        const data = await fetchFromDataGateway(url, 'GET');

        const items = Array.isArray(data) ? data : [];
        const total = (data as any)?._totalCount ?? items.length;

        this.widgets = items.map(mapToWidget);
        this.totalCount = total;
      } catch (e: any) {
        this.error = e.message ?? 'Widget listesi yüklenirken hata oluştu';
        this.widgets = [];
        this.totalCount = 0;
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async fetchWidgetById(id: string) {
      this.loading = true;
      this.error = null;

      try {
        const url = `/api/v1/data/${WIDGETS_DATASET}/${encodeURIComponent(id)}`;
        const data = await fetchFromDataGateway(url, 'GET');

        const raw = Array.isArray(data) && data.length ? data[0] : data;
        if (!raw) throw new Error('Widget bulunamadı');

        const widget = mapToWidget(raw);
        this.currentWidget = widget;

        const idx = this.widgets.findIndex((w) => (w.__dataId ?? w.dataId) === id);
        if (idx !== -1) this.widgets[idx] = widget;

        return widget;
      } catch (e: any) {
        this.error = e.message ?? 'Widget yüklenirken hata oluştu';
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async fetchWidgetsByCategory(categoryId: string, params?: Omit<FetchWidgetsParams, 'category'>) {
      return this.fetchWidgets({ ...params, category: categoryId });
    },

    async fetchWidgetsByType(type: 'card' | 'chart' | 'table' | 'banner' | 'map' | 'gauge', params?: Omit<FetchWidgetsParams, 'type'>) {
      return this.fetchWidgets({ ...params, type });
    },

    async createWidget(dto: CreateWidgetDto) {
      this.loading = true;
      this.error = null;

      try {
        const url = `/api/v1/data/${WIDGETS_DATASET}`;
        const data = await fetchFromDataGateway(url, 'POST', dto);

        const raw = Array.isArray(data) && data.length ? data[0] : data;
        if (!raw) throw new Error('Widget oluşturulamadı');

        const widget = mapToWidget(raw);
        this.widgets.unshift(widget);
        this.totalCount += 1;
        this.currentWidget = widget;
        return widget;
      } catch (e: any) {
        this.error = e.message ?? 'Widget oluşturulurken hata oluştu';
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async updateWidget(id: string, dto: UpdateWidgetDto) {
      this.loading = true;
      this.error = null;

      try {
        const url = `/api/v1/data/${WIDGETS_DATASET}/${encodeURIComponent(id)}`;
        const data = await fetchFromDataGateway(url, 'PUT', dto);

        const raw = Array.isArray(data) && data.length ? data[0] : data;
        if (!raw) throw new Error('Widget güncellenemedi');

        const widget = mapToWidget(raw);
        const idx = this.widgets.findIndex((w) => (w.__dataId ?? w.dataId) === id);
        if (idx !== -1) this.widgets[idx] = widget;
        if (this.currentWidget && (this.currentWidget.__dataId ?? this.currentWidget.dataId) === id) {
          this.currentWidget = widget;
        }
        return widget;
      } catch (e: any) {
        this.error = e.message ?? 'Widget güncellenirken hata oluştu';
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async deleteWidget(id: string) {
      this.loading = true;
      this.error = null;

      try {
        const url = `/api/v1/data/${WIDGETS_DATASET}/${encodeURIComponent(id)}`;
        await fetchFromDataGateway(url, 'DELETE');

        this.widgets = this.widgets.filter((w) => (w.__dataId ?? w.dataId) !== id);
        this.totalCount = Math.max(0, this.totalCount - 1);
        if (this.currentWidget && (this.currentWidget.__dataId ?? this.currentWidget.dataId) === id) {
          this.currentWidget = null;
        }
      } catch (e: any) {
        this.error = e.message ?? 'Widget silinirken hata oluştu';
        throw e;
      } finally {
        this.loading = false;
      }
    },

    clearError() {
      this.error = null;
    },

    resetCurrent() {
      this.currentWidget = null;
      this.currentCategory = null;
    },
  },
});
