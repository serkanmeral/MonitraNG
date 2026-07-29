<template>
  <div class="space-y-6">
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Politika ve sistem</h1>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Toplayıcı bağlantısı ve ajan politika ayarları
        </p>
      </div>
      <UButton size="sm" variant="outline" :loading="loading" @click="load">Yenile</UButton>
    </div>

    <UAlert v-if="message" :color="messageError ? 'red' : 'green'" variant="soft" :title="message" />

    <UCard>
      <template #header>
        <span class="font-semibold">Sistem</span>
      </template>
      <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <UFormGroup label="Toplayıcı adresi (URL)">
          <UInput v-model="system.collectorBaseUrl" />
        </UFormGroup>
        <UFormGroup label="Ana bilgisayar kimliği (boş = makine adı)">
          <UInput v-model="system.hostId" placeholder="ör. TERMINAL-pilot" />
        </UFormGroup>
        <UFormGroup label="API anahtarı (boş bırakırsanız değişmez)">
          <UInput v-model="system.apiKey" type="password" placeholder="••••••••" />
        </UFormGroup>
        <div class="text-sm text-gray-500 self-end pb-2">
          API anahtarı kayıtlı: <strong>{{ apiKeyConfigured ? 'Evet' : 'Hayır' }}</strong>
          <br />
          Yerel arayüz: {{ systemMeta.localUiHost }}:{{ systemMeta.localUiPort }}
        </div>
      </div>
      <div class="mt-4">
        <UButton color="primary" :loading="savingSystem" @click="saveSystemCfg">Sistemi kaydet</UButton>
      </div>
    </UCard>

    <UCard>
      <template #header>
        <span class="font-semibold">Politika</span>
      </template>
      <div v-if="policy" class="space-y-6">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <UFormGroup label="Alan (domain)">
            <UInput v-model="policy.domain" />
          </UFormGroup>
          <UFormGroup label="Kalp atışı aralığı (sn)">
            <UInput v-model.number="policy.heartbeatIntervalSeconds" type="number" />
          </UFormGroup>
          <UFormGroup label="Gönderim aralığı (sn)">
            <UInput v-model.number="policy.shipIntervalSeconds" type="number" />
          </UFormGroup>
          <UFormGroup label="Parti başına en fazla olay">
            <UInput v-model.number="policy.maxEventsPerBatch" type="number" />
          </UFormGroup>
        </div>

        <div class="border-t border-gray-200 dark:border-gray-700 pt-4 space-y-3">
          <p class="font-medium text-sm">Metrik</p>
          <div class="flex flex-wrap gap-6">
            <div class="flex items-center gap-2">
              <UToggle v-model="policy.metrics.enabled" />
              <span class="text-sm">Açık</span>
            </div>
            <div class="flex items-center gap-2">
              <UToggle v-model="policy.metrics.includeHostResources" />
              <span class="text-sm">İşlemci / bellek / disk</span>
            </div>
            <div class="flex items-center gap-2">
              <UToggle v-model="policy.metrics.includeTopProcesses" />
              <span class="text-sm">Üst süreç listesi (yerel)</span>
            </div>
          </div>
          <UFormGroup v-if="policy.metrics.includeTopProcesses" label="Üst süreç sayısı" class="max-w-xs">
            <UInput v-model.number="policy.metrics.topProcessCount" type="number" :min="1" :max="15" />
          </UFormGroup>
          <p class="text-xs text-gray-500">
            Üst süreç listesi Faz 1 metrik özetidir: Durum ekranında gösterilir ve
            kalp atışında toplayıcıya <span class="font-mono">process.top_cpu</span> /
            <span class="font-mono">process.top_memory</span> olarak gönderilir.
          </p>
        </div>

        <div class="border-t border-gray-200 dark:border-gray-700 pt-4 space-y-3">
          <p class="font-medium text-sm">Olay günlüğü</p>
          <div class="flex items-center gap-2">
            <UToggle v-model="policy.eventLog.enabled" />
            <span class="text-sm">Açık</span>
          </div>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <UFormGroup label="Sorgulama aralığı (sn)">
              <UInput v-model.number="policy.eventLog.pollIntervalSeconds" type="number" />
            </UFormGroup>
            <UFormGroup label="Sorgulama başına en fazla olay">
              <UInput v-model.number="policy.eventLog.maxEventsPerPoll" type="number" />
            </UFormGroup>
          </div>
          <p class="text-xs text-gray-500">
            Paket listesi boşsa varsayılan security-auth + system-lifecycle kullanılır.
          </p>
        </div>

        <div class="border-t border-gray-200 dark:border-gray-700 pt-4 space-y-3">
          <div class="flex items-center justify-between">
            <p class="font-medium text-sm">Servis izleme</p>
            <div class="flex items-center gap-2">
              <UToggle v-model="policy.serviceWatch.enabled" />
              <span class="text-sm">Açık</span>
            </div>
          </div>
          <UFormGroup label="Sorgulama aralığı (sn)">
            <UInput v-model.number="policy.serviceWatch.pollIntervalSeconds" type="number" class="max-w-xs" />
          </UFormGroup>
          <div class="space-y-2">
            <div
              v-for="(svc, i) in policy.serviceWatch.services"
              :key="i"
              class="flex flex-wrap items-center gap-2"
            >
              <UInput v-model="svc.name" placeholder="Servis adı (ör. Spooler)" class="flex-1 min-w-[12rem]" />
              <div class="flex items-center gap-2">
                <UToggle v-model="svc.restartAllowed" />
                <span class="text-xs text-gray-500">Yeniden başlatmaya izin ver</span>
              </div>
              <UButton size="xs" color="red" variant="ghost" icon="i-heroicons-trash" @click="removeService(i)" />
            </div>
            <UButton size="sm" variant="outline" icon="i-heroicons-plus" @click="addService">
              Servis ekle
            </UButton>
          </div>
        </div>

        <UButton color="primary" :loading="savingPolicy" @click="savePolicyCfg">Politikayı kaydet</UButton>
      </div>
    </UCard>
  </div>
