<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Loglar</h1>
      <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
        Son okunan (üretilen) ve son gönderilen olaylar
      </p>
    </div>

    <UCard>
      <template #header>
        <div class="flex items-center justify-between gap-4 flex-wrap">
          <div class="flex items-center gap-3">
            <USelectMenu
              v-model="selectedDirection"
              :options="directionOptions"
              option-attribute="label"
              value-attribute="value"
              class="w-44"
              size="sm"
            />
            <UButton size="sm" variant="outline" :loading="loading" @click="fetchEvents">Yenile</UButton>
            <UButton size="sm" color="neutral" variant="outline" :loading="clearing" @click="clear">
              Temizle
            </UButton>
          </div>
          <div class="flex items-center gap-2">
            <UToggle v-model="autoRefresh" size="sm" />
            <span class="text-sm text-gray-500">Otomatik yenile</span>
          </div>
        </div>
      </template>

      <div
        class="font-mono text-xs overflow-auto rounded-lg bg-gray-900 dark:bg-gray-950 text-gray-100 p-4"
        style="max-height: 70vh; min-height: 400px"
      >
        <div v-if="loading && items.length === 0" class="py-8 text-center text-gray-500">Yükleniyor...</div>
        <div v-else-if="items.length === 0" class="py-8 text-center text-gray-500">Kayıt yok</div>
        <div v-else class="space-y-0.5">
          <div
            v-for="(entry, i) in items"
            :key="i"
            class="flex gap-3 py-1 hover:bg-gray-800/50 rounded px-1 -mx-1 cursor-pointer items-start"
            @click="openDetail(entry)"
          >
            <span class="shrink-0 text-gray-500 tabular-nums pt-0.5">{{ formatTime(entry.atUtc) }}</span>
            <UBadge
              :color="entry.direction === 'shipped' ? 'primary' : 'amber'"
              variant="soft"
              size="xs"
              class="shrink-0 w-24 justify-center"
            >
              {{ directionLabel(entry.direction) }}
            </UBadge>
            <UBadge
              :color="severityColor(entry.severity)"
              variant="soft"
              size="xs"
              class="shrink-0"
            >
              {{ sourceLabel(entry.source) }}
            </UBadge>
            <span class="break-all flex-1 min-w-0">
              <template v-if="entry.metricName != null && entry.metricValue != null">
                {{ metricLabel(entry.metricName) }}
                <span v-if="entry.detail" class="text-gray-400"> ({{ entry.detail }})</span>
                =
                <span class="text-emerald-300">{{ formatMetricValue(entry.metricName, entry.metricValue) }}</span>
              </template>
              <template v-else>
                {{ entry.action || entry.message || '—' }}
                <span v-if="entry.detail" class="text-gray-400"> · {{ entry.detail }}</span>
                <span v-if="entry.message && entry.action && entry.message !== entry.action" class="text-gray-400">
                  · {{ entry.message }}
                </span>
              </template>
            </span>
            <UButton
              size="xs"
              variant="soft"
              color="gray"
              class="shrink-0"
              @click.stop="openDetail(entry)"
            >
              Detay
            </UButton>
          </div>
        </div>
      </div>
    </UCard>

    <EventDetailModal v-model:open="detailOpen" :event="selectedEvent" />
  </div>
</template>

<script setup lang="ts">
import type { RecentEventEntry } from '~/composables/useAgentApi'
import { formatMetricValue, metricLabel, sourceLabel } from '~/composables/useAgentApi'

const { getEvents, clearEvents } = useAgentApi()

const directionOptions = [
  { label: 'Tümü', value: 'all' },
  { label: 'Üretilen', value: 'produced' },
  { label: 'Gönderilen', value: 'shipped' }
]

/** Bound to option value when value-attribute is set (Nuxt UI v2). */
const selectedDirection = ref<'all' | 'produced' | 'shipped'>('all')
const items = ref<RecentEventEntry[]>([])
const loading = ref(false)
const clearing = ref(false)
const autoRefresh = ref(true)
const detailOpen = ref(false)
const selectedEvent = ref<RecentEventEntry | null>(null)
let timer: ReturnType<typeof setInterval> | null = null

function severityColor(s?: string | null) {
  if (s === 'error') return 'red'
  if (s === 'warning') return 'amber'
  return 'gray'
}

function directionLabel(d: string) {
  if (d === 'shipped') return 'Gönderildi'
  if (d === 'produced') return 'Üretildi'
  return d
}

function formatTime(value: string) {
  try {
    return new Date(value).toLocaleTimeString('tr-TR')
  } catch {
    return value
  }
}

function openDetail(entry: RecentEventEntry) {
  selectedEvent.value = entry
  detailOpen.value = true
}

async function fetchEvents() {
  loading.value = true
  try {
    const dir = selectedDirection.value || 'all'
    const res = await getEvents(dir, 150)
    items.value = res.items
  } finally {
    loading.value = false
  }
}

async function clear() {
  clearing.value = true
  try {
    await clearEvents()
    items.value = []
    detailOpen.value = false
    selectedEvent.value = null
  } finally {
    clearing.value = false
  }
}

watch(selectedDirection, () => fetchEvents())

onMounted(async () => {
  await fetchEvents()
  timer = setInterval(() => {
    if (autoRefresh.value) fetchEvents()
  }, 8000)
})

onUnmounted(() => {
  if (timer) clearInterval(timer)
})
</script>
