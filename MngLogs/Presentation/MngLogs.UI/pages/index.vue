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
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Tür</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Event ID</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Kanal</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Paket</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Özet</th>
                    <th class="px-3 py-2 text-right text-xs font-medium text-gray-500 uppercase"> </th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr v-for="(e, i) in eventLogRows" :key="e.id || i">
                    <td class="px-3 py-2 text-sm text-gray-500 whitespace-nowrap">
                      <div>{{ formatRelativeTr(e.atUtc, nowMs) }}</div>
                      <div class="text-[11px] text-gray-400">{{ formatDate(e.atUtc) }}</div>
                    </td>
                    <td class="px-3 py-2 text-sm">
                      <UBadge size="xs" variant="soft" :color="severityColor(e.severity)">
                        {{ severityLabel(e.severity) }}
                      </UBadge>
                    </td>
                    <td class="px-3 py-2 text-sm tabular-nums font-mono">
                      {{ e.eventId ?? '—' }}
                    </td>
                    <td class="px-3 py-2 text-sm text-gray-600 dark:text-gray-300 max-w-[8rem] truncate" :title="e.channel || ''">
                      {{ e.channel || '—' }}
                    </td>
                    <td class="px-3 py-2 text-sm text-gray-500 max-w-[7rem] truncate" :title="e.package || ''">
                      {{ e.package || '—' }}
                    </td>
                    <td class="px-3 py-2 text-sm font-medium max-w-[14rem] truncate" :title="e.message || e.action || ''">
                      {{ summarizeEvent(e) }}
                    </td>
                    <td class="px-3 py-2 text-right whitespace-nowrap">
                      <UButton size="xs" variant="soft" color="gray" @click="openEventDetail(e)">
                        Detayları göster
                      </UButton>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>

          <UModal
            v-model="eventDetailOpen"
            :ui="{ width: 'w-full sm:max-w-3xl', background: 'bg-white dark:bg-gray-900' }"
          >
            <div
              v-if="selectedEvent"
              class="relative overflow-hidden rounded-lg border border-gray-200 dark:border-gray-700 shadow-xl"
            >
              <!-- Severity accent bar -->
              <div
                class="absolute inset-y-0 left-0 w-1"
                :class="severityAccentClass(selectedEvent.severity)"
              />

              <!-- Header -->
              <div class="pl-5 pr-4 pt-4 pb-3 border-b border-gray-100 dark:border-gray-800">
                <div class="flex items-start justify-between gap-3">
                  <div class="min-w-0 flex-1">
                    <div class="flex flex-wrap items-center gap-2 mb-1.5">
                      <UBadge size="sm" variant="soft" :color="severityColor(selectedEvent.severity)">
                        {{ severityLabel(selectedEvent.severity) }}
                      </UBadge>
                      <span
                        v-if="selectedEvent.eventId != null"
                        class="inline-flex items-center rounded-md bg-gray-100 dark:bg-gray-800 px-2 py-0.5 text-xs font-semibold tabular-nums text-gray-800 dark:text-gray-100 font-mono"
                      >
                        ID {{ selectedEvent.eventId }}
                      </span>
                      <span
                        v-if="selectedEvent.package"
                        class="inline-flex items-center rounded-md bg-primary-50 dark:bg-primary-900/30 px-2 py-0.5 text-xs text-primary-700 dark:text-primary-300"
                      >
                        {{ selectedEvent.package }}
                      </span>
                    </div>
                    <h3 class="text-base font-semibold text-gray-900 dark:text-white leading-snug">
                      Olay detayı
                    </h3>
                    <p class="mt-1 text-xs text-gray-500 flex flex-wrap gap-x-2 gap-y-0.5">
                      <span>{{ formatDate(selectedEvent.atUtc) }}</span>
                      <span class="text-gray-300 dark:text-gray-600">·</span>
                      <span>{{ formatRelativeTr(selectedEvent.atUtc, nowMs) }}</span>
                      <template v-if="selectedEvent.channel">
                        <span class="text-gray-300 dark:text-gray-600">·</span>
                        <span class="font-mono truncate max-w-[16rem]" :title="selectedEvent.channel">
                          {{ selectedEvent.channel }}
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
                    @click="eventDetailOpen = false"
                  />
                </div>
              </div>

              <!-- Message callout -->
              <div class="px-5 pt-4">
                <div
                  class="rounded-lg border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50 px-3.5 py-3"
                >
                  <p class="text-[11px] font-medium uppercase tracking-wide text-gray-500 mb-1.5">
                    Mesaj
                  </p>
                  <p class="text-sm text-gray-900 dark:text-gray-100 whitespace-pre-wrap leading-relaxed max-h-28 overflow-y-auto">
                    {{ selectedEvent.message || selectedEvent.action || '—' }}
                  </p>
                </div>
              </div>

              <!-- Inner tabs -->
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
                      v-for="cell in selectedMetaCells"
                      :key="cell.label"
                      class="rounded-lg border border-gray-200 dark:border-gray-700 px-3 py-2.5 bg-white dark:bg-gray-900/40"
                    >
                      <p class="text-[10px] uppercase tracking-wide text-gray-400 mb-0.5">
                        {{ cell.label }}
                      </p>
                      <p
                        class="text-sm font-medium text-gray-900 dark:text-gray-100 truncate"
                        :class="cell.mono ? 'font-mono text-xs' : ''"
                        :title="cell.value"
                      >
                        {{ cell.value }}
                      </p>
                    </div>
                  </div>

                  <div v-if="selectedExtraFields.length" class="mt-1">
                    <p class="text-[11px] font-medium uppercase tracking-wide text-gray-500 mb-2">
                      Ek alanlar
                    </p>
                    <div
                      class="rounded-lg border border-gray-200 dark:border-gray-700 divide-y divide-gray-100 dark:divide-gray-800 overflow-hidden"
                    >
                      <div
                        v-for="row in selectedExtraFields"
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
                    <p class="text-[11px] font-medium uppercase tracking-wide text-gray-500">
                      Ham yük
                    </p>
                    <UButton
                      size="xs"
                      color="gray"
                      variant="soft"
                      :icon="rawCopied ? 'i-heroicons-check-20-solid' : 'i-heroicons-clipboard-document-20-solid'"
                      @click="copyRawJson"
                    >
                      {{ rawCopied ? 'Kopyalandı' : 'Kopyala' }}
                    </UButton>
                  </div>
                  <pre
                    class="text-[11px] leading-relaxed font-mono rounded-lg border border-gray-200 dark:border-gray-700 bg-slate-950 text-slate-100 p-3.5 max-h-[min(40vh,22rem)] overflow-auto whitespace-pre-wrap break-all"
                  >{{ selectedRawPretty }}</pre>
                </template>
              </div>

              <!-- Footer -->
              <div
                class="px-5 py-3 border-t border-gray-100 dark:border-gray-800 bg-gray-50/80 dark:bg-gray-800/40 flex items-center justify-between gap-3"
              >
                <p class="text-[11px] text-gray-400 font-mono truncate min-w-0" :title="selectedEvent.id || ''">
                  {{ selectedEvent.id || '—' }}
                </p>
                <UButton color="primary" variant="soft" size="sm" @click="eventDetailOpen = false">
                  Kapat
                </UButton>
              </div>
            </div>
          </UModal>
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
            <h3 class="text-sm font-semibold text-gray-900 dark:text-white mb-3">İzlenen hedefler</h3>
            <div
              v-if="!(status?.watchSnapshot?.length)"
              class="text-sm text-gray-500 py-6 text-center rounded-lg border border-dashed border-gray-200 dark:border-gray-700"
            >
              Henüz hedef yok veya ilk tarama bekleniyor. Politika’dan servis / uygulama ekleyin.
            </div>
            <div v-else class="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
              <table class="w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
                <thead class="bg-gray-50 dark:bg-gray-800/80">
                  <tr>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Tip</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Ad</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Durum</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Detay</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Son OS</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Restart</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr v-for="w in (status?.watchSnapshot || [])" :key="`${w.kind}-${w.name}`">
                    <td class="px-3 py-2">
                      <UBadge size="xs" variant="soft" :color="w.kind === 'application' ? 'primary' : 'gray'">
                        {{ w.kind === 'application' ? 'Uygulama' : 'Servis' }}
                      </UBadge>
                    </td>
                    <td class="px-3 py-2 font-medium">
                      {{ w.displayName || w.name }}
                      <span v-if="w.displayName" class="block text-xs text-gray-400 font-mono">{{ w.name }}</span>
                    </td>
                    <td class="px-3 py-2">
                      <UBadge size="xs" variant="soft" :color="watchHealthColor(w.health)">
                        {{ watchHealthLabel(w.health) }}
                      </UBadge>
                    </td>
                    <td class="px-3 py-2 text-gray-500">
                      <template v-if="w.kind === 'application'">
                        {{ w.instanceCount ?? 0 }} / {{ w.minCount ?? 1 }} örnek
                      </template>
                      <template v-else>
                        {{ w.statusText || '—' }}
                      </template>
                    </td>
                    <td class="px-3 py-2 text-xs text-gray-500 max-w-[14rem]">
                      <template v-if="w.kind === 'service' && w.lastOsEventId">
                        <span class="font-mono">{{ w.lastOsEventId }}</span>
                        <span v-if="w.lastOsEventAction"> · {{ w.lastOsEventAction }}</span>
                        <span v-if="w.lastOsEventAtUtc" class="block text-gray-400">
                          {{ formatRelativeTr(w.lastOsEventAtUtc, nowMs) }}
                        </span>
                      </template>
                      <template v-else>—</template>
                    </td>
                    <td class="px-3 py-2 text-xs text-gray-500">
                      <template v-if="!w.restartAllowed">Kapalı</template>
                      <template v-else>
                        <span :class="w.lastRestartOk === false ? 'text-amber-600' : ''">
                          {{ w.lastRestartOk == null ? 'Bekliyor' : (w.lastRestartOk ? 'OK' : 'Fail') }}
                        </span>
                        <span v-if="(w.restartAttemptCount || 0) > 0"> · {{ w.restartAttemptCount }}x</span>
                        <span v-if="w.lastRestartAtUtc" class="block text-gray-400">
                          {{ formatRelativeTr(w.lastRestartAtUtc, nowMs) }}
                        </span>
                      </template>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>

          <section>
            <div class="flex items-center justify-between gap-2 mb-3">
              <h3 class="text-sm font-semibold text-gray-900 dark:text-white">Son izleme olayları</h3>
              <NuxtLink
                to="/sources"
                class="text-sm text-primary-600 dark:text-primary-400 hover:underline"
              >
                Kaynaklar
              </NuxtLink>
            </div>
            <div
              v-if="!watchEventRows.length"
              class="text-sm text-gray-500 py-6 text-center rounded-lg border border-dashed border-gray-200 dark:border-gray-700"
            >
              Geçiş olayı yok (hedef sağlıklıysa veya henüz değişim yoksa normal).
            </div>
            <div v-else class="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
              <table class="w-full divide-y divide-gray-200 dark:divide-gray-700">
                <thead class="bg-gray-50 dark:bg-gray-800/80">
                  <tr>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Zaman</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Tip</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Tür</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Olay</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Hedef</th>
                    <th class="px-3 py-2 text-right text-xs font-medium text-gray-500 uppercase"> </th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr v-for="(e, i) in watchEventRows" :key="e.id || i">
                    <td class="px-3 py-2 text-sm text-gray-500 whitespace-nowrap">
                      {{ formatRelativeTr(e.atUtc, nowMs) }}
                    </td>
                    <td class="px-3 py-2 text-sm">
                      <UBadge size="xs" variant="soft" :color="e.source === 'app-watch' ? 'primary' : 'gray'">
                        {{ e.source === 'app-watch' ? 'Uygulama' : 'Servis' }}
                      </UBadge>
                    </td>
                    <td class="px-3 py-2 text-sm">
                      <UBadge size="xs" variant="soft" :color="severityColor(e.severity)">
                        {{ severityLabel(e.severity) }}
                      </UBadge>
                    </td>
                    <td class="px-3 py-2 text-sm font-medium font-mono text-xs">
                      {{ e.action || e.message || '—' }}
                    </td>
                    <td class="px-3 py-2 text-sm text-gray-600 dark:text-gray-300">
                      {{ e.detail || '—' }}
                    </td>
                    <td class="px-3 py-2 text-right">
                      <UButton size="xs" variant="soft" color="gray" @click="openEventDetail(e)">
                        Detay
                      </UButton>
                    </td>
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
import type { AgentStatus, FreshnessKind, RecentEventEntry, TopProcessItem } from '~/composables/useAgentApi'
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
const eventDetailOpen = ref(false)
const selectedEvent = ref<RecentEventEntry | null>(null)
const detailPane = ref<'parsed' | 'raw'>('parsed')
const rawCopied = ref(false)
let rawCopyTimer: ReturnType<typeof setTimeout> | null = null

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
    'İzleme',
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
    label: 'İzleme',
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

