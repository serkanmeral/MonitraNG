<template>
  <div>
    <!-- Loading State -->
    <div v-if="loading" class="flex justify-center items-center py-12">
      <UIcon name="i-heroicons-arrow-path" class="w-8 h-8 animate-spin text-primary" />
    </div>

    <!-- Error State -->
    <UAlert
      v-else-if="error"
      color="red"
      variant="soft"
      :title="error"
      class="mb-4"
    />

    <!-- Domain Details -->
    <div v-else-if="domain" class="space-y-6">
      <!-- Header -->
      <div class="flex justify-between items-start">
        <div>
          <div class="flex items-center gap-3 mb-2">
            <h1 class="text-3xl font-bold" style="color: #111827;">{{ domain.displayName }}</h1>
            <UBadge
              :color="getStatusColor(domain.status)"
              variant="soft"
              size="lg"
            >
              {{ domain.status }}
            </UBadge>
          </div>
          <p class="font-medium" style="color: #1f2937;">{{ domain.name }}</p>
        </div>
        <div class="flex gap-2">
          <UButton
            color="gray"
            variant="outline"
            icon="i-heroicons-arrow-left"
            to="/domains"
          >
            Back
          </UButton>
          <UButton
            v-if="!isEditing"
            color="primary"
            icon="i-heroicons-pencil"
            @click="isEditing = true"
          >
            Edit
          </UButton>
        </div>
      </div>

      <!-- Authentication Section -->
      <UCard v-if="!isEditing" class="bg-gray-50 border-gray-200">
        <template #header>
          <h3 class="text-lg font-semibold text-gray-900 dark:text-gray-100">Authentication</h3>
        </template>
        <div class="space-y-3">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-sm text-gray-600">
                Authenticate to enable dataset operations
              </p>
              <p v-if="accessToken" class="text-xs text-green-600 mt-1">
                ✓ Authenticated as {{ authenticatedUsername }}
              </p>
              <p v-else class="text-xs text-gray-500 mt-1">
                ⚠ Not authenticated
              </p>
            </div>
            <div class="flex gap-2">
              <UButton
                color="primary"
                variant="outline"
                icon="i-heroicons-key"
                @click="showAuthModal = true"
              >
                {{ accessToken ? 'Re-authenticate' : 'Authenticate' }}
              </UButton>
              <UButton
                v-if="accessToken"
                color="gray"
                variant="outline"
                icon="i-heroicons-eye"
                @click="showTokenModal = true"
              >
                View Token
              </UButton>
            </div>
          </div>
        </div>
      </UCard>

      <!-- Test Users & Groups Actions -->
      <UCard v-if="!isEditing" class="bg-purple-50 border-purple-200">
        <template #header>
          <h3 class="text-lg font-semibold text-purple-900">Test Users & Groups</h3>
        </template>
        <div class="space-y-3">
          <p class="text-sm text-gray-600">
            Create test users and groups for testing purposes.
          </p>
          <div class="flex gap-2">
            <UButton
              color="purple"
              variant="outline"
              icon="i-heroicons-user-group"
              :loading="creatingGroups"
              :disabled="!accessToken"
              @click="handleCreateTestGroupsClick"
            >
              Create Test Groups
            </UButton>
            <UButton
              color="indigo"
              variant="outline"
              icon="i-heroicons-users"
              :loading="creatingUsers"
              :disabled="!accessToken"
              @click="handleCreateTestUsersClick"
            >
              Create Test Users
            </UButton>
          </div>
          <UAlert
            v-if="userGroupActionMessage"
            :color="userGroupActionSuccess ? 'green' : 'red'"
            variant="soft"
            :title="userGroupActionMessage"
            class="mt-2"
            @close="userGroupActionMessage = null"
          />
        </div>
      </UCard>

      <!-- Test Dataset Actions -->
      <UCard v-if="!isEditing" class="bg-blue-50 border-blue-200">
        <template #header>
          <h3 class="text-lg font-semibold text-blue-900">Test Dataset Actions</h3>
        </template>
        <div class="space-y-3">
          <p class="text-sm text-gray-600">
            Create test datasets (tst_publishers, tst_genres, tst_books) and load sample data for testing purposes.
          </p>
          <div class="flex gap-2">
            <UButton
              color="blue"
              variant="outline"
              icon="i-heroicons-document-plus"
              :loading="creatingDatasets"
              :disabled="!accessToken"
              @click="handleCreateTestDatasetsClick"
            >
              Create Test Datasets
            </UButton>
            <UButton
              color="green"
              variant="outline"
              icon="i-heroicons-arrow-down-tray"
              :loading="loadingTestData"
              :disabled="!accessToken"
              @click="handleInsertTestDataClick"
            >
              Load Test Data
            </UButton>
          </div>
          <UAlert
            v-if="datasetActionMessage"
            :color="datasetActionSuccess ? 'green' : 'red'"
            variant="soft"
            :title="datasetActionMessage"
            class="mt-2"
            @close="datasetActionMessage = null"
          />
        </div>
      </UCard>

      <!-- Admin Authentication Modal -->
      <DomainAdminAuthModal
        v-model="showAuthModal"
        :domain-name="domain?.name || ''"
        @authenticated="handleAuthenticated"
        @cancel="handleAuthCancel"
      />

      <!-- Create Users Modal -->
      <DomainCreateUsersModal
        v-model="showCreateUsersModal"
        @confirmed="handleCreateUsersConfirmed"
        @cancel="showCreateUsersModal = false"
      />

      <!-- Token View Modal -->
      <DomainTokenViewModal
        v-model="showTokenModal"
        :token="accessToken"
      />

      <!-- Domain Information Cards -->
      <div v-if="!isEditing" class="grid grid-cols-1 md:grid-cols-2 gap-6">
        <!-- Basic Information -->
        <UCard class="bg-white dark:bg-gray-800">
          <template #header>
            <h3 class="text-lg font-semibold">Basic Information</h3>
          </template>
          <div class="space-y-4">
            <div>
              <label class="text-sm font-medium text-gray-500 dark:text-gray-400">Domain Name</label>
              <p class="text-gray-900 dark:text-gray-100">{{ domain.name }}</p>
            </div>
            <div>
              <label class="text-sm font-medium text-gray-500 dark:text-gray-400">Display Name</label>
              <p class="text-gray-900 dark:text-gray-100">{{ domain.displayName }}</p>
            </div>
            <div>
              <label class="text-sm font-medium text-gray-500 dark:text-gray-400">Database Name</label>
              <p class="text-gray-900 dark:text-gray-100 font-mono text-sm">{{ domain.databaseName }}</p>
            </div>
            <div>
              <label class="text-sm font-medium text-gray-500 dark:text-gray-400">Realm Name</label>
              <p class="text-gray-900 dark:text-gray-100 font-mono text-sm">{{ domain.realmName }}</p>
            </div>
            <div>
              <label class="text-sm font-medium text-gray-500 dark:text-gray-400">Storage Bucket</label>
              <p class="text-gray-900 dark:text-gray-100 font-mono text-sm">{{ domain.storageBucket }}</p>
            </div>
          </div>
        </UCard>

        <!-- Storage & Status -->
        <UCard class="bg-white dark:bg-gray-800">
          <template #header>
            <h3 class="text-lg font-semibold">Storage & Status</h3>
          </template>
          <div class="space-y-4">
            <div>
              <label class="text-sm font-medium text-gray-500 dark:text-gray-400">Storage Used</label>
              <div class="mt-1">
                <p class="text-gray-900 dark:text-gray-100">{{ formatBytes(domain.storageUsed) }} / {{ formatBytes(domain.storageQuota) }}</p>
                <UProgress
                  :value="(domain.storageUsed / domain.storageQuota) * 100"
                  color="primary"
                  class="mt-2"
                />
              </div>
            </div>
            <div>
              <label class="text-sm font-medium text-gray-500 dark:text-gray-400">Status</label>
              <p>
                <UBadge
                  :color="getStatusColor(domain.status)"
                  variant="soft"
                >
                  {{ domain.status }}
                </UBadge>
              </p>
            </div>
            <div>
              <label class="text-sm font-medium text-gray-500 dark:text-gray-400">Created At</label>
              <p class="text-gray-900 dark:text-gray-100">{{ formatDate(domain.createdAt) }}</p>
            </div>
            <div v-if="domain.updatedAt">
              <label class="text-sm font-medium text-gray-500 dark:text-gray-400">Updated At</label>
              <p class="text-gray-900 dark:text-gray-100">{{ formatDate(domain.updatedAt) }}</p>
            </div>
            <div v-if="domain.expiresAt">
              <label class="text-sm font-medium text-gray-500 dark:text-gray-400">Expires At</label>
              <p class="text-gray-900 dark:text-gray-100">{{ formatDate(domain.expiresAt) }}</p>
            </div>
          </div>
        </UCard>

        <!-- Settings -->
        <UCard class="md:col-span-2 bg-white dark:bg-gray-800">
          <template #header>
            <h3 class="text-lg font-semibold">Settings</h3>
          </template>
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <label class="text-sm font-medium text-gray-500 dark:text-gray-400">Max Users</label>
              <p class="text-gray-900 dark:text-gray-100">{{ domain.settings?.maxUsers || 'N/A' }}</p>
            </div>
            <div>
              <label class="text-sm font-medium text-gray-500 dark:text-gray-400">Max Assets</label>
              <p class="text-gray-900 dark:text-gray-100">{{ domain.settings?.maxAssets || 'N/A' }}</p>
            </div>
            <div>
              <label class="text-sm font-medium text-gray-500 dark:text-gray-400">MQTT Enabled</label>
              <p class="text-gray-900 dark:text-gray-100">
                <UBadge :color="domain.settings?.enableMqtt ? 'green' : 'gray'" variant="soft">
                  {{ domain.settings?.enableMqtt ? 'Yes' : 'No' }}
                </UBadge>
              </p>
            </div>
          </div>
        </UCard>
      </div>

      <!-- Edit Form -->
      <UCard v-else>
        <template #header>
          <div class="flex justify-between items-center">
            <h3 class="text-lg font-semibold">Edit Domain</h3>
            <UButton
              color="gray"
              variant="ghost"
              icon="i-heroicons-x-mark"
              @click="cancelEdit"
            />
          </div>
        </template>
        <DomainEditForm
          :domain="domain"
          @success="handleUpdateSuccess"
          @cancel="cancelEdit"
        />
      </UCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Domain, DomainStatus } from '~/types/domain'