</template>

<script setup lang="ts">
import type { PolicyConfig } from '~/composables/useAgentApi'

const { getConfig, saveSystem, savePolicy } = useAgentApi()

const loading = ref(false)
const savingSystem = ref(false)
const savingPolicy = ref(false)
const message = ref('')
const messageError = ref(false)
const apiKeyConfigured = ref(false)
const system = reactive({
  collectorBaseUrl: '',
  hostId: '',
  apiKey: ''
})
const systemMeta = reactive({
  localUiHost: '127.0.0.1',
  localUiPort: 5092
})
const policy = ref<PolicyConfig | null>(null)

function flash(text: string, isError = false) {
  message.value = text
  messageError.value = isError
}

async function load() {
  loading.value = true
  try {
    const cfg = await getConfig()
    system.collectorBaseUrl = cfg.system.collectorBaseUrl
    system.hostId = cfg.system.hostId || ''
    system.apiKey = ''
    apiKeyConfigured.value = cfg.system.apiKeyConfigured
    systemMeta.localUiHost = cfg.system.localUiHost
    systemMeta.localUiPort = cfg.system.localUiPort
    policy.value = structuredClone(cfg.policy)
  } catch (e: any) {
    flash(e?.message || 'Yapılandırma yüklenemedi', true)
  } finally {
    loading.value = false
  }
}

async function saveSystemCfg() {
  savingSystem.value = true
  try {
    const body: { collectorBaseUrl?: string; apiKey?: string; hostId?: string } = {
      collectorBaseUrl: system.collectorBaseUrl,
      hostId: system.hostId
    }
    if (system.apiKey) body.apiKey = system.apiKey
    await saveSystem(body)
    flash('Sistem ayarları kaydedildi')
    await load()
  } catch (e: any) {
    flash(e?.message || 'Sistem kaydı başarısız', true)
  } finally {
    savingSystem.value = false
  }
}

async function savePolicyCfg() {
  if (!policy.value) return
  savingPolicy.value = true
  try {
    await savePolicy(policy.value)
    flash('Politika kaydedildi (işçiler bir sonraki döngüde uygular)')
    await load()
  } catch (e: any) {
    flash(e?.message || 'Politika kaydı başarısız', true)
  } finally {
    savingPolicy.value = false
  }
}

function addService() {
  policy.value?.serviceWatch.services.push({ name: '', restartAllowed: false })
}

function removeService(i: number) {
  policy.value?.serviceWatch.services.splice(i, 1)
}

onMounted(load)
</script>
