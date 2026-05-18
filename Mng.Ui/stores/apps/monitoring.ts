import { defineStore } from 'pinia';
import { fetchFromDataGateway } from '@/services/apiService';
import type {
  MonEngine,
  MonEngineError,
  MonAgent,
  MonSchedule,
  MonCollectionPeriod,
  MonAgentAssetConfig,
} from '@/types/apps/monitoring';

const PERIODS_DATASET = 'mon_collection_periods';
const SCHEDULES_DATASET = 'mon_schedules';
const ENGINES_DATASET = 'mon_engines';
const AGENTS_DATASET = 'mon_agents';

function parseArrayResponse(response: unknown): unknown[] {
  if (response && Array.isArray(response)) return response;
  if (response && typeof response === 'object' && 'items' in response && Array.isArray((response as any).items))
    return (response as any).items;
  if (response && typeof response === 'object' && 'data' in response && Array.isArray((response as any).data))
    return (response as any).data;
  return [];
}

function normId(v: any): string {
  if (v == null) return '';
  if (typeof v === 'string') return v;
  if (typeof v === 'object' && v !== null && ('__dataId' in v || 'dataId' in v))
    return (v as any).__dataId ?? (v as any).dataId ?? '';
  return String(v);
}

function mapPeriod(raw: any): MonCollectionPeriod {
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    description: raw.description ?? raw.Description ?? null,
    expression: raw.expression ?? raw.Expression ?? '',
  };
}

function mapSchedule(raw: any): MonSchedule {
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    description: raw.description ?? raw.Description ?? null,
    type: (raw.type ?? raw.Type ?? 'always') === 'scheduled' ? 'scheduled' : 'always',
    config: raw.config ?? raw.Config ?? null,
  };
}

function mapEngineError(raw: any): MonEngineError {
  return {
    assetId: raw?.assetId ?? raw?.AssetId ?? '',
    agentId: raw?.agentId ?? raw?.AgentId ?? '',
    errorCode: raw?.errorCode ?? raw?.ErrorCode ?? 'unknown',
    message: raw?.message ?? raw?.Message ?? '',
    occurredAt: raw?.occurredAt ?? raw?.OccurredAt ?? '',
  };
}

function mapEngine(raw: any): MonEngine {
  const errList = raw.lastErrors ?? raw.LastErrors ?? [];
  const arr = Array.isArray(errList) ? errList : [];
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    description: raw.description ?? raw.Description ?? null,
    status: raw.status ?? raw.Status ?? 'active',
    domain: raw.domain ?? raw.Domain ?? null,
    username: raw.username ?? raw.Username ?? '',
    password: '', // API'dan password alınmaz (güvenlik)
    sendSchedule: raw.sendSchedule ?? raw.SendSchedule ?? '0 */5 * * *',
    configSyncPeriodMinutes: raw.configSyncPeriodMinutes ?? raw.ConfigSyncPeriodMinutes ?? null,
    lastSeenAt: raw.lastSeenAt ?? raw.LastSeenAt ?? null,
    health: raw.health ?? raw.Health ?? (arr.length > 0 ? 'degraded' : 'ok'),
    hostAddress: raw.hostAddress ?? raw.HostAddress ?? null,
    lastErrors: arr.length > 0 ? arr.map((e: any) => mapEngineError(e)) : null,
  };
}

function mapAssetConfig(raw: any): MonAgentAssetConfig {
  return {
    assetId: normId(raw.assetId ?? raw.AssetId),
    periodId: raw.periodId != null ? normId(raw.periodId ?? raw.PeriodId) || null : null,
    scheduleId: raw.scheduleId != null ? normId(raw.scheduleId ?? raw.ScheduleId) || null : null,
    active: raw.active ?? raw.Active ?? true,
    description: raw.description ?? raw.Description ?? null,
  };
}

function mapAgent(raw: any): MonAgent {
  const configs = raw.asset_configs ?? raw.AssetConfigs ?? raw.assetConfigs ?? [];
  const arr = Array.isArray(configs) ? configs : [];
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    description: raw.description ?? raw.Description ?? null,
    status: raw.status ?? raw.Status ?? 'active',
    engineId: normId(raw.engineId ?? raw.EngineId),
    defaultPeriodId: raw.defaultPeriodId != null ? normId(raw.defaultPeriodId ?? raw.DefaultPeriodId) || null : null,
    defaultScheduleId: raw.defaultScheduleId != null ? normId(raw.defaultScheduleId ?? raw.DefaultScheduleId) || null : null,
    tags: raw.tags ?? raw.Tags ?? null,
    asset_configs: arr.map((c: any) => mapAssetConfig(c)),
  };
}

interface State {
  collectionPeriods: MonCollectionPeriod[];
  schedules: MonSchedule[];
  engines: MonEngine[];
  agents: MonAgent[];
  loading: boolean;
  error: string | null;
}

