<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Queue</h1>
      <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
        Ingest batch kuyruğu – Reactor'a gönderilmeyi bekleyen metrik batch'leri
      </p>
    </div>

    <UCard>
      <template #header>
        <div class="flex items-center justify-between">
          <span class="font-semibold">Kuyruk İçeriği</span>
          <div class="flex items-center gap-2">
            <UButton size="sm" variant="outline" :loading="loading" @click="fetchQueue">
              Yenile
            </UButton>
            <UBadge :color="queueCount > 0 ? 'amber' : 'neutral'" variant="soft">
              {{ queueCount }} batch
            </UBadge>
          </div>
        </div>
      </template>

      <div v-if="loading && queueItems.length === 0" class="py-12 text-center text-gray-500">
        Yükleniyor...
      </div>
      <div v-else-if="queueItems.length === 0" class="py-12 text-center text-gray-500">
        <p>Kuyruk boş. CollectorJob çalıştıkça batch'ler eklenecektir.</p>
        <p class="mt-2 text-sm">SendJob her 2 dakikada verileri Reactor'a gönderir; gönderilen veriler MngDataGateway / Reactor tarafında görüntülenir.</p>
      </div>
      <div v-else class="overflow-x-auto">
        <table class="w-full min-w-full divide-y divide-gray-200 dark:divide-gray-700">
          <thead class="bg-gray-50 dark:bg-gray-800">
            <tr>
              <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Agent</th>
              <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Asset</th>
              <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Item</th>
              <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Toplama Zamanı</th>
              <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Metrik</th>
              <th scope="col" class="px-4 py-3 text-right text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">İşlem</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
            <tr v-for="(q, i) in queueItems" :key="i" class="bg-white dark:bg-gray-900 hover:bg-gray-50 dark:hover:bg-gray-800">
              <td class="px-4 py-3 text-sm text-gray-900 dark:text-gray-100">
                <span class="font-medium">{{ displayName(q.agentName, q.agentId) }}</span>
                <p v-if="!q.agentName" class="text-xs text-gray-500 mt-0.5">{{ q.agentId }}</p>
              </td>
              <td class="px-4 py-3 text-sm text-gray-900 dark:text-gray-100">
                <span class="font-medium">{{ displayName(q.assetName, q.assetId) }}</span>
                <p v-if="!q.assetName" class="text-xs text-gray-500 mt-0.5">{{ q.assetId }}</p>
              </td>
              <td class="px-4 py-3 text-sm text-gray-500 dark:text-gray-400">
                {{ displayName(q.itemName, q.itemId) || '-' }}
              </td>
              <td class="px-4 py-3 text-sm text-gray-500 dark:text-gray-400">{{ formatDate(q.collectedAt) }}</td>
              <td class="px-4 py-3 text-sm">
                <UBadge size="xs" variant="soft" color="primary">{{ q.metricCount }}</UBadge>
              </td>
              <td class="px-4 py-3 text-right">
                <UButton size="xs" variant="ghost" color="primary" @click="openDetail(q)">
                  Detay
                </UButton>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>

    <!-- Detay Modal -->
    <UModal v-model="detailOpen">
      <UCard v-if="detailItem" class="overflow-hidden">
        <template #header>
          <div class="flex items-center justify-between">
            <span class="font-semibold">Batch Detayı</span>
            <UButton size="xs" variant="ghost" color="neutral" icon="i-heroicons-x-mark" @click="detailOpen = false" />
          </div>
        </template>
        <div class="space-y-4">
          <div class="grid grid-cols-2 gap-3 text-sm">
            <span class="text-gray-500">Agent:</span>
            <span class="font-medium">{{ displayName(detailItem.agentName, detailItem.agentId) }}</span>
            <span class="text-gray-500">Asset:</span>
            <span class="font-medium">{{ displayName(detailItem.assetName, detailItem.assetId) }}</span>
            <span class="text-gray-500">Item:</span>
            <span>{{ displayName(detailItem.itemName, detailItem.itemId) || '-' }}</span>
            <span class="text-gray-500">Toplama Zamanı:</span>
            <span>{{ formatDateFull(detailItem.collectedAt) }}</span>
            <span class="text-gray-500">Metrik Sayısı:</span>
            <span><UBadge size="xs" variant="soft" color="primary">{{ detailItem.metricCount }}</UBadge></span>
          </div>

          <div v-if="detailItem.metrics?.length" class="pt-3 border-t border-gray-200 dark:border-gray-700">
            <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Metrik Değerleri</p>
            <div class="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
              <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
                <thead class="bg-gray-50 dark:bg-gray-800">
                  <tr>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 dark:text-gray-400">Collectible</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 dark:text-gray-400">Değer</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 dark:text-gray-400">Birim</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr v-for="m in detailItem.metrics" :key="m.collectibleCode" class="bg-white dark:bg-gray-900">
                    <td class="px-3 py-2 text-sm font-mono text-gray-900 dark:text-gray-100">{{ m.collectibleCode }}</td>
                    <td class="px-3 py-2 text-sm text-gray-900 dark:text-gray-100">{{ formatMetricValue(m.value) }}</td>
                    <td class="px-3 py-2 text-sm text-gray-500 dark:text-gray-400">{{ m.unit || '-' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </UCard>
    </UModal>
  </div>
</template>

<script setup lang="ts">
import type { QueueBatch } from '~/composables/useEngineApi'

const { getQueue } = useEngineApi()

const queueItems = ref<QueueBatch[]>([])
const queueCount = ref(0)
const loading = ref(false)
const detailOpen = ref(false)
const detailItem = ref<QueueBatch | null>(null)

function displayName(name?: string | null, id?: string | null): string {
  if (name) return name
  return id || '-'
}

function formatDate(s: string | undefined) {
  if (!s) return '-'
  try {
    return new Date(s).toLocaleString('tr-TR')
  } catch {
    return s
  }
}

function formatDateFull(s: string | undefined) {
  if (!s) return '-'
  try {
    return new Date(s).toLocaleString('tr-TR', { dateStyle: 'full', timeStyle: 'medium' })
  } catch {
    return s
  }
}

function formatMetricValue(v: unknown): string {
  if (v == null) return '-'
  if (typeof v === 'object') return JSON.stringify(v)
  return String(v)
}

function openDetail(q: QueueBatch) {
  detailItem.value = q
  detailOpen.value = true
}

watch(detailOpen, (v) => { if (!v) detailItem.value = null })

async function fetchQueue() {
  loading.value = true
  try {
    const res = await getQueue()
    queueItems.value = res.items
    queueCount.value = res.count
  } catch {
    queueItems.value = []
    queueCount.value = 0
  } finally {
    loading.value = false
  }
}

onMounted(fetchQueue)
</script>
