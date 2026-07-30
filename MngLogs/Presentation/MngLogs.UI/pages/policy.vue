<template>
  <div class="space-y-6">
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Politika ve sistem</h1>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Toplayıcı bağlantısı ve ajan politika ayarları — yazma işlemleri PIN ile korunur
        </p>
      </div>
      <div class="flex flex-wrap gap-2">
        <UButton
          v-if="auth?.unlocked"
          size="sm"
          variant="outline"
          color="amber"
          icon="i-heroicons-lock-closed"
          :loading="locking"
          @click="doLock"
        >
          Kilitle
        </UButton>
        <UButton
          v-if="auth?.unlocked"
          size="sm"
          variant="outline"
          :loading="loading"
          @click="refreshAll"
        >
          Yenile
        </UButton>
        <UButton
          v-if="auth?.unlocked && activeTabKey !== 'system'"
          size="sm"
          color="primary"
          :loading="savingPolicy"
          :disabled="!policy"
          @click="savePolicyCfg"
        >
          Politikayı kaydet
        </UButton>
        <UButton
          v-else-if="auth?.unlocked"
          size="sm"
          color="primary"
          :loading="savingSystem"
          @click="saveSystemCfg"
        >
          Sistemi kaydet
        </UButton>
      </div>
    </div>

    <UAlert v-if="message" :color="messageError ? 'red' : 'green'" variant="soft" :title="message" />

    <div v-if="authLoading" class="py-12 text-center text-gray-500">Yükleniyor…</div>

    <!-- First-time PIN setup -->
    <UCard v-else-if="auth && !auth.configured" class="max-w-md mx-auto">
      <template #header>
        <span class="font-semibold">Politika PIN’i oluştur</span>
      </template>
      <p class="text-sm text-gray-500 mb-4">
        Bu makinede politika ve sistem yazma işlemlerini korumak için bir PIN belirleyin
        (en az {{ auth.minPinLength || 4 }} karakter). PIN hash olarak saklanır.
      </p>
      <div class="space-y-3">
        <UFormGroup label="PIN">
          <UInput v-model="setupPinValue" type="password" autocomplete="new-password" @keyup.enter="doSetup" />
        </UFormGroup>
        <UFormGroup label="PIN (tekrar)">
          <UInput v-model="setupPinConfirm" type="password" autocomplete="new-password" @keyup.enter="doSetup" />
        </UFormGroup>
        <UButton color="primary" block :loading="authBusy" @click="doSetup">PIN oluştur ve aç</UButton>
      </div>
    </UCard>

    <!-- Unlock -->
    <UCard v-else-if="auth && auth.configured && !auth.unlocked" class="max-w-md mx-auto">
      <template #header>
        <span class="font-semibold">Politika kilitli</span>
      </template>
      <p class="text-sm text-gray-500 mb-4">
        Değişiklik yapmak için PIN girin. Oturum yaklaşık
        {{ Math.round((auth.sessionTtlSeconds || 1200) / 60) }} dakika geçerlidir.
      </p>
      <UAlert
        v-if="auth.lockedUntilUtc"
        class="mb-3"
        color="amber"
        variant="soft"
        :title="`Geçici kilit: ${formatDate(auth.lockedUntilUtc)}`"
      />
      <div class="space-y-3">
        <UFormGroup label="PIN">
          <UInput v-model="unlockPinValue" type="password" autocomplete="current-password" @keyup.enter="doUnlock" />
        </UFormGroup>
        <UButton color="primary" block :loading="authBusy" @click="doUnlock">Kilidi aç</UButton>
      </div>
    </UCard>

    <div v-else-if="!policy && loading" class="py-12 text-center text-gray-500">Yükleniyor…</div>

    <UTabs
      v-else-if="auth?.unlocked && policy"
      v-model="activeTab"
      :items="tabItems"
      :ui="{ list: { width: 'w-full' } }"
      class="w-full"
    >
      <!-- System -->
      <template #system>
        <div class="pt-4 space-y-4">
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
          <UButton color="primary" :loading="savingSystem" @click="saveSystemCfg">Sistemi kaydet</UButton>
        </div>
      </template>

      <!-- General policy -->
      <template #general>
        <div class="pt-4 space-y-4">
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
          <UButton color="primary" :loading="savingPolicy" @click="savePolicyCfg">Politikayı kaydet</UButton>
        </div>
      </template>

      <!-- Metrics -->
      <template #metrics>
        <div class="pt-4 space-y-4">
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
          <UButton color="primary" :loading="savingPolicy" @click="savePolicyCfg">Politikayı kaydet</UButton>
        </div>
      </template>

      <!-- Event log -->
      <template #eventlog>
        <div class="pt-4 space-y-5">
          <div class="flex items-center gap-2">
            <UToggle v-model="policy.eventLog.enabled" />
            <span class="text-sm">Açık</span>
          </div>
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <UFormGroup label="Sorgulama aralığı (sn)">
              <UInput v-model.number="policy.eventLog.pollIntervalSeconds" type="number" />
            </UFormGroup>
            <UFormGroup label="Sorgulama başına en fazla olay">
              <UInput v-model.number="policy.eventLog.maxEventsPerPoll" type="number" />
            </UFormGroup>
            <UFormGroup label="Sunucu katalog sync (sn)">
              <UInput
                v-model.number="policy.eventLog.packageCatalogSyncIntervalSeconds"
                type="number"
                :min="60"
              />
            </UFormGroup>
          </div>

          <UAlert
            v-if="packagePlan?.legacyMode"
            color="amber"
            variant="soft"
            title="Eski tam-liste modu"
            description="Bu agent’ta eski Packages listesi var (sunucu ile birleştirilmiyor). Override modeline aktarın veya kaydetmeden önce özel paketleri tanımlayın."
          >
            <template #actions>
              <UButton size="xs" color="amber" variant="solid" @click="migrateLegacyToOverrides">
                Override modeline aktar
              </UButton>
            </template>
          </UAlert>

          <section class="space-y-2">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <div>
                <p class="text-sm font-medium text-gray-900 dark:text-white">Sunucu paketleri</p>
                <p class="text-xs text-gray-500">
                  Kaynak: <span class="font-mono">{{ packagePlan?.source || '—' }}</span>
                  <span v-if="packagePlan?.lastSyncedUtc">
                    · Son sync: {{ formatDate(packagePlan.lastSyncedUtc) }}
                  </span>
                </p>
              </div>
              <UButton
                size="xs"
                variant="outline"
                :loading="syncingCatalog"
                icon="i-heroicons-arrow-path"
                @click="syncCatalogNow"
              >
                Katalogu yenile
              </UButton>
            </div>

            <div class="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
              <table class="w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
                <thead class="bg-gray-50 dark:bg-gray-800/80">
                  <tr>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Paket</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Kanal</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Event ID</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Bu agent’ta</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr v-for="p in (packagePlan?.server || [])" :key="'srv-' + p.name">
                    <td class="px-3 py-2 font-medium">{{ p.name }}</td>
                    <td class="px-3 py-2 font-mono text-xs">{{ p.channel }}</td>
                    <td class="px-3 py-2 font-mono text-xs">{{ p.eventIds.join(', ') }}</td>
                    <td class="px-3 py-2">
                      <div class="flex items-center gap-2">
                        <UToggle
                          :model-value="!isServerDisabled(p.name)"
                          @update:model-value="(on: boolean) => setServerEnabled(p.name, on)"
                        />
                        <span class="text-xs text-gray-500">
                          {{ isServerDisabled(p.name) ? 'Kapalı' : 'Açık' }}
                        </span>
                      </div>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
            <p class="text-xs text-gray-500">
              İsteğe bağlı (elevation):
              <span
                v-for="o in (packagePlan?.optional || [])"
                :key="o.name"
                class="font-mono ml-1"
              >{{ o.name }}</span>
              — agent override olarak eklenebilir.
            </p>
          </section>

          <section class="space-y-2">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <div>
                <p class="text-sm font-medium text-gray-900 dark:text-white">Agent özel paketleri (override)</p>
                <p class="text-xs text-gray-500">
                  Aynı isim sunucu paketini değiştirir; yeni isim ek paket ekler.
                </p>
              </div>
            </div>

            <div
              v-for="(pkg, i) in (policy.eventLog.agentOverrides || [])"
              :key="'ovr-' + i"
              class="rounded-lg border border-gray-200 dark:border-gray-700 p-3 space-y-2"
            >
              <div class="flex flex-wrap items-start gap-2">
                <UFormGroup label="Paket adı" class="flex-1 min-w-[8rem]">
                  <UInput v-model="pkg.name" placeholder="ör. custom-app" />
                </UFormGroup>
                <UFormGroup label="Kanal" class="flex-[2] min-w-[12rem]">
                  <UInput v-model="pkg.channel" placeholder="ör. Application" />
                </UFormGroup>
                <UButton
                  size="xs"
                  color="red"
                  variant="ghost"
                  icon="i-heroicons-trash"
                  class="mt-6"
                  @click="removeOverride(i)"
                />
              </div>
              <UFormGroup label="Event ID’ler (virgülle)">
                <UInput
                  :model-value="eventIdsText(pkg)"
                  placeholder="ör. 1000, 1001"
                  @update:model-value="(v: string) => setEventIdsText(pkg, v)"
                />
              </UFormGroup>
            </div>

            <div class="flex flex-wrap gap-2 items-end">
              <UFormGroup label="Hazır / isteğe bağlı paketten ekle" class="min-w-[16rem] flex-1">
                <USelectMenu
                  v-model="presetToAdd"
                  :options="availableOverridePresets"
                  option-attribute="label"
                  value-attribute="value"
                  placeholder="Paket seç…"
                  size="sm"
                />
              </UFormGroup>
              <UButton size="sm" variant="outline" :disabled="!presetToAdd" @click="addPresetOverride">
                Hazır ekle
              </UButton>
              <UButton size="sm" variant="outline" icon="i-heroicons-plus" @click="addBlankOverride">
                Boş override
              </UButton>
            </div>
          </section>

          <section v-if="packagePlan?.effective?.length" class="space-y-2">
            <p class="text-sm font-medium text-gray-900 dark:text-white">Efektif paketler</p>
            <p class="text-xs text-gray-500">
              Sunucu ⊕ override (− kapalı) — worker’ın toplayacağı liste.
            </p>
            <div class="flex flex-wrap gap-2">
              <UBadge
                v-for="e in packagePlan.effective"
                :key="'eff-' + e.name"
                variant="soft"
                color="primary"
              >
                {{ e.name }}
              </UBadge>
            </div>
          </section>

          <UButton color="primary" :loading="savingPolicy" @click="savePolicyCfg">Politikayı kaydet</UButton>
        </div>
      </template>

      <!-- Watch -->
      <template #watch>
        <div class="pt-4 space-y-5">
          <div class="flex items-center justify-between gap-4">
            <p class="text-sm text-gray-500">Servis ve uygulama izleme tanımları</p>
            <div class="flex items-center gap-2">
              <UToggle v-model="policy.serviceWatch.enabled" />
              <span class="text-sm">Açık</span>
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <UFormGroup label="Sorgulama aralığı (sn)">
              <UInput v-model.number="policy.serviceWatch.pollIntervalSeconds" type="number" />
            </UFormGroup>
            <UFormGroup label="Restart cooldown (sn)">
              <UInput
                v-model.number="policy.serviceWatch.restartCooldownSeconds"
                type="number"
                :min="30"
              />
            </UFormGroup>
            <UFormGroup label="Max restart denemesi">
              <UInput
                v-model.number="policy.serviceWatch.restartMaxAttempts"
                type="number"
                :min="1"
              />
            </UFormGroup>
            <UFormGroup
              v-if="policy.serviceWatch.includeInventory !== false"
              label="Envanter aralığı (sn)"
            >
              <UInput v-model.number="policy.serviceWatch.inventoryIntervalSeconds" type="number" :min="15" />
            </UFormGroup>
          </div>

          <div class="flex items-center justify-between gap-4 rounded-lg border border-gray-200 dark:border-gray-700 p-3">
            <div>
              <p class="text-sm font-medium">Envanter özeti (metrik)</p>
              <p class="text-xs text-gray-500">
                Periyodik <span class="font-mono">watch.inventory</span> — hedef listesi + durum.
              </p>
            </div>
            <UToggle v-model="policy.serviceWatch.includeInventory" />
          </div>

          <p class="text-xs text-gray-500">
            Restart yalnızca <strong>Yeniden başlatmaya izin ver</strong> açıksa denenir.
            Cooldown ve max deneme aynı incident için geçerlidir; hedef toparlanınca sayaç sıfırlanır.
          </p>

          <section class="space-y-2">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <p class="text-xs font-medium text-gray-500 uppercase tracking-wide">Windows servisleri</p>
              <UButton
                size="xs"
                variant="ghost"
                :loading="loadingServices"
                icon="i-heroicons-arrow-path"
                @click="loadHostServices"
              >
                Listeyi yenile
                <span v-if="hostServices.length" class="ml-1 tabular-nums">({{ hostServices.length }})</span>
              </UButton>
            </div>
            <p class="text-xs text-gray-500">
              Listeden seçin veya yazmaya başlayıp özel ad girin. Politikaya
              <span class="font-mono">ServiceName</span> kaydedilir.
            </p>
            <UAlert
              v-if="hostServicesError"
              color="amber"
              variant="soft"
              :title="hostServicesError"
            />
            <div
              v-for="(svc, i) in policy.serviceWatch.services"
              :key="'svc-' + i"
              class="flex flex-wrap items-center gap-2 rounded-lg border border-gray-200 dark:border-gray-700 p-2.5"
            >
              <USelectMenu
                v-model="svc.name"
                :options="serviceOptionsForRow(svc.name)"
                searchable
                creatable
                value-attribute="value"
                option-attribute="label"
                placeholder="Servis ara / seç…"
                class="flex-1 min-w-[16rem]"
                size="sm"
              >
                <template #option="{ option }">
                  <div class="flex items-center justify-between gap-3 w-full min-w-0">
                    <span class="truncate">{{ option.label }}</span>
                    <UBadge
                      v-if="option.status"
                      size="xs"
                      variant="soft"
                      :color="serviceStatusColor(option.status)"
                      class="shrink-0"
                    >
                      {{ serviceStatusLabel(option.status) }}
                    </UBadge>
                  </div>
                </template>
              </USelectMenu>
              <div class="flex items-center gap-2">
                <UToggle v-model="svc.restartAllowed" />
                <span class="text-xs text-gray-500">Yeniden başlatmaya izin ver</span>
              </div>
              <UButton size="xs" color="red" variant="ghost" icon="i-heroicons-trash" @click="removeService(i)" />
            </div>
            <UButton size="sm" variant="outline" icon="i-heroicons-plus" @click="addService">
              Servis ekle
            </UButton>
          </section>

          <section class="space-y-2">
            <p class="text-xs font-medium text-gray-500 uppercase tracking-wide">Uygulamalar (process)</p>
            <p class="text-xs text-gray-500">
              Process adı (ör. <span class="font-mono">notepad</span>).
              Restart için exe yolu zorunlu —
              <strong>Gözat</strong> agent makinesinde native dosya seçici açar.
            </p>
            <div
              v-for="(app, i) in (policy.serviceWatch.applications || [])"
              :key="'app-' + i"
              class="space-y-2 rounded-lg border border-gray-200 dark:border-gray-700 p-3"
            >
              <div class="flex flex-wrap items-center gap-2">
                <UInput v-model="app.name" placeholder="Process adı (ör. notepad)" class="flex-1 min-w-[10rem]" />
                <UFormGroup label="Min" class="w-20">
                  <UInput v-model.number="app.minCount" type="number" :min="1" />
                </UFormGroup>
                <div class="flex items-center gap-2">
                  <UToggle v-model="app.restartAllowed" />
                  <span class="text-xs text-gray-500">Restart</span>
                </div>
                <UButton size="xs" color="red" variant="ghost" icon="i-heroicons-trash" @click="removeApplication(i)" />
              </div>
              <div v-if="app.restartAllowed" class="flex flex-wrap gap-2">
                <div class="flex flex-1 min-w-[16rem] gap-2">
                  <UInput
                    v-model="app.executablePath"
                    placeholder="Exe yolu (zorunlu)"
                    class="flex-1"
                  />
                  <UButton
                    size="sm"
                    variant="outline"
                    icon="i-heroicons-folder-open"
                    :loading="browsingAppIndex === i"
                    @click="browseAppExe(i)"
                  >
                    Gözat
                  </UButton>
                </div>
                <UInput
                  v-model="app.arguments"
                  placeholder="Args (opsiyonel)"
                  class="flex-1 min-w-[10rem]"
                />
                <UInput
                  v-model="app.workingDirectory"
                  placeholder="Working dir (opsiyonel)"
                  class="flex-1 min-w-[10rem]"
                />
              </div>
            </div>
            <UButton size="sm" variant="outline" icon="i-heroicons-plus" @click="addApplication">
              Uygulama ekle
            </UButton>
          </section>

          <UButton color="primary" :loading="savingPolicy" @click="savePolicyCfg">Politikayı kaydet</UButton>
        </div>
      </template>
    </UTabs>
  </div>
