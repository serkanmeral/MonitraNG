<template>
  <div class="space-y-6">
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Ajan durumu</h1>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Bağlantı özeti ve kaynak sağlığı
        </p>
      </div>
      <div class="flex items-center gap-3 shrink-0">
        <UToggle v-model="autoRefresh" size="sm" />
        <span class="text-sm text-gray-500">Otomatik yenile</span>
        <UButton size="sm" variant="outline" :loading="loading" icon="i-heroicons-arrow-path" @click="refresh">
          Yenile
        </UButton>
      </div>
    </div>

    <UAlert v-if="error" color="red" variant="soft" :title="error" />

    <!-- Compact status header -->
    <div
      class="rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 px-4 py-4 sm:px-5"
    >
      <div class="flex flex-col lg:flex-row lg:items-start lg:justify-between gap-4">
        <div class="min-w-0 space-y-1">
          <div class="flex flex-wrap items-center gap-2">
            <h2 class="text-lg font-semibold text-gray-900 dark:text-white truncate">
              {{ status?.hostname || '—' }}
            </h2>
            <span class="text-sm text-gray-500 font-mono truncate">{{ status?.hostId }}</span>
            <UBadge :color="healthColor" variant="soft" size="sm">{{ healthLabel }}</UBadge>
            <UBadge
              :color="status?.lastShipError ? 'red' : 'green'"
              variant="soft"
              size="sm"
            >
              {{ status?.lastShipError ? 'Gönderim hatası' : 'Gönderim tamam' }}
            </UBadge>
          </div>
          <p class="text-sm text-gray-500 dark:text-gray-400">
            <span class="font-mono">{{ status?.domain || '—' }}</span>
            <span class="mx-1.5 text-gray-300 dark:text-gray-600">·</span>
            <span class="font-mono break-all">{{ status?.collectorBaseUrl || '—' }}</span>
          </p>
        </div>

        <dl class="grid grid-cols-2 sm:grid-cols-4 gap-x-6 gap-y-2 text-sm shrink-0">
          <div>
            <dt class="text-xs text-gray-500">Kuyruk</dt>
            <dd class="font-semibold tabular-nums text-gray-900 dark:text-white">
              {{ status?.queuePending ?? '—' }}
            </dd>
          </div>
          <div>
            <dt class="text-xs text-gray-500">Son başarı</dt>
            <dd class="font-medium text-gray-900 dark:text-white">
              {{ formatRelativeTr(status?.lastShipSuccessUtc, nowMs) }}
            </dd>
          </div>
          <div>
            <dt class="text-xs text-gray-500">Gönderilen</dt>
            <dd class="font-medium tabular-nums text-gray-900 dark:text-white">
              {{ (status?.eventsShipped ?? 0).toLocaleString('tr-TR') }}
            </dd>
          </div>
          <div>
            <dt class="text-xs text-gray-500">Çalışma süresi</dt>
            <dd class="font-medium text-gray-900 dark:text-white">
              {{ formatUptime(status?.startedAtUtc, nowMs) }}
            </dd>
          </div>
        </dl>
      </div>

      <UAlert
        v-if="status?.lastShipError"
        class="mt-3"
        color="red"
        variant="soft"
        :title="status.lastShipError"
      />

      <details class="mt-3 group">
        <summary
          class="text-xs text-gray-500 cursor-pointer select-none hover:text-gray-700 dark:hover:text-gray-300 list-none flex items-center gap-1"
        >
          <UIcon
            name="i-heroicons-chevron-right"
            class="w-3.5 h-3.5 transition-transform group-open:rotate-90"
          />
          Teknik ayrıntılar
        </summary>
        <div class="mt-2 pl-5 text-xs text-gray-500 space-y-1 font-mono break-all">
          <p>Başlangıç (UTC): {{ formatDate(status?.startedAtUtc) }}</p>
          <p>Veri dizini: {{ status?.dataDirectory || '—' }}</p>
          <p>Son gönderim denemesi: {{ formatDate(status?.lastShipUtc) }}</p>
        </div>
      </details>
    </div>

    <!-- Source tabs -->
    <UTabs
      v-model="activeTab"
      :items="tabItems"
      :ui="{ list: { width: 'w-full' } }"
      class="w-full"
    >
      <template #metrics>
        <div class="pt-4 space-y-5">
          <div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
            <UBadge size="sm" variant="soft" :color="freshnessColor(metricTile.freshness)">
              {{ freshnessLabel(metricTile.freshness) }}
            </UBadge>
            <span class="text-gray-500">
              Son okuma:
              <span class="text-gray-800 dark:text-gray-200">{{ metricTile.lastReadLabel }}</span>
            </span>
            <span class="text-gray-500">
              Son gönderim:
              <span class="text-gray-800 dark:text-gray-200">{{ metricTile.lastShipLabel }}</span>
            </span>
            <span v-if="metricTile.hint" class="text-xs text-gray-400 w-full sm:w-auto">
              {{ metricTile.hint }}
            </span>
          </div>

          <section>
            <h3 class="text-sm font-semibold text-gray-900 dark:text-white mb-3">Son metrikler</h3>
            <div v-if="!(status?.latestMetrics?.length)" class="text-sm text-gray-500 py-6 text-center rounded-lg border border-dashed border-gray-200 dark:border-gray-700">
              Metrik verisi bekleniyor (kalp atışı sonrası dolar).
            </div>
            <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
              <div
                v-for="m in status.latestMetrics"
                :key="`${m.name}-${m.detail || ''}`"
                class="rounded-lg border border-gray-200 dark:border-gray-700 bg-gray-50/80 dark:bg-gray-800/40 px-3 py-3"
              >
                <p class="text-xs text-gray-500 dark:text-gray-400">
                  {{ metricLabel(m.name) }}
                  <span v-if="m.detail" class="font-mono"> · {{ m.detail }}</span>
                </p>
                <p class="mt-1 text-xl font-semibold tabular-nums text-gray-900 dark:text-white">
                  {{ formatMetricValue(m.name, m.value) }}
                </p>
                <p class="mt-1 text-xs text-gray-400">{{ formatRelativeTr(m.atUtc, nowMs) }}</p>
              </div>
            </div>
          </section>

          <section>
            <div class="flex items-center justify-between gap-2 mb-3 flex-wrap">
              <h3 class="text-sm font-semibold text-gray-900 dark:text-white">
                En çok kaynak kullanan süreçler
              </h3>
              <span class="text-xs text-gray-500">
                {{
                  status?.topProcesses?.atUtc
                    ? `Merkez + yerel · ${formatRelativeTr(status.topProcesses.atUtc, nowMs)}`
                    : 'Kalp atışında yerel + toplayıcıya özet'
                }}
              </span>
            </div>

            <div
              v-if="status?.includeTopProcesses === false"
              class="text-sm text-gray-500 py-4"
            >
              Politika: üst süreç listesi kapalı.
            </div>
            <div
              v-else-if="!status?.topProcesses"
              class="text-sm text-gray-500 py-6 text-center rounded-lg border border-dashed border-gray-200 dark:border-gray-700"
            >
              İlk kalp atışı sonrası dolar.
            </div>
            <div
              v-else
              class="grid grid-cols-1 lg:grid-cols-2 gap-6 rounded-lg border border-gray-200 dark:border-gray-700 p-4"
            >
              <div>
                <p class="text-xs font-medium text-gray-500 uppercase tracking-wide mb-2">İşlemci</p>
                <p v-if="status.topProcesses.cpuPending" class="text-sm text-gray-500 mb-2">
                  CPU sıralaması bir sonraki kalp atışında hazır olur.
                </p>
                <div v-else-if="!status.topProcesses.byCpu?.length" class="text-sm text-gray-500">
                  Veri yok
                </div>
                <table v-else class="w-full text-sm">
                  <thead>
                    <tr class="text-left text-xs text-gray-500 border-b border-gray-100 dark:border-gray-700">
                      <th class="py-1.5 pr-2">
                        <button type="button" class="font-medium hover:text-gray-800 dark:hover:text-gray-200" @click="toggleCpuSort('name')">
                          Süreç{{ sortMark(cpuSort, 'name') }}
                        </button>
                      </th>
                      <th class="py-1.5 pr-2">
                        <button type="button" class="font-medium hover:text-gray-800 dark:hover:text-gray-200" @click="toggleCpuSort('pid')">
                          PID{{ sortMark(cpuSort, 'pid') }}
                        </button>
                      </th>
                      <th class="py-1.5 text-right">
                        <button type="button" class="font-medium hover:text-gray-800 dark:hover:text-gray-200" @click="toggleCpuSort('cpu')">
                          CPU{{ sortMark(cpuSort, 'cpu') }}
                        </button>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="p in sortedCpuRows"
                      :key="`cpu-${p.pid}`"
                      class="border-b border-gray-50 dark:border-gray-800 last:border-0"
                    >
                      <td class="py-2 pr-2 font-medium truncate max-w-[10rem]" :title="p.name">
                        {{ p.name }}
                      </td>
                      <td class="py-2 pr-2 text-gray-500 tabular-nums">{{ p.pid }}</td>
                      <td class="py-2 text-right tabular-nums">
                        %{{ (p.cpuPercent ?? 0).toLocaleString('tr-TR', { maximumFractionDigits: 1 }) }}
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <div>
                <p class="text-xs font-medium text-gray-500 uppercase tracking-wide mb-2">Bellek</p>
                <div v-if="!status.topProcesses.byMemory?.length" class="text-sm text-gray-500">
                  Veri yok
                </div>
                <table v-else class="w-full text-sm">
                  <thead>
                    <tr class="text-left text-xs text-gray-500 border-b border-gray-100 dark:border-gray-700">
                      <th class="py-1.5 pr-2">
                        <button type="button" class="font-medium hover:text-gray-800 dark:hover:text-gray-200" @click="toggleMemSort('name')">
                          Süreç{{ sortMark(memSort, 'name') }}
                        </button>
                      </th>
                      <th class="py-1.5 pr-2">
                        <button type="button" class="font-medium hover:text-gray-800 dark:hover:text-gray-200" @click="toggleMemSort('pid')">
                          PID{{ sortMark(memSort, 'pid') }}
                        </button>
                      </th>
                      <th class="py-1.5 text-right">
                        <button type="button" class="font-medium hover:text-gray-800 dark:hover:text-gray-200" @click="toggleMemSort('ram')">
                          RAM{{ sortMark(memSort, 'ram') }}
                        </button>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="p in sortedMemRows"
                      :key="`mem-${p.pid}`"
                      class="border-b border-gray-50 dark:border-gray-800 last:border-0"
                    >
                      <td class="py-2 pr-2 font-medium truncate max-w-[10rem]" :title="p.name">
                        {{ p.name }}
                      </td>
                      <td class="py-2 pr-2 text-gray-500 tabular-nums">{{ p.pid }}</td>
                      <td class="py-2 text-right tabular-nums">{{ formatBytes(p.workingSetBytes) }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </section>

          <p
            v-if="status?.metricsEnabled && metricTile.produced != null"
            class="text-[11px] text-gray-400 tabular-nums"
            title="Ajan ömrü boyunca üretilen olay sayısı (debug)"
          >
            Üretilen (ömür): {{ metricTile.produced.toLocaleString('tr-TR') }}
          </p>
        </div>
      </template>

      <template #eventlog>
        <div class="pt-4 space-y-5">
          <div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
            <UBadge size="sm" variant="soft" :color="freshnessColor(eventTile.freshness)">
              {{ freshnessLabel(eventTile.freshness) }}
            </UBadge>
            <span class="text-gray-500">
              Son okuma:
              <span class="text-gray-800 dark:text-gray-200">{{ eventTile.lastReadLabel }}</span>
            </span>
            <span class="text-gray-500">
              Son gönderim:
              <span class="text-gray-800 dark:text-gray-200">{{ eventTile.lastShipLabel }}</span>
            </span>
          </div>

          <UAlert
            v-if="eventTile.hint"
            :color="status?.lastEventLogError ? 'amber' : 'gray'"
            variant="soft"
            :title="eventTile.hint"
          />

          <section>
            <div class="flex items-center justify-between gap-2 mb-3">
              <h3 class="text-sm font-semibold text-gray-900 dark:text-white">Son olaylar</h3>
              <NuxtLink
                to="/logs"
                class="text-sm text-primary-600 dark:text-primary-400 hover:underline"
              >
                Tümünü gör
              </NuxtLink>
            </div>
            <div
              v-if="!eventLogRows.length"
              class="text-sm text-gray-500 py-6 text-center rounded-lg border border-dashed border-gray-200 dark:border-gray-700"
            >
              Bu sekmede gösterilecek olay yok. Event Log kapalıysa veya henüz geçiş yoksa normal.
            </div>
            <div v-else class="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
              <table class="w-full divide-y divide-gray-200 dark:divide-gray-700">
                <thead class="bg-gray-50 dark:bg-gray-800/80">
                  <tr>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Zaman</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Özet</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Detay</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr v-for="(e, i) in eventLogRows" :key="i">
                    <td class="px-3 py-2 text-sm text-gray-500 whitespace-nowrap">
                      {{ formatRelativeTr(e.atUtc, nowMs) }}
                    </td>
                    <td class="px-3 py-2 text-sm font-medium">{{ e.action || e.message || '—' }}</td>
                    <td class="px-3 py-2 text-sm text-gray-500">{{ e.detail || '—' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>
        </div>
      </template>

      <template #services>
        <div class="pt-4 space-y-5">
          <div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
            <UBadge size="sm" variant="soft" :color="freshnessColor(serviceTile.freshness)">
              {{ freshnessLabel(serviceTile.freshness) }}
            </UBadge>
            <span class="text-gray-500">
              Son okuma:
              <span class="text-gray-800 dark:text-gray-200">{{ serviceTile.lastReadLabel }}</span>
            </span>
            <span class="text-gray-500">
              Son gönderim:
              <span class="text-gray-800 dark:text-gray-200">{{ serviceTile.lastShipLabel }}</span>
            </span>
          </div>

          <UAlert
            v-if="serviceTile.hint"
            :color="status?.lastServiceWatchError ? 'amber' : 'gray'"
            variant="soft"
            :title="serviceTile.hint"
          />

          <section>
            <div class="flex items-center justify-between gap-2 mb-3">
              <h3 class="text-sm font-semibold text-gray-900 dark:text-white">Son servis olayları</h3>
              <NuxtLink
                to="/sources"
                class="text-sm text-primary-600 dark:text-primary-400 hover:underline"
              >
                Kaynaklar
              </NuxtLink>
            </div>
            <div
              v-if="!serviceRows.length"
              class="text-sm text-gray-500 py-6 text-center rounded-lg border border-dashed border-gray-200 dark:border-gray-700"
            >
              Servis izleme olayı yok. Ayrıntılı anlık görüntü için Kaynaklar sekmesine bakın.
            </div>
            <div v-else class="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
              <table class="w-full divide-y divide-gray-200 dark:divide-gray-700">
                <thead class="bg-gray-50 dark:bg-gray-800/80">
                  <tr>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Zaman</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Özet</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Detay</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr v-for="(e, i) in serviceRows" :key="i">
                    <td class="px-3 py-2 text-sm text-gray-500 whitespace-nowrap">
                      {{ formatRelativeTr(e.atUtc, nowMs) }}
                    </td>
                    <td class="px-3 py-2 text-sm font-medium">{{ e.action || e.message || '—' }}</td>
                    <td class="px-3 py-2 text-sm text-gray-500">{{ e.detail || '—' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>
        </div>
      </template>

      <template #activity>
        <div class="pt-4">
          <p class="text-xs text-gray-500 mb-3">
            Ajan iç notları (debug). Operasyon için yukarıdaki sekmeler yeterlidir.
          </p>
          <div
            v-if="!status?.recent?.length"
            class="text-sm text-gray-500 py-6 text-center rounded-lg border border-dashed border-gray-200 dark:border-gray-700"
          >
            Henüz kayıt yok
          </div>
          <ul
            v-else
            class="font-mono text-xs space-y-1.5 max-h-80 overflow-auto rounded-lg border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/40 p-3"
          >
            <li
              v-for="(line, i) in status.recent.slice().reverse()"
              :key="i"
              class="text-gray-700 dark:text-gray-300"
            >
              {{ line }}
            </li>
          </ul>
        </div>
      </template>
    </UTabs>
  </div>
</template>

<script setup lang="ts">
import type { AgentStatus, FreshnessKind, TopProcessItem } from '~/composables/useAgentApi'
import {
  formatBytes,
  formatDate,
  formatMetricValue,
  formatRelativeTr,
  freshnessColor,
  freshnessLabel,
  freshnessOf,
  metricLabel
} from '~/composables/useAgentApi'

const { getStatus } = useAgentApi()

const status = ref<AgentStatus | null>(null)
const loading = ref(false)
const error = ref('')
const autoRefresh = ref(true)
const activeTab = ref(0)
const nowMs = ref(Date.now())

type SortDir = 'asc' | 'desc'
type CpuSortKey = 'name' | 'pid' | 'cpu'
type MemSortKey = 'name' | 'pid' | 'ram'

const cpuSort = ref<{ key: CpuSortKey; dir: SortDir }>({ key: 'cpu', dir: 'desc' })
const memSort = ref<{ key: MemSortKey; dir: SortDir }>({ key: 'ram', dir: 'desc' })

let timer: ReturnType<typeof setInterval> | null = null
let clock: ReturnType<typeof setInterval> | null = null

const healthColor = computed(() => {
  if (status.value?.collectorHealthy === true) return 'green'
  if (status.value?.collectorHealthy === false) return 'red'
  return 'gray'
})

const healthLabel = computed(() => {
  if (status.value?.collectorHealthy === true) return 'Toplayıcı sağlıklı'
  if (status.value?.collectorHealthy === false) return 'Toplayıcıya erişilemiyor'
  return 'Bilinmiyor'
})

function buildTile(
  label: string,
  enabled: boolean,
  lastUtc: string | null | undefined,
  interval: number | undefined,
  produced: number,
  hint: string | null
) {
  const n = nowMs.value
  const freshness = freshnessOf(enabled, lastUtc, interval, n)
  return {
    label,
    enabled,
    freshness,
    lastReadLabel: enabled ? formatRelativeTr(lastUtc, n) : '—',
    lastShipLabel: enabled ? formatRelativeTr(status.value?.lastShipSuccessUtc, n) : '—',
    produced,
    hint
  }
}

const metricTile = computed(() => {
  const s = status.value
  const freshness = freshnessOf(
    !!s?.metricsEnabled,
    s?.lastHeartbeatUtc,
    s?.heartbeatIntervalSeconds,
    nowMs.value
  )
  return buildTile(
    'Metrik',
    !!s?.metricsEnabled,
    s?.lastHeartbeatUtc,
    s?.heartbeatIntervalSeconds,
    s?.metricEventsProduced ?? 0,
    metricHint(s, freshness)
  )
})

const eventTile = computed(() => {
  const s = status.value
  const freshness = freshnessOf(
    !!s?.eventLogEnabled,
    s?.lastEventLogUtc,
    s?.eventLogPollIntervalSeconds,
    nowMs.value
  )
  return buildTile(
    'Olay günlüğü',
    !!s?.eventLogEnabled,
    s?.lastEventLogUtc,
    s?.eventLogPollIntervalSeconds,
    s?.eventLogEventsProduced ?? 0,
    eventLogHint(s, freshness)
  )
})

const serviceTile = computed(() => {
  const s = status.value
  const freshness = freshnessOf(
    !!s?.serviceWatchEnabled,
    s?.lastServiceWatchUtc,
    s?.serviceWatchPollIntervalSeconds,
    nowMs.value
  )
  return buildTile(
    'Servis izleme',
    !!s?.serviceWatchEnabled,
    s?.lastServiceWatchUtc,
    s?.serviceWatchPollIntervalSeconds,
    s?.serviceWatchEventsProduced ?? 0,
    serviceWatchHint(s, freshness)
  )
})

const tabItems = computed(() => [
  {
    key: 'metrics',
    label: 'Metrik',
    slot: 'metrics',
    badge: freshnessLabel(metricTile.value.freshness)
  },
  {
    key: 'eventlog',
    label: 'Olay günlüğü',
    slot: 'eventlog',
    badge: freshnessLabel(eventTile.value.freshness)
  },
  {
    key: 'services',
    label: 'Servis izleme',
    slot: 'services',
    badge: freshnessLabel(serviceTile.value.freshness)
  },
  {
    key: 'activity',
    label: 'Aktivite',
    slot: 'activity'
  }
])

const eventLogRows = computed(() =>
  (status.value?.latestLogs || []).filter(
    e =>
      e.source === 'event-log' ||
      e.source === 'windows-eventlog'
  )
)

const serviceRows = computed(() =>
  (status.value?.latestLogs || []).filter(e => e.source === 'service-watch')
)

const sortedCpuRows = computed(() =>
  sortProcesses(status.value?.topProcesses?.byCpu || [], cpuSort.value.key, cpuSort.value.dir)
)

const sortedMemRows = computed(() =>
  sortProcesses(status.value?.topProcesses?.byMemory || [], memSort.value.key, memSort.value.dir)
)

function sortMark(state: { key: string; dir: SortDir }, key: string) {
  if (state.key !== key) return ''
  return state.dir === 'asc' ? ' ↑' : ' ↓'
}

function toggleCpuSort(key: CpuSortKey) {
  if (cpuSort.value.key === key) {
    cpuSort.value = { key, dir: cpuSort.value.dir === 'asc' ? 'desc' : 'asc' }
  } else {
    cpuSort.value = { key, dir: key === 'name' ? 'asc' : 'desc' }
  }
}

function toggleMemSort(key: MemSortKey) {
  if (memSort.value.key === key) {
    memSort.value = { key, dir: memSort.value.dir === 'asc' ? 'desc' : 'asc' }
  } else {
    memSort.value = { key, dir: key === 'name' ? 'asc' : 'desc' }
  }
}

function sortProcesses(
  rows: TopProcessItem[],
  key: CpuSortKey | MemSortKey,
  dir: SortDir
): TopProcessItem[] {
  const mul = dir === 'asc' ? 1 : -1
  return [...rows].sort((a, b) => {
    let cmp = 0
    if (key === 'name') {
      cmp = a.name.localeCompare(b.name, 'tr', { sensitivity: 'base' })
    } else if (key === 'pid') {
      cmp = a.pid - b.pid
    } else if (key === 'cpu') {
      cmp = (a.cpuPercent ?? 0) - (b.cpuPercent ?? 0)
    } else {
      cmp = a.workingSetBytes - b.workingSetBytes
    }
    return cmp * mul
  })
}

function metricHint(s: AgentStatus | null, freshness: FreshnessKind) {
  if (!s?.metricsEnabled) return 'Politika: metrik kapalı'
  if (freshness === 'none') return 'Kalp atışı döngüsü bekleniyor'
  const up = s.latestMetrics?.find(m => m.name === 'up')
  const cpu = s.latestMetrics?.find(m => m.name === 'cpu.percent')
  const parts: string[] = []
  if (up) parts.push(up.value >= 1 ? 'up=1' : 'up=0')
  if (cpu) parts.push(`CPU %${cpu.value.toLocaleString('tr-TR', { maximumFractionDigits: 0 })}`)
  if (s.heartbeatsProduced)
    parts.push(`${s.heartbeatsProduced.toLocaleString('tr-TR')} kalp atışı`)
  return parts.length ? parts.join(' · ') : null
}

function eventLogHint(s: AgentStatus | null, freshness: FreshnessKind) {
  if (!s?.eventLogEnabled) return 'Politika: olay günlüğü kapalı (admin gerekir)'
  if (s.lastEventLogError) return s.lastEventLogError
  if (freshness === 'none') return 'Henüz olay toplanmadı'
  return null
}

function serviceWatchHint(s: AgentStatus | null, freshness: FreshnessKind) {
  if (!s?.serviceWatchEnabled) return 'Politika: servis izleme kapalı'
  if (s.lastServiceWatchError) return s.lastServiceWatchError
  if (freshness === 'none') return 'İlk tarama bekleniyor'
  return null
}

function formatUptime(startedAt?: string | null, now = Date.now()) {
  if (!startedAt) return '—'
  const t = new Date(startedAt).getTime()
  if (Number.isNaN(t)) return '—'
  const sec = Math.max(0, Math.floor((now - t) / 1000))
  if (sec < 60) return `${sec} sn`
  const min = Math.floor(sec / 60)
  if (min < 60) return `${min} dk`
  const hr = Math.floor(min / 60)
  const remMin = min % 60
  if (hr < 48) return remMin ? `${hr} sa ${remMin} dk` : `${hr} sa`
  const day = Math.floor(hr / 24)
  return `${day} g`
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    status.value = await getStatus()
    nowMs.value = Date.now()
  } catch (e: any) {
    error.value = e?.message || 'Durum alınamadı'
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await refresh()
  timer = setInterval(() => {
    if (autoRefresh.value) refresh()
  }, 8000)
  clock = setInterval(() => {
    nowMs.value = Date.now()
  }, 1000)
})

onUnmounted(() => {
  if (timer) clearInterval(timer)
  if (clock) clearInterval(clock)
})
</script>
