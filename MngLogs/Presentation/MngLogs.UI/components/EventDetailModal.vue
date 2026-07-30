<template>
  <UModal
    :model-value="open"
    :ui="{ width: 'w-full sm:max-w-3xl', background: 'bg-white dark:bg-gray-900' }"
    @update:model-value="emit('update:open', $event)"
  >
    <div
      v-if="event"
      class="relative overflow-hidden rounded-lg border border-gray-200 dark:border-gray-700 shadow-xl"
    >
      <div class="absolute inset-y-0 left-0 w-1" :class="severityAccentClass(event.severity)" />

      <div class="pl-5 pr-4 pt-4 pb-3 border-b border-gray-100 dark:border-gray-800">
        <div class="flex items-start justify-between gap-3">
          <div class="min-w-0 flex-1">
            <div class="flex flex-wrap items-center gap-2 mb-1.5">
              <UBadge size="sm" variant="soft" :color="severityColor(event.severity)">
                {{ severityLabel(event.severity) }}
              </UBadge>
              <UBadge
                v-if="event.direction"
                size="sm"
                variant="soft"
                :color="event.direction === 'shipped' ? 'primary' : 'amber'"
              >
                {{ event.direction === 'shipped' ? 'Gönderildi' : 'Üretildi' }}
              </UBadge>
              <span
                v-if="event.eventId != null"
                class="inline-flex items-center rounded-md bg-gray-100 dark:bg-gray-800 px-2 py-0.5 text-xs font-semibold tabular-nums text-gray-800 dark:text-gray-100 font-mono"
              >
                ID {{ event.eventId }}
              </span>
              <span
                v-if="event.package"
                class="inline-flex items-center rounded-md bg-primary-50 dark:bg-primary-900/30 px-2 py-0.5 text-xs text-primary-700 dark:text-primary-300"
              >
                {{ event.package }}
              </span>
            </div>
            <h3 class="text-base font-semibold text-gray-900 dark:text-white leading-snug">
              Olay detayı
            </h3>
            <p class="mt-1 text-xs text-gray-500 flex flex-wrap gap-x-2 gap-y-0.5">
              <span>{{ formatDate(event.atUtc) }}</span>
              <template v-if="event.channel">
                <span class="text-gray-300 dark:text-gray-600">·</span>
                <span class="font-mono truncate max-w-[16rem]" :title="event.channel">
                  {{ event.channel }}
                </span>
              </template>
            </p>
          </div>
          <UButton
            color="gray"
            variant="ghost"
            icon="i-heroicons-x-mark-20-solid"
            size="sm"
            class="-mt-1 -mr-1"
            @click="emit('update:open', false)"
          />
        </div>
      </div>

      <div class="px-5 pt-4">
        <div
          class="rounded-lg border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50 px-3.5 py-3"
        >
          <p class="text-[11px] font-medium uppercase tracking-wide text-gray-500 mb-1.5">Mesaj</p>
          <p class="text-sm text-gray-900 dark:text-gray-100 whitespace-pre-wrap leading-relaxed max-h-28 overflow-y-auto">
            {{ event.message || event.action || '—' }}
          </p>
        </div>
      </div>

      <div class="px-5 pt-4 pb-1">
        <div class="flex gap-1 p-0.5 rounded-lg bg-gray-100 dark:bg-gray-800 w-fit">
          <button
            type="button"
            class="px-3 py-1.5 text-xs font-medium rounded-md transition-colors"
            :class="
              detailPane === 'parsed'
                ? 'bg-white dark:bg-gray-700 text-gray-900 dark:text-white shadow-sm'
                : 'text-gray-500 hover:text-gray-700 dark:hover:text-gray-300'
            "
            @click="detailPane = 'parsed'"
          >
            Parse edilmiş
          </button>
          <button
            type="button"
            class="px-3 py-1.5 text-xs font-medium rounded-md transition-colors"
            :class="
              detailPane === 'raw'
                ? 'bg-white dark:bg-gray-700 text-gray-900 dark:text-white shadow-sm'
                : 'text-gray-500 hover:text-gray-700 dark:hover:text-gray-300'
            "
            @click="detailPane = 'raw'"
          >
            Ham JSON
          </button>
        </div>
      </div>

      <div class="px-5 py-4 max-h-[min(52vh,28rem)] overflow-y-auto">
        <template v-if="detailPane === 'parsed'">
          <div class="grid grid-cols-2 sm:grid-cols-3 gap-2.5 mb-4">
            <div
              v-for="cell in metaCells"
              :key="cell.label"
              class="rounded-lg border border-gray-200 dark:border-gray-700 px-3 py-2.5 bg-white dark:bg-gray-900/40"
            >
              <p class="text-[10px] uppercase tracking-wide text-gray-400 mb-0.5">{{ cell.label }}</p>
              <p
                class="text-sm font-medium text-gray-900 dark:text-gray-100 truncate"
                :class="cell.mono ? 'font-mono text-xs' : ''"
                :title="cell.value"
              >
                {{ cell.value }}
              </p>
            </div>
          </div>

          <div v-if="extraFields.length" class="mt-1">
            <p class="text-[11px] font-medium uppercase tracking-wide text-gray-500 mb-2">Ek alanlar</p>
            <div
              class="rounded-lg border border-gray-200 dark:border-gray-700 divide-y divide-gray-100 dark:divide-gray-800 overflow-hidden"
            >
              <div
                v-for="row in extraFields"
                :key="row.label"
                class="flex gap-3 px-3 py-2 text-sm bg-white dark:bg-gray-900/30"
              >
                <span class="shrink-0 w-36 text-xs text-gray-500 font-mono pt-0.5 truncate" :title="row.label">
                  {{ row.label }}
                </span>
                <span class="min-w-0 flex-1 text-gray-800 dark:text-gray-200 break-words whitespace-pre-wrap">
                  {{ row.value }}
                </span>
              </div>
            </div>
          </div>
        </template>

        <template v-else>
          <div class="flex items-center justify-between gap-2 mb-2">
            <p class="text-[11px] font-medium uppercase tracking-wide text-gray-500">Ham yük</p>
            <UButton
              size="xs"
              color="gray"
              variant="soft"
              :icon="rawCopied ? 'i-heroicons-check-20-solid' : 'i-heroicons-clipboard-document-20-solid'"
              @click="copyRaw"
            >
              {{ rawCopied ? 'Kopyalandı' : 'Kopyala' }}
            </UButton>
          </div>
          <pre
            class="text-[11px] leading-relaxed font-mono rounded-lg border border-gray-200 dark:border-gray-700 bg-slate-950 text-slate-100 p-3.5 max-h-[min(40vh,22rem)] overflow-auto whitespace-pre-wrap break-all"
          >{{ rawPretty }}</pre>
        </template>
      </div>

      <div
        class="px-5 py-3 border-t border-gray-100 dark:border-gray-800 bg-gray-50/80 dark:bg-gray-800/40 flex items-center justify-between gap-3"
      >
        <p class="text-[11px] text-gray-400 font-mono truncate min-w-0" :title="event.id || ''">
          {{ event.id || '—' }}
        </p>
        <UButton color="primary" variant="soft" size="sm" @click="emit('update:open', false)">
          Kapat
        </UButton>
      </div>
    </div>
  </UModal>
