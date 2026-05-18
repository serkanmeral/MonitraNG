/**
 * Monitoring — mon_engines, mon_agents, mon_schedules, mon_collection_periods.
 * Ref: docs/content/monitoring_plans/MONITORING_AGENT_ARCHITECTURE.md, MONITORING_ENGINE_ARCHITECTURE.md
 */

/** mon_collection_periods */
export interface MonCollectionPeriod {
  __dataId: string;
  name: string;
  description?: string | null;
  expression: string;
}

/** mon_schedules — config: type=scheduled için weekdays, startTime, endTime */
export interface MonSchedule {
  __dataId: string;
  name: string;
  description?: string | null;
  type: 'always' | 'scheduled';
  config?: {
    weekdays?: number[];
    startTime?: string;
    endTime?: string;
  } | null;
}

/** mon_engines - lastErrors öğesi (Engine status'tan) */
export interface MonEngineError {
  assetId: string;
  agentId: string;
  errorCode: string;
  message: string;
  occurredAt: string;
}

/** mon_engines */
export interface MonEngine {
  __dataId: string;
  name: string;
  description?: string | null;
  status: string;
  domain?: string | null;
  username: string;
  password: string;
  sendSchedule: string;
  configSyncPeriodMinutes?: number | null;
  lastSeenAt?: string | null;
  /** ok | degraded | error — Engine status'tan */
  health?: string | null;
  /** Engine'in çalıştığı makinenin IP adresi (son bilinen) */
  hostAddress?: string | null;
  /** Son toplama hataları (Reactor EngineStatus'tan) */
  lastErrors?: MonEngineError[] | null;
}

/** mon_agents — asset_configs elemanı */
export interface MonAgentAssetConfig {
  assetId: string;
  periodId?: string | null;
  scheduleId?: string | null;
  active: boolean;
  description?: string | null;
}

/** mon_agents */
export interface MonAgent {
  __dataId: string;
  name: string;
  description?: string | null;
  status: string;
  engineId: string;
  defaultPeriodId?: string | null;
  defaultScheduleId?: string | null;
  tags?: Array<{ key: string; value: string }> | null;
  asset_configs: MonAgentAssetConfig[];
}
