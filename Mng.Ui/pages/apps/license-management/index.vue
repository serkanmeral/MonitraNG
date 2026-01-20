<template>
  <div>
    <v-row>
      <v-col cols="12">
        <v-card>
          <v-card-title class="d-flex justify-space-between align-center">
            <span>Lisans Yönetimi</span>
            <v-btn
              color="primary"
              icon="mdi-refresh"
              @click="loadLicenses"
              :loading="loading"
            >
              <v-icon>mdi-refresh</v-icon>
            </v-btn>
          </v-card-title>

          <v-card-text>
            <!-- Filters -->
            <v-row class="mb-4">
              <v-col cols="12" md="4">
                <v-text-field
                  v-model="searchQuery"
                  label="Domain Ara"
                  prepend-inner-icon="mdi-magnify"
                  variant="outlined"
                  density="compact"
                  clearable
                />
              </v-col>
              <v-col cols="12" md="4">
                <v-select
                  v-model="statusFilter"
                  :items="statusOptions"
                  label="Lisans Durumu"
                  variant="outlined"
                  density="compact"
                  clearable
                />
              </v-col>
              <v-col cols="12" md="4">
                <v-select
                  v-model="typeFilter"
                  :items="typeOptions"
                  label="Lisans Tipi"
                  variant="outlined"
                  density="compact"
                  clearable
                />
              </v-col>
            </v-row>

            <!-- License Table -->
            <v-data-table
              :headers="headers"
              :items="filteredLicenses"
              :loading="loading"
              item-key="domainName"
              class="elevation-1"
            >
              <template #item.domainName="{ item }">
                <strong>{{ item.domainName }}</strong>
              </template>

              <template #item.licenseType="{ item }">
                <v-chip
                  :color="item.licenseType === 'Real' ? 'blue' : 'yellow'"
                  size="small"
                  variant="flat"
                >
                  {{ item.licenseType === 'Real' ? 'Gerçek' : 'Trial' }}
                </v-chip>
              </template>

              <template #item.isValid="{ item }">
                <v-chip
                  :color="item.isValid ? 'green' : 'red'"
                  size="small"
                  variant="flat"
                >
                  {{ item.isValid ? 'Geçerli' : 'Süresi Dolmuş' }}
                </v-chip>
              </template>

              <template #item.expiresAt="{ item }">
                {{ formatDate(item.expiresAt) }}
              </template>

              <template #item.userCount="{ item }">
                <div v-if="item.userCount">
                  {{ item.userCount.activeUserCount }}
                  <span v-if="item.userCount.maxUsers">
                    / {{ item.userCount.maxUsers }}
                  </span>
                </div>
                <span v-else>-</span>
              </template>

              <template #item.actions="{ item }">
                <v-btn
                  icon="mdi-upload"
                  size="small"
                  variant="text"
                  @click="showUploadDialog(item.domainName)"
                  title="Lisans Yükle"
                />
                <v-btn
                  icon="mdi-download"
                  size="small"
                  variant="text"
                  @click="downloadLicense(item.domainName, item.licenseType)"
                  title="Lisans İndir"
                />
                <v-btn
                  icon="mdi-eye"
                  size="small"
                  variant="text"
                  @click="viewLicense(item)"
                  title="Detayları Görüntüle"
                />
              </template>
            </v-data-table>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Upload Dialog -->
    <v-dialog v-model="showUpload" max-width="500">
      <v-card>
        <v-card-title>Lisans Dosyası Yükle</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="uploadDomainName"
            label="Domain Adı"
            variant="outlined"
            readonly
            class="mb-4"
          />
          <v-file-input
            v-model="licenseFile"
            label="Lisans Dosyası (.enc)"
            accept=".enc"
            variant="outlined"
            show-size
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn color="gray" variant="text" @click="showUpload = false">
            İptal
          </v-btn>
          <v-btn
            color="primary"
            @click="handleUpload"
            :loading="uploading"
            :disabled="!licenseFile"
          >
            Yükle
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- License Details Dialog -->
    <v-dialog v-model="showDetails" max-width="800">
      <v-card v-if="selectedLicense">
        <v-card-title>Lisans Detayları</v-card-title>
        <v-card-text>
          <v-row>
            <v-col cols="6">
              <v-text-field
                label="Domain Adı"
                :value="selectedLicense.domainName"
                readonly
                variant="outlined"
                density="compact"
              />
            </v-col>
            <v-col cols="6">
              <v-text-field
                label="Lisans Tipi"
                :value="selectedLicense.licenseType"
                readonly
                variant="outlined"
                density="compact"
              />
            </v-col>
            <v-col cols="6">
              <v-text-field
                label="Yayınlanma Tarihi"
                :value="formatDate(selectedLicense.issuedAt)"
                readonly
                variant="outlined"
                density="compact"
              />
            </v-col>
            <v-col cols="6">
              <v-text-field
                label="Bitiş Tarihi"
                :value="formatDate(selectedLicense.expiresAt)"
                readonly
                variant="outlined"
                density="compact"
              />
            </v-col>
            <v-col cols="12" v-if="selectedLicense.customerInfo">
              <v-divider class="my-2" />
              <h3 class="mb-2">Müşteri Bilgileri</h3>
              <v-text-field
                label="Müşteri Adı"
                :value="selectedLicense.customerInfo.customerName"
                readonly
                variant="outlined"
                density="compact"
              />
            </v-col>
            <v-col cols="12" v-if="selectedLicense.licenseFeatures">
              <v-divider class="my-2" />
              <h3 class="mb-2">Lisans Özellikleri</h3>
              <v-text-field
                label="Maksimum Kullanıcı"
                :value="selectedLicense.licenseFeatures.maxUsers"
                readonly
                variant="outlined"
                density="compact"
              />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn color="primary" @click="showDetails = false">Kapat</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'