</template>

<script setup lang="ts">
import type { RecentEventEntry } from '~/composables/useAgentApi'
import { formatDate } from '~/composables/useAgentApi'

const props = defineProps<{
  open: boolean
  event: RecentEventEntry | null
}>()

const emit = defineEmits<{ 'update:open': [value: boolean] }>()

const detailPane = ref<'parsed' | 'raw'>('parsed')
const rawCopied = ref(false)
let rawCopyTimer: ReturnType<typeof setTimeout> | null = null

watch(
  () => props.event,
  () => {
    detailPane.value = 'parsed'
    rawCopied.value = false
  }
)

const metaCells = computed(() => {
  const e = props.event
  if (!e) return [] as { label: string; value: string; mono?: boolean }[]
  return [
    { label: 'Event ID', value: e.eventId != null ? String(e.eventId) : '—', mono: true },
    { label: 'Record ID', value: e.recordId != null ? String(e.recordId) : '—', mono: true },
    { label: 'Kanal', value: e.channel || '—' },
    { label: 'Paket', value: e.package || '—' },
    { label: 'Sağlayıcı', value: e.provider || '—', mono: true },
    { label: 'Kaynak', value: e.source || '—' }
  ]
})

const extraFields = computed(() => {
  const e = props.event
  if (!e?.fields) return [] as { label: string; value: string }[]
  const skip = new Set(['channel', 'package', 'provider', 'eventId', 'recordId'])
  return Object.entries(e.fields)
    .filter(([k]) => !skip.has(k))
    .map(([k, v]) => ({ label: k, value: formatFieldValue(v) }))
})

const rawPretty = computed(() => {
  const e = props.event
  if (!e) return '—'
  if (e.rawJson) {
    try {
      return JSON.stringify(JSON.parse(e.rawJson), null, 2)
    } catch {
      return e.rawJson
    }
  }
  return JSON.stringify(
    {
      id: e.id,
      atUtc: e.atUtc,
      direction: e.direction,
      source: e.source,
      severity: e.severity,
      message: e.message,
      action: e.action,
      channel: e.channel,
      package: e.package,
      eventId: e.eventId,
      recordId: e.recordId,
      provider: e.provider,
      metricName: e.metricName,
      metricValue: e.metricValue,
      fields: e.fields
    },
    null,
    2
  )
})

async function copyRaw() {
  try {
    await navigator.clipboard.writeText(rawPretty.value)
    rawCopied.value = true
    if (rawCopyTimer) clearTimeout(rawCopyTimer)
    rawCopyTimer = setTimeout(() => {
      rawCopied.value = false
    }, 1600)
  } catch {
    rawCopied.value = false
  }
}

function severityLabel(s?: string | null) {
  switch ((s || '').toLowerCase()) {
    case 'critical': return 'Kritik'
    case 'error': return 'Hata'
    case 'warning': return 'Uyarı'
    case 'verbose': return 'Ayrıntılı'
    case 'info': return 'Bilgi'
    default: return s || '—'
  }
}

function severityColor(s?: string | null): 'gray' | 'green' | 'amber' | 'red' | 'blue' | 'primary' {
  switch ((s || '').toLowerCase()) {
    case 'critical':
    case 'error':
      return 'red'
    case 'warning':
      return 'amber'
    case 'info':
      return 'blue'
    default:
      return 'gray'
  }
}

function severityAccentClass(s?: string | null) {
  switch ((s || '').toLowerCase()) {
    case 'critical':
    case 'error':
      return 'bg-red-500'
    case 'warning':
      return 'bg-amber-500'
    case 'info':
      return 'bg-blue-500'
    default:
      return 'bg-gray-400'
  }
}

function formatFieldValue(v: unknown) {
  if (v == null) return '—'
  if (typeof v === 'string' || typeof v === 'number' || typeof v === 'boolean') return String(v)
  try {
    return JSON.stringify(v)
  } catch {
    return String(v)
  }
}
</script>
