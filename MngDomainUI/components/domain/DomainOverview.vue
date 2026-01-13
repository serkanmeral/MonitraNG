<template>
  <div class="space-y-6">
    <!-- Authentication Section -->
    <UCard class="bg-gray-50 border-gray-200">
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
              @click="$emit('authenticate')"
            >
              {{ accessToken ? 'Re-authenticate' : 'Authenticate' }}
            </UButton>
            <UButton
              v-if="accessToken"
              color="gray"
              variant="outline"
              icon="i-heroicons-eye"
              @click="$emit('view-token')"
            >
              View Token
            </UButton>
          </div>
        </div>
      </div>
    </UCard>

    <!-- Test Users & Groups Actions -->
    <UCard class="bg-purple-50 border-purple-200">
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
            @click="$emit('create-test-groups')"
          >
            Create Test Groups
          </UButton>
          <UButton
            color="indigo"
            variant="outline"
            icon="i-heroicons-users"
            :loading="creatingUsers"
            :disabled="!accessToken"
            @click="$emit('create-test-users')"
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
          @close="$emit('clear-user-group-message')"
        />
      </div>
    </UCard>

    <!-- Test Dataset Actions -->
    <UCard class="bg-blue-50 border-blue-200">
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
            @click="$emit('create-test-datasets')"
          >
            Create Test Datasets
          </UButton>
          <UButton
            color="green"
            variant="outline"
            icon="i-heroicons-arrow-down-tray"
            :loading="loadingTestData"
            :disabled="!accessToken"
            @click="$emit('insert-test-data')"
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
          @close="$emit('clear-dataset-message')"
        />
      </div>
    </UCard>

    <!-- Domain Information Cards -->
    <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
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
  </div>
</template>

<script setup lang="ts">
import type { Domain, DomainStatus } from '~/types/domain'

defineProps<{
  domain: Domain
  accessToken: string | null
  authenticatedUsername: string | null
  creatingDatasets: boolean
  loadingTestData: boolean
  creatingUsers: boolean
  creatingGroups: boolean
  datasetActionMessage: string | null
  datasetActionSuccess: boolean
  userGroupActionMessage: string | null
  userGroupActionSuccess: boolean
}>()

defineEmits<{
  authenticate: []
  'view-token': []
  'create-test-groups': []
  'create-test-users': []
  'create-test-datasets': []
  'insert-test-data': []
  'clear-dataset-message': []
  'clear-user-group-message': []
}>()

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
</script>
