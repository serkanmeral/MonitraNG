<template>
  <div class="space-y-8">
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Engine Durumu</h1>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Konfigürasyon ve toplama durumunu görüntüleyin
        </p>
      </div>
      <div class="flex gap-2">
        <UButton
          :variant="configStatus?.hasConfig ? 'outline' : 'solid'"
          color="primary"
          icon="i-heroicons-document-plus"
          @click="openConfigModal"
        >
          {{ configStatus?.hasConfig ? 'Config Güncelle' : 'Config Ekle' }}
        </UButton>
        <UButton
          v-if="configStatus?.hasConfig"
          color="red"
          variant="outline"
          icon="i-heroicons-trash"
          :loading="deleting"
          @click="confirmDeleteConfig"
        >
          Config Sil
        </UButton>
      </div>
    </div>

    <!-- Özet Kartları -->
    <div class="grid grid-cols-1 gap-6 lg:grid-cols-2">
      <!-- Konfigürasyon -->
      <UCard>
        <template #header>
          <div class="flex items-center justify-between">
            <span class="font-semibold">Konfigürasyon</span>
            <UBadge
              :color="configStatus?.hasConfig ? 'green' : 'gray'"
              variant="soft"
              size="sm"
            >
              {{ configStatus?.hasConfig ? 'Yüklü' : 'Yok' }}
            </UBadge>
          </div>
        </template>
        <div v-if="configStatus?.hasConfig" class="space-y-2 text-sm">
          <p>
            <span class="text-gray-500 dark:text-gray-400">Engine:</span>
            <span class="font-medium">{{ displayEngineName }}</span>
          </p>
          <p v-if="configStatus.lastSyncAt">
            <span class="text-gray-500 dark:text-gray-400">Son Sync:</span>
            {{ formatDate(configStatus.lastSyncAt) }}
          </p>
          <p>
            <span class="text-gray-500 dark:text-gray-400">Agent:</span> {{ configStatus.agentCount ?? 0 }} ·
            <span class="text-gray-500 dark:text-gray-400">Asset:</span> {{ configStatus.assetConfigCount ?? 0 }}
          </p>
          <div class="pt-2 flex items-center gap-2 flex-wrap">
            <UButton
              size="xs"
              variant="outline"
              color="primary"
              icon="i-heroicons-arrow-path"
              :loading="syncLoading"
              :disabled="syncLoading"
              @click="runConfigSync"
            >
              Config sync çalıştır
            </UButton>
            <span v-if="syncMessage" class="text-xs" :class="syncError ? 'text-red-600 dark:text-red-400' : 'text-green-600 dark:text-green-400'">
              {{ syncMessage }}
            </span>
          </div>
        </div>
        <p v-else class="text-sm text-gray-500 dark:text-gray-400">
          Config henüz yüklenmedi. Yeni config eklemek için "Config Ekle" butonuna tıklayın.
        </p>
      </UCard>

      <!-- Bağlantı -->
      <UCard>
        <template #header>
          <div class="flex items-center justify-between">
            <span class="font-semibold">Bağlantı</span>
            <UBadge
              :color="healthStatus === 'healthy' ? 'green' : healthStatus === 'error' ? 'red' : 'gray'"
              variant="soft"
            >
              {{ healthLabel }}
            </UBadge>
          </div>
        </template>
        <div v-if="configStatus?.hasConfig" class="space-y-3 text-sm">
          <div>
            <span class="text-gray-500 dark:text-gray-400">Engine ID</span>
            <p class="font-mono text-gray-900 dark:text-gray-100">{{ configStatus.engineId || '-' }}</p>
          </div>
          <div v-if="configStatus.engineName">
            <span class="text-gray-500 dark:text-gray-400">Engine Adı</span>
            <p class="font-medium text-gray-900 dark:text-gray-100">{{ configStatus.engineName }}</p>
          </div>
          <div v-if="configStatus.serverUrl">
            <span class="text-gray-500 dark:text-gray-400">Reactor</span>
            <p class="font-mono text-gray-900 dark:text-gray-100 break-all">{{ configStatus.serverUrl }}</p>
          </div>
          <div v-if="configStatus.mqttUrl">
            <span class="text-gray-500 dark:text-gray-400">MQTT</span>
            <p class="font-mono text-gray-900 dark:text-gray-100 break-all">{{ configStatus.mqttUrl }}</p>
          </div>
          <p v-if="!configStatus.serverUrl && !configStatus.mqttUrl" class="text-gray-500 dark:text-gray-400">
            Bağlantı bilgisi mevcut değil
          </p>
        </div>
        <p v-else class="text-sm text-gray-500 dark:text-gray-400">
          Config yüklendiğinde Reactor ve MQTT adresleri görünecektir.
        </p>
      </UCard>
    </div>

    <!-- Toplama Durumu -->
    <UCard>
      <template #header>
        <span class="font-semibold">Toplama Durumu</span>
      </template>
      <div class="grid grid-cols-2 sm:grid-cols-4 gap-4">
        <div class="p-4 rounded-lg bg-gray-100 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700">
          <p class="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wide">Agent</p>
          <p class="mt-1 text-2xl font-semibold text-gray-900 dark:text-gray-100">{{ engineStatus?.agentCount ?? '-' }}</p>
        </div>
        <div class="p-4 rounded-lg bg-gray-100 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700">
          <p class="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wide">Asset</p>
          <p class="mt-1 text-2xl font-semibold text-gray-900 dark:text-gray-100">{{ engineStatus?.assetCount ?? '-' }}</p>
        </div>
        <div class="p-4 rounded-lg bg-gray-100 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700">
          <p class="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wide">Job</p>
          <p class="mt-1 text-2xl font-semibold text-gray-900 dark:text-gray-100">{{ engineStatus?.jobCount ?? '-' }}</p>
        </div>
        <div class="p-4 rounded-lg bg-gray-100 dark:bg-gray-800/50 border border-gray-200 dark:border-gray-700">
          <p class="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wide">Queue</p>
          <p class="mt-1 text-2xl font-semibold text-gray-900 dark:text-gray-100">{{ engineStatus?.queueBatchCount ?? '-' }}</p>
        </div>
      </div>
    </UCard>

    <!-- Config String Modal -->
    <UModal v-model="configModalOpen">
      <UCard class="overflow-hidden">
        <template #header>
          <div class="flex items-center justify-between">
            <span class="font-semibold">{{ configStatus?.hasConfig ? 'Config Güncelle' : 'Config Ekle' }}</span>
            <UButton size="xs" variant="ghost" color="neutral" icon="i-heroicons-x-mark" @click="configModalOpen = false" />
          </div>
        </template>
        <div class="space-y-4">
          <p class="text-sm text-gray-500 dark:text-gray-400">
            MonitraNG UI'dan alınan config string'i aşağıya yapıştırın. Mevcut config güncellenecektir.
          </p>
          <UTextarea
            v-model="configText"
            placeholder="Config string'i buraya yapıştırın..."
            :rows="5"
            class="font-mono text-sm"
          />
          <div class="flex gap-2">
            <UButton :loading="saving" :disabled="!configText.trim()" @click="saveConfig">
              Kaydet
            </UButton>
            <UButton variant="outline" @click="configModalOpen = false">
              İptal
            </UButton>
          </div>
          <UAlert
            v-if="configMessage"
            :color="configError ? 'red' : 'green'"
            :title="configError ? 'Hata' : 'Başarılı'"
            :description="configMessage"
            @close="configMessage = ''"
          />
        </div>
      </UCard>
    </UModal>

    <!-- Config Sil Onay Modal -->
    <UModal v-model="deleteConfirmOpen">
      <UCard class="overflow-hidden">
        <template #header>
          <div class="flex items-center justify-between">
            <span class="font-semibold text-red-600 dark:text-red-400">Config Sil</span>
            <UButton size="xs" variant="ghost" color="neutral" icon="i-heroicons-x-mark" @click="deleteConfirmOpen = false" />
          </div>
        </template>
        <div class="space-y-4">
          <p class="text-sm text-gray-700 dark:text-gray-300">
            Config silindiğinde Engine <strong>sıfır kurulum moduna</strong> geçer. Tüm cache ve sync verileri temizlenir.
            Yeni config yapıştırarak tekrar başlatabilirsiniz.
          </p>
          <p class="text-sm text-gray-600 dark:text-gray-400">
            Bu işlemi gerçekleştirmek istediğinize emin misiniz?
          </p>
          <div class="flex gap-2 justify-end">
            <UButton variant="outline" @click="deleteConfirmOpen = false">İptal</UButton>
            <UButton color="red" :loading="deleting" @click="deleteConfig">
              Evet, Sil
            </UButton>
          </div>
        </div>
      </UCard>
    </UModal>
  </div>
