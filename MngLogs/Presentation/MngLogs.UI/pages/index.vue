<template>
  <div class="space-y-8">
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Ajan Durumu</h1>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Toplayıcı bağlantısı, üretim sayaçları ve son aktivite
        </p>
      </div>
      <div class="flex items-center gap-3">
        <UToggle v-model="autoRefresh" size="sm" />
        <span class="text-sm text-gray-500">Otomatik yenile</span>
        <UButton size="sm" variant="outline" :loading="loading" icon="i-heroicons-arrow-path" @click="refresh">
          Yenile
        </UButton>
      </div>
    </div>

    <UAlert v-if="error" color="red" variant="soft" :title="error" />

    <div class="grid grid-cols-1 gap-6 lg:grid-cols-2">
      <UCard>
        <template #header>
          <div class="flex items-center justify-between">
            <span class="font-semibold">Bağlantı</span>
            <UBadge :color="healthColor" variant="soft" size="sm">{{ healthLabel }}</UBadge>
          </div>
        </template>
        <div v-if="status" class="space-y-3 text-sm">
          <div>
            <span class="text-gray-500 dark:text-gray-400">Ana bilgisayar</span>
            <p class="font-medium text-gray-900 dark:text-gray-100">{{ status.hostname }} · {{ status.hostId }}</p>
          </div>
          <div>
            <span class="text-gray-500 dark:text-gray-400">Alan (domain)</span>
            <p class="font-mono">{{ status.domain }}</p>
          </div>
          <div>
            <span class="text-gray-500 dark:text-gray-400">Toplayıcı</span>
            <p class="font-mono break-all">{{ status.collectorBaseUrl }}</p>
          </div>
          <div>
            <span class="text-gray-500 dark:text-gray-400">Başlangıç (UTC)</span>
            <p>{{ formatDate(status.startedAtUtc) }}</p>
          </div>
          <div>
            <span class="text-gray-500 dark:text-gray-400">Veri dizini</span>
            <p class="font-mono text-xs break-all">{{ status.dataDirectory }}</p>
          </div>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div class="flex items-center justify-between">
            <span class="font-semibold">Gönderim</span>
            <UBadge :color="status?.lastShipError ? 'red' : 'green'" variant="soft" size="sm">
              {{ status?.lastShipError ? 'Hata' : 'Tamam' }}
            </UBadge>
          </div>
        </template>
        <div v-if="status" class="space-y-2 text-sm">
          <p><span class="text-gray-500">Kuyruk:</span> <strong>{{ status.queuePending }}</strong></p>
          <p><span class="text-gray-500">Gönderilen:</span> {{ status.eventsShipped }}</p>
          <p><span class="text-gray-500">Son gönderim:</span> {{ formatDate(status.lastShipUtc) }}</p>
          <p><span class="text-gray-500">Son başarı:</span> {{ formatDate(status.lastShipSuccessUtc) }}</p>
          <UAlert
            v-if="status.lastShipError"
            class="mt-2"
            color="red"
            variant="soft"
            :title="status.lastShipError"
          />
        </div>
      </UCard>
    </div>

    <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
      <UCard v-for="tile in tiles" :key="tile.label">
        <p class="text-xs text-gray-500 dark:text-gray-400">{{ tile.label }}</p>
        <p class="mt-1 text-2xl font-semibold tabular-nums">{{ tile.value }}</p>
        <UBadge class="mt-2" size="xs" variant="soft" :color="tile.enabled ? 'green' : 'gray'">
          {{ tile.enabled ? 'Açık' : 'Kapalı' }}
        </UBadge>
      </UCard>
    </div>

    <UCard>
      <template #header>
        <span class="font-semibold">Son aktivite</span>
      </template>
      <div v-if="!status?.recent?.length" class="text-sm text-gray-500 py-4">Henüz kayıt yok</div>
      <ul v-else class="font-mono text-xs space-y-1 max-h-64 overflow-auto">
        <li
          v-for="(line, i) in status.recent.slice().reverse()"
          :key="i"
          class="text-gray-700 dark:text-gray-300"
        >
          {{ line }}
        </li>
      </ul>
    </UCard>
  </div>
</template>

<script setup lang="ts">
import type { AgentStatus } from '~/composables/useAgentApi'

const { getStatus, formatDate } = useAgentApi()

const status = ref<AgentStatus | null>(null)
const loading = ref(false)
const error = ref('')
const autoRefresh = ref(true)
let timer: ReturnType<typeof setInterval> | null = null

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

const tiles = computed(() => {
  const s = status.value
  return [
    { label: 'Metrik', value: s?.metricEventsProduced ?? 0, enabled: !!s?.metricsEnabled },
    { label: 'Olay günlüğü', value: s?.eventLogEventsProduced ?? 0, enabled: !!s?.eventLogEnabled },
    { label: 'Servis izleme', value: s?.serviceWatchEventsProduced ?? 0, enabled: !!s?.serviceWatchEnabled },
    { label: 'Kalp atışı', value: s?.heartbeatsProduced ?? 0, enabled: !!s?.metricsEnabled }
  ]
})

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    status.value = await getStatus()
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
  }, 10000)
})

onUnmounted(() => {
  if (timer) clearInterval(timer)
})
</script>