const watchEventRows = computed(() =>
  (status.value?.latestLogs || []).filter(
    e => e.source === 'service-watch' || e.source === 'app-watch'
  )
)

function watchHealthColor(h?: string) {
  if (h === 'Running') return 'green'
  if (h === 'NotRunning' || h === 'Missing') return 'red'
  return 'gray'
}

function watchHealthLabel(h?: string) {
  switch (h) {
    case 'Running':
      return 'Çalışıyor'
    case 'NotRunning':
      return 'Durmuş'
    case 'Missing':
      return 'Yok'
    case 'Unknown':
      return 'Bilinmiyor'
    default:
      return h || '—'
  }
}

const selectedMetaCells = computed(() => {
  const e = selectedEvent.value
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

const selectedExtraFields = computed(() => {
  const e = selectedEvent.value
  if (!e?.fields) return [] as { label: string; value: string }[]
  const skip = new Set(['channel', 'package', 'provider', 'eventId', 'recordId'])
  return Object.entries(e.fields)
    .filter(([k]) => !skip.has(k))
    .map(([k, v]) => ({ label: k, value: formatFieldValue(v) }))
})

const selectedRawPretty = computed(() => {
  const e = selectedEvent.value
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
      source: e.source,
      severity: e.severity,
      message: e.message,
      channel: e.channel,
      package: e.package,
      eventId: e.eventId,
      recordId: e.recordId,
      provider: e.provider,
      fields: e.fields
    },
    null,
    2
  )
})

