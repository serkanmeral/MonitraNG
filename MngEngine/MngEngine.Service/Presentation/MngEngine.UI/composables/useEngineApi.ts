const API_BASE = '/api'

export interface ConfigStatus {
  hasConfig: boolean
  engineId?: string
  engineName?: string
  domain?: string
  serverUrl?: string
  mqttUrl?: string
  lastSyncAt?: string
  agentCount?: number
  assetConfigCount?: number
}

export interface EngineStatus {
  agentCount: number
  assetCount: number
  jobCount: number
  queueBatchCount?: number
}

export interface HealthResponse {
  status: string
}

export interface LogEntry {
  timestamp: string
  level: string
  message: string
  exception?: string
}

export interface AssetConfig {
  id?: string
  agentId: string
  assetId: string
  itemId?: string
  agentName?: string
  assetName?: string
  itemName?: string
  collectionMethod: string
  collectibles?: { code: string; enabled: boolean }[]
  connectionInfo?: Record<string, unknown> | null
  lastCollectedAt?: string | null
}

export interface JobAssetSchedule {
  assetId: string
  assetName: string
  agentName: string
  periodExpression?: string | null
}

export interface JobDetail {
  name: string
  group: string
  description: string
  cronExpression?: string | null
  nextFireTimeUtc?: string | null
  assets?: JobAssetSchedule[] | null
}

export interface QueueMetric {
  collectibleCode: string
  value: unknown
  unit?: string | null
}

export interface QueueBatch {
  assetId: string
  agentId: string
  itemId?: string
  agentName?: string
  assetName?: string
  itemName?: string
  collectedAt: string
  metricCount: number
  metrics?: QueueMetric[]
}

export interface QueueResponse {
  count: number
  items: QueueBatch[]
}

export function useEngineApi() {
  const applyConfig = async (configText: string) => {
    return $fetch<{ success: boolean }>(`${API_BASE}/config`, {
      method: 'POST',
      body: { configText }
    })
  }

  const deleteConfig = async () => {
    return $fetch<{ success: boolean; message?: string }>(`${API_BASE}/config`, {
      method: 'DELETE'
    })
  }

  const getConfigStatus = async () => {
    return $fetch<ConfigStatus>(`${API_BASE}/config/status`)
  }

  const triggerConfigSync = async () => {
    return $fetch<{ success: boolean; message?: string }>(`${API_BASE}/config/sync`, {
      method: 'POST'
    })
  }

  const getStatus = async () => {
    return $fetch<EngineStatus>(`${API_BASE}/status`)
  }

  const getHealth = async () => {
    return $fetch<HealthResponse>(`${API_BASE}/health`)
  }

  const getLogs = async (tail = 200) => {
    return $fetch<LogEntry[]>(`${API_BASE}/logs`, { params: { tail } })
  }

  const clearLogs = async () => {
    return $fetch<{ success: boolean }>(`${API_BASE}/logs`, { method: 'DELETE' })
  }

  const getAssets = async () => {
    return $fetch<AssetConfig[]>(`${API_BASE}/assets`)
  }

  const getJobs = async () => {
    return $fetch<JobDetail[]>(`${API_BASE}/jobs`)
  }

  const getQueue = async () => {
    return $fetch<QueueResponse>(`${API_BASE}/queue`)
  }

  return {
    applyConfig,
    deleteConfig,
    getConfigStatus,
    triggerConfigSync,
    getStatus,
    getHealth,
    getLogs,
    clearLogs,
    getAssets,
    getJobs,
    getQueue
  }
}
