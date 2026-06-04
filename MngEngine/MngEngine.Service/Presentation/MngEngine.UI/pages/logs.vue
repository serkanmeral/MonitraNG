<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Log Ekranı</h1>
      <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
        Engine uygulama loglarını canlı olarak görüntüleyin
      </p>
    </div>

    <UCard>
      <template #header>
        <div class="flex items-center justify-between gap-4 flex-wrap">
          <div class="flex items-center gap-3">
            <USelectMenu
              v-model="selectedLevel"
              :items="levelOptions"
              value-key="value"
              class="w-32"
              size="sm"
            />
            <UButton
              size="sm"
              variant="outline"
              :loading="loading"
              @click="fetchLogs"
            >
              Yenile
            </UButton>
            <UButton
              size="sm"
              color="neutral"
              variant="outline"
              :loading="clearing"
              @click="clearLogs"
            >
              Temizle
            </UButton>
          </div>
          <div class="flex items-center gap-2">
            <UToggle
              v-model="autoRefresh"
              size="sm"
            />
            <span class="text-sm text-gray-500 dark:text-gray-400">Otomatik yenile ({{ refreshInterval / 1000 }}s)</span>
          </div>
        </div>
      </template>

      <div
        ref="logContainerRef"
        class="font-mono text-xs overflow-auto rounded-lg bg-gray-900 dark:bg-gray-950 text-gray-100 p-4"
        style="max-height: 70vh; min-height: 400px"
      >
        <div v-if="loading && logs.length === 0" class="py-8 text-center text-gray-500">
          Yükleniyor...
        </div>
        <div v-else-if="filteredLogs.length === 0" class="py-8 text-center text-gray-500">
          Log kaydı yok
        </div>
        <div
          v-else
          class="space-y-0.5"
        >
          <div
            v-for="(entry, i) in filteredLogs"
            :key="i"
            class="flex gap-3 py-0.5 hover:bg-gray-800/50 rounded px-1 -mx-1"
          >
            <span class="shrink-0 text-gray-500 tabular-nums">
              {{ formatTime(entry.timestamp) }}
            </span>
            <UBadge
              :color="levelColor(entry.level)"
              variant="soft"
              size="xs"
              class="shrink-0 w-16 justify-center"
            >
              {{ entry.level }}
            </UBadge>
            <span class="break-all">{{ entry.message }}</span>
          </div>
        </div>
      </div>

      <template #footer>
        <p class="text-sm text-gray-500 dark:text-gray-400">
          Son {{ filteredLogs.length }} kayıt ({{ selectedLevel === 'all' ? 'tüm seviyeler' : selectedLevel }})
        </p>
      </template>
    </UCard>
  </div>
</template>

<script setup lang="ts">
import type { LogEntry } from '~/composables/useEngineApi'

const { getLogs, clearLogs: apiClearLogs } = useEngineApi()

const logs = ref<LogEntry[]>([])
const loading = ref(false)
const clearing = ref(false)
const autoRefresh = ref(true)
const refreshInterval = 2000
const selectedLevel = ref('all')
const logContainerRef = ref<HTMLElement | null>(null)

const levelOptions = [
  { value: 'all', label: 'Tümü' },
  { value: 'Verbose', label: 'Verbose' },
  { value: 'Debug', label: 'Debug' },
  { value: 'Information', label: 'Info' },
  { value: 'Warning', label: 'Warning' },
  { value: 'Error', label: 'Error' },
  { value: 'Fatal', label: 'Fatal' }
]

const filteredLogs = computed(() => {
  if (selectedLevel.value === 'all') return logs.value
  return logs.value.filter((e) => e.level === selectedLevel.value)
})

function formatTime(ts: string | undefined) {
  if (!ts) return '-'
  try {
    const d = new Date(ts)
    return d.toLocaleTimeString('tr-TR', { hour12: false })
  } catch {
    return ts
  }
}

function levelColor(level: string) {
  switch (level) {
    case 'Error':
    case 'Fatal':
      return 'red'
    case 'Warning':
      return 'yellow'
    case 'Information':
      return 'green'
    case 'Debug':
      return 'blue'
    default:
      return 'gray'
  }
}

async function fetchLogs() {
  loading.value = true
  try {
    logs.value = await getLogs(500)
    nextTick(() => {
      if (logContainerRef.value) {
        logContainerRef.value.scrollTop = logContainerRef.value.scrollHeight
      }
    })
  } catch {
    logs.value = []
  } finally {
    loading.value = false
  }
}

async function clearLogs() {
  clearing.value = true
  try {
    await apiClearLogs()
    logs.value = []
  } catch {
    // ignore
  } finally {
    clearing.value = false
  }
}

let refreshTimer: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  fetchLogs()
  refreshTimer = setInterval(() => {
    if (autoRefresh.value) fetchLogs()
  }, refreshInterval)
})

onUnmounted(() => {
  if (refreshTimer) clearInterval(refreshTimer)
})
</script>
