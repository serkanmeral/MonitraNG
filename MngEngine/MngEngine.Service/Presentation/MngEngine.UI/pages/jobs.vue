<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Job'lar</h1>
      <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
        Quartz scheduler job listesi – cron periyotları ve işlem detayları
      </p>
    </div>

    <UCard>
      <template #header>
        <div class="flex items-center justify-between">
          <span class="font-semibold">Job Listesi</span>
          <div class="flex items-center gap-2">
            <UButton size="sm" variant="outline" :loading="loading" @click="fetchJobs">
              Yenile
            </UButton>
            <UBadge color="neutral" variant="soft">{{ jobs.length }} job</UBadge>
          </div>
        </div>
      </template>

      <div v-if="loading && jobs.length === 0" class="py-12 text-center text-gray-500">
        Yükleniyor...
      </div>
      <div v-else-if="jobs.length === 0" class="py-12 text-center text-gray-500">
        Job bulunamadı.
      </div>
      <div v-else class="overflow-x-auto">
        <table class="w-full min-w-full divide-y divide-gray-200 dark:divide-gray-700">
          <thead class="bg-gray-50 dark:bg-gray-800">
            <tr>
              <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Name</th>
              <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Cron</th>
              <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Sonraki Çalışma</th>
              <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase"></th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
            <tr
              v-for="(j, i) in jobs"
              :key="`${j.group}-${j.name}-${i}`"
              class="bg-white dark:bg-gray-900 hover:bg-gray-50 dark:hover:bg-gray-800 cursor-pointer transition-colors"
              @click="selectedJob = j"
            >
              <td class="px-4 py-3 text-sm">
                <code class="text-xs bg-gray-100 dark:bg-gray-800 px-1.5 py-0.5 rounded">{{ jobShortName(j.name) }}</code>
              </td>
              <td class="px-4 py-3 text-sm text-gray-600 dark:text-gray-300 font-mono text-xs">{{ j.cronExpression || '-' }}</td>
              <td class="px-4 py-3 text-sm text-gray-500 dark:text-gray-400">
                {{ formatNextFire(j.nextFireTimeUtc) }}
              </td>
              <td class="px-4 py-3">
                <UButton size="xs" variant="ghost" icon="i-heroicons-eye" @click.stop="selectedJob = j">
                  Detay
                </UButton>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>

    <!-- Detay Modal -->
    <UModal v-model="detailOpen" :ui="{ width: 'max-w-2xl' }">
      <UCard v-if="selectedJob" class="overflow-hidden">
        <template #header>
          <div class="flex items-center justify-between">
            <span class="font-semibold">{{ jobShortName(selectedJob.name) }}</span>
            <UButton size="xs" variant="ghost" color="neutral" icon="i-heroicons-x-mark" @click="detailOpen = false" />
          </div>
        </template>
        <div class="space-y-6">
          <p class="text-sm text-gray-500 dark:text-gray-400">{{ selectedJob.description || selectedJob.name }}</p>

          <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label class="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Cron İfadesi</label>
              <p class="mt-1 font-mono text-sm text-gray-900 dark:text-gray-100">
                {{ selectedJob.cronExpression || 'Belirtilmemiş' }}
              </p>
              <p v-if="selectedJob.cronExpression" class="mt-0.5 text-xs text-amber-600 dark:text-amber-400">
                Quartz format (saniye dahil): sn dk sa gün ay hafta
              </p>
            </div>
            <div>
              <label class="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Sonraki Çalışma (UTC)</label>
              <p class="mt-1 text-sm text-gray-900 dark:text-gray-100">
                {{ formatNextFire(selectedJob.nextFireTimeUtc) || '-' }}
              </p>
            </div>
          </div>

          <!-- CollectorJob: Toplanan Asset'ler -->
          <div v-if="selectedJob.assets && selectedJob.assets.length > 0">
            <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Toplanan Asset'ler</h3>
            <p class="mt-0.5 text-xs text-amber-600 dark:text-amber-400">
              Not: Engine şu an tüm asset'leri aynı cron periyotta topluyor. Config'teki period bilgisi ileride kullanılacak.
            </p>
            <div class="mt-3 overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
              <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
                <thead class="bg-gray-50 dark:bg-gray-800">
                  <tr>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 dark:text-gray-400">Agent</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 dark:text-gray-400">Asset</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 dark:text-gray-400">Config Periyodu</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr v-for="a in selectedJob.assets" :key="a.assetId" class="bg-white dark:bg-gray-900">
                    <td class="px-3 py-2 text-sm text-gray-900 dark:text-gray-100">{{ a.agentName || a.assetId }}</td>
                    <td class="px-3 py-2 text-sm text-gray-900 dark:text-gray-100">{{ a.assetName || a.assetId }}</td>
                    <td class="px-3 py-2 text-sm font-mono text-gray-600 dark:text-gray-400">{{ a.periodExpression || 'Varsayılan' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
          <div v-else-if="isCollectorJob(selectedJob)" class="rounded-lg border border-amber-200 dark:border-amber-800 bg-amber-50 dark:bg-amber-900/20 p-4">
            <p class="text-sm text-amber-800 dark:text-amber-200">
              Config henüz senkronize edilmedi veya toplanacak asset yok. ConfigSyncJob çalıştıktan sonra bu listede asset'ler görünecektir.
            </p>
          </div>

          <div class="flex justify-end pt-2">
            <UButton variant="outline" @click="detailOpen = false">Kapat</UButton>
          </div>
        </div>
      </UCard>
    </UModal>
  </div>
</template>

<script setup lang="ts">
import type { JobDetail } from '~/composables/useEngineApi'

const CollectorJobName = 'MngEngine.Persistence.Jobs.CollectorJob'

const { getJobs } = useEngineApi()

const jobs = ref<JobDetail[]>([])
const loading = ref(false)
const selectedJob = ref<JobDetail | null>(null)
const detailOpen = ref(false)

watch(selectedJob, (v) => { detailOpen.value = !!v })
watch(detailOpen, (v) => { if (!v) selectedJob.value = null })

function jobShortName(name: string): string {
  if (name.endsWith('CollectorJob')) return 'CollectorJob'
  if (name.endsWith('SendJob')) return 'SendJob'
  if (name.endsWith('ConfigSyncJob')) return 'ConfigSyncJob'
  return name
}

function isCollectorJob(j: JobDetail): boolean {
  return j.name === CollectorJobName
}

function formatNextFire(iso?: string | null): string {
  if (!iso) return '-'
  try {
    const d = new Date(iso)
    return d.toLocaleString('tr-TR', { timeZone: 'UTC' })
  } catch {
    return iso
  }
}

async function fetchJobs() {
  loading.value = true
  try {
    jobs.value = await getJobs()
  } catch {
    jobs.value = []
  } finally {
    loading.value = false
  }
}

onMounted(fetchJobs)
</script>
