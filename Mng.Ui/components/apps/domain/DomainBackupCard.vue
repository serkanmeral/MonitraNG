<template>
  <v-card elevation="10" class="mb-4">
    <v-card-title class="d-flex align-center justify-space-between">
      <div class="d-flex align-center">
        <v-icon class="mr-2">mdi-archive</v-icon>
        {{ t('domain.cards.backupInfo') }}
      </div>
      <div class="d-flex align-center gap-2">
        <v-btn
          icon=""
          size="small"
          variant="text"
          :loading="loading"
          @click="refreshBackups"
        >
          <RefreshIcon size="20" />
        </v-btn>
        <v-btn
          color="primary"
          size="small"
          prepend-icon="mdi-download"
          :loading="creatingBackup"
          @click="handleCreateBackup"
        >
          {{ t('domain.backup.createBackup') }}
        </v-btn>
      </div>
    </v-card-title>

    <v-divider />

    <v-card-text>
      <!-- Success/Error Messages -->
      <v-alert
        v-if="actionMessage"
        :type="actionSuccess ? 'success' : 'error'"
        variant="tonal"
        class="mb-4"
        closable
        @click:close="actionMessage = null"
      >
        {{ actionMessage }}
      </v-alert>

      <!-- Loading State -->
      <div v-if="loading" class="text-center py-8">
        <v-progress-circular indeterminate color="primary" size="32" />
        <p class="mt-2">{{ t('domain.messages.loading') }}</p>
      </div>

      <!-- Empty State -->
      <div v-else-if="backups.length === 0" class="text-center py-8">
        <v-icon size="64" color="grey-lighten-1" class="mb-4">mdi-archive-off</v-icon>
        <p class="text-h6 mb-2">{{ t('domain.backup.noBackups') }}</p>
        <p class="text-body-2 text-medium-emphasis">{{ t('domain.backup.noBackupsDescription') }}</p>
      </div>

      <!-- Backup List -->
      <v-table v-else>
        <thead>
          <tr>
            <th class="text-left">{{ t('domain.backup.status') }}</th>
            <th class="text-left">{{ t('domain.backup.database') }}</th>
            <th class="text-left">{{ t('domain.backup.created') }}</th>
            <th class="text-left">{{ t('domain.backup.size') }}</th>
            <th class="text-left">{{ t('domain.backup.duration') }}</th>
            <th class="text-left">{{ t('domain.backup.path') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="backup in backups" :key="backup.id">
            <td>
              <v-chip
                :color="getStatusColor(backup.status)"
                size="small"
                variant="tonal"
              >
                {{ getStatusLabel(backup.status) }}
              </v-chip>
            </td>
            <td>{{ backup.databaseName }}</td>
            <td>{{ formatDate(backup.startedAt) }}</td>
            <td>{{ backup.sizeBytes ? formatBytes(backup.sizeBytes) : '-' }}</td>
            <td>{{ backup.durationMs ? formatDuration(backup.durationMs) : '-' }}</td>
            <td class="text-caption font-mono">{{ backup.backupPath || '-' }}</td>
          </tr>
        </tbody>
      </v-table>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { RefreshIcon } from 'vue-tabler-icons'
import type { BackupResponse } from '@/types/backup'
import { useBackup } from '@/composables/useBackup'

// Get i18n instance for legacy mode
const nuxtApp = useNuxtApp()
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n
const t = (key: string, params?: any) => {
  if (i18n && i18n.t) {
    return i18n.t(key, params)
  }
  if (i18n?.global?.t) {
    return i18n.global.t(key, params)
  }
  return key
}

interface Props {
  domainName: string
}

const props = defineProps<Props>()

const { getDomainBackups, createDomainBackup } = useBackup()
const backups = ref<BackupResponse[]>([])
const loading = ref(false)
const creatingBackup = ref(false)
const actionMessage = ref<string | null>(null)
const actionSuccess = ref(false)

const getStatusColor = (status: string): string => {
  switch (status) {
    case 'completed':
      return 'success'
    case 'in_progress':
      return 'warning'
    case 'failed':
      return 'error'
    default:
      return 'grey'
  }
}

const getStatusLabel = (status: string): string => {
  const statusMap: Record<string, string> = {
    completed: t('domain.backup.statusCompleted'),
    in_progress: t('domain.backup.statusInProgress'),
    failed: t('domain.backup.statusFailed'),
  }
  return statusMap[status] || status
}

const formatDate = (dateString: string): string => {
  const date = new Date(dateString)
  return new Intl.DateTimeFormat('tr-TR', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date)
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

const fetchBackups = async () => {
  if (!props.domainName) return

  loading.value = true
  try {
    const result = await getDomainBackups(props.domainName)
    backups.value = result.backups || []
  } catch (err: any) {
    console.error('Failed to fetch backups:', err)
    backups.value = []
    actionMessage.value = err.message || t('domain.backup.fetchError')
    actionSuccess.value = false
  } finally {
    loading.value = false
  }
}

const refreshBackups = async () => {
  await fetchBackups()
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
    actionMessage.value = t('domain.backup.createSuccess')
    
    // Refresh list after a short delay
    setTimeout(() => {
      fetchBackups()
    }, 1000)
  } catch (err: any) {
    actionSuccess.value = false
    actionMessage.value = err.message || t('domain.backup.createError')
    console.error('Failed to create backup:', err)
  } finally {
    creatingBackup.value = false
  }
}

onMounted(() => {
  fetchBackups()
})
</script>
