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

      <!-- Tabs (shown when not editing) -->
      <UTabs v-if="!isEditing" :items="tabs" v-model="activeTab" class="w-full">
        <template #default="{ item }">
          <div class="flex items-center gap-2">
            <UIcon :name="item.icon" class="w-4 h-4" />
            <span>{{ item.label }}</span>
          </div>
        </template>

        <template #item="{ item, index }">
          <div v-if="index === 0" class="space-y-6">
            <!-- Overview Content -->
            <DomainOverview
              :domain="domain"
              :access-token="accessToken"
              :authenticated-username="authenticatedUsername"
              :creating-datasets="creatingDatasets"
              :loading-test-data="loadingTestData"
              :creating-users="creatingUsers"
              :creating-groups="creatingGroups"
              :dataset-action-message="datasetActionMessage"
              :dataset-action-success="datasetActionSuccess"
              :user-group-action-message="userGroupActionMessage"
              :user-group-action-success="userGroupActionSuccess"
              @authenticate="showAuthModal = true"
              @view-token="showTokenModal = true"
              @create-test-groups="handleCreateTestGroupsClick"
              @create-test-users="handleCreateTestUsersClick"
              @create-test-datasets="handleCreateTestDatasetsClick"
              @insert-test-data="handleInsertTestDataClick"
              @clear-dataset-message="datasetActionMessage = null"
              @clear-user-group-message="userGroupActionMessage = null"
            />
          </div>
          <div v-else-if="index === 1" class="space-y-6">
            <!-- Template Management -->
            <DomainTemplateManagement
              :domain-id="domain.id"
              :domain-name="domain.name"
            />
          </div>
        </template>
      </UTabs>

      <!-- Modals (shared across tabs) -->
      <DomainAdminAuthModal
        v-model="showAuthModal"
        :domain-name="domain?.name || ''"
        @authenticated="handleAuthenticated"
        @cancel="handleAuthCancel"
      />

      <DomainCreateUsersModal
        v-model="showCreateUsersModal"
        @confirmed="handleCreateUsersConfirmed"
        @cancel="showCreateUsersModal = false"
      />

      <DomainTokenViewModal
        v-model="showTokenModal"
        :token="accessToken"
      />

      <!-- Edit Form (shown when editing) -->
      <UCard v-if="isEditing">
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
const activeTab = ref(0)

const tabs = [
  { label: 'Overview', value: 'overview', icon: 'i-heroicons-home' },
  { label: 'Templates', value: 'templates', icon: 'i-heroicons-document-duplicate' },
]

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