</template>

<script setup lang="ts">
import type { ConfigStatus, EngineStatus } from '~/composables/useEngineApi'

const { applyConfig, deleteConfig: deleteConfigApi, getConfigStatus, triggerConfigSync, getStatus, getHealth } = useEngineApi()

const configText = ref('')
const saving = ref(false)
const deleting = ref(false)
const configMessage = ref('')
const configError = ref(false)
const configStatus = ref<ConfigStatus | null>(null)
const engineStatus = ref<EngineStatus | null>(null)
const healthStatus = ref<'healthy' | 'error' | 'loading'>('loading')
const configModalOpen = ref(false)
const deleteConfirmOpen = ref(false)
const syncLoading = ref(false)
const syncMessage = ref('')
const syncError = ref(false)

const displayEngineName = computed(() => {
  const s = configStatus.value
  if (!s) return '-'
  if (s.engineName) return `${s.engineName} (${s.engineId || ''})`
  return s.engineId || '-'
})

const healthLabel = computed(() => {
  switch (healthStatus.value) {
    case 'healthy': return 'Bağlantı OK'
    case 'error': return 'Bağlantı Hata'
    default: return 'Kontrol ediliyor...'
  }
})

function formatDate(s: string | undefined) {
  if (!s) return '-'
  try {
    return new Date(s).toLocaleString('tr-TR')
  } catch {
    return s
  }
}

