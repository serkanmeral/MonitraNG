<template>
  <div class="space-y-6">
    <UCard>
      <template #header>
        <div class="flex items-center justify-between">
          <h3 class="text-lg font-semibold">Lisans Yönetimi</h3>
          <UButton
            v-if="license"
            color="gray"
            variant="outline"
            size="sm"
            icon="i-heroicons-arrow-path"
            @click="refreshLicense"
            :loading="loading"
          >
            Yenile
          </UButton>
        </div>
      </template>

      <!-- Token Required Alert -->
      <UAlert
        v-if="!accessToken"
        color="yellow"
        variant="soft"
        title="Kimlik Doğrulaması Gerekli"
        description="Lisans bilgilerini görüntülemek ve yönetmek için domain admin kimlik bilgilerinizle giriş yapmanız gerekmektedir."
        class="mb-4"
      />

      <!-- License Info -->
      <div v-if="license && accessToken" class="space-y-4">
        <!-- License Status -->
        <div class="flex items-center gap-4">
          <UBadge
            :color="license.isValid ? 'green' : 'red'"
            variant="soft"
            size="lg"
          >
            {{ license.isValid ? 'Geçerli' : 'Süresi Dolmuş' }}
          </UBadge>
          <UBadge
            :color="getLicenseType(license.licenseType) === 'Real' ? 'blue' : 'yellow'"
            variant="soft"
          >
            {{ getLicenseType(license.licenseType) === 'Real' ? 'Gerçek Lisans' : 'Trial Lisans' }}
          </UBadge>
        </div>

        <!-- Expiration Behavior Status -->
        <div v-if="license.isExpired && license.expirationBehavior" class="border-t pt-4">
          <h4 class="font-semibold mb-3">Süre Dolunca İzin Durumları</h4>
          <div class="grid grid-cols-2 gap-3">
            <div class="flex items-center justify-between p-3 rounded-lg border" :class="license.expirationBehavior.blockTokenGeneration ? 'bg-red-50 border-red-300' : 'bg-green-50 border-green-300'">
              <div class="flex items-center gap-2">
                <UIcon :name="license.expirationBehavior.blockTokenGeneration ? 'i-heroicons-x-circle' : 'i-heroicons-check-circle'" 
                       :class="license.expirationBehavior.blockTokenGeneration ? 'text-red-700' : 'text-green-700'" 
                       class="w-5 h-5" />
                <div class="flex flex-col">
                  <span class="font-medium text-gray-900">Token Üretimi</span>
                  <span class="text-xs text-gray-500">blockTokenGeneration: {{ license.expirationBehavior.blockTokenGeneration }}</span>
                </div>
              </div>
              <UBadge 
                :color="license.expirationBehavior.blockTokenGeneration ? 'red' : 'green'"
                variant="solid"
              >
                {{ license.expirationBehavior.blockTokenGeneration ? 'Engellendi' : 'İzinli' }}
              </UBadge>
            </div>
            
            <div class="flex items-center justify-between p-3 rounded-lg border" :class="license.expirationBehavior.blockGetOperations ? 'bg-red-50 border-red-300' : 'bg-green-50 border-green-300'">
              <div class="flex items-center gap-2">
                <UIcon :name="license.expirationBehavior.blockGetOperations ? 'i-heroicons-x-circle' : 'i-heroicons-check-circle'" 
                       :class="license.expirationBehavior.blockGetOperations ? 'text-red-700' : 'text-green-700'" 
                       class="w-5 h-5" />
                <div class="flex flex-col">
                  <span class="font-medium text-gray-900">GET İşlemleri</span>
                  <span class="text-xs text-gray-500">blockGetOperations: {{ license.expirationBehavior.blockGetOperations }}</span>
                </div>
              </div>
              <UBadge 
                :color="license.expirationBehavior.blockGetOperations ? 'red' : 'green'"
                variant="solid"
              >
                {{ license.expirationBehavior.blockGetOperations ? 'Engellendi' : 'İzinli' }}
              </UBadge>
            </div>
            
            <div class="flex items-center justify-between p-3 rounded-lg border" :class="license.expirationBehavior.blockCrudOperations ? 'bg-red-50 border-red-300' : 'bg-green-50 border-green-300'">
              <div class="flex items-center gap-2">
                <UIcon :name="license.expirationBehavior.blockCrudOperations ? 'i-heroicons-x-circle' : 'i-heroicons-check-circle'" 
                       :class="license.expirationBehavior.blockCrudOperations ? 'text-red-700' : 'text-green-700'" 
                       class="w-5 h-5" />
                <div class="flex flex-col">
                  <span class="font-medium text-gray-900">CRUD İşlemleri</span>
                  <span class="text-xs text-gray-500">blockCrudOperations: {{ license.expirationBehavior.blockCrudOperations }}</span>
                </div>
              </div>
              <UBadge 
                :color="license.expirationBehavior.blockCrudOperations ? 'red' : 'green'"
                variant="solid"
              >
                {{ license.expirationBehavior.blockCrudOperations ? 'Engellendi' : 'İzinli' }}
              </UBadge>
            </div>
            
            <div class="flex items-center justify-between p-3 rounded-lg border" :class="license.expirationBehavior.allowReadOnly ? 'bg-blue-50 border-blue-300' : 'bg-gray-100 border-gray-300'">
              <div class="flex items-center gap-2">
                <UIcon :name="license.expirationBehavior.allowReadOnly ? 'i-heroicons-check-circle' : 'i-heroicons-minus-circle'" 
                       :class="license.expirationBehavior.allowReadOnly ? 'text-blue-700' : 'text-gray-600'" 
                       class="w-5 h-5" />
                <div class="flex flex-col">
                  <span class="font-medium text-gray-900">Sadece Okuma</span>
                  <span class="text-xs text-gray-500">allowReadOnly: {{ license.expirationBehavior.allowReadOnly }}</span>
                </div>
              </div>
              <UBadge 
                :color="license.expirationBehavior.allowReadOnly ? 'blue' : 'gray'"
                variant="solid"
              >
                {{ license.expirationBehavior.allowReadOnly ? 'Aktif' : 'Pasif' }}
              </UBadge>
            </div>
          </div>
          
          <UAlert
            v-if="license.expirationBehavior.customMessage"
            color="orange"
            variant="soft"
            :title="license.expirationBehavior.customMessage"
            class="mt-3"
          />
        </div>

        <!-- User Count Info -->
        <div v-if="userCount" class="border-t pt-4">
          <h4 class="font-semibold mb-3">Kullanıcı Durumu</h4>
          <div class="grid grid-cols-3 gap-4 items-center">
            <div>
              <p class="text-sm text-gray-500 mb-1">Aktif Kullanıcı Sayısı</p>
              <p class="text-3xl font-bold text-primary">{{ userCount.activeUserCount }}</p>
            </div>
            <div v-if="userCount.maxUsers">
              <p class="text-sm text-gray-500 mb-1">Maksimum Kullanıcı</p>
              <p class="text-3xl font-bold text-gray-700">{{ userCount.maxUsers }}</p>
            </div>
            <div>
              <p class="text-sm text-gray-500 mb-1">Yeni Kullanıcı Eklenebilir</p>
              <UBadge 
                :color="userCount.canCreateUser ? 'green' : 'red'"
                variant="solid"
                size="lg"
              >
                {{ userCount.canCreateUser ? 'Evet' : 'Hayır' }}
              </UBadge>
            </div>
          </div>
          <div v-if="userCount.maxUsers" class="mt-4">
            <UProgress
              :value="Math.min((userCount.activeUserCount / userCount.maxUsers) * 100, 100)"
              :color="userCount.canCreateUser ? 'green' : 'red'"
              size="md"
            />
            <p class="text-xs text-gray-500 mt-1 text-center">
              {{ userCount.activeUserCount }} / {{ userCount.maxUsers }} kullanıcı
            </p>
          </div>
        </div>

        <!-- License Details -->
        <div class="border-t pt-4">
          <h4 class="font-semibold mb-3">Lisans Detayları</h4>
          <div class="grid grid-cols-2 gap-4">
            <div>
              <p class="text-sm text-gray-500">Yayınlanma Tarihi</p>
              <p class="font-medium">{{ formatDate(license.issuedAt) }}</p>
            </div>
            <div>
              <p class="text-sm text-gray-500">Bitiş Tarihi</p>
              <p class="font-medium">{{ formatDate(license.expiresAt) }}</p>
            </div>
            <div>
              <p class="text-sm text-gray-500">Yayınlayan</p>
              <p class="font-medium">{{ license.issuedBy }}</p>
            </div>
            <div v-if="license.licenseFeatures">
              <p class="text-sm text-gray-500">Maksimum Kullanıcı</p>
              <p class="font-medium">{{ license.licenseFeatures.maxUsers }}</p>
            </div>
          </div>
        </div>

        <!-- Customer Info (Real License) -->
        <div v-if="license.customerInfo" class="border-t pt-4">
          <h4 class="font-semibold mb-2">Müşteri Bilgileri</h4>
          <div class="grid grid-cols-2 gap-4">
            <div>
              <p class="text-sm text-gray-500">Müşteri Adı</p>
              <p class="font-medium">{{ license.customerInfo.customerName }}</p>
            </div>
            <div>
              <p class="text-sm text-gray-500">İletişim E-posta</p>
              <p class="font-medium">{{ license.customerInfo.contactEmail }}</p>
            </div>
          </div>
        </div>

        <!-- Actions -->
        <div class="flex gap-2 border-t pt-4">
          <UButton
            color="primary"
            variant="outline"
            icon="i-heroicons-arrow-down-tray"
            @click="handleDownload"
          >
            Lisans İndir
          </UButton>
          <UButton
            color="primary"
            variant="outline"
            icon="i-heroicons-arrow-up-tray"
            @click="showUploadModal = true"
          >
            Lisans Yükle
          </UButton>
          <UButton
            color="green"
            variant="outline"
            icon="i-heroicons-plus-circle"
            @click="showCreateModal = true"
          >
            Real Lisans Oluştur
          </UButton>
        </div>
      </div>

      <!-- Loading State -->
      <div v-else-if="loading" class="flex justify-center py-8">
        <UIcon name="i-heroicons-arrow-path" class="w-8 h-8 animate-spin text-primary" />
      </div>

      <!-- Error State -->
      <UAlert
        v-else-if="error"
        color="red"
        variant="soft"
        :title="error"
      />
    </UCard>

    <!-- Upload Modal -->
    <UModal v-model="showUploadModal">
      <UCard>
        <template #header>
          <h3 class="text-lg font-semibold">Lisans Dosyası Yükle</h3>
        </template>

        <div class="space-y-4">
          <UFormGroup label="Lisans Dosyası">
            <input
              type="file"
              accept=".enc"
              @change="handleFileSelect"
              class="block w-full text-sm text-gray-500 file:mr-4 file:py-2 file:px-4 file:rounded-md file:border-0 file:text-sm file:font-semibold file:bg-primary file:text-white hover:file:bg-primary-dark"
            />
          </UFormGroup>

          <div class="flex justify-end gap-2">
            <UButton
              color="gray"
              variant="outline"
              @click="showUploadModal = false"
            >
              İptal
            </UButton>
            <UButton
              color="primary"
              @click="handleUpload"
              :loading="uploading"
              :disabled="!selectedFile"
            >
              Yükle
            </UButton>
          </div>
        </div>
      </UCard>
    </UModal>

    <!-- Create Real License Modal -->
    <UModal v-model="showCreateModal" :ui="{ width: 'max-w-4xl' }">
      <UCard>
        <template #header>
          <h3 class="text-lg font-semibold">Real Lisans Oluştur</h3>
        </template>

        <div class="space-y-6 max-h-[80vh] overflow-y-auto">
          <!-- Expiration Date -->
          <UFormGroup label="Bitiş Tarihi" required>
            <UInput
              v-model="createForm.expiresAt"
              type="datetime-local"
              required
            />
          </UFormGroup>

          <!-- Expiration Behavior -->
          <div class="space-y-4">
            <h4 class="font-semibold">Süre Dolunca Davranış</h4>
            
            <UFormGroup label="Token Üretimini Engelle">
              <UToggle v-model="createForm.expirationBehavior.blockTokenGeneration" />
            </UFormGroup>

            <UFormGroup label="CRUD İşlemlerini Engelle">
              <UToggle v-model="createForm.expirationBehavior.blockCrudOperations" />
            </UFormGroup>

            <UFormGroup label="GET İşlemlerini Engelle">
              <UToggle v-model="createForm.expirationBehavior.blockGetOperations" />
            </UFormGroup>

            <UFormGroup label="Sadece Okuma Moduna İzin Ver">
              <UToggle v-model="createForm.expirationBehavior.allowReadOnly" />
            </UFormGroup>

            <UFormGroup label="Özel Mesaj">
              <UTextarea
                v-model="createForm.expirationBehavior.customMessage"
                placeholder="Lisans süreniz dolmuştur. Lütfen lisansınızı yenileyin."
              />
            </UFormGroup>
          </div>

          <!-- License Features -->
          <div class="space-y-4">
            <h4 class="font-semibold">Lisans Özellikleri</h4>
            
            <UFormGroup label="Maksimum Kullanıcı Sayısı" required>
              <UInput
                v-model.number="createForm.licenseFeatures.maxUsers"
                type="number"
                min="1"
                required
              />
            </UFormGroup>

            <UFormGroup label="Maksimum Domain Sayısı">
              <UInput
                v-model.number="createForm.licenseFeatures.maxDomains"
                type="number"
                min="0"
              />
            </UFormGroup>

            <UFormGroup label="Maksimum Depolama (GB)">
              <UInput
                v-model.number="createForm.licenseFeatures.maxStorageGB"
                type="number"
                min="0"
              />
            </UFormGroup>

            <UFormGroup label="Gelişmiş Özellikleri Etkinleştir">
              <UToggle v-model="createForm.licenseFeatures.enableAdvancedFeatures" />
            </UFormGroup>

            <UFormGroup label="Destek Seviyesi">
              <USelect
                v-model="createForm.licenseFeatures.supportLevel"
                :options="supportLevelOptions"
                placeholder="Destek seviyesi seçin"
              />
            </UFormGroup>

            <UFormGroup label="Sadece Aktif Kullanıcıları Say">
              <UToggle v-model="createForm.licenseFeatures.countActiveUsersOnly" />
            </UFormGroup>

            <div v-if="createForm.licenseFeatures.countActiveUsersOnly && createForm.licenseFeatures.activeUserDefinition" class="pl-4 border-l-2 space-y-2">
              <UFormGroup label="Aktif Kullanıcı Tanımı">
                <UFormGroup label="Kullanıcı Aktif Olmalı">
                  <UToggle 
                    v-model="createForm.licenseFeatures.activeUserDefinition.isActive" 
                  />
                </UFormGroup>
                <UFormGroup label="Son Giriş (Gün)">
                  <UInput
                    v-model.number="createForm.licenseFeatures.activeUserDefinition.lastLoginDays"
                    type="number"
                    min="1"
                    placeholder="Örn: 90"
                  />
                </UFormGroup>
              </UFormGroup>
            </div>
          </div>

          <!-- Customer Info (Optional) -->
          <div class="space-y-4">
            <div class="flex items-center justify-between">
              <h4 class="font-semibold">Müşteri Bilgileri (Opsiyonel)</h4>
              <UButton
                color="gray"
                variant="ghost"
                size="xs"
                @click="showCustomerInfo = !showCustomerInfo"
              >
                {{ showCustomerInfo ? 'Gizle' : 'Göster' }}
              </UButton>
            </div>

            <div v-if="showCustomerInfo && createForm.customerInfo" class="space-y-4 pl-4 border-l-2">
              <UFormGroup label="Müşteri Adı">
                <UInput v-model="createForm.customerInfo.customerName" />
              </UFormGroup>

              <UFormGroup label="Müşteri ID">
                <UInput v-model="createForm.customerInfo.customerId" />
              </UFormGroup>

              <UFormGroup label="İletişim E-posta">
                <UInput v-model="createForm.customerInfo.contactEmail" type="email" />
              </UFormGroup>

              <UFormGroup label="İletişim Telefonu">
                <UInput v-model="createForm.customerInfo.contactPhone" />
              </UFormGroup>
            </div>
          </div>

          <!-- Metadata (Optional) -->
          <div class="space-y-4">
            <div class="flex items-center justify-between">
              <h4 class="font-semibold">Metadata (Opsiyonel)</h4>
              <UButton
                color="gray"
                variant="ghost"
                size="xs"
                @click="showMetadata = !showMetadata"
              >
                {{ showMetadata ? 'Gizle' : 'Göster' }}
              </UButton>
            </div>

            <div v-if="showMetadata && createForm.metadata" class="space-y-4 pl-4 border-l-2">
              <UFormGroup label="Satın Alma Tarihi">
                <UInput v-model="createForm.metadata.purchaseDate" type="datetime-local" />
              </UFormGroup>

              <UFormGroup label="Fatura Numarası">
                <UInput v-model="createForm.metadata.invoiceNumber" />
              </UFormGroup>

              <UFormGroup label="Satış Temsilcisi">
                <UInput v-model="createForm.metadata.salesRep" />
              </UFormGroup>
            </div>
          </div>

          <!-- Actions -->
          <div class="flex justify-end gap-2 border-t pt-4">
            <UButton
              color="gray"
              variant="outline"
              @click="handleCreateCancel"
            >
              İptal
            </UButton>
            <UButton
              color="green"
              @click="handleCreate"
              :loading="creating"
            >
              Oluştur ve Uygula
            </UButton>
          </div>
        </div>
      </UCard>
    </UModal>
  </div>
