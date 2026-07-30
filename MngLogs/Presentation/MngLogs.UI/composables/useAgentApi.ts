const API_BASE = '/api'

export interface LatestMetricItem {
  name: string
  value: number
  message?: string | null
  detail?: string | null
  atUtc: string
}

export interface RecentEventEntry {
  atUtc: string
  direction: string
  source: string
  severity?: string | null
  message?: string | null
  action?: string | null
  metricName?: string | null
  metricValue?: number | null
  detail?: string | null
  id?: string | null
  channel?: string | null
  package?: string | null
  eventId?: number | null
  recordId?: number | null
  provider?: string | null
  rawJson?: string | null
  fields?: Record<string, unknown> | null
}

export interface TopProcessItem {
  pid: number
  name: string
  cpuPercent?: number | null
  workingSetBytes: number
}

export interface TopProcessSnapshot {
  atUtc: string
  byCpu: TopProcessItem[]
  byMemory: TopProcessItem[]
  cpuPending?: boolean
}

export interface WatchSnapshotItem {
  kind: string
  name: string
  displayName?: string | null
  health: string
  statusText?: string | null
  restartAllowed: boolean
  instanceCount?: number | null
  minCount?: number | null
  updatedAtUtc: string
  lastOsEventId?: number | null
  lastOsEventAtUtc?: string | null
  lastOsEventAction?: string | null
  lastOsEventMessage?: string | null
  lastRestartAtUtc?: string | null
  lastRestartOk?: boolean | null
  lastRestartError?: string | null
  restartAttemptCount?: number
}

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
  includeTopProcesses?: boolean
  heartbeatIntervalSeconds?: number
  eventLogPollIntervalSeconds?: number
  serviceWatchPollIntervalSeconds?: number
  dataDirectory: string
  recent: string[]
  latestMetrics?: LatestMetricItem[]
  latestLogs?: RecentEventEntry[]
  topProcesses?: TopProcessSnapshot | null
  watchSnapshot?: WatchSnapshotItem[]
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
  metrics: { enabled: boolean; includeHostResources: boolean; includeTopProcesses?: boolean; topProcessCount?: number }
  eventLog: {
    enabled: boolean
    pollIntervalSeconds: number
    maxEventsPerPoll: number
    packages: { name: string; channel: string; eventIds: number[] }[]
    agentOverrides?: { name: string; channel: string; eventIds: number[] }[]
    disabledServerPackages?: string[]
    packageCatalogSyncIntervalSeconds?: number
  }
  serviceWatch: {
    enabled: boolean
    pollIntervalSeconds: number
    restartCooldownSeconds?: number
    restartMaxAttempts?: number
    includeInventory?: boolean
    inventoryIntervalSeconds?: number
    services: { name: string; restartAllowed: boolean }[]
    applications?: {
      name: string
      minCount: number
      restartAllowed?: boolean
      executablePath?: string | null
      arguments?: string | null
      workingDirectory?: string | null
    }[]
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
  readOnly?: boolean
  producers?: {
    sourceType: string
    label: string
    enabled: boolean
    intervalSeconds?: number
    lastUtc?: string | null
    eventsProduced?: number
    lastError?: string | null
  }[]
  metrics: Record<string, unknown> & {
    enabled?: boolean
    heartbeatIntervalSeconds?: number
    includeHostResources?: boolean
    includeTopProcesses?: boolean
    topProcessCount?: number
    eventsProduced?: number
    lastHeartbeatUtc?: string | null
    definitions?: {
      name: string
      metric: string
      action: string
      sourceType: string
      description: string
      intervalSeconds: number
      enabled: boolean
    }[]
  }
  eventLog: Record<string, unknown> & {
    packages?: { name: string; channel: string; eventIds: number[]; enabled: boolean }[]
    knownOptional?: { name: string; channel: string; eventIds: number[]; requiresElevation?: boolean }[]
    lastError?: string | null
    pollIntervalSeconds?: number
    maxEventsPerPoll?: number
    eventsProduced?: number
    lastEventLogUtc?: string | null
    enabled?: boolean
  }
  serviceWatch: Record<string, unknown> & {
    enabled?: boolean
    pollIntervalSeconds?: number
    restartCooldownSeconds?: number
    restartMaxAttempts?: number
    includeInventory?: boolean
    inventoryIntervalSeconds?: number
    configured?: { name: string; restartAllowed: boolean }[]
    applications?: {
      name: string
      minCount: number
      restartAllowed?: boolean
      executablePath?: string | null
      arguments?: string | null
      workingDirectory?: string | null
    }[]
    snapshot?: {
      kind?: string
      name: string
      displayName?: string | null
      health: string
      statusText?: string | null
      restartAllowed: boolean
      instanceCount?: number | null
      minCount?: number | null
      updatedAtUtc: string
      lastOsEventId?: number | null
      lastOsEventAtUtc?: string | null
      lastOsEventAction?: string | null
      lastOsEventMessage?: string | null
      lastRestartAtUtc?: string | null
      lastRestartOk?: boolean | null
      lastRestartError?: string | null
      restartAttemptCount?: number
    }[]
    lastError?: string | null
  }
  ship: Record<string, unknown>
}

