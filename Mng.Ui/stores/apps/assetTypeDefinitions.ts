import { defineStore } from 'pinia';
import { fetchFromDataGateway } from '@/services/apiService';
import type { MonAssetTypeFamily, MonAssetTypeFull, MonCollectibleTemplate } from '@/types/apps/assetTypeDefinitions';

const FAMILY_DATASET = 'mon_asset_type_family';
const TYPE_DATASET = 'mon_asset_types';
const TEMPLATE_DATASET = 'mon_collectible_templates';

function parseArrayResponse(response: unknown): unknown[] {
  if (response && Array.isArray(response)) return response;
  if (response && typeof response === 'object' && 'items' in response && Array.isArray((response as any).items))
    return (response as any).items;
  if (response && typeof response === 'object' && 'data' in response && Array.isArray((response as any).data))
    return (response as any).data;
  return [];
}

function mapFamily(raw: any): MonAssetTypeFamily {
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    code: raw.code ?? raw.Code ?? null,
    description: raw.description ?? raw.Description ?? null,
  };
}

function mapType(raw: any): MonAssetTypeFull {
  const collectibles = raw.collectibles ?? raw.Collectibles ?? [];
  const arr = Array.isArray(collectibles) ? collectibles : [];
  const familyRaw = raw.family ?? raw.Family ?? '';
  const familyId = typeof familyRaw === 'object' && familyRaw?.__dataId ? familyRaw.__dataId : String(familyRaw ?? '');
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    family: familyId,
    collection_method: raw.collection_method ?? raw.CollectionMethod ?? raw.collectionMethod ?? '',
    description: raw.description ?? raw.Description ?? null,
    collectibles: arr.map((c: any) => ({
      code: c.code ?? c.Code ?? '',
      name: c.name ?? c.Name,
      data_type: c.data_type ?? c.dataType ?? c.DataType,
      metric_key: c.metric_key ?? c.metricKey ?? c.MetricKey,
      oid: c.oid ?? c.Oid,
      path: c.path ?? c.Path,
      overridable_params: c.overridable_params ?? c.overridableParams ?? c.overridableParams ?? [],
    })),
  };
}

function mapTemplate(raw: any): MonCollectibleTemplate {
  const collectibles = raw.collectibles ?? raw.Collectibles ?? [];
  const arr = Array.isArray(collectibles) ? collectibles : [];
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    collection_method: raw.collection_method ?? raw.CollectionMethod ?? raw.collectionMethod ?? '',
    description: raw.description ?? raw.Description ?? null,
    collectibles: arr.map((c: any) => ({
      code: c.code ?? c.Code ?? '',
      name: c.name ?? c.Name,
      data_type: c.data_type ?? c.dataType ?? c.DataType,
      metric_key: c.metric_key ?? c.metricKey ?? c.MetricKey,
      oid: c.oid ?? c.Oid,
      path: c.path ?? c.Path,
      overridable_params: c.overridable_params ?? c.overridableParams ?? c.overridableParams ?? [],
    })),
  };
}

interface State {
  families: MonAssetTypeFamily[];
  types: MonAssetTypeFull[];
  templates: MonCollectibleTemplate[];
  loading: boolean;
  error: string | null;
}