</template>

<script setup lang="ts">
import type {
  EventLogPackagePlan,
  HostServiceItem,
  KnownEventLogPackage,
  LocalUiAuthStatus,
  PolicyConfig
} from '~/composables/useAgentApi'
import { formatDate } from '~/composables/useAgentApi'

const {
  getConfig,
  saveSystem,
  savePolicy,
  getHostServices,
  getEventLogPackagePlan,
  syncEventLogCatalog,
  browseExecutable,
  getAuthStatus,
  setupPin,
  unlockPin,
  lockPin
} = useAgentApi()

const loading = ref(false)
const authLoading = ref(true)
const authBusy = ref(false)
const locking = ref(false)
const savingSystem = ref(false)
const savingPolicy = ref(false)
const loadingServices = ref(false)
const loadingPackagePlan = ref(false)
const syncingCatalog = ref(false)
const browsingAppIndex = ref<number | null>(null)
const activeTab = ref(0)
const message = ref('')
const messageError = ref(false)
const apiKeyConfigured = ref(false)
const hostServices = ref<HostServiceItem[]>([])
const hostServicesError = ref('')
const packagePlan = ref<EventLogPackagePlan | null>(null)
const presetToAdd = ref<string | null>(null)
const auth = ref<LocalUiAuthStatus | null>(null)
const setupPinValue = ref('')
const setupPinConfirm = ref('')
const unlockPinValue = ref('')
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