import { useDomainStore } from '~/stores/domain'

definePageMeta({
  layout: 'default',
  middleware: 'auth'
})

const route = useRoute()
const domainStore = useDomainStore()
const { getDomainById } = useDomain()

const domain = ref<Domain | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const isEditing = ref(false)
const creatingDatasets = ref(false)
const loadingTestData = ref(false)
const creatingUsers = ref(false)
const creatingGroups = ref(false)
const datasetActionMessage = ref<string | null>(null)
const datasetActionSuccess = ref(false)
const userGroupActionMessage = ref<string | null>(null)
const userGroupActionSuccess = ref(false)
const showAuthModal = ref(false)
const showCreateUsersModal = ref(false)
const showTokenModal = ref(false)
const accessToken = ref<string | null>(null)
const authenticatedUsername = ref<string | null>(null)

const domainId = route.params.id as string

// Fetch domain on mount
onMounted(async () => {
  await fetchDomain()
})

const fetchDomain = async () => {
  loading.value = true
  error.value = null
  try {
    domain.value = await getDomainById(domainId)
  } catch (err: any) {
    error.value = err.message || 'Failed to fetch domain'
    console.error('Failed to fetch domain:', err)
  } finally {
    loading.value = false
  }
}

const getStatusColor = (status: DomainStatus): 'green' | 'yellow' | 'orange' | 'gray' | 'red' => {
  const colors: Record<DomainStatus, 'green' | 'yellow' | 'orange' | 'gray' | 'red'> = {
    Active: 'green',
    Pending: 'yellow',
    Suspended: 'orange',
    Expired: 'gray',
    Deleted: 'red',
    Failed: 'red'
  }
  return colors[status] || 'gray'
}