export const useAssetTypeDefinitionsStore = defineStore('assetTypeDefinitions', {
  state: (): State => ({
    families: [],
    types: [],
    templates: [],
    loading: false,
    error: null,
  }),

  getters: {
    familyOptions: (state) =>
      state.families.map((f) => ({ title: f.name, value: f.__dataId })),
    /** Belirli toplama metoduna göre şablon seçenekleri (Asset Type formunda "Şablon uygula" için) */
    templateOptionsByMethod: (state) => (collectionMethod: string) =>
      state.templates
        .filter((t) => (t.collection_method || '').toLowerCase() === (collectionMethod || '').toLowerCase())
        .map((t) => ({ title: t.name, value: t.__dataId })),
  },

  actions: {
    clearError() {
      this.error = null;
    },

    async loadFamilies() {
      try {
        const response = await fetchFromDataGateway(
          `/api/v1/data/${FAMILY_DATASET}?limit=500`
        );
        const raw = parseArrayResponse(response);
        this.families = raw.map((r: any) => mapFamily(r));
      } catch (e: any) {
        this.families = [];
        throw e;
      }
    },

    async loadTypes() {
      try {
        const response = await fetchFromDataGateway(
          `/api/v1/data/${TYPE_DATASET}?limit=500`
        );
        const raw = parseArrayResponse(response);
        this.types = raw.map((r: any) => mapType(r));
      } catch (e: any) {
        this.types = [];
        throw e;
      }
    },

    async loadTemplates() {
      try {
        const response = await fetchFromDataGateway(
          `/api/v1/data/${TEMPLATE_DATASET}?limit=500`
        );
        const raw = parseArrayResponse(response);
        this.templates = raw.map((r: any) => mapTemplate(r));
      } catch (e: any) {
        this.templates = [];
        throw e;
      }
    },

    async loadAll() {
      this.loading = true;
      this.error = null;
      try {
        await this.loadFamilies();
        await this.loadTypes();
        await this.loadTemplates();
      } catch (error: any) {
        this.error = error.message || 'Veri yüklenirken hata oluştu';
      } finally {
        this.loading = false;
      }
    },

    async createFamily(payload: Partial<MonAssetTypeFamily>) {
      this.loading = true;
      this.error = null;
      try {
        const clean = { ...payload };
        delete (clean as any).__dataId;
        await fetchFromDataGateway(`/api/v1/data/${FAMILY_DATASET}`, 'POST', clean);
        await this.loadFamilies();
      } catch (error: any) {
        this.error = error.message || 'Aile oluşturulurken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async updateFamily(dataId: string, payload: Partial<MonAssetTypeFamily>) {
      this.loading = true;
      this.error = null;
      try {
        const clean = { ...payload };
        delete (clean as any).__dataId;
        await fetchFromDataGateway(`/api/v1/data/${FAMILY_DATASET}/${dataId}`, 'PUT', clean);
        await this.loadFamilies();
      } catch (error: any) {
        this.error = error.message || 'Aile güncellenirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async deleteFamily(dataId: string) {
      this.loading = true;
      this.error = null;
      try {
        await fetchFromDataGateway(`/api/v1/data/${FAMILY_DATASET}/${dataId}`, 'DELETE');
        await this.loadFamilies();
      } catch (error: any) {
        this.error = error.message || 'Aile silinirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async createType(payload: Partial<MonAssetTypeFull>) {
      this.loading = true;
      this.error = null;
      try {
        const clean = { ...payload };
        delete (clean as any).__dataId;
        if (!Array.isArray(clean.collectibles)) clean.collectibles = [];
        await fetchFromDataGateway(`/api/v1/data/${TYPE_DATASET}`, 'POST', clean);
        await this.loadTypes();
      } catch (error: any) {
        this.error = error.message || 'Tip oluşturulurken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async updateType(dataId: string, payload: Partial<MonAssetTypeFull>) {
      this.loading = true;
      this.error = null;
      try {
        const clean = { ...payload };
        delete (clean as any).__dataId;
        if (!Array.isArray(clean.collectibles)) clean.collectibles = [];
        await fetchFromDataGateway(`/api/v1/data/${TYPE_DATASET}/${dataId}`, 'PUT', clean);
        await this.loadTypes();
      } catch (error: any) {
        this.error = error.message || 'Tip güncellenirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async deleteType(dataId: string) {
      this.loading = true;
      this.error = null;
      try {
        await fetchFromDataGateway(`/api/v1/data/${TYPE_DATASET}/${dataId}`, 'DELETE');
        await this.loadTypes();
      } catch (error: any) {
        this.error = error.message || 'Tip silinirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async createTemplate(payload: Partial<MonCollectibleTemplate>) {
      this.loading = true;
      this.error = null;
      try {
        const clean = { ...payload };
        delete (clean as any).__dataId;
        if (!Array.isArray(clean.collectibles)) clean.collectibles = [];
        await fetchFromDataGateway(`/api/v1/data/${TEMPLATE_DATASET}`, 'POST', clean);
        await this.loadTemplates();
      } catch (error: any) {
        this.error = error.message || 'Şablon oluşturulurken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async updateTemplate(dataId: string, payload: Partial<MonCollectibleTemplate>) {
      this.loading = true;
      this.error = null;
      try {
        const clean = { ...payload };
        delete (clean as any).__dataId;
        if (!Array.isArray(clean.collectibles)) clean.collectibles = [];
        await fetchFromDataGateway(`/api/v1/data/${TEMPLATE_DATASET}/${dataId}`, 'PUT', clean);
        await this.loadTemplates();
      } catch (error: any) {
        this.error = error.message || 'Şablon güncellenirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async deleteTemplate(dataId: string) {
      this.loading = true;
      this.error = null;
      try {
        await fetchFromDataGateway(`/api/v1/data/${TEMPLATE_DATASET}/${dataId}`, 'DELETE');
        await this.loadTemplates();
      } catch (error: any) {
        this.error = error.message || 'Şablon silinirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },
  },
});
