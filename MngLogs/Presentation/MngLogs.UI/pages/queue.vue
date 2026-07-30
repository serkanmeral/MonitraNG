<template>
  <div class="space-y-6">
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Kuyruk</h1>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Toplayıcıya gönderilmeyi bekleyen olaylar (disk kuyruğu)
        </p>
      </div>
      <UButton size="sm" variant="outline" :loading="loading" @click="refresh">Yenile</UButton>
    </div>

    <UCard>
      <template #header>
        <div class="flex items-center justify-between">
          <span class="font-semibold">Bekleyen olaylar</span>
          <UBadge :color="data?.count ? 'amber' : 'neutral'" variant="soft">
            {{ data?.count ?? 0 }} bekleyen
          </UBadge>
        </div>
      </template>

      <div v-if="loading && !data" class="py-12 text-center text-gray-500">Yükleniyor...</div>
      <div v-else-if="!data?.items?.length" class="py-12 text-center text-gray-500">
        Kuyruk boş. Üretilen olaylar ship interval içinde gönderilir.
      </div>
      <div v-else class="overflow-x-auto">
        <table class="w-full min-w-full divide-y divide-gray-200 dark:divide-gray-700">
          <thead class="bg-gray-50 dark:bg-gray-800">
            <tr>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Zaman</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Kaynak</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Önem</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Mesaj</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Dosya</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
            <tr
              v-for="(item, i) in data.items"
              :key="i"
              class="bg-white dark:bg-gray-900 hover:bg-gray-50 dark:hover:bg-gray-800"
            >
              <td class="px-4 py-3 text-sm text-gray-500 whitespace-nowrap">{{ formatDate(item.timestampUtc) }}</td>
              <td class="px-4 py-3 text-sm font-medium">{{ item.source }}</td>
              <td class="px-4 py-3 text-sm">
                <UBadge size="xs" variant="soft" :color="severityColor(item.severity)">
                  {{ severityLabel(item.severity) }}
                </UBadge>
              </td>
              <td class="px-4 py-3 text-sm break-all">{{ item.message || '—' }}</td>
              <td class="px-4 py-3 text-xs font-mono text-gray-500">{{ item.fileName }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>
  </div>
</template>

<script setup lang="ts">
import type { QueueResponse } from '~/composables/useAgentApi'
import { formatDate } from '~/composables/useAgentApi'

const { getQueue } = useAgentApi()
const data = ref<QueueResponse | null>(null)
const loading = ref(false)

function severityColor(s?: string | null) {
  if (s === 'error') return 'red'
  if (s === 'warning') return 'amber'
  if (s === 'info') return 'green'
  return 'gray'
}

function severityLabel(s?: string | null) {
  if (!s) return '—'
  const map: Record<string, string> = {
    error: 'hata',
    warning: 'uyarı',
    info: 'bilgi'
  }
  return map[s] || s
}

async function refresh() {
  loading.value = true
  try {
    data.value = await getQueue()
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