const tabItems = [
  { key: 'system', label: 'Sistem', slot: 'system' },
  { key: 'general', label: 'Genel', slot: 'general' },
  { key: 'metrics', label: 'Metrik', slot: 'metrics' },
  { key: 'eventlog', label: 'Olay günlüğü', slot: 'eventlog' },
  { key: 'watch', label: 'İzleme', slot: 'watch' }
]

const activeTabKey = computed(() => tabItems[activeTab.value]?.key || 'system')

const availableOverridePresets = computed(() => {
  const taken = new Set(
    (policy.value?.eventLog.agentOverrides || [])
      .map(p => (p.name || '').trim().toLowerCase())
      .filter(Boolean)
  )
  const fromServer = (packagePlan.value?.server || []).map(p => ({
    name: p.name,
    optional: false as boolean
  }))
  const fromOptional = (packagePlan.value?.optional || []).map(p => ({
    name: p.name,
    optional: true
  }))
  return [...fromServer, ...fromOptional]
    .filter(p => !taken.has(p.name.toLowerCase()))
    .map(p => ({
      label: p.optional ? `${p.name} (elevation)` : `${p.name} (sunucu kopyası)`,
      value: p.name
    }))
})

type EventLogPkg = { name: string; channel: string; eventIds: number[] }

function ensureOverrides() {
  if (!policy.value) return
  if (!policy.value.eventLog.agentOverrides) policy.value.eventLog.agentOverrides = []
  if (!policy.value.eventLog.disabledServerPackages) policy.value.eventLog.disabledServerPackages = []
}

