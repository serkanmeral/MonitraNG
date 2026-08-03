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
          @click="() => savePolicyCfg()"
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
            <UFormGroup label="Bilgisayar kimliği (HostId)" help="Boş bırakılırsa PC adı kullanılır ve kaydedilir.">
              <UInput v-model="system.hostId" :placeholder="hostIdPlaceholder" />
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
          <UButton color="primary" :loading="savingPolicy" @click="() => savePolicyCfg()">Politikayı kaydet</UButton>
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
          <UButton color="primary" :loading="savingPolicy" @click="() => savePolicyCfg()">Politikayı kaydet</UButton>
        </div>
      </template>

      <!-- Event log / Journal -->
      <template #eventlog>
        <div v-if="isLinux && policy" class="pt-4 space-y-5">
          <UAlert
            color="sky"
            variant="soft"
            title="Journal politikası"
            description="Yapı burada Windows ile aynı ekranda gösterilir. Paket düzenleme ve kayıt akışı sonraki dilimde konuşulacak; şu an salt okunur."
          />
          <div class="flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
            <UBadge size="sm" variant="soft" :color="policy.journal?.enabled ? 'green' : 'gray'">
              {{ policy.journal?.enabled ? 'Açık' : 'Kapalı' }}
            </UBadge>
            <span class="text-gray-500">
              Sorgulama:
              <span class="text-gray-800 dark:text-gray-200">{{ policy.journal?.pollIntervalSeconds ?? '—' }} sn</span>
            </span>
            <span class="text-gray-500">
              Max/poll:
              <span class="text-gray-800 dark:text-gray-200">{{ policy.journal?.maxEventsPerPoll ?? '—' }}</span>
            </span>
          </div>
          <div class="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
            <table class="w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
              <thead class="bg-gray-50 dark:bg-gray-800/80">
                <tr>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Paket</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Unit / id</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Grep / prio</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                <tr v-for="p in (policy.journal?.packages || [])" :key="p.name">
                  <td class="px-3 py-2 font-medium">{{ p.name }}</td>
                  <td class="px-3 py-2 font-mono text-xs">{{ p.unit || (p.identifier ? `id:${p.identifier}` : '—') }}</td>
                  <td class="px-3 py-2 font-mono text-xs truncate max-w-[16rem]" :title="p.grep || p.priority || ''">
                    {{ p.grep || p.priority || '—' }}
                  </td>
                </tr>
                <tr v-if="!(policy.journal?.packages?.length)">
                  <td colspan="3" class="px-3 py-6 text-center text-gray-500">
                    Builtin paketler etkin (sshd / sudo / unit-fail). Detay Kaynaklar’da.
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
        <div v-else class="pt-4 space-y-5">
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
            color="sky"
            variant="soft"
            title="Paketler sunucudan yönetilir"
            description="Tanım ve host ataması SIEM Ayarları / Discovery → Paketler üzerinden yapılır. Bu ekranda katalogu yenileyin; sync sonrası politika otomatik kaydedilir."
            class="mb-1"
          />

          <UAlert
            v-if="packagePlan?.legacyMode || hasLocalPackageOverrides"
            color="amber"
            variant="soft"
            title="Eski lokal paket ayarı"
            description="Bu agent’ta eski override / tam liste kalıntısı var. Temizleyip sunucu modeline geçmeniz önerilir."
          >
            <template #actions>
              <UButton size="xs" color="amber" variant="solid" :loading="savingPolicy" @click="clearLocalPackageOverrides">
                Lokal paketleri temizle ve kaydet
              </UButton>
            </template>
          </UAlert>

          <section class="space-y-2">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <div>
                <p class="text-sm font-medium text-gray-900 dark:text-white">Sunucu katalogu</p>
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
                :loading="syncingCatalog || savingPolicy"
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
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Tür</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr v-for="p in catalogActivePackages" :key="'srv-' + p.name">
                    <td class="px-3 py-2 font-medium">{{ p.name }}</td>
                    <td class="px-3 py-2 font-mono text-xs">{{ p.channel }}</td>
                    <td class="px-3 py-2 font-mono text-xs">{{ formatCatalogPackageIds(p) }}</td>
                    <td class="px-3 py-2 text-xs text-gray-500">{{ packageKindLabel(p) }}</td>
                  </tr>
                  <tr v-for="p in catalogOptionalPackages" :key="'opt-' + p.name">
                    <td class="px-3 py-2 font-medium">{{ p.name }}</td>
                    <td class="px-3 py-2 font-mono text-xs">{{ p.channel }}</td>
                    <td class="px-3 py-2 font-mono text-xs">{{ formatCatalogPackageIds(p) }}</td>
                    <td class="px-3 py-2 text-xs text-gray-500">Opsiyonel</td>
                  </tr>
                  <tr v-if="!catalogActivePackages.length && !catalogOptionalPackages.length">
                    <td colspan="4" class="px-3 py-3 text-xs text-gray-500">
                      Katalog boş veya henüz sync edilmedi.
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
            <p class="text-xs text-gray-500">
              «Atandı» = SIEM host ataması ile açılmış opsiyonel. «Opsiyonel» = henüz bu host’a atanmamış.
            </p>
          </section>

          <section v-if="packagePlan?.effective?.length" class="space-y-2">
            <p class="text-sm font-medium text-gray-900 dark:text-white">Efektif paketler</p>
            <p class="text-xs text-gray-500">
              Worker’ın toplayacağı liste (sunucu katalog ⊕ host ataması − lokal kapalı).
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

          <section v-if="!isLinux && cursorRows.length" class="space-y-2">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <div>
                <p class="text-sm font-medium text-gray-900 dark:text-white">Okuma geçmişi (bookmark)</p>
                <p class="text-xs text-gray-500">
                  Varsayılan: şimdiden dinle. İsterseniz paket için son 6–72 saatlik geçmişe konumlanın; ardından canlı devam eder.
                </p>
              </div>
              <UButton
                size="xs"
                variant="outline"
                :loading="loadingBookmarks"
                icon="i-heroicons-arrow-path"
                @click="loadBookmarks"
              >
                Yenile
              </UButton>
            </div>
            <div class="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
              <table class="w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
                <thead class="bg-gray-50 dark:bg-gray-800/80">
                  <tr>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Paket</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Durum</th>
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Geçmiş</th>
                    <th class="px-3 py-2 text-right text-xs font-medium text-gray-500 uppercase">İşlem</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 dark:divide-gray-700">
                  <tr v-for="row in cursorRows" :key="'cur-' + row.packageName">
                    <td class="px-3 py-2">
                      <div class="font-medium">{{ row.packageName }}</div>
                      <div class="font-mono text-xs text-gray-500">{{ row.channel }}</div>
                    </td>
                    <td class="px-3 py-2 text-xs text-gray-600 dark:text-gray-300">
                      <div>{{ cursorModeLabel(row) }}</div>
                      <div class="font-mono text-[11px] text-gray-500 mt-0.5">
                        RecordId &gt; {{ row.lastRecordId ?? '—' }}
                        <span v-if="row.seededAtUtc"> · {{ formatDate(row.seededAtUtc) }}</span>
                      </div>
                    </td>
                    <td class="px-3 py-2">
                      <USelect
                        v-model="historyHoursByPackage[row.packageName]"
                        :options="historyHourOptions"
                        size="xs"
                        class="w-28"
                      />
                    </td>
                    <td class="px-3 py-2 text-right whitespace-nowrap">
                      <UButton
                        size="xs"
                        variant="soft"
                        color="primary"
                        class="mr-1"
                        :loading="cursorBusy === row.packageName + ':now'"
                        @click="applyCursor(row.packageName, 'now')"
                      >
                        Şimdiden
                      </UButton>
                      <UButton
                        size="xs"
                        variant="soft"
                        color="amber"
                        :loading="cursorBusy === row.packageName + ':hours'"
                        @click="applyCursor(row.packageName, 'hours')"
                      >
                        Geçmişi al
                      </UButton>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
            <p class="text-xs text-gray-500">
              «Şimdiden» bookmark’ı kanalın sonuna alır (geçmiş gelmez). «Geçmişi al» seçilen saat penceresinin en eski kaydından itibaren okumaya başlar.
            </p>
          </section>

          <UButton color="primary" :loading="savingPolicy" @click="savePolicyCfg()">Politikayı kaydet</UButton>
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
              <p class="text-xs font-medium text-gray-500 uppercase tracking-wide">
                {{ isLinux ? 'systemd unit’leri' : 'Windows servisleri' }}
              </p>
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

          <UButton color="primary" :loading="savingPolicy" @click="() => savePolicyCfg()">Politikayı kaydet</UButton>
        </div>
      </template>
    </UTabs>
  </div>