const formatDate = (dateString: string): string => {
  const date = new Date(dateString)
  return date.toLocaleDateString('tr-TR', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const formatBytes = (bytes: number): string => {
  if (bytes === 0) return '0 Bytes'
  const k = 1024
  const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i]
}

const handleUpdateSuccess = async () => {
  isEditing.value = false
  await fetchDomain()
  // Refresh domain list in store
  await domainStore.fetchDomains()
}

const cancelEdit = () => {
  isEditing.value = false
}

const { createTestDatasets, insertTestData, createTestUsers, createTestGroups } = useDataset()

// Handle authentication
const handleAuthenticated = (data: { token: string; username: string }) => {
  accessToken.value = data.token
  authenticatedUsername.value = data.username
  
  // Log token to console
  console.log('Access Token:', data.token)
  console.log('Authenticated as:', data.username)
}

const handleAuthCancel = () => {
  // Do nothing on cancel
}

// Clear token when leaving the page
onUnmounted(() => {
  accessToken.value = null
  authenticatedUsername.value = null
})

const handleCreateTestDatasetsClick = async () => {
  if (!domain.value || !accessToken.value) return
  await handleCreateTestDatasets()
}

const handleInsertTestDataClick = async () => {
  if (!domain.value || !accessToken.value) return
  await handleInsertTestData()
}

const handleCreateTestDatasets = async () => {
  if (!domain.value || !accessToken.value) return

  creatingDatasets.value = true
  datasetActionMessage.value = null

  try {
    const result = await createTestDatasets(domain.value.name, {
      adminEmail: authenticatedUsername.value || '',
      adminPassword: '', // Password not needed, we have token
      token: accessToken.value
    }) as any
    
    datasetActionSuccess.value = true
    datasetActionMessage.value = 'Test datasets created successfully!'
    
    // Show detailed results
    if (result.results) {
      const details = []
      if (result.results.category) details.push(`Category: ${result.results.category.created ? 'Created' : 'Exists'}`)
      if (result.results.publishers) details.push(`Publishers: ${result.results.publishers.created ? 'Created' : 'Exists'}`)
      if (result.results.genres) details.push(`Genres: ${result.results.genres.created ? 'Created' : 'Exists'}`)
      if (result.results.books) details.push(`Books: ${result.results.books.created ? 'Created' : 'Exists'}`)
      
      if (details.length > 0) {
        datasetActionMessage.value += ` (${details.join(', ')})`
      }
    }
  } catch (err: any) {
    datasetActionSuccess.value = false
    datasetActionMessage.value = err.message || 'Failed to create test datasets'
    console.error('Failed to create test datasets:', err)
    
    // Clear token on 401 error
    if (err.statusCode === 401) {
      accessToken.value = null
      authenticatedUsername.value = null
    }
  } finally {
    creatingDatasets.value = false
  }
}

const handleInsertTestData = async () => {
  if (!domain.value || !accessToken.value) return

  loadingTestData.value = true
  datasetActionMessage.value = null

  try {
    const result = await insertTestData(domain.value.name, {
      adminEmail: authenticatedUsername.value || '',
      adminPassword: '', // Password not needed, we have token
      token: accessToken.value
    }) as any
    
    datasetActionSuccess.value = true
    
    if (result.summary) {
      datasetActionMessage.value = `Test data loaded successfully! (${result.summary.publishers} publishers, ${result.summary.genres} genres, ${result.summary.books} books)`
    } else {
      datasetActionMessage.value = 'Test data loaded successfully!'
    }
  } catch (err: any) {
    datasetActionSuccess.value = false
    datasetActionMessage.value = err.message || 'Failed to load test data'
    console.error('Failed to load test data:', err)
    
    // Clear token on 401 error
    if (err.statusCode === 401) {
      accessToken.value = null
      authenticatedUsername.value = null
    }
  } finally {
    loadingTestData.value = false
  }
}

const handleCreateTestUsersClick = () => {
  if (!domain.value || !accessToken.value) return
  showCreateUsersModal.value = true
}

const handleCreateUsersConfirmed = async (data: { userCount: number; password: string }) => {
  if (!domain.value || !accessToken.value) return
  await handleCreateTestUsers(data.userCount, data.password)
}

const handleCreateTestGroupsClick = async () => {
  if (!domain.value || !accessToken.value) return
  await handleCreateTestGroups()
}

const handleCreateTestUsers = async (userCount: number = 5, password: string = 'Test123!') => {
  if (!domain.value || !accessToken.value) return

  creatingUsers.value = true
  userGroupActionMessage.value = null

  try {
    const result = await createTestUsers(domain.value.name, {
      adminEmail: authenticatedUsername.value || '',
      adminPassword: '',
      token: accessToken.value,
      userCount: userCount,
      defaultPassword: password
    }) as any
    
    userGroupActionSuccess.value = true
    
    if (result.summary) {
      userGroupActionMessage.value = `Test users created successfully! (${result.summary.users} users)`
    } else {
      userGroupActionMessage.value = 'Test users created successfully!'
    }
  } catch (err: any) {
    userGroupActionSuccess.value = false
    userGroupActionMessage.value = err.message || 'Failed to create test users'
    console.error('Failed to create test users:', err)
    
    if (err.statusCode === 401) {
      accessToken.value = null
      authenticatedUsername.value = null
    }
  } finally {
    creatingUsers.value = false
  }
}

const handleCreateTestGroups = async () => {
  if (!domain.value || !accessToken.value) return

  creatingGroups.value = true
  userGroupActionMessage.value = null

  try {
    const result = await createTestGroups(domain.value.name, {
      adminEmail: authenticatedUsername.value || '',
      adminPassword: '',
      token: accessToken.value
    }) as any
    
    userGroupActionSuccess.value = true
    
    if (result.summary) {
      userGroupActionMessage.value = `Test groups created successfully! (${result.summary.groups} groups)`
    } else {
      userGroupActionMessage.value = 'Test groups created successfully!'
    }
  } catch (err: any) {
    userGroupActionSuccess.value = false
    userGroupActionMessage.value = err.message || 'Failed to create test groups'
    console.error('Failed to create test groups:', err)
    
    if (err.statusCode === 401) {
      accessToken.value = null
      authenticatedUsername.value = null
    }
  } finally {
    creatingGroups.value = false
  }
}
</script>