</template>

<script setup lang="ts">
import { useLicense, type LicenseInfo, type UserCountInfo, type CreateRealLicenseRequest, getLicenseType } from '~/composables/useLicense'

const props = defineProps<{
  domainName: string
  accessToken?: string | null
}>()

// Get license functions - will be recreated when token changes
const getLicenseFunctions = () => useLicense(props.accessToken)

const license = ref<LicenseInfo | null>(null)
const userCount = ref<UserCountInfo | null>(null)
const loading = ref(false)
const uploading = ref(false)
const creating = ref(false)
const error = ref<string | null>(null)
const showUploadModal = ref(false)
const showCreateModal = ref(false)
const showCustomerInfo = ref(false)
const showMetadata = ref(false)
const selectedFile = ref<File | null>(null)

const supportLevelOptions = [
  { label: 'Basic', value: 'basic' },
  { label: 'Standard', value: 'standard' },
  { label: 'Premium', value: 'premium' },
  { label: 'Enterprise', value: 'enterprise' }
]

const createForm = ref<CreateRealLicenseRequest>({
  expiresAt: '',
  expirationBehavior: {
    blockTokenGeneration: false,
    blockCrudOperations: false,
    blockGetOperations: false,
    allowReadOnly: false,
    customMessage: 'Lisans süreniz dolmuştur. Lütfen lisansınızı yenileyin.'
  },
  licenseFeatures: {
    maxUsers: 100,
    maxDomains: 1,
    maxStorageGB: 100,
    enableAdvancedFeatures: false,
    supportLevel: undefined,
    countActiveUsersOnly: true,
    activeUserDefinition: {
      isActive: true,
      lastLoginDays: 90
    }
  },
  customerInfo: {
    customerName: '',
    customerId: '',
    contactEmail: '',
    contactPhone: ''
  },
  metadata: {
    purchaseDate: '',
    invoiceNumber: '',
    salesRep: ''
  }
})