function openConfigModal() {
  configText.value = ''
  configMessage.value = ''
  configError.value = false
  configModalOpen.value = true
}

function confirmDeleteConfig() {
  deleteConfirmOpen.value = true
}

async function refreshConfigStatus() {
  try {
    configStatus.value = await getConfigStatus()
  } catch {
    configStatus.value = null
  }
}

async function refreshStatus() {
  try {
    engineStatus.value = await getStatus()
  } catch {
    engineStatus.value = null
  }
}

async function checkHealth() {
  try {
    await getHealth()
    healthStatus.value = 'healthy'
  } catch {
    healthStatus.value = 'error'
  }
}

async function saveConfig() {
  if (!configText.value.trim()) return
  saving.value = true
  configMessage.value = ''
  configError.value = false
  try {
    await applyConfig(configText.value.trim())
    configMessage.value = 'Config başarıyla kaydedildi.'
    configError.value = false
    configModalOpen.value = false
    await refreshConfigStatus()
    await refreshStatus()
  } catch (e: unknown) {
    configError.value = true
    configMessage.value = e instanceof Error ? e.message : 'Kaydetme hatası'
  } finally {
    saving.value = false
  }
}

async function deleteConfig() {
  deleting.value = true
  try {
    await deleteConfigApi()
    deleteConfirmOpen.value = false
    await refreshConfigStatus()
    await refreshStatus()
  } catch (e: unknown) {
    configMessage.value = e instanceof Error ? e.message : 'Config silinemedi'
    configError.value = true
  } finally {
    deleting.value = false
  }
}

async function runConfigSync() {
  syncLoading.value = true
  syncMessage.value = ''
  syncError.value = false
  try {
    await triggerConfigSync()
    syncMessage.value = 'Config sync tetiklendi.'
    await refreshConfigStatus()
    await refreshStatus()
    setTimeout(() => { syncMessage.value = '' }, 4000)
  } catch (e: unknown) {
    syncError.value = true
    syncMessage.value = e instanceof Error ? e.message : 'Sync tetiklenemedi'
  } finally {
    syncLoading.value = false
  }
}

let refreshTimer: ReturnType<typeof setInterval> | null = null

onMounted(async () => {
  await Promise.all([refreshConfigStatus(), refreshStatus(), checkHealth()])
  refreshTimer = setInterval(() => {
    refreshConfigStatus()
    refreshStatus()
  }, 30000)
})

onUnmounted(() => {
  if (refreshTimer) clearInterval(refreshTimer)
})
</script>