function eventIdsText(pkg: EventLogPkg) {
  return (pkg.eventIds || []).join(', ')
}

function setEventIdsText(pkg: EventLogPkg, raw: string) {
  pkg.eventIds = (raw || '')
    .split(/[,;\s]+/)
    .map(s => s.trim())
    .filter(Boolean)
    .map(s => Number.parseInt(s, 10))
    .filter(n => Number.isFinite(n) && n >= 0)
}

function isServerDisabled(name: string) {
  return (policy.value?.eventLog.disabledServerPackages || []).some(
    d => d.toLowerCase() === name.toLowerCase()
  )
}

function setServerEnabled(name: string, enabled: boolean) {
  ensureOverrides()
  const list = policy.value!.eventLog.disabledServerPackages!
  const idx = list.findIndex(d => d.toLowerCase() === name.toLowerCase())
  if (enabled && idx >= 0) list.splice(idx, 1)
  if (!enabled && idx < 0) list.push(name)
}

async function loadPackagePlan() {
  loadingPackagePlan.value = true
  try {
    packagePlan.value = await getEventLogPackagePlan()
  } catch (e: any) {
    flash(apiErrorMessage(e, 'Paket planı alınamadı'), true)
  } finally {
    loadingPackagePlan.value = false
  }
}

async function syncCatalogNow() {
  syncingCatalog.value = true
  try {
    await syncEventLogCatalog()
    flash('Sunucu katalogu yenilendi')
    await loadPackagePlan()
  } catch (e: any) {
    flash(apiErrorMessage(e, 'Katalog yenilenemedi'), true)
  } finally {
    syncingCatalog.value = false
  }
}