export const useMonitoringStore = defineStore('monitoring', {
  state: (): State => ({
    collectionPeriods: [],
    schedules: [],
    engines: [],
    agents: [],
    loading: false,
    error: null,
  }),

  getters: {
    periodOptions: (state) =>
      state.collectionPeriods.map((p) => ({ title: `${p.name} (${p.expression})`, value: p.__dataId })),
    scheduleOptions: (state) =>
      state.schedules.map((s) => ({ title: s.name, value: s.__dataId })),
    engineOptions: (state) =>
      state.engines.map((e) => ({ title: e.name, value: e.__dataId })),
  },

  actions: {
    clearError() {
      this.error = null;
    },

    async loadCollectionPeriods() {
      try {
        const res = await fetchFromDataGateway(`/api/v1/data/${PERIODS_DATASET}?limit=500`);
        this.collectionPeriods = parseArrayResponse(res).map((r: any) => mapPeriod(r));
      } catch (e: any) {
        this.collectionPeriods = [];
        this.error = e.message ?? 'Periyotlar yüklenemedi';
        throw e;
      }
    },

    async loadSchedules() {
      try {
        const res = await fetchFromDataGateway(`/api/v1/data/${SCHEDULES_DATASET}?limit=500`);
        this.schedules = parseArrayResponse(res).map((r: any) => mapSchedule(r));
      } catch (e: any) {
        this.schedules = [];
        this.error = e.message ?? 'Schedule\'lar yüklenemedi';
        throw e;
      }
    },

    async loadEngines() {
      try {
        const res = await fetchFromDataGateway(`/api/v1/data/${ENGINES_DATASET}?limit=500`);
        const raw = parseArrayResponse(res);
        if (raw.length > 0) {
          console.log('[Monitoring] mon_engines raw ilk kayıt:', JSON.stringify(raw[0], null, 2));
        }
        this.engines = raw.map((r: any) => mapEngine(r));
        if (this.engines.length > 0) {
          console.log('[Monitoring] mon_engines mapped:', { name: this.engines[0].name, hostAddress: this.engines[0].hostAddress, lastSeenAt: this.engines[0].lastSeenAt });
        }
      } catch (e: any) {
        this.engines = [];
        this.error = e.message ?? 'Engine\'ler yüklenemedi';
        throw e;
      }
    },

    async loadAgents() {
      try {
        const res = await fetchFromDataGateway(`/api/v1/data/${AGENTS_DATASET}?limit=500`);
        this.agents = parseArrayResponse(res).map((r: any) => mapAgent(r));
      } catch (e: any) {
        this.agents = [];
        this.error = e.message ?? 'Agent\'lar yüklenemedi';
        throw e;
      }
    },

    async loadAll() {
      this.loading = true;
      this.error = null;
      try {
        await Promise.all([
          this.loadCollectionPeriods(),
          this.loadSchedules(),
          this.loadEngines(),
          this.loadAgents(),
        ]);
      } catch {
        // error set in individual load
      } finally {
        this.loading = false;
      }
    },

    /** Sadece Engine ve Agent (kontrol sayfası için) */
    async loadEnginesAndAgents() {
      this.loading = true;
      this.error = null;
      try {
        await Promise.all([this.loadEngines(), this.loadAgents()]);
      } catch {
        // error set in individual load
      } finally {
        this.loading = false;
      }
    },

    async createPeriod(payload: Partial<MonCollectionPeriod>) {
      const { __dataId, ...rest } = payload as any;
      const res = await fetchFromDataGateway(`/api/v1/data/${PERIODS_DATASET}`, 'POST', rest);
      await this.loadCollectionPeriods();
      return res;
    },
    async updatePeriod(dataId: string, payload: Partial<MonCollectionPeriod>) {
      const { __dataId, ...rest } = payload as any;
      await fetchFromDataGateway(`/api/v1/data/${PERIODS_DATASET}/${dataId}`, 'PUT', rest);
      await this.loadCollectionPeriods();
    },
    async deletePeriod(dataId: string) {
      await fetchFromDataGateway(`/api/v1/data/${PERIODS_DATASET}/${dataId}`, 'DELETE');
      await this.loadCollectionPeriods();
    },

    async createSchedule(payload: Partial<MonSchedule>) {
      const { __dataId, ...rest } = payload as any;
      const res = await fetchFromDataGateway(`/api/v1/data/${SCHEDULES_DATASET}`, 'POST', rest);
      await this.loadSchedules();
      return res;
    },
    async updateSchedule(dataId: string, payload: Partial<MonSchedule>) {
      const { __dataId, ...rest } = payload as any;
      await fetchFromDataGateway(`/api/v1/data/${SCHEDULES_DATASET}/${dataId}`, 'PUT', rest);
      await this.loadSchedules();
    },
    async deleteSchedule(dataId: string) {
      await fetchFromDataGateway(`/api/v1/data/${SCHEDULES_DATASET}/${dataId}`, 'DELETE');
      await this.loadSchedules();
    },

    async createEngine(payload: Partial<MonEngine>) {
      const { __dataId, lastSeenAt, ...rest } = payload as any;
      const res = await fetchFromDataGateway(`/api/v1/data/${ENGINES_DATASET}`, 'POST', rest);
      await this.loadEngines();
      return res;
    },
    async updateEngine(dataId: string, payload: Partial<MonEngine>) {
      const { __dataId, lastSeenAt, password, ...rest } = payload as any;
      // Boş password gönderme (mevcut parolayı korumak için)
      if (password && String(password).trim()) rest.password = String(password).trim();
      await fetchFromDataGateway(`/api/v1/data/${ENGINES_DATASET}/${dataId}`, 'PUT', rest);
      await this.loadEngines();
    },
    async deleteEngine(dataId: string) {
      await fetchFromDataGateway(`/api/v1/data/${ENGINES_DATASET}/${dataId}`, 'DELETE');
      await this.loadEngines();
    },

    async createAgent(payload: Partial<MonAgent>) {
      const { __dataId, ...rest } = payload as any;
      const res = await fetchFromDataGateway(`/api/v1/data/${AGENTS_DATASET}`, 'POST', rest);
      await this.loadAgents();
      return res;
    },
    async updateAgent(dataId: string, payload: Partial<MonAgent>) {
      const { __dataId, ...rest } = payload as any;
      await fetchFromDataGateway(`/api/v1/data/${AGENTS_DATASET}/${dataId}`, 'PUT', rest);
      await this.loadAgents();
    },
    async deleteAgent(dataId: string) {
      await fetchFromDataGateway(`/api/v1/data/${AGENTS_DATASET}/${dataId}`, 'DELETE');
      await this.loadAgents();
    },
  },
});
