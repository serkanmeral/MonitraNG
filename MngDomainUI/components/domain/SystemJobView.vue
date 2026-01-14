<template>
  <div v-if="job" class="space-y-6">
    <!-- Basic Info -->
    <div class="grid grid-cols-2 gap-4">
      <div>
        <label class="text-sm font-medium text-gray-500">Job ID</label>
        <p class="mt-1 font-mono text-sm">{{ job.jobId }}</p>
      </div>
      <div>
        <label class="text-sm font-medium text-gray-500">Status</label>
        <p class="mt-1">
          <UBadge
            :color="job.isActive ? 'green' : 'gray'"
            variant="soft"
          >
            {{ job.isActive ? 'Active' : 'Inactive' }}
          </UBadge>
        </p>
      </div>
      <div>
        <label class="text-sm font-medium text-gray-500">Name</label>
        <p class="mt-1 font-medium">{{ job.name }}</p>
      </div>
      <div>
        <label class="text-sm font-medium text-gray-500">Cron Expression</label>
        <p class="mt-1 font-mono text-sm">{{ job.cronExpression }}</p>
      </div>
      <div>
        <label class="text-sm font-medium text-gray-500">Endpoint</label>
        <p class="mt-1">
          <span class="font-medium">{{ job.httpMethod }}</span>
          <span class="text-gray-600 ml-2">{{ job.endpointUrl }}</span>
        </p>
      </div>
      <div>
        <label class="text-sm font-medium text-gray-500">Timeout</label>
        <p class="mt-1">{{ job.timeoutSeconds }} seconds</p>
      </div>
    </div>

    <!-- Description -->
    <div v-if="job.description">
      <label class="text-sm font-medium text-gray-500">Description</label>
      <p class="mt-1 text-gray-700">{{ job.description }}</p>
    </div>

    <!-- Dates -->
    <div class="grid grid-cols-2 gap-4">
      <div v-if="job.startDate">
        <label class="text-sm font-medium text-gray-500">Start Date</label>
        <p class="mt-1">{{ formatDate(job.startDate) }}</p>
      </div>
      <div v-if="job.expireDate">
        <label class="text-sm font-medium text-gray-500">Expire Date</label>
        <p class="mt-1">{{ formatDate(job.expireDate) }}</p>
      </div>
    </div>

    <!-- Execution Limits -->
    <div v-if="job.maxExecutionCount" class="grid grid-cols-2 gap-4">
      <div>
        <label class="text-sm font-medium text-gray-500">Max Execution Count</label>
        <p class="mt-1">{{ job.maxExecutionCount }}</p>
      </div>
      <div>
        <label class="text-sm font-medium text-gray-500">Current Execution Count</label>
        <p class="mt-1">{{ job.totalExecutionCount }} / {{ job.maxExecutionCount }}</p>
      </div>
    </div>

    <!-- Execution Statistics -->
    <div class="grid grid-cols-3 gap-4">
      <div>
        <label class="text-sm font-medium text-gray-500">Total Executions</label>
        <p class="mt-1 text-lg font-semibold">{{ job.totalExecutionCount }}</p>
      </div>
      <div>
        <label class="text-sm font-medium text-gray-500">Successful</label>
        <p class="mt-1 text-lg font-semibold text-green-600">{{ job.successfulExecutionCount }}</p>
      </div>
      <div>
        <label class="text-sm font-medium text-gray-500">Failed</label>
        <p class="mt-1 text-lg font-semibold text-red-600">{{ job.failedExecutionCount }}</p>
      </div>
    </div>

    <!-- Payload (for POST) -->
    <div v-if="job.httpMethod === 'POST' && job.payload">
      <label class="text-sm font-medium text-gray-500">Payload</label>
      <pre class="mt-1 p-3 bg-gray-50 rounded text-xs font-mono overflow-x-auto">{{ formatJson(job.payload) }}</pre>
    </div>

    <!-- Last Execution -->
    <div v-if="job.lastExecution">
      <label class="text-sm font-medium text-gray-500">Last Execution</label>
      <div class="mt-1 p-3 bg-gray-50 rounded">
        <div class="grid grid-cols-2 gap-4 text-sm">
          <div>
            <span class="font-medium">Status:</span>
            <UBadge
              :color="job.lastExecution.status === 'success' ? 'green' : 'red'"
              variant="soft"
              class="ml-2"
            >
              {{ job.lastExecution.status }}
            </UBadge>
          </div>
          <div>
            <span class="font-medium">Executed At:</span>
            <span class="ml-2">{{ formatDate(job.lastExecution.executedAt) }}</span>
          </div>
          <div v-if="job.lastExecution.responseTimeMs">
            <span class="font-medium">Response Time:</span>
            <span class="ml-2">{{ job.lastExecution.responseTimeMs }}ms</span>
          </div>
          <div v-if="job.lastExecution.responseCode">
            <span class="font-medium">Response Code:</span>
            <span class="ml-2">{{ job.lastExecution.responseCode }}</span>
          </div>
        </div>
        <div v-if="job.lastExecution.errorMessage" class="mt-2">
          <span class="font-medium text-red-600">Error:</span>
          <p class="mt-1 text-sm text-red-600">{{ job.lastExecution.errorMessage }}</p>
        </div>
      </div>
    </div>

    <!-- Actions -->
    <div class="flex justify-end gap-2 pt-4 border-t">
      <UButton
        color="primary"
        icon="i-heroicons-pencil"
        @click="$emit('edit')"
      >
        Edit Job
      </UButton>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { ScheduledJob } from '~/composables/useScheduler'

defineProps<{
  job: ScheduledJob
}>()

defineEmits<{
  edit: []
}>()

const formatDate = (dateString: string): string => {
  const date = new Date(dateString)
  return date.toLocaleString('tr-TR', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

const formatJson = (jsonString: string): string => {
  try {
    const parsed = JSON.parse(jsonString)
    return JSON.stringify(parsed, null, 2)
  } catch {
    return jsonString
  }
}
</script>