function findPresetPackage(name: string): KnownEventLogPackage | null {
  const srv = packagePlan.value?.server?.find(p => p.name === name)
  if (srv) return { name: srv.name, channel: srv.channel, eventIds: [...srv.eventIds] }
  const opt = packagePlan.value?.optional?.find(p => p.name === name)
  if (opt) return { name: opt.name, channel: opt.channel, eventIds: [...(opt.eventIds || [])], optional: true }
  return null
}

function addBlankOverride() {
  ensureOverrides()
  policy.value!.eventLog.agentOverrides!.push({ name: '', channel: '', eventIds: [] })
}

function addPresetOverride() {
  if (!policy.value || !presetToAdd.value) return
  ensureOverrides()
  const src = findPresetPackage(presetToAdd.value)
  if (!src) return
  const exists = policy.value.eventLog.agentOverrides!.some(
    p => (p.name || '').toLowerCase() === src.name.toLowerCase()
  )
  if (exists) {
    flash('Bu override zaten listede', true)
    return
  }
  policy.value.eventLog.agentOverrides!.push({
    name: src.name,
    channel: src.channel,
    eventIds: [...src.eventIds]
  })
  presetToAdd.value = null
}

function removeOverride(i: number) {
  policy.value?.eventLog.agentOverrides?.splice(i, 1)
}

