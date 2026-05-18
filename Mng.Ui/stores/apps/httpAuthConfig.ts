import { defineStore } from 'pinia';
import { fetchFromDataGateway } from '@/services/apiService';
import type { MonHttpAuthConfig } from '@/types/apps/httpAuthConfig';

const DATASET = 'mon_http_auth_configs';

function parseArrayResponse(response: unknown): unknown[] {
  if (response && Array.isArray(response)) return response;
  if (response && typeof response === 'object' && 'items' in response && Array.isArray((response as any).items))
    return (response as any).items;
  if (response && typeof response === 'object' && 'data' in response && Array.isArray((response as any).data))
    return (response as any).data;
  return [];
}

function mapConfig(raw: any): MonHttpAuthConfig {
  const tokenBody = raw.tokenBody ?? raw.token_body ?? {};
  const body = typeof tokenBody === 'object' ? tokenBody : {};
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    tokenUrl: raw.tokenUrl ?? raw.token_url ?? '',
    tokenMethod: (raw.tokenMethod ?? raw.token_method ?? 'POST') as 'GET' | 'POST',
    tokenBodyType: (raw.tokenBodyType ?? raw.token_body_type ?? 'json') as 'json' | 'form',
    tokenBody: body,
    tokenResponsePath: raw.tokenResponsePath ?? raw.token_response_path ?? '$.access_token',
    description: raw.description ?? raw.Description ?? null,
  };
}

interface State {
  items: MonHttpAuthConfig[];
  loading: boolean;
  error: string | null;
}

export const useHttpAuthConfigStore = defineStore('httpAuthConfig', {
  state: (): State => ({
    items: [],
    loading: false,
    error: null,
  }),

  getters: {
    options: (state) =>
      state.items.map((c) => ({ title: c.name, value: c.__dataId })),
  },

  actions: {
    clearError() {
      this.error = null;
    },

    async loadAll() {
      this.loading = true;
      this.error = null;
      try {
        const response = await fetchFromDataGateway(`/api/v1/data/${DATASET}?limit=500`);
        const raw = parseArrayResponse(response);
        this.items = raw.map((r: any) => mapConfig(r));
      } catch (e: any) {
        this.items = [];
        throw e;
      } finally {
        this.loading = false;
      }
    },

    async create(payload: Partial<MonHttpAuthConfig>) {
      this.loading = true;
      this.error = null;
      try {
        const clean = { ...payload };
        delete (clean as any).__dataId;
        if (!clean.tokenBody || typeof clean.tokenBody !== 'object') clean.tokenBody = {};
        await fetchFromDataGateway(`/api/v1/data/${DATASET}`, 'POST', clean);
        await this.loadAll();
      } catch (error: any) {
        this.error = error.message || 'Tanım oluşturulurken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async update(dataId: string, payload: Partial<MonHttpAuthConfig>) {
      this.loading = true;
      this.error = null;
      try {
        const clean = { ...payload };
        delete (clean as any).__dataId;
        if (!clean.tokenBody || typeof clean.tokenBody !== 'object') clean.tokenBody = {};
        await fetchFromDataGateway(`/api/v1/data/${DATASET}/${dataId}`, 'PUT', clean);
        await this.loadAll();
      } catch (error: any) {
        this.error = error.message || 'Tanım güncellenirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async remove(dataId: string) {
      this.loading = true;
      this.error = null;
      try {
        await fetchFromDataGateway(`/api/v1/data/${DATASET}/${dataId}`, 'DELETE');
        await this.loadAll();
      } catch (error: any) {
        this.error = error.message || 'Tanım silinirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },
  },
});
