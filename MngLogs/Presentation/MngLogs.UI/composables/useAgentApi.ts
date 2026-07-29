const API_BASE = '/api'

export interface AgentStatus {
  service: string
  startedAtUtc: string
  hostId: string
  hostname: string
  domain: string
  collectorBaseUrl: string
  collectorHealthy: boolean | null
  queuePending: number
  lastHeartbeatUtc?: string | null
  lastEventLogUtc?: string | null
  lastEventLogError?: string | null
  lastServiceWatchUtc?: string | null
  lastServiceWatchError?: string | null
  lastShipUtc?: string | null
  lastShipSuccessUtc?: string | null
  lastShipError?: string | null
  heartbeatsProduced: number
  metricEventsProduced: number
  eventLogEventsProduced: number
  serviceWatchEventsProduced: number
  eventsShipped: number
  metricsEnabled: boolean
  eventLogEnabled: boolean
  serviceWatchEnabled: boolean
  dataDirectory: string
  recent: string[]
}

export interface AgentConfig {
  system: {
    collectorBaseUrl: string
    apiKeyConfigured: boolean
    hostId: string
    localUiHost: string
    localUiPort: number
    dataDirectory: string
  }
  policy: PolicyConfig
}

export interface PolicyConfig {
  domain: string
  heartbeatIntervalSeconds: number
  shipIntervalSeconds: number
  maxEventsPerBatch: number
  metrics: { enabled: boolean; includeHostResources: boolean }
  eventLog: {
    enabled: boolean
    pollIntervalSeconds: number
    maxEventsPerPoll: number
    packages: { name: string; channel: string; eventIds: number[] }[]
  }
  serviceWatch: {
    enabled: boolean
    pollIntervalSeconds: number
    services: { name: string; restartAllowed: boolean }[]
  }
}

export interface QueueResponse {
  count: number
  items: {
    fileName: string
    timestampUtc?: string | null
    source: string
    severity?: string | null
    message?: string | null
    sourceProduct?: string | null
  }[]
}

export interface SourcesResponse {
  metrics: Record<string, unknown>
  eventLog: Record<string, unknown> & {
    packages?: { name: string; channel: string; eventIds: number[]; enabled: boolean }[]
    lastError?: string | null
  }
  serviceWatch: Record<string, unknown> & {
    configured?: { name: string; restartAllowed: boolean }[]
    snapshot?: {
      name: string
      displayName?: string | null
      health: string
      statusText?: string | null
      restartAllowed: boolean
      updatedAtUtc: string
    }[]
    lastError?: string | null
  }
  ship: Record<string, unknown>
}

export interface RecentEventEntry {
  atUtc: string
  direction: string
  source: string
  severity?: string | null
  message?: string | null
  action?: string | null
}

export function useAgentApi() {
  const getStatus = () => $fetch<AgentStatus>(`${API_BASE}/status`)
  const getConfig = () => $fetch<AgentConfig>(`${API_BASE}/config`)
  const getQueue = () => $fetch<QueueResponse>(`${API_BASE}/queue`)
  const getSources = () => $fetch<SourcesResponse>(`${API_BASE}/sources`)
  const getEvents = (direction = 'all', take = 100) =>
    $fetch<{ direction: string; items: RecentEventEntry[] }>(`${API_BASE}/events`, {
      params: { direction, take }
    })
  const clearEvents = () => $fetch<{ cleared: boolean }>(`${API_BASE}/events`, { method: 'DELETE' })
  const saveSystem = (body: { collectorBaseUrl?: string; apiKey?: string; hostId?: string }) =>
    $fetch<{ saved: boolean }>(`${API_BASE}/config/system`, { method: 'POST', body })
  const savePolicy = (policy: PolicyConfig) =>
    $fetch<{ saved: boolean }>(`${API_BASE}/config/policy`, { method: 'POST', body: policy })
  const getHealth = () => $fetch<{ status: string }>(`/health`)

  return {
    getStatus,
    getConfig,
    getQueue,
    getSources,
    getEvents,
    clearEvents,
    saveSystem,
    savePolicy,
    getHealth
  }
}

export function formatDate(value?: string | null) {
  if (!value) return '—'
  try {
    return new Date(value).toLocaleString('tr-TR')
  } catch {
    return value
  }
}