function migrateLegacyToOverrides() {
  if (!policy.value) return
  ensureOverrides()
  const legacy = policy.value.eventLog.packages || []
  const legacyNames = new Set(legacy.map(p => (p.name || '').toLowerCase()).filter(Boolean))

  policy.value.eventLog.disabledServerPackages = (packagePlan.value?.server || [])
    .map(s => s.name)
    .filter(n => !legacyNames.has(n.toLowerCase()))

  policy.value.eventLog.agentOverrides = structuredClone(legacy)
  policy.value.eventLog.packages = []
  flash('Eski liste override modeline aktarıldı — kaydetmeyi unutmayın')
  if (packagePlan.value) packagePlan.value.legacyMode = false
}

const serviceSelectOptions = computed(() =>
  hostServices.value.map(s => ({
    label: s.displayName === s.name ? s.name : `${s.displayName} (${s.name})`,
    value: s.name,
    status: s.status
  }))
)

function serviceOptionsForRow(currentName: string) {
  const taken = new Set(
    (policy.value?.serviceWatch.services || [])
      .map(s => (s.name || '').trim())
      .filter(n => n && n !== currentName)
  )
  const opts = serviceSelectOptions.value.filter(o => !taken.has(o.value))
  const cur = (currentName || '').trim()
  if (cur && !opts.some(o => o.value === cur)) {
    opts.unshift({ label: cur, value: cur, status: '' })
  }
  return opts
}