definePageMeta({
  pageType: 'manager'
})

interface LicenseInfo {
  domainName: string
  licenseType: 'Trial' | 'Real'
  isValid: boolean
  isExpired: boolean
  expiresAt: string
  issuedAt: string
  issuedBy: string
  customerInfo?: {
    customerName: string
    customerId: string
    contactEmail: string
  }
  licenseFeatures?: {
    maxUsers: number
  }
}

interface UserCountInfo {
  domainName: string
  activeUserCount: number
  maxUsers?: number
  canCreateUser: boolean
}

interface LicenseWithUserCount extends LicenseInfo {
  userCount?: UserCountInfo
}

const loading = ref(false)
const licenses = ref<LicenseWithUserCount[]>([])
const searchQuery = ref('')
const statusFilter = ref<string | null>(null)
const typeFilter = ref<string | null>(null)
const showUpload = ref(false)
const showDetails = ref(false)
const uploadDomainName = ref('')
const licenseFile = ref<File[]>([])
const uploading = ref(false)
const selectedLicense = ref<LicenseInfo | null>(null)

const statusOptions = [
  { title: 'Geçerli', value: 'valid' },
  { title: 'Süresi Dolmuş', value: 'expired' },
]

const typeOptions = [
  { title: 'Trial', value: 'Trial' },
  { title: 'Real', value: 'Real' },
]

const headers = [
  { title: 'Domain Adı', key: 'domainName' },
  { title: 'Lisans Tipi', key: 'licenseType' },
  { title: 'Durum', key: 'isValid' },
  { title: 'Bitiş Tarihi', key: 'expiresAt' },
  { title: 'Kullanıcı Sayısı', key: 'userCount' },
  { title: 'İşlemler', key: 'actions', sortable: false },
]

const filteredLicenses = computed(() => {
  let filtered = licenses.value

  if (searchQuery.value) {
    filtered = filtered.filter(l => 
      l.domainName.toLowerCase().includes(searchQuery.value!.toLowerCase())
    )
  }

  if (statusFilter.value) {
    filtered = filtered.filter(l => 
      statusFilter.value === 'valid' ? l.isValid : !l.isValid
    )
  }

  if (typeFilter.value) {
    filtered = filtered.filter(l => l.licenseType === typeFilter.value)
  }

  return filtered
})

const loadLicenses = async () => {
  loading.value = true
  try {
    // Get all domains first
    const domains = await $fetch('/api/keeper/domain')
    
    // Load license info for each domain
    const licensePromises = domains.map(async (domain: any) => {
      try {
        const license = await $fetch(`/api/keeper/license/${domain.name}`)
        const userCount = await $fetch(`/api/keeper/license/${domain.name}/user-count`)
        return {
          ...license,
          userCount
        }
      } catch (err) {
        // Domain might not have license yet
        return null
      }
    })

    const results = await Promise.all(licensePromises)
    licenses.value = results.filter((l): l is LicenseWithUserCount => l !== null)
  } catch (err: any) {
    console.error('Failed to load licenses:', err)
  } finally {
    loading.value = false
  }
}

const showUploadDialog = (domainName: string) => {
  uploadDomainName.value = domainName
  showUpload.value = true
}

const handleUpload = async () => {
  if (!licenseFile.value || licenseFile.value.length === 0) return

  uploading.value = true
  try {
    const formData = new FormData()
    formData.append('domainName', uploadDomainName.value)
    formData.append('licenseFile', licenseFile.value[0])

    await $fetch('/api/keeper/license/upload', {
      method: 'POST',
      body: formData,
    })

    showUpload.value = false
    licenseFile.value = []
    await loadLicenses()
  } catch (err: any) {
    console.error('Failed to upload license:', err)
  } finally {
    uploading.value = false
  }
}

const downloadLicense = async (domainName: string, licenseType: string) => {
  try {
    const response = await fetch(`/api/keeper/license/${domainName}/download?type=${licenseType.toLowerCase()}`)
    const blob = await response.blob()
    const url = window.URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `license-${licenseType.toLowerCase()}-${domainName}.enc`
    document.body.appendChild(a)
    a.click()
    window.URL.revokeObjectURL(url)
    document.body.removeChild(a)
  } catch (err: any) {
    console.error('Failed to download license:', err)
  }
}

const viewLicense = (license: LicenseInfo) => {
  selectedLicense.value = license
  showDetails.value = true
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

onMounted(() => {
  loadLicenses()
})
</script>