const loadLicense = async () => {
  if (!props.accessToken) {
    error.value = 'Lisans bilgilerini görüntülemek için önce kimlik doğrulaması yapmalısınız'
    return
  }

  loading.value = true
  error.value = null
  try {
    const { getLicense: getLicenseFn, getUserCount: getUserCountFn } = getLicenseFunctions()
    // Load license and user count in parallel
    const [licenseData, userCountData] = await Promise.all([
      getLicenseFn(props.domainName),
      getUserCountFn(props.domainName).catch(err => {
        console.error('Failed to load user count:', err)
        // Return a default object so UI can still show something
        return {
          domainName: props.domainName,
          activeUserCount: 0,
          maxUsers: undefined,
          canCreateUser: true
        } as UserCountInfo
      })
    ])
    license.value = licenseData
    userCount.value = userCountData
    console.log('License loaded:', licenseData)
    console.log('User count loaded:', userCountData)
  } catch (err: any) {
    if (err.statusCode === 401 || err.status === 401) {
      error.value = 'Kimlik doğrulaması gerekli. Lütfen tekrar giriş yapın.'
    } else {
      error.value = err.message || 'Lisans bilgisi yüklenemedi'
    }
  } finally {
    loading.value = false
  }
}

const refreshLicense = () => {
  loadLicense()
}