export function useAgentApi() {
  const TOKEN_KEY = 'mnglogs-local-ui-token'
  const TOKEN_HEADER = 'X-Local-Ui-Token'

  function getStoredToken(): string | null {
    if (!import.meta.client) return null
    try {
      return sessionStorage.getItem(TOKEN_KEY)
    } catch {
      return null
    }
  }

  function setStoredToken(token: string | null) {
    if (!import.meta.client) return
    try {
      if (token) sessionStorage.setItem(TOKEN_KEY, token)
      else sessionStorage.removeItem(TOKEN_KEY)
    } catch {
      /* ignore */
    }
  }

  function authHeaders(): Record<string, string> {
    const token = getStoredToken()
    return token ? { [TOKEN_HEADER]: token } : {}
  }

  function rememberAuthResult(res: { ok?: boolean; token?: string | null }) {
    if (res?.ok && res.token) setStoredToken(res.token)
  }

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
    $fetch<{ saved: boolean }>(`${API_BASE}/config/system`, {
      method: 'POST',
      body,
      headers: authHeaders()
    })
  const savePolicy = (policy: PolicyConfig) =>
    $fetch<{ saved: boolean }>(`${API_BASE}/config/policy`, {
      method: 'POST',
      body: policy,
      headers: authHeaders()
    })
  const getHealth = () => $fetch<{ status: string }>(`/health`)
  const getHostServices = () =>
    $fetch<{ items: HostServiceItem[]; count: number; error?: string }>(`${API_BASE}/host/services`, {
      headers: authHeaders()
    })
  const getKnownEventLogPackages = () =>
    $fetch<KnownEventLogPackagesResponse>(`${API_BASE}/eventlog/known-packages`)
  const getEventLogPackagePlan = () =>
    $fetch<EventLogPackagePlan>(`${API_BASE}/eventlog/package-plan`)
  const syncEventLogCatalog = () =>
    $fetch<{ synced: boolean; source: string; lastSyncedUtc?: string; count: number }>(
      `${API_BASE}/eventlog/sync-catalog`,
      { method: 'POST', headers: authHeaders() }
    )
  /** Opens a native OpenFileDialog on the agent host; may take a long time while the dialog is open. */
  const browseExecutable = () =>
    $fetch<BrowseExecutableResult>(`${API_BASE}/host/browse-executable`, {
      method: 'POST',
      timeout: 300_000,
      headers: authHeaders()
    })

  const getAuthStatus = () =>
    $fetch<LocalUiAuthStatus>(`${API_BASE}/auth/status`, { headers: authHeaders() })

  const setupPin = async (pin: string, pinConfirm: string) => {
    const res = await $fetch<LocalUiAuthResult>(`${API_BASE}/auth/setup`, {
      method: 'POST',
      body: { pin, pinConfirm }
    })
    rememberAuthResult(res)
    return res
  }

  const unlockPin = async (pin: string) => {
    const res = await $fetch<LocalUiAuthResult>(`${API_BASE}/auth/unlock`, {
      method: 'POST',
      body: { pin }
    })
    rememberAuthResult(res)
    return res
  }

  const lockPin = async () => {
    try {
      await $fetch(`${API_BASE}/auth/lock`, { method: 'POST', headers: authHeaders() })
    } finally {
      setStoredToken(null)
    }
  }

  const changePin = (currentPin: string, newPin: string, newPinConfirm: string) =>
    $fetch<LocalUiAuthResult>(`${API_BASE}/auth/change-pin`, {
      method: 'POST',
      body: { currentPin, newPin, newPinConfirm },
      headers: authHeaders()
    })

  return {
    getStatus,
    getConfig,
    getQueue,
    getSources,
    getEvents,
    clearEvents,
    saveSystem,
    savePolicy,
    getHealth,
    getHostServices,
    getKnownEventLogPackages,
    getEventLogPackagePlan,
    syncEventLogCatalog,
    browseExecutable,
    getAuthStatus,
    setupPin,
    unlockPin,
    lockPin,
    changePin,
    getStoredToken,
    setStoredToken,
    formatDate,
    formatRelativeTr
  }
}

export interface LocalUiAuthStatus {
  configured: boolean
  unlocked: boolean
  sessionExpiresAtUtc?: string | null
  lockedUntilUtc?: string | null
  failedAttempts?: number
  sessionTtlSeconds?: number
  minPinLength?: number
}

export interface LocalUiAuthResult {
  ok: boolean
  error?: string | null
  token?: string | null
  expiresAtUtc?: string | null
  lockedUntilUtc?: string | null
}