function openEventDetail(e: RecentEventEntry) {
  selectedEvent.value = e
  detailPane.value = 'parsed'
  rawCopied.value = false
  eventDetailOpen.value = true
}

async function copyRawJson() {
  try {
    await navigator.clipboard.writeText(selectedRawPretty.value)
    rawCopied.value = true
    if (rawCopyTimer) clearTimeout(rawCopyTimer)
    rawCopyTimer = setTimeout(() => {
      rawCopied.value = false
    }, 1600)
  } catch {
    rawCopied.value = false
  }
}

function summarizeEvent(e: RecentEventEntry) {
  const text = (e.message || e.action || '—').replace(/\s+/g, ' ').trim()
  return text.length > 80 ? `${text.slice(0, 80)}…` : text
}

function severityLabel(s?: string | null) {
  switch ((s || '').toLowerCase()) {
    case 'critical':
      return 'Kritik'
    case 'error':
      return 'Hata'
    case 'warning':
      return 'Uyarı'
    case 'verbose':
      return 'Ayrıntılı'
    case 'info':
      return 'Bilgi'
    default:
      return s || '—'
  }
}

function severityColor(s?: string | null): 'gray' | 'green' | 'amber' | 'red' | 'blue' {
  switch ((s || '').toLowerCase()) {
    case 'critical':
    case 'error':
      return 'red'
    case 'warning':
      return 'amber'
    case 'info':
      return 'blue'
    case 'verbose':
      return 'gray'
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
      return 'bg-sky-500'
    default:
      return 'bg-gray-400'
  }
}

function formatFieldValue(v: unknown) {
  if (v == null) return '—'
  if (typeof v === 'string') return v
  try {
    return JSON.stringify(v)
  } catch {
    return String(v)
  }
}

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
  if (!s?.serviceWatchEnabled) return 'Politika: izleme kapalı'
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
