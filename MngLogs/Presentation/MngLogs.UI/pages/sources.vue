<template>
  <div class="space-y-6">
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Kaynaklar</h1>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Toplanan tüm verilerin salt-okunur yapılandırması ve anlık durum.
          Politika ekranına erişim olmadan da buradan config görülebilir.
        </p>
      </div>
      <UButton size="sm" variant="outline" :loading="loading" @click="refresh">Yenile</UButton>
    </div>

    <UAlert
      v-if="sources"
      color="primary"
      variant="soft"
      title="Salt okunur görünüm"
      description="Değişiklik için Politika gerekir (yetki varsa). Bu sayfa yalnızca tanımları ve sağlığı gösterir."
    />

    <div v-if="!sources && loading" class="py-12 text-center text-gray-500">Yükleniyor…</div>

    <UTabs
      v-else-if="sources"
      v-model="activeTab"
      :items="tabItems"
      :ui="{ list: { width: 'w-full' } }"
      class="w-full"
    >
      <!-- Event log -->
      <template #eventlog>
        <div class="pt-4 space-y-4">
          <div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
            <UBadge
              size="sm"
              variant="soft"
              :color="sources.eventLog.enabled ? 'green' : 'gray'"
            >
              {{ sources.eventLog.enabled ? 'Açık' : 'Kapalı' }}
            </UBadge>
            <span class="text-gray-500">
              Sorgulama:
              <span class="text-gray-800 dark:text-gray-200">{{ sources.eventLog.pollIntervalSeconds }} sn</span>
            </span>
            <span class="text-gray-500">
              Max/poll:
              <span class="text-gray-800 dark:text-gray-200">{{ sources.eventLog.maxEventsPerPoll ?? '—' }}</span>
            </span>
            <span class="text-gray-500">
              Üretilen:
              <span class="text-gray-800 dark:text-gray-200">{{ sources.eventLog.eventsProduced ?? 0 }}</span>
            </span>
            <span class="text-gray-500">
              Son:
              <span class="text-gray-800 dark:text-gray-200">{{ formatDate(sources.eventLog.lastEventLogUtc as string) }}</span>
            </span>
          </div>

          <UAlert
            v-if="sources.eventLog.lastError"
            color="amber"
            variant="soft"
            :title="String(sources.eventLog.lastError)"
          />

          <div
            v-if="!(sources.eventLog.packages?.length)"
            class="text-sm text-gray-500 py-6 text-center rounded-lg border border-dashed border-gray-200 dark:border-gray-700"
          >
            Aktif Event Log paketi yok.
          </div>
          <div v-else class="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
            <table class="w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
              <thead class="bg-gray-50 dark:bg-gray-800/80">
                <tr>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Paket</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Kanal</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Event ID</th>
                  <th class="px-3 py-2 text-right text-xs font-medium text-gray-500 uppercase"> </th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                <tr v-for="p in sources.eventLog.packages || []" :key="p.name">
                  <td class="px-3 py-2 font-medium">{{ p.name }}</td>
                  <td class="px-3 py-2 font-mono text-xs">{{ p.channel }}</td>
                  <td class="px-3 py-2 font-mono text-xs">{{ p.eventIds.join(', ') }}</td>
                  <td class="px-3 py-2 text-right whitespace-nowrap">
                    <UButton size="xs" variant="soft" color="primary" @click="openPackageDetail(p)">
                      Detay
                    </UButton>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <p v-if="sources.eventLog.knownOptional?.length" class="text-xs text-gray-500">
            İsteğe bağlı (elevation):
            <span
              v-for="o in sources.eventLog.knownOptional"
              :key="o.name"
              class="font-mono ml-1"
            >{{ o.name }}</span>
            — Politika’da paket olarak eklenebilir.
          </p>
        </div>
      </template>

      <!-- Metrics -->
      <template #metrics>
        <div class="pt-4 space-y-4">
          <div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
            <UBadge size="sm" variant="soft" :color="sources.metrics.enabled ? 'green' : 'gray'">
              {{ sources.metrics.enabled ? 'Açık' : 'Kapalı' }}
            </UBadge>
            <span class="text-gray-500">
              Aralık:
              <span class="text-gray-800 dark:text-gray-200">{{ sources.metrics.heartbeatIntervalSeconds }} sn</span>
            </span>
            <span class="text-gray-500">
              Host kaynakları:
              <span class="text-gray-800 dark:text-gray-200">{{ sources.metrics.includeHostResources ? 'Açık' : 'Kapalı' }}</span>
            </span>
            <span class="text-gray-500">
              Üst süreç:
              <span class="text-gray-800 dark:text-gray-200">
                {{
                  sources.metrics.includeTopProcesses
                    ? `Top ${sources.metrics.topProcessCount ?? 5}`
                    : 'Kapalı'
                }}
              </span>
            </span>
            <span class="text-gray-500">
              Üretilen / son:
              <span class="text-gray-800 dark:text-gray-200">
                {{ sources.metrics.eventsProduced ?? 0 }}
                · {{ formatDate(sources.metrics.lastHeartbeatUtc as string) }}
              </span>
            </span>
          </div>

          <div
            v-if="!(sources.metrics.definitions?.length)"
            class="text-sm text-gray-500 py-6 text-center rounded-lg border border-dashed border-gray-200 dark:border-gray-700"
          >
            Metrik kapalı veya tanım yok.
          </div>
          <div v-else class="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
            <table class="w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
              <thead class="bg-gray-50 dark:bg-gray-800/80">
                <tr>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Metric</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Action</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Açıklama</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Aralık</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                <tr v-for="d in sources.metrics.definitions" :key="d.name">
                  <td class="px-3 py-2 font-mono text-xs">{{ d.metric }}</td>
                  <td class="px-3 py-2 font-mono text-xs">{{ d.action }}</td>
                  <td class="px-3 py-2 text-gray-600 dark:text-gray-300">{{ d.description }}</td>
                  <td class="px-3 py-2 text-gray-500">{{ d.intervalSeconds }} sn</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </template>

      <!-- Watch -->
      <template #watch>
        <div class="pt-4 space-y-5">
          <div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
            <UBadge
              size="sm"
              variant="soft"
              :color="sources.serviceWatch.enabled ? 'green' : 'gray'"
            >
              {{ sources.serviceWatch.enabled ? 'Açık' : 'Kapalı' }}
            </UBadge>
            <span class="text-gray-500">
              Sorgulama:
              <span class="text-gray-800 dark:text-gray-200">{{ sources.serviceWatch.pollIntervalSeconds }} sn</span>
            </span>
            <span class="text-gray-500">
              Restart:
              <span class="text-gray-800 dark:text-gray-200">
                {{ sources.serviceWatch.restartCooldownSeconds ?? 300 }} sn
                · {{ sources.serviceWatch.restartMaxAttempts ?? 3 }} deneme
              </span>
            </span>
            <span class="text-gray-500">
              Envanter:
              <span class="text-gray-800 dark:text-gray-200">
                {{
                  sources.serviceWatch.includeInventory === false
                    ? 'Kapalı'
                    : `Açık · ${sources.serviceWatch.inventoryIntervalSeconds ?? 60} sn`
                }}
              </span>
            </span>
          </div>

          <UAlert
            v-if="sources.serviceWatch.lastError"
            color="amber"
            variant="soft"
            :title="String(sources.serviceWatch.lastError)"
          />

          <section>
            <h3 class="text-sm font-semibold text-gray-900 dark:text-white mb-2">Tanımlar (config)</h3>
            <h4 class="text-xs font-medium uppercase tracking-wide text-gray-500 mb-2">Servisler</h4>
            <div
              v-if="!(sources.serviceWatch.configured?.length)"
              class="text-sm text-gray-500 mb-4 py-3 text-center border border-dashed border-gray-200 dark:border-gray-700 rounded-lg"
            >
              Tanımlı servis yok.
            </div>
            <div v-else class="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700 mb-4">
              <table class="w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
                <thead class="bg-gray-50 dark:bg-gray-800/80">
                  <tr>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Servis adı</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Restart</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr v-for="s in sources.serviceWatch.configured" :key="'cfg-svc-' + s.name">
                    <td class="px-3 py-2 font-mono text-xs">{{ s.name }}</td>
                    <td class="px-3 py-2">{{ s.restartAllowed ? 'İzinli' : 'Kapalı' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <h4 class="text-xs font-medium uppercase tracking-wide text-gray-500 mb-2">Uygulamalar</h4>
            <div
              v-if="!(sources.serviceWatch.applications?.length)"
              class="text-sm text-gray-500 py-3 text-center border border-dashed border-gray-200 dark:border-gray-700 rounded-lg"
            >
              Tanımlı uygulama yok.
            </div>
            <div v-else class="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
              <table class="w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
                <thead class="bg-gray-50 dark:bg-gray-800/80">
                  <tr>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Process</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Min</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Restart</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Exe / args</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr v-for="a in sources.serviceWatch.applications" :key="'cfg-app-' + a.name">
                    <td class="px-3 py-2 font-mono text-xs">{{ a.name }}</td>
                    <td class="px-3 py-2">{{ a.minCount }}</td>
                    <td class="px-3 py-2">{{ a.restartAllowed ? 'İzinli' : 'Kapalı' }}</td>
                    <td class="px-3 py-2 text-xs text-gray-500">
                      <span class="font-mono break-all">{{ a.executablePath || '—' }}</span>
                      <span v-if="a.arguments" class="block font-mono">{{ a.arguments }}</span>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>

          <section>
            <h3 class="text-sm font-semibold text-gray-900 dark:text-white mb-2">Anlık durum (runtime)</h3>
            <div
              v-if="!watchRows.length"
              class="text-sm text-gray-500 py-6 text-center rounded-lg border border-dashed border-gray-200 dark:border-gray-700"
            >
              Henüz snapshot yok.
            </div>
            <div v-else class="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
              <table class="w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
                <thead class="bg-gray-50 dark:bg-gray-800/80">
                  <tr>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Tip</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Ad</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Sağlık</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Detay</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Son OS</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Restart</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Güncelleme</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr v-for="row in watchRows" :key="`live-${row.kind}-${row.name}`">
                    <td class="px-3 py-2">
                      <UBadge size="xs" variant="soft" :color="row.kind === 'application' ? 'primary' : 'gray'">
                        {{ row.kind === 'application' ? 'Uygulama' : 'Servis' }}
                      </UBadge>
                    </td>
                    <td class="px-3 py-2 font-medium">
                      {{ row.displayName || row.name }}
                      <span v-if="row.displayName" class="block text-xs text-gray-400 font-mono">{{ row.name }}</span>
                    </td>
                    <td class="px-3 py-2">
                      <UBadge size="xs" variant="soft" :color="healthBadge(row.health)">
                        {{ healthLabel(row.health) }}
                      </UBadge>
                    </td>
                    <td class="px-3 py-2 text-gray-500">
                      <template v-if="row.kind === 'application'">
                        {{ row.instanceCount ?? 0 }} / {{ row.minCount ?? 1 }}
                      </template>
                      <template v-else>{{ statusLabel(row.statusText) }}</template>
                    </td>
                    <td class="px-3 py-2 text-xs text-gray-500">
                      <template v-if="row.lastOsEventId">
                        {{ row.lastOsEventId }}
                        <span v-if="row.lastOsEventAction"> · {{ row.lastOsEventAction }}</span>
                      </template>
                      <template v-else>—</template>
                    </td>
                    <td class="px-3 py-2 text-xs text-gray-500">
                      <template v-if="!row.restartAllowed">Kapalı</template>
                      <template v-else-if="row.lastRestartOk == null">Bekliyor</template>
                      <template v-else>
                        {{ row.lastRestartOk ? 'OK' : 'Fail' }}
                        <span v-if="(row.restartAttemptCount || 0) > 0"> · {{ row.restartAttemptCount }}x</span>
                      </template>
                    </td>
                    <td class="px-3 py-2 text-gray-500">{{ formatDate(row.updatedAtUtc) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>
        </div>
      </template>

      <!-- Producers -->
      <template #producers>
        <div class="pt-4">
          <div class="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
            <table class="w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
              <thead class="bg-gray-50 dark:bg-gray-800/80">
                <tr>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Kaynak</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Etiket</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Durum</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Aralık</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Üretilen</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Son</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                <tr v-for="p in (sources.producers || [])" :key="p.sourceType">
                  <td class="px-3 py-2 font-mono text-xs">{{ p.sourceType }}</td>
                  <td class="px-3 py-2">{{ p.label }}</td>
                  <td class="px-3 py-2">
                    <UBadge size="xs" variant="soft" :color="p.enabled ? 'green' : 'gray'">
                      {{ p.enabled ? 'Açık' : 'Kapalı' }}
                    </UBadge>
                  </td>
                  <td class="px-3 py-2 text-gray-500">{{ p.intervalSeconds ?? '—' }} sn</td>
                  <td class="px-3 py-2 tabular-nums">{{ p.eventsProduced ?? 0 }}</td>
                  <td class="px-3 py-2 text-gray-500">{{ formatDate(p.lastUtc as string) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </template>

      <!-- Ship -->
      <template #ship>
        <div class="pt-4 space-y-4">
          <dl class="text-sm grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div>
              <dt class="text-gray-500">Aralık</dt>
              <dd class="font-medium text-gray-900 dark:text-white">{{ sources.ship.shipIntervalSeconds }} sn</dd>
            </div>
            <div>
              <dt class="text-gray-500">Parti üst sınırı</dt>
              <dd class="font-medium text-gray-900 dark:text-white">{{ sources.ship.maxEventsPerBatch }}</dd>
            </div>
            <div>
              <dt class="text-gray-500">Gönderilen</dt>
              <dd class="font-medium tabular-nums text-gray-900 dark:text-white">{{ sources.ship.eventsShipped }}</dd>
            </div>
            <div>
              <dt class="text-gray-500">Son başarılı</dt>
              <dd class="font-medium text-gray-900 dark:text-white">
                {{ formatDate(sources.ship.lastShipSuccessUtc as string) }}
              </dd>
            </div>
          </dl>
          <UAlert
            v-if="sources.ship.lastError"
            color="red"
            variant="soft"
            :title="String(sources.ship.lastError)"
          />
        </div>
      </template>
    </UTabs>

    <UModal
      v-model="packageDetailOpen"
      :ui="{ width: 'w-full sm:max-w-2xl', background: 'bg-white dark:bg-gray-900' }"
    >
      <div v-if="selectedPackage" class="p-4">
        <div class="flex items-start justify-between gap-3 mb-3">
          <div>
            <p class="font-semibold text-gray-900 dark:text-white">{{ selectedPackage.name }}</p>
            <p class="text-xs font-mono text-gray-500 mt-0.5">{{ selectedPackage.channel }}</p>
          </div>
          <UButton
            color="gray"
            variant="ghost"
            icon="i-heroicons-x-mark-20-solid"
            class="-my-1"
            @click="packageDetailOpen = false"
          />
        </div>
        <p class="text-sm text-gray-500 mb-3">
          Bu pakette toplanan Event ID’ler ve bilinen açıklamalar:
        </p>
        <div class="space-y-2 max-h-[60vh] overflow-y-auto">
          <div
            v-for="info in selectedEventInfos"
            :key="info.id"
            class="rounded-lg border border-gray-200 dark:border-gray-700 p-3"
          >
            <div class="flex items-center gap-2">
              <span class="font-mono text-sm font-semibold">{{ info.id }}</span>
              <span class="text-sm font-medium text-gray-900 dark:text-white">{{ info.title }}</span>
            </div>
            <p v-if="info.description" class="mt-1 text-sm text-gray-500">
              {{ info.description }}
            </p>
          </div>
        </div>
      </div>
    </UModal>
  </div>
</template>

<script setup lang="ts">
import type { SourcesResponse } from '~/composables/useAgentApi'
import { formatDate } from '~/composables/useAgentApi'
import { describeEventIds } from '~/utils/eventLogIdCatalog'

const { getSources } = useAgentApi()
const sources = ref<SourcesResponse | null>(null)
const loading = ref(false)
const activeTab = ref(0)

const packageDetailOpen = ref(false)
const selectedPackage = ref<{
  name: string
  channel: string
  eventIds: number[]
} | null>(null)

const selectedEventInfos = computed(() =>
  selectedPackage.value ? describeEventIds(selectedPackage.value.eventIds) : []
)

const tabItems = computed(() => {
  const s = sources.value
  return [
    {
      key: 'eventlog',
      label: 'Olay günlüğü',
      slot: 'eventlog',
      badge: s?.eventLog?.enabled ? 'Açık' : 'Kapalı'
    },
    {
      key: 'metrics',
      label: 'Metrik',
      slot: 'metrics',
      badge: s?.metrics?.enabled ? 'Açık' : 'Kapalı'
    },
    {
      key: 'watch',
      label: 'İzleme',
      slot: 'watch',
      badge: s?.serviceWatch?.enabled ? 'Açık' : 'Kapalı'
    },
    {
      key: 'producers',
      label: 'Üreticiler',
      slot: 'producers',
      badge: String(s?.producers?.length ?? 0)
    },
    {
      key: 'ship',
      label: 'Gönderim',
      slot: 'ship'
    }
  ]
})

function openPackageDetail(p: { name: string; channel: string; eventIds: number[] }) {
  selectedPackage.value = p
  packageDetailOpen.value = true
}

const watchRows = computed(() => {
  const snap = sources.value?.serviceWatch.snapshot || []
  if (snap.length) {
    return snap.map(s => ({
      kind: s.kind || 'service',
      name: s.name,
      displayName: s.displayName ?? null,
      health: s.health,
      statusText: s.statusText,
      restartAllowed: s.restartAllowed,
      instanceCount: s.instanceCount,
      minCount: s.minCount,
      updatedAtUtc: s.updatedAtUtc,
      lastOsEventId: s.lastOsEventId,
      lastOsEventAction: s.lastOsEventAction,
      lastRestartOk: s.lastRestartOk,
      restartAttemptCount: s.restartAttemptCount
    }))
  }
  return []
})

function healthBadge(h?: string) {
  if (h === 'Running') return 'green'
  if (h === 'NotRunning' || h === 'Missing') return 'red'
  return 'gray'
}

function healthLabel(h?: string) {
  switch (h) {
    case 'Running': return 'Çalışıyor'
    case 'NotRunning': return 'Durmuş'
    case 'Missing': return 'Yok'
    case 'Unknown': return 'Bilinmiyor'
    default: return h || '—'
  }
}

function statusLabel(s?: string | null) {
  if (!s) return '—'
  const map: Record<string, string> = {
    Running: 'Çalışıyor',
    Stopped: 'Durduruldu',
    Missing: 'Yok',
    StartPending: 'Başlatılıyor',
    StopPending: 'Durduruluyor',
    ContinuePending: 'Sürdürülüyor',
    PausePending: 'Duraklatılıyor',
    Paused: 'Duraklatıldı'
  }
  return map[s] || s
}

async function refresh() {
  loading.value = true
  try {
    sources.value = await getSources()
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