export interface HostServiceItem {
  name: string
  displayName: string
  status: string
}

export interface KnownEventLogPackage {
  name: string
  channel: string
  eventIds: number[]
  optional?: boolean
}

export interface KnownEventLogPackagesResponse {
  defaults: KnownEventLogPackage[]
  optional: KnownEventLogPackage[]
  all: KnownEventLogPackage[]
  source?: string
  lastSyncedUtc?: string | null
}

export interface EventLogPackagePlan {
  source: string
  lastSyncedUtc?: string | null
  syncIntervalSeconds: number
  legacyMode: boolean
  server: { name: string; channel: string; eventIds: number[]; disabled: boolean }[]
  optional: KnownEventLogPackage[]
  agentOverrides: { name: string; channel: string; eventIds: number[] }[]
  disabledServerPackages: string[]
  effective: { name: string; channel: string; eventIds: number[] }[]
}

export interface BrowseExecutableResult {
  cancelled: boolean
  path?: string
  processName?: string
  fileName?: string
  directory?: string | null
  error?: string
}

export function formatDate(value?: string | null) {
  if (!value) return '—'
  try {
    return new Date(value).toLocaleString('tr-TR')
  } catch {
    return value
  }
}

/** Relative age in Turkish, e.g. "34 sn önce". */
export function formatRelativeTr(value?: string | null, nowMs = Date.now()) {
  if (!value) return '—'
  const t = new Date(value).getTime()
  if (Number.isNaN(t)) return '—'
  const sec = Math.max(0, Math.floor((nowMs - t) / 1000))
  if (sec < 5) return 'az önce'
  if (sec < 60) return `${sec} sn önce`
  const min = Math.floor(sec / 60)
  if (min < 60) return `${min} dk önce`
  const hr = Math.floor(min / 60)
  if (hr < 48) return `${hr} sa önce`
  const day = Math.floor(hr / 24)
  return `${day} g önce`
}

export type FreshnessKind = 'off' | 'none' | 'fresh' | 'late' | 'stale'

/** fresh < 2×interval, late < 4×interval, else stale. */
export function freshnessOf(
  enabled: boolean,
  lastUtc: string | null | undefined,
  intervalSeconds: number | undefined,
  nowMs = Date.now()
): FreshnessKind {
  if (!enabled) return 'off'
  if (!lastUtc) return 'none'
  const t = new Date(lastUtc).getTime()
  if (Number.isNaN(t)) return 'none'
  const ageSec = Math.max(0, (nowMs - t) / 1000)
  const interval = Math.max(5, intervalSeconds ?? 60)
  if (ageSec <= interval * 2) return 'fresh'
  if (ageSec <= interval * 4) return 'late'
  return 'stale'
}

export function freshnessLabel(kind: FreshnessKind) {
  switch (kind) {
    case 'off':
      return 'Kapalı'
    case 'none':
      return 'Henüz yok'
    case 'fresh':
      return 'Taze'
    case 'late':
      return 'Gecikmeli'
    case 'stale':
      return 'Eski'
  }
}

export function freshnessColor(kind: FreshnessKind): 'gray' | 'green' | 'amber' | 'red' {
  switch (kind) {
    case 'off':
      return 'gray'
    case 'none':
      return 'gray'
    case 'fresh':
      return 'green'
    case 'late':
      return 'amber'
    case 'stale':
      return 'red'
  }
}

export function formatMetricValue(name: string, value: number) {
  if (name === 'up') return value >= 1 ? 'Açık (1)' : 'Kapalı (0)'
  if (name === 'cpu.percent') return `%${value.toLocaleString('tr-TR', { maximumFractionDigits: 1 })}`
  if (name.includes('bytes')) {
    return formatBytes(value)
  }
  return value.toLocaleString('tr-TR')
}

export function formatBytes(value: number) {
  const gb = value / (1024 * 1024 * 1024)
  if (Math.abs(gb) >= 1)
    return `${gb.toLocaleString('tr-TR', { maximumFractionDigits: 2 })} GB`
  const mb = value / (1024 * 1024)
  return `${mb.toLocaleString('tr-TR', { maximumFractionDigits: 1 })} MB`
}

export function metricLabel(name: string) {
  const map: Record<string, string> = {
    up: 'Ana bilgisayar (up)',
    'cpu.percent': 'İşlemci',
    'memory.available_bytes': 'Boş bellek',
    'memory.process_working_set_bytes': 'Ajan bellek kullanımı',
    'disk.free_bytes': 'Boş disk',
    'disk.total_bytes': 'Toplam disk'
  }
  return map[name] || name
}

export function sourceLabel(s: string) {
  const map: Record<string, string> = {
    metric: 'metrik',
    'event-log': 'olay günlüğü',
    'windows-eventlog': 'olay günlüğü',
    'service-watch': 'servis izleme',
    unknown: 'bilinmiyor'
  }
  return map[s] || s
}
