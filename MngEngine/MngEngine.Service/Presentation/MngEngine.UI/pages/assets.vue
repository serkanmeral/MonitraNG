<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Asset'ler</h1>
      <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
        Config sync'ten gelen asset listesi
      </p>
    </div>

    <UCard>
      <template #header>
        <div class="flex items-center justify-between">
          <span class="font-semibold">Asset Listesi</span>
          <div class="flex items-center gap-2">
            <UButton size="sm" variant="outline" :loading="loading" @click="fetchAssets">
              Yenile
            </UButton>
            <UBadge color="neutral" variant="soft">{{ assets.length }} asset</UBadge>
          </div>
        </div>
      </template>

      <div v-if="loading && assets.length === 0" class="py-12 text-center text-gray-500">
        Yükleniyor...
      </div>
      <div v-else-if="assets.length === 0" class="py-12 text-center text-gray-500">
        Asset bulunamadı. Config sync tamamlandığında liste görünecektir.
      </div>
      <div v-else class="overflow-x-auto">
        <table class="w-full min-w-full divide-y divide-gray-200 dark:divide-gray-700">
          <thead class="bg-gray-50 dark:bg-gray-800">
            <tr>
              <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Agent</th>
              <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Asset</th>
              <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Item</th>
              <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Metod</th>
              <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Son okuma</th>
              <th scope="col" class="px-4 py-3 text-right text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">İşlem</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
            <tr v-for="(a, i) in assets" :key="a.id ?? i" class="bg-white dark:bg-gray-900">
              <td class="px-4 py-3 text-sm text-gray-900 dark:text-gray-100">
                <span class="font-medium">{{ displayName(a.agentName, a.agentId) }}</span>
                <p class="text-xs text-gray-500 mt-0.5">{{ a.agentId }}</p>
              </td>
              <td class="px-4 py-3 text-sm text-gray-900 dark:text-gray-100">
                <span class="font-medium">{{ displayName(a.assetName, a.assetId) }}</span>
                <p class="text-xs text-gray-500 mt-0.5">{{ a.assetId }}</p>
              </td>
              <td class="px-4 py-3 text-sm text-gray-900 dark:text-gray-100">
                <span>{{ displayName(a.itemName, a.itemId) }}</span>
                <p v-if="a.itemId" class="text-xs text-gray-500 mt-0.5">{{ a.itemId }}</p>
              </td>
              <td class="px-4 py-3 text-sm">
                <UBadge size="xs" variant="soft" color="neutral">{{ a.collectionMethod || 'ssh' }}</UBadge>
              </td>
              <td class="px-4 py-3 text-sm">
                <span v-if="a.lastCollectedAt" class="text-green-600 dark:text-green-400 font-medium" :title="formatDateFull(a.lastCollectedAt)">
                  {{ formatRelative(a.lastCollectedAt) }}
                </span>
                <span v-else class="text-gray-400">—</span>
              </td>
              <td class="px-4 py-3 text-right">
                <UButton size="xs" variant="ghost" color="primary" @click="openDetail(a)">
                  Detaylar
                </UButton>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>

    <!-- Detay modal -->
    <UModal v-model="detailOpen">
      <UCard v-if="detailAsset" class="overflow-hidden">
        <template #header>
          <div class="flex items-center justify-between">
            <span class="font-semibold">Asset Detayı</span>
            <UButton size="xs" variant="ghost" color="neutral" icon="i-heroicons-x-mark" @click="detailOpen = false" />
          </div>
        </template>
        <div class="space-y-3 text-sm">
          <div class="grid grid-cols-2 gap-2">
            <span class="text-gray-500">Agent:</span>
            <span class="font-medium">{{ displayName(detailAsset.agentName, detailAsset.agentId) }}</span>
            <span class="text-gray-500">Asset:</span>
            <span class="font-medium">{{ displayName(detailAsset.assetName, detailAsset.assetId) }}</span>
            <span class="text-gray-500">Item:</span>
            <span>{{ displayName(detailAsset.itemName, detailAsset.itemId) }}</span>
            <span class="text-gray-500">Metod:</span>
            <span><UBadge size="xs" variant="soft">{{ detailAsset.collectionMethod || 'ssh' }}</UBadge></span>
            <span class="text-gray-500">Son okuma:</span>
            <span>
              <span v-if="detailAsset.lastCollectedAt" class="text-green-600 dark:text-green-400">
                {{ formatDateFull(detailAsset.lastCollectedAt) }}
              </span>
              <span v-else class="text-gray-400">Veri henüz okunmadı</span>
            </span>
          </div>
          <div v-if="detailAsset.collectibles?.length" class="pt-2 border-t border-gray-200 dark:border-gray-700">
            <p class="text-gray-500 mb-2">Collectible'lar:</p>
            <div class="flex flex-wrap gap-1">
              <UBadge v-for="c in detailAsset.collectibles" :key="c.code" size="xs" :color="c.enabled ? 'primary' : 'neutral'" variant="soft">
                {{ c.code }}
              </UBadge>
            </div>
          </div>
          <div v-if="detailAsset.connectionInfo && Object.keys(detailAsset.connectionInfo).length" class="pt-2 border-t border-gray-200 dark:border-gray-700">
            <p class="text-gray-500 mb-2">Connection Info:</p>
            <pre class="text-xs bg-gray-100 dark:bg-gray-800 p-2 rounded overflow-x-auto">{{ JSON.stringify(detailAsset.connectionInfo, null, 2) }}</pre>
          </div>
        </div>
      </UCard>
    </UModal>
  </div>
</template>

<script setup lang="ts">
import type { AssetConfig } from '~/composables/useEngineApi'

const { getAssets } = useEngineApi()

const assets = ref<AssetConfig[]>([])
const loading = ref(false)
const detailOpen = ref(false)
const detailAsset = ref<AssetConfig | null>(null)

function displayName(name?: string | null, id?: string | null): string {
  if (name && name.trim()) return name
  return id || '—'
}

function formatDateFull(s: string | undefined): string {
  if (!s) return '—'
  try {
    return new Date(s).toLocaleString('tr-TR')
  } catch {
    return s
  }
}

function formatRelative(s: string | undefined): string {
  if (!s) return '—'
  try {
    const d = new Date(s)
    if (Number.isNaN(d.getTime())) return s
    const diffMs = Date.now() - d.getTime()
    const diffMin = Math.floor(diffMs / 60000)
    const diffSec = Math.floor(diffMs / 1000)
    if (diffSec < 60) return 'az önce'
    if (diffMin < 60) return `${diffMin} dk önce`
    const diffHour = Math.floor(diffMin / 60)
    return `${diffHour} sa önce`
  } catch {
    return s
  }
}

function openDetail(a: AssetConfig) {
  detailAsset.value = a
  detailOpen.value = true
}

async function fetchAssets() {
  loading.value = true
  try {
    assets.value = await getAssets()
  } catch {
    assets.value = []
  } finally {
    loading.value = false
  }
}

onMounted(fetchAssets)
</script>
