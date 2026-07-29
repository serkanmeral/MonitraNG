<template>
  <div class="space-y-6">
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Kaynaklar</h1>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Metrik, olay günlüğü paketleri ve servis izleme durumu
        </p>
      </div>
      <UButton size="sm" variant="outline" :loading="loading" @click="refresh">Yenile</UButton>
    </div>

    <div v-if="sources" class="grid grid-cols-1 lg:grid-cols-3 gap-4">
      <UCard>
        <template #header>
          <div class="flex items-center justify-between">
            <span class="font-semibold">Metrik</span>
            <UBadge :color="sources.metrics.enabled ? 'green' : 'gray'" variant="soft" size="sm">
              {{ sources.metrics.enabled ? 'Açık' : 'Kapalı' }}
            </UBadge>
          </div>
        </template>
        <dl class="text-sm space-y-2">
          <div class="flex justify-between gap-2">
            <dt class="text-gray-500">Aralık</dt>
            <dd>{{ sources.metrics.heartbeatIntervalSeconds }} sn</dd>
          </div>
          <div class="flex justify-between gap-2">
            <dt class="text-gray-500">Kaynak metrikleri</dt>
            <dd>{{ sources.metrics.includeHostResources ? 'Evet' : 'Hayır' }}</dd>
          </div>
          <div class="flex justify-between gap-2">
            <dt class="text-gray-500">Üst süreç listesi</dt>
            <dd>
              {{
                sources.metrics.includeTopProcesses
                  ? `Evet (Top ${sources.metrics.topProcessCount ?? 5})`
                  : 'Hayır'
              }}
            </dd>
          </div>
          <div class="flex justify-between gap-2">
            <dt class="text-gray-500">Üretilen</dt>
            <dd class="tabular-nums">{{ sources.metrics.eventsProduced }}</dd>
          </div>
          <div class="flex justify-between gap-2">
            <dt class="text-gray-500">Son</dt>
            <dd>{{ formatDate(sources.metrics.lastHeartbeatUtc as string) }}</dd>
          </div>
        </dl>
      </UCard>

      <UCard>
        <template #header>
          <div class="flex items-center justify-between">
            <span class="font-semibold">Olay günlüğü</span>
            <UBadge :color="sources.eventLog.enabled ? 'green' : 'gray'" variant="soft" size="sm">
              {{ sources.eventLog.enabled ? 'Açık' : 'Kapalı' }}
            </UBadge>
          </div>
        </template>
        <dl class="text-sm space-y-2">
          <div class="flex justify-between gap-2">
            <dt class="text-gray-500">Sorgulama aralığı</dt>
            <dd>{{ sources.eventLog.pollIntervalSeconds }} sn</dd>
          </div>
          <div class="flex justify-between gap-2">
            <dt class="text-gray-500">Üretilen</dt>
            <dd class="tabular-nums">{{ sources.eventLog.eventsProduced }}</dd>
          </div>
          <div class="flex justify-between gap-2">
            <dt class="text-gray-500">Son</dt>
            <dd>{{ formatDate(sources.eventLog.lastEventLogUtc as string) }}</dd>
          </div>
        </dl>
        <UAlert
          v-if="sources.eventLog.lastError"
          class="mt-3"
          color="amber"
          variant="soft"
          :title="String(sources.eventLog.lastError)"
        />
      </UCard>

      <UCard>
        <template #header>
          <div class="flex items-center justify-between">
            <span class="font-semibold">Gönderim</span>
            <UBadge color="primary" variant="soft" size="sm">Toplayıcı</UBadge>
          </div>
        </template>
        <dl class="text-sm space-y-2">
          <div class="flex justify-between gap-2">
            <dt class="text-gray-500">Aralık</dt>
            <dd>{{ sources.ship.shipIntervalSeconds }} sn</dd>
          </div>
          <div class="flex justify-between gap-2">
            <dt class="text-gray-500">Parti üst sınırı</dt>
            <dd>{{ sources.ship.maxEventsPerBatch }}</dd>
          </div>
          <div class="flex justify-between gap-2">
            <dt class="text-gray-500">Gönderilen</dt>
            <dd class="tabular-nums">{{ sources.ship.eventsShipped }}</dd>
          </div>
        </dl>
        <UAlert
          v-if="sources.ship.lastError"
          class="mt-3"
          color="red"
          variant="soft"
          :title="String(sources.ship.lastError)"
        />
      </UCard>
    </div>

    <UCard v-if="sources">
      <template #header>
        <span class="font-semibold">Olay günlüğü paketleri</span>
      </template>
      <div class="overflow-x-auto">
        <table class="w-full divide-y divide-gray-200 dark:divide-gray-700">
          <thead class="bg-gray-50 dark:bg-gray-800">
            <tr>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Paket</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Kanal</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Olay kimliği</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
            <tr v-for="p in sources.eventLog.packages || []" :key="p.name">
              <td class="px-4 py-3 text-sm font-medium">{{ p.name }}</td>
              <td class="px-4 py-3 text-sm">{{ p.channel }}</td>
              <td class="px-4 py-3 text-sm font-mono text-xs">{{ p.eventIds.join(', ') }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>

    <UCard v-if="sources">
      <template #header>
        <div class="flex items-center justify-between">
          <span class="font-semibold">Servis izleme</span>
          <UBadge :color="sources.serviceWatch.enabled ? 'green' : 'gray'" variant="soft" size="sm">
            {{ sources.serviceWatch.enabled ? 'Açık' : 'Kapalı' }}
          </UBadge>
        </div>
      </template>
      <UAlert
        v-if="sources.serviceWatch.lastError"
        class="mb-4"
        color="amber"
        variant="soft"
        :title="String(sources.serviceWatch.lastError)"
      />
      <div v-if="!(sources.serviceWatch.snapshot?.length || sources.serviceWatch.configured?.length)" class="text-sm text-gray-500 py-4">
        İzlenen servis yok. Politika ekranından ekleyin.
      </div>
      <div v-else class="overflow-x-auto">
        <table class="w-full divide-y divide-gray-200 dark:divide-gray-700">
          <thead class="bg-gray-50 dark:bg-gray-800">
            <tr>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Servis</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Durum</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Sağlık</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Yeniden başlatma</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Güncelleme</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
            <tr v-for="row in serviceRows" :key="row.name">
              <td class="px-4 py-3 text-sm">
                <span class="font-medium">{{ row.displayName || row.name }}</span>
                <p v-if="row.displayName" class="text-xs text-gray-500 font-mono">{{ row.name }}</p>
              </td>
              <td class="px-4 py-3 text-sm">{{ statusLabel(row.statusText) }}</td>
              <td class="px-4 py-3 text-sm">
                <UBadge size="xs" variant="soft" :color="healthBadge(row.health)">
                  {{ healthLabel(row.health) }}
                </UBadge>
              </td>
              <td class="px-4 py-3 text-sm">{{ row.restartAllowed ? 'İzinli' : 'Hayır' }}</td>
              <td class="px-4 py-3 text-sm text-gray-500">{{ formatDate(row.updatedAtUtc) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>
  </div>
</template>

<script setup lang="ts">
import type { SourcesResponse } from '~/composables/useAgentApi'

const { getSources, formatDate } = useAgentApi()
const sources = ref<SourcesResponse | null>(null)
const loading = ref(false)

const serviceRows = computed(() => {
  const snap = sources.value?.serviceWatch.snapshot || []
  if (snap.length) return snap
  return (sources.value?.serviceWatch.configured || []).map(s => ({
    name: s.name,
    displayName: null as string | null,
    health: '—',
    statusText: 'Henüz sorgulanmadı',
    restartAllowed: s.restartAllowed,
    updatedAtUtc: '' as string
  }))
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
    case '—': return '—'
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