function serviceStatusColor(status?: string) {
  if (status === 'Running') return 'green'
  if (status === 'Stopped') return 'red'
  return 'gray'
}

function serviceStatusLabel(status?: string) {
  switch (status) {
    case 'Running': return 'Çalışıyor'
    case 'Stopped': return 'Durmuş'
    case 'StartPending': return 'Başlatılıyor'
    case 'StopPending': return 'Durduruluyor'
    default: return status || '—'
  }
}

function apiErrorMessage(e: any, fallback: string) {
  const status = e?.statusCode || e?.status || e?.response?.status
  if (status === 401) {
    return e?.data?.error || 'Oturum gerekli — PIN ile kilidi açın.'
  }
  if (status === 404 || status === 405) {
    return 'Agent API güncel değil. Agent’ı yeni derlemeyle yeniden başlatın (net9.0-windows).'
  }
  const data = e?.data
  if (typeof data === 'string' && data.includes('<!DOCTYPE html>')) {
    return 'Agent API güncel değil. Agent’ı yeni derlemeyle yeniden başlatın (net9.0-windows).'
  }
  return e?.data?.error || e?.data?.message || e?.message || fallback
}

async function refreshAuth() {
  authLoading.value = true
  try {
    auth.value = await getAuthStatus()
  } catch (e: any) {
    auth.value = null
    flash(apiErrorMessage(e, 'Kimlik durumu alınamadı'), true)
  } finally {
    authLoading.value = false
  }
}

async function doSetup() {
  authBusy.value = true
  message.value = ''
  try {
    const res = await setupPin(setupPinValue.value, setupPinConfirm.value)
    if (!res.ok) {
      flash(res.error || 'PIN oluşturulamadı', true)
      return
    }
    setupPinValue.value = ''
    setupPinConfirm.value = ''
    flash('PIN oluşturuldu — politika açık')
    await refreshAuth()
    await refreshAll()
  } catch (e: any) {
    flash(apiErrorMessage(e, 'PIN oluşturulamadı'), true)
  } finally {
    authBusy.value = false
  }
}

async function doUnlock() {
  authBusy.value = true
  message.value = ''
  try {
    const res = await unlockPin(unlockPinValue.value)
    if (!res.ok) {
      flash(res.error || 'PIN hatalı', true)
      await refreshAuth()
      return
    }
    unlockPinValue.value = ''
    flash('Kilit açıldı')
    await refreshAuth()
    await refreshAll()
  } catch (e: any) {
    flash(apiErrorMessage(e, 'Kilit açılamadı'), true)
    await refreshAuth()
  } finally {
    authBusy.value = false
  }
}

async function doLock() {
  locking.value = true
  try {
    await lockPin()
    policy.value = null
    hostServices.value = []
    flash('Politika kilitlendi')
    await refreshAuth()
  } catch (e: any) {
    flash(apiErrorMessage(e, 'Kilitleme başarısız'), true)
  } finally {
    locking.value = false
  }
}

async function loadHostServices() {
  loadingServices.value = true
  hostServicesError.value = ''
  try {
    const res = await getHostServices()
    if (!Array.isArray(res?.items)) {
      hostServicesError.value = 'Servis listesi beklenen formatta değil — agent yeniden başlatılmalı.'
      hostServices.value = []
      return
    }
    hostServices.value = res.items
    if (res.error) hostServicesError.value = res.error
  } catch (e: any) {
    hostServicesError.value = apiErrorMessage(e, 'Servis listesi alınamadı')
    hostServices.value = []
  } finally {
    loadingServices.value = false
  }
}