const handleDownload = async () => {
  if (!props.accessToken) {
    error.value = 'Lisans indirmek için önce kimlik doğrulaması yapmalısınız'
    return
  }

  try {
    const { downloadLicense: downloadLicenseFn } = getLicenseFunctions()
    const licenseType = license.value ? getLicenseType(license.value.licenseType).toLowerCase() as 'trial' | 'real' : 'real'
    await downloadLicenseFn(props.domainName, licenseType)
  } catch (err: any) {
    error.value = err.message || 'Lisans indirilemedi'
  }
}

const handleFileSelect = (event: Event) => {
  const target = event.target as HTMLInputElement
  if (target.files && target.files.length > 0) {
    selectedFile.value = target.files[0]
  }
}

const handleUpload = async () => {
  if (!selectedFile.value) return

  if (!props.accessToken) {
    error.value = 'Lisans yüklemek için önce kimlik doğrulaması yapmalısınız'
    return
  }

  uploading.value = true
  error.value = null
  try {
    const { uploadLicense: uploadLicenseFn } = getLicenseFunctions()
    await uploadLicenseFn(props.domainName, selectedFile.value)
    showUploadModal.value = false
    selectedFile.value = null
    await loadLicense()
  } catch (err: any) {
    error.value = err.message || 'Lisans yüklenemedi'
  } finally {
    uploading.value = false
  }
}

