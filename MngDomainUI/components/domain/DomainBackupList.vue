<template>
  <div class="space-y-4">
    <!-- Header with Create Backup Button -->
    <div class="flex justify-between items-center">
      <div>
        <h3 class="text-lg font-semibold text-gray-900">Domain Backups</h3>
        <p class="text-sm text-gray-600 mt-1">Manage and view domain backups</p>
      </div>
      <UButton
        color="primary"
        icon="i-heroicons-arrow-down-tray"
        :loading="creatingBackup"
        @click="handleCreateBackup"
      >
        Create Backup
      </UButton>
    </div>

    <!-- Success/Error Messages -->
    <UAlert
      v-if="actionMessage"
      :color="actionSuccess ? 'green' : 'red'"
      variant="soft"
      :title="actionMessage"
      class="mb-4"
      @close="actionMessage = null"
    />

    <!-- Loading State -->
    <div v-if="loading" class="flex justify-center items-center py-12">
      <UIcon name="i-heroicons-arrow-path" class="w-8 h-8 animate-spin text-primary" />
    </div>

    <!-- Empty State -->
    <UCard v-else-if="backups.length === 0" class="text-center py-12">
      <UIcon name="i-heroicons-archive-box" class="w-16 h-16 mx-auto text-gray-400 mb-4" />
      <h3 class="text-lg font-semibold text-gray-900 mb-2">No backups found</h3>
      <p class="text-gray-600 mb-4">Create your first backup to get started</p>
    </UCard>

    <!-- Backup List -->
    <UCard v-else>
      <div class="overflow-x-auto">
        <table class="w-full">
          <thead>
            <tr class="border-b border-gray-200">
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Status</th>
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Database</th>
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Created</th>
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Size</th>
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Duration</th>
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Path</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="backup in backups"
              :key="backup.id"
              class="border-b border-gray-100 hover:bg-gray-50"
            >
              <td class="py-3 px-4">
                <UBadge
                  :color="getStatusColor(backup.status)"
                  variant="soft"
                >
                  {{ backup.status }}
                </UBadge>
              </td>
              <td class="py-3 px-4 text-gray-700">{{ backup.databaseName }}</td>
              <td class="py-3 px-4 text-gray-600 text-sm">
                {{ formatDate(backup.startedAt) }}
              </td>
              <td class="py-3 px-4 text-gray-600 text-sm">
                {{ backup.sizeBytes ? formatBytes(backup.sizeBytes) : '-' }}
              </td>
              <td class="py-3 px-4 text-gray-600 text-sm">
                {{ backup.durationMs ? formatDuration(backup.durationMs) : '-' }}
              </td>
              <td class="py-3 px-4 text-gray-600 text-sm font-mono text-xs">
                {{ backup.backupPath || '-' }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Refresh Button -->
      <div v-if="backups.length > 0" class="mt-4 pt-4 border-t border-gray-200">
        <UButton
          color="gray"
          variant="outline"
          icon="i-heroicons-arrow-path"
          :loading="loading"
          @click="$emit('refresh')"
        >
          Refresh
        </UButton>
      </div>
    </UCard>
  </div>
</template>

<script setup lang="ts">
import type { BackupResponse } from '~/types/backup'

interface Props {
  domainName: string
  backups: BackupResponse[]
  loading: boolean
}

const props = defineProps<Props>()

const emit = defineEmits<{
  refresh: []
  backupCreated: [backup: BackupResponse]
}>()

const { getDomainBackups, createDomainBackup } = useBackup()
const creatingBackup = ref(false)
const actionMessage = ref<string | null>(null)
const actionSuccess = ref(false)

const getStatusColor = (status: string): 'green' | 'yellow' | 'red' | 'gray' => {
  switch (status) {
    case 'completed':
      return 'green'
    case 'in_progress':
      return 'yellow'
    case 'failed':
      return 'red'
    default:
      return 'gray'
  }
}

const formatDate = (dateString: string): string => {
  const date = new Date(dateString)
  return date.toLocaleDateString('tr-TR', {
    year: 'numeric',
    month: 'short',
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

const formatDuration = (ms: number): string => {
  if (ms < 1000) return `${ms}ms`
  if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`
  const minutes = Math.floor(ms / 60000)
  const seconds = Math.floor((ms % 60000) / 1000)
  return `${minutes}m ${seconds}s`
}

const handleCreateBackup = async () => {
  if (!props.domainName) return

  creatingBackup.value = true
  actionMessage.value = null

  try {
    const result = await createDomainBackup(props.domainName, {
      databaseType: 'mongodb'
    })
    
    actionSuccess.value = true
    actionMessage.value = 'Backup created successfully!'
    
    // Emit event to parent
    emit('backupCreated', result)
    
    // Refresh list after a short delay
    setTimeout(() => {
      emit('refresh')
    }, 1000)
  } catch (err: any) {
    actionSuccess.value = false
    actionMessage.value = err.message || 'Failed to create backup'
    console.error('Failed to create backup:', err)
  } finally {
    creatingBackup.value = false
  }
}
</script>