async function browseAppExe(i: number) {
  const apps = policy.value?.serviceWatch.applications
  if (!apps?.[i]) return
  browsingAppIndex.value = i
  try {
    const res = await browseExecutable()
    if (res.error) {
      flash(res.error, true)
      return
    }
    if (res.cancelled || !res.path) return
    apps[i].executablePath = res.path
    if (!apps[i].name && res.processName) apps[i].name = res.processName
    if (!apps[i].workingDirectory && res.directory) apps[i].workingDirectory = res.directory
  } catch (e: any) {
    flash(apiErrorMessage(e, 'Dosya seçici açılamadı'), true)
  } finally {
    browsingAppIndex.value = null
  }
}

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
    if (policy.value && !policy.value.serviceWatch.applications) {
      policy.value.serviceWatch.applications = []
    }
    if (policy.value && !policy.value.eventLog.packages) {
      policy.value.eventLog.packages = []
    }
    if (policy.value && !policy.value.eventLog.agentOverrides) {
      policy.value.eventLog.agentOverrides = []
    }
    if (policy.value && !policy.value.eventLog.disabledServerPackages) {
      policy.value.eventLog.disabledServerPackages = []
    }
    if (policy.value && !policy.value.eventLog.packageCatalogSyncIntervalSeconds) {
      policy.value.eventLog.packageCatalogSyncIntervalSeconds = 3600
    }
    if (policy.value && policy.value.serviceWatch.restartCooldownSeconds == null) {
      policy.value.serviceWatch.restartCooldownSeconds = 300
    }
    if (policy.value && policy.value.serviceWatch.restartMaxAttempts == null) {
      policy.value.serviceWatch.restartMaxAttempts = 3
    }
    if (policy.value && policy.value.serviceWatch.includeInventory == null) {
      policy.value.serviceWatch.includeInventory = true
    }
    if (policy.value && policy.value.serviceWatch.inventoryIntervalSeconds == null) {
      policy.value.serviceWatch.inventoryIntervalSeconds = 60
    }
  } catch (e: any) {
    flash(e?.message || 'Yapılandırma yüklenemedi', true)
  } finally {
    loading.value = false
  }
}

async function refreshAll() {
  await load()
  await loadPackagePlan()
  if (auth.value?.unlocked) {
    await loadHostServices()
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
    flash(apiErrorMessage(e, 'Sistem kaydı başarısız'), true)
    if ((e?.statusCode || e?.status) === 401) await refreshAuth()
  } finally {
    savingSystem.value = false
  }
}

async function savePolicyCfg() {
  if (!policy.value) return
  ensureOverrides()
  // Persist override model; clear legacy full list.
  policy.value.eventLog.packages = []
  policy.value.eventLog.agentOverrides = (policy.value.eventLog.agentOverrides || []).filter(
    p => (p.name || '').trim() && (p.channel || '').trim() && (p.eventIds?.length || 0) > 0
  )
  policy.value.eventLog.disabledServerPackages = (policy.value.eventLog.disabledServerPackages || [])
    .map(n => n.trim())
    .filter(Boolean)
  savingPolicy.value = true
  try {
    await savePolicy(policy.value)
    flash('Politika kaydedildi (işçiler bir sonraki döngüde uygular)')
    await load()
    await loadPackagePlan()
  } catch (e: any) {
    flash(apiErrorMessage(e, 'Politika kaydı başarısız'), true)
    if ((e?.statusCode || e?.status) === 401) await refreshAuth()
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

function addApplication() {
  if (!policy.value) return
  if (!policy.value.serviceWatch.applications) {
    policy.value.serviceWatch.applications = []
  }
  policy.value.serviceWatch.applications.push({
    name: '',
    minCount: 1,
    restartAllowed: false,
    executablePath: '',
    arguments: '',
    workingDirectory: ''
  })
}

function removeApplication(i: number) {
  policy.value?.serviceWatch.applications?.splice(i, 1)
}

onMounted(async () => {
  await refreshAuth()
  await loadPackagePlan()
  if (auth.value?.unlocked) {
    await refreshAll()
  }
})
</script>