const formatDate = (dateString: string) => {
  return new Date(dateString).toLocaleDateString('tr-TR', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

const handleCreateCancel = () => {
  showCreateModal.value = false
  // Reset form
  createForm.value = {
    expiresAt: '',
    expirationBehavior: {
      blockTokenGeneration: false,
      blockCrudOperations: false,
      blockGetOperations: false,
      allowReadOnly: false,
      customMessage: 'Lisans süreniz dolmuştur. Lütfen lisansınızı yenileyin.'
    },
      licenseFeatures: {
        maxUsers: 100,
        maxDomains: 1,
        maxStorageGB: 100,
        enableAdvancedFeatures: false,
        supportLevel: undefined,
        countActiveUsersOnly: true,
        activeUserDefinition: {
          isActive: true,
          lastLoginDays: 90
        }
      },
    customerInfo: {
      customerName: '',
      customerId: '',
      contactEmail: '',
      contactPhone: ''
    },
    metadata: {
      purchaseDate: '',
      invoiceNumber: '',
      salesRep: ''
    }
  }
  showCustomerInfo.value = false
  showMetadata.value = false
}

const handleCreate = async () => {
  if (!props.accessToken) {
    error.value = 'Real lisans oluşturmak için önce kimlik doğrulaması yapmalısınız'
    return
  }

  if (!createForm.value.expiresAt) {
    error.value = 'Bitiş tarihi gereklidir'
    return
  }

  if (createForm.value.licenseFeatures.maxUsers < 1) {
    error.value = 'Maksimum kullanıcı sayısı en az 1 olmalıdır'
    return
  }

  creating.value = true
  error.value = null

  try {
    const { createRealLicense: createRealLicenseFn } = getLicenseFunctions()
    // Prepare request - convert dates to ISO strings, remove empty optional fields
    const request: CreateRealLicenseRequest = {
      expiresAt: new Date(createForm.value.expiresAt).toISOString(),
      expirationBehavior: createForm.value.expirationBehavior,
      licenseFeatures: {
        ...createForm.value.licenseFeatures,
        activeUserDefinition: createForm.value.licenseFeatures.countActiveUsersOnly
          ? (createForm.value.licenseFeatures.activeUserDefinition || {
              isActive: true,
              lastLoginDays: 90
            })
          : undefined
      },
      customerInfo: showCustomerInfo.value && createForm.value.customerInfo && 
        (createForm.value.customerInfo.customerName || 
         createForm.value.customerInfo.customerId || 
         createForm.value.customerInfo.contactEmail)
        ? createForm.value.customerInfo
        : undefined,
      metadata: showMetadata.value && createForm.value.metadata &&
        (createForm.value.metadata.purchaseDate ||
         createForm.value.metadata.invoiceNumber ||
         createForm.value.metadata.salesRep)
        ? {
            purchaseDate: createForm.value.metadata.purchaseDate
              ? new Date(createForm.value.metadata.purchaseDate).toISOString()
              : undefined,
            invoiceNumber: createForm.value.metadata.invoiceNumber || undefined,
            salesRep: createForm.value.metadata.salesRep || undefined
          }
        : undefined
    }

    await createRealLicenseFn(props.domainName, request)
    showCreateModal.value = false
    handleCreateCancel() // Reset form
    await loadLicense()
  } catch (err: any) {
    error.value = err.message || 'Real lisans oluşturulamadı'
    console.error('Failed to create real license:', err)
  } finally {
    creating.value = false
  }
}

// Watch for token changes and reload license
watch(() => props.accessToken, (newToken) => {
  if (newToken) {
    loadLicense()
  }
}, { immediate: true })

onMounted(() => {
  if (props.accessToken) {
    loadLicense()
  }
})
</script>