</template>

<script setup lang="ts">
import type {
  AgentStatus,
  EventLogCursorStatus,
  EventLogPackagePlan,
  HostServiceItem,
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
  getEventLogBookmarks,
  setEventLogCursor,
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
const loadingBookmarks = ref(false)
const cursorBusy = ref<string | null>(null)
const cursorRows = ref<EventLogCursorStatus[]>([])
const historyHoursByPackage = reactive<Record<string, number>>({})
const historyHourOptions = [
  { label: '6 saat', value: 6 },
  { label: '24 saat', value: 24 },
  { label: '48 saat', value: 48 },
  { label: '72 saat', value: 72 }
]
const browsingAppIndex = ref<number | null>(null)
const activeTab = ref(0)
const message = ref('')
const messageError = ref(false)
const apiKeyConfigured = ref(false)
const hostServices = ref<HostServiceItem[]>([])
const hostServicesError = ref('')
const packagePlan = ref<EventLogPackagePlan | null>(null)
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
const hostIdPlaceholder = computed(() => system.hostId?.trim() || 'PC adı (otomatik)')
const policy = ref<PolicyConfig | null>(null)
const { isLinux, logSourceLabel, applyFromStatus } = useAgentPlatform()

const tabItems = computed(() => [
  { key: 'system', label: 'Sistem', slot: 'system' },
  { key: 'general', label: 'Genel', slot: 'general' },
  { key: 'metrics', label: 'Metrik', slot: 'metrics' },
  { key: 'eventlog', label: logSourceLabel.value, slot: 'eventlog' },
  { key: 'watch', label: 'İzleme', slot: 'watch' }
])

const activeTabKey = computed(() => tabItems.value[activeTab.value]?.key || 'system')

const hasLocalPackageOverrides = computed(() => {
  const el = policy.value?.eventLog
  if (!el) return false
  return (el.agentOverrides?.length || 0) > 0
    || (el.disabledServerPackages?.length || 0) > 0
    || (el.packages?.length || 0) > 0
})

/** Active packages for this host (fleet defaults + assigned optionals). */
const catalogActivePackages = computed(() => packagePlan.value?.server || [])

/** Optional packages not already in the active list (defensive dedupe). */
const catalogOptionalPackages = computed(() => {
  const active = new Set(
    (packagePlan.value?.server || []).map(p => (p.name || '').toLowerCase()).filter(Boolean)
  )
  return (packagePlan.value?.optional || []).filter(
    p => !active.has((p.name || '').toLowerCase())
  )
})

function packageKindLabel(p: { kind?: string; isDefault?: boolean }) {
  if (p.kind === 'assigned' || p.isDefault === false) return 'Atandı'
  return 'Varsayılan'
}

function formatCatalogPackageIds(p: {
  selectionMode?: string
  eventIds?: number[]
  excludedEventIds?: number[]
}): string {
  if (String(p.selectionMode || '').toLowerCase() === 'all') {
    const ex = (p.excludedEventIds || []).length
      ? ` (− ${(p.excludedEventIds || []).join(', ')})`
      : ''
    return `Tümü${ex}`
  }
  return (p.eventIds || []).join(', ')
}

function ensureOverrides() {
  if (!policy.value) return
  if (!policy.value.eventLog.agentOverrides) policy.value.eventLog.agentOverrides = []
  if (!policy.value.eventLog.disabledServerPackages) policy.value.eventLog.disabledServerPackages = []
}

async function loadPackagePlan() {
  loadingPackagePlan.value = true
  try {
    packagePlan.value = await getEventLogPackagePlan()
    if (!isLinux.value) await loadBookmarks()
  } catch (e: any) {
    flash(apiErrorMessage(e, 'Paket planı alınamadı'), true)
  } finally {
    loadingPackagePlan.value = false
  }
}

function cursorModeLabel(row: EventLogCursorStatus): string {
  if (!row.hasBookmark) return 'Henüz bookmark yok (ilk poll’da şimdiden seed edilir)'
  if (row.cursorMode === 'hours' && row.historyHours) {
    return `Geçmiş: son ${row.historyHours} saat`
  }
  if (row.cursorMode === 'now') return 'Şimdiden (canlı)'
  return 'Bookmark mevcut'
}

async function loadBookmarks() {
  if (isLinux.value) return
  loadingBookmarks.value = true
  try {
    const res = await getEventLogBookmarks()
    cursorRows.value = res.items || []
    for (const item of cursorRows.value) {
      if (historyHoursByPackage[item.packageName] == null) {
        historyHoursByPackage[item.packageName] = item.historyHours || 24
      }
    }
  } catch (e: any) {
    flash(apiErrorMessage(e, 'Bookmark durumu alınamadı'), true)
  } finally {
    loadingBookmarks.value = false
  }
}

async function applyCursor(packageName: string, mode: 'now' | 'hours') {
  cursorBusy.value = `${packageName}:${mode}`
  try {
    const hours = historyHoursByPackage[packageName] || 24
    const res = await setEventLogCursor({
      packageName,
      mode,
      hours: mode === 'hours' ? hours : undefined
    })
    flash(res.message || 'Okuma konumu güncellendi')
    await loadBookmarks()
  } catch (e: any) {
    const data = e?.data || e?.response?._data
    flash(data?.error || apiErrorMessage(e, 'Okuma konumu güncellenemedi'), true)
  } finally {
    cursorBusy.value = null
  }
}

async function syncCatalogNow() {
  syncingCatalog.value = true
  try {
    const sync = await syncEventLogCatalog()
    await loadPackagePlan()
    if (!sync?.synced) {
      flash(
        sync?.message
          || `Katalog sunucudan alınamadı (kaynak: ${sync?.source || '—'}). Collector URL / API key / agent sürümünü kontrol edin.`,
        true
      )
      return
    }
    const detail =
      sync.message
      || `Katalog yenilendi (${sync.count ?? 0} varsayılan, ${sync.optionalCount ?? 0} opsiyonel, kaynak: ${sync.source})`
    try {
      await savePolicyCfg({ successMessage: detail })
    } catch {
      flash(`${detail} — politika kaydı başarısız; «Politikayı kaydet» ile tekrar deneyin`, true)
    }
  } catch (e: any) {
    flash(apiErrorMessage(e, 'Katalog yenilenemedi'), true)
  } finally {
    syncingCatalog.value = false
  }
}

async function clearLocalPackageOverrides() {
  if (!policy.value) return
  ensureOverrides()
  policy.value.eventLog.packages = []
  policy.value.eventLog.agentOverrides = []
  policy.value.eventLog.disabledServerPackages = []
  if (packagePlan.value) packagePlan.value.legacyMode = false
  await savePolicyCfg({
    successMessage: 'Lokal paket kalıntıları temizlendi; sunucu katalogu geçerli'
  })
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
    if (cfg.platform) applyFromStatus({ platform: cfg.platform } as AgentStatus)
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

async function savePolicyCfg(opts?: { successMessage?: string }) {
  if (!policy.value) return
  ensureOverrides()
  // Persist override model; clear legacy full list.
  policy.value.eventLog.packages = []
  policy.value.eventLog.agentOverrides = (policy.value.eventLog.agentOverrides || []).filter(
    p =>
      (p.name || '').trim() &&
      (p.channel || '').trim() &&
      (String(p.selectionMode || '').toLowerCase() === 'all' || (p.eventIds?.length || 0) > 0)
  )
  policy.value.eventLog.disabledServerPackages = (policy.value.eventLog.disabledServerPackages || [])
    .map(n => n.trim())
    .filter(Boolean)

  // Normalize process names (strip .exe / path) and drop empty / duplicate apps.
  const seenApps = new Set<string>()
  policy.value.serviceWatch.applications = (policy.value.serviceWatch.applications || [])
    .map(a => {
      let name = (a.name || '').trim()
      if (/[\\/]/.test(name)) {
        const base = name.split(/[\\/]/).pop() || name
        name = base.replace(/\.exe$/i, '')
      } else if (/\.exe$/i.test(name)) {
        name = name.replace(/\.exe$/i, '')
      }
      return { ...a, name, minCount: a.minCount > 0 ? a.minCount : 1 }
    })
    .filter(a => {
      const key = a.name.toLowerCase()
      if (!key || seenApps.has(key)) return false
      seenApps.add(key)
      return true
    })

  savingPolicy.value = true
  try {
    await savePolicy(policy.value)
    flash(opts?.successMessage || 'Politika kaydedildi (işçiler bir sonraki döngüde uygular)')
    await load()
    await loadPackagePlan()
  } catch (e: any) {
    flash(apiErrorMessage(e, 'Politika kaydı başarısız'), true)
    if ((e?.statusCode || e?.status) === 401) await refreshAuth()
    throw e
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
