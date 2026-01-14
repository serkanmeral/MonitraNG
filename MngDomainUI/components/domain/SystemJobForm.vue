<template>
  <div>
    <form @submit.prevent="handleSubmit" class="space-y-6">
      <!-- Job ID -->
      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Job ID <span class="text-red-500">*</span>
        </label>
        <UInput
          v-model="form.jobId"
          placeholder="unique-job-id"
          :disabled="!!job || loading"
          required
          class="w-full"
        />
        <p class="mt-1 text-xs text-gray-500">
          Unique identifier for the job (alphanumeric, hyphens, underscores)
        </p>
      </div>

      <!-- Name -->
      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Name <span class="text-red-500">*</span>
        </label>
        <UInput
          v-model="form.name"
          placeholder="Job Name"
          required
          class="w-full"
        />
      </div>

      <!-- Description -->
      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Description
        </label>
        <UTextarea
          v-model="form.description"
          placeholder="Job description"
          :rows="2"
          class="w-full"
        />
      </div>

      <!-- Cron Expression -->
      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Cron Expression <span class="text-red-500">*</span>
        </label>
        <UInput
          v-model="form.cronExpression"
          placeholder="0 0 * * * ?"
          required
          class="w-full font-mono"
        />
        <p class="mt-1 text-xs text-gray-500">
          Quartz cron expression (e.g., "0/30 * * * * ?" for every 30 seconds)
        </p>
      </div>

      <!-- Endpoint URL -->
      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Endpoint URL <span class="text-red-500">*</span>
        </label>
        <UInput
          v-model="form.endpointUrl"
          type="url"
          placeholder="http://localhost:1880/endpoint"
          required
          class="w-full"
        />
      </div>

      <!-- HTTP Method -->
      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          HTTP Method <span class="text-red-500">*</span>
        </label>
        <USelect
          v-model="form.httpMethod"
          :options="httpMethods"
          required
          class="w-full"
        />
      </div>

      <!-- Payload (for POST) -->
      <div v-if="form.httpMethod === 'POST'">
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Payload (JSON)
        </label>
        <UTextarea
          v-model="form.payload"
          placeholder='{"key": "value"}'
          :rows="4"
          class="w-full font-mono text-sm"
        />
        <p class="mt-1 text-xs text-gray-500">
          JSON payload for POST requests (leave empty for default "{}")
        </p>
      </div>

      <!-- Timeout -->
      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Timeout (seconds) <span class="text-red-500">*</span>
        </label>
        <UInput
          v-model.number="form.timeoutSeconds"
          type="number"
          min="1"
          max="3600"
          required
          class="w-full"
        />
      </div>

      <!-- Start Date -->
      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Start Date (optional)
        </label>
        <UInput
          v-model="form.startDate"
          type="datetime-local"
          class="w-full"
        />
        <p class="mt-1 text-xs text-gray-500">
          Job will not execute before this date
        </p>
      </div>

      <!-- Expire Date -->
      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Expire Date (optional)
        </label>
        <UInput
          v-model="form.expireDate"
          type="datetime-local"
          class="w-full"
        />
        <p class="mt-1 text-xs text-gray-500">
          Job will not execute after this date
        </p>
      </div>

      <!-- Max Execution Count -->
      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Max Execution Count (optional)
        </label>
        <UInput
          v-model.number="form.maxExecutionCount"
          type="number"
          min="1"
          class="w-full"
        />
        <p class="mt-1 text-xs text-gray-500">
          Job will be deactivated after reaching this count (leave empty for unlimited)
        </p>
      </div>

      <!-- Is Active -->
      <div>
        <UToggle
          v-model="form.isActive"
          label="Active"
        />
        <p class="mt-1 text-xs text-gray-500">
          Job will only execute when active
        </p>
      </div>

      <!-- Error Message -->
      <UAlert
        v-if="error"
        color="red"
        variant="soft"
        :title="error"
      />

      <!-- Form Actions -->
      <div class="flex justify-end gap-2 pt-4">
        <UButton
          color="gray"
          variant="outline"
          @click="$emit('cancel')"
          :disabled="loading"
        >
          Cancel
        </UButton>
        <UButton
          type="submit"
          color="primary"
          :loading="loading"
        >
          {{ job ? 'Update' : 'Create' }} Job
        </UButton>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import type { ScheduledJob, CreateJobRequest, UpdateJobRequest } from '~/composables/useScheduler'
import { useScheduler } from '~/composables/useScheduler'

const props = defineProps<{
  job?: ScheduledJob | null
}>()

const emit = defineEmits<{
  success: []
  cancel: []
}>()

const { createSystemJob, updateSystemJob } = useScheduler()

const loading = ref(false)
const error = ref<string | null>(null)

const httpMethods = ['GET', 'POST']

const form = reactive<{
  jobId: string
  jobType: number
  name: string
  description: string
  cronExpression: string
  endpointUrl: string
  httpMethod: string
  payload: string
  isActive: boolean
  startDate: string | undefined
  expireDate: string | undefined
  maxExecutionCount: number | undefined
  timeoutSeconds: number
}>({
  jobId: '',
  jobType: 0, // System
  name: '',
  description: '',
  cronExpression: '',
  endpointUrl: '',
  httpMethod: 'POST',
  payload: '',
  isActive: true,
  startDate: undefined,
  expireDate: undefined,
  maxExecutionCount: undefined,
  timeoutSeconds: 300,
})

// Initialize form with job data if editing
watch(() => props.job, (job) => {
  if (job) {
    form.jobId = job.jobId
    form.name = job.name
    form.description = job.description || ''
    form.cronExpression = job.cronExpression
    form.endpointUrl = job.endpointUrl
    form.httpMethod = job.httpMethod
    form.payload = job.payload || ''
    form.isActive = job.isActive
    form.startDate = job.startDate ? new Date(job.startDate).toISOString().slice(0, 16) : undefined
    form.expireDate = job.expireDate ? new Date(job.expireDate).toISOString().slice(0, 16) : undefined
    form.maxExecutionCount = job.maxExecutionCount || undefined
    form.timeoutSeconds = job.timeoutSeconds
  } else {
    // Reset form
    form.jobId = ''
    form.name = ''
    form.description = ''
    form.cronExpression = ''
    form.endpointUrl = ''
    form.httpMethod = 'POST'
    form.payload = ''
    form.isActive = true
    form.startDate = undefined
    form.expireDate = undefined
    form.maxExecutionCount = undefined
    form.timeoutSeconds = 300
  }
}, { immediate: true })

const handleSubmit = async () => {
  loading.value = true
  error.value = null

  try {
    // Convert datetime-local to ISO string
    const startDate = form.startDate ? new Date(form.startDate).toISOString() : undefined
    const expireDate = form.expireDate ? new Date(form.expireDate).toISOString() : undefined

    const jobData: CreateJobRequest | UpdateJobRequest = {
      jobId: form.jobId,
      jobType: 0,
      name: form.name,
      description: form.description || undefined,
      cronExpression: form.cronExpression,
      endpointUrl: form.endpointUrl,
      httpMethod: form.httpMethod,
      payload: form.httpMethod === 'POST' ? (form.payload || '{}') : undefined,
      isActive: form.isActive,
      startDate: startDate || null,
      expireDate: expireDate || null,
      maxExecutionCount: form.maxExecutionCount ?? null,
      timeoutSeconds: form.timeoutSeconds,
    }

    if (props.job) {
      await updateSystemJob(props.job.jobId, jobData)
    } else {
      await createSystemJob(jobData)
    }

    emit('success')
  } catch (err: any) {
    error.value = err.message || 'Failed to save job'
    console.error('Failed to save job:', err)
  } finally {
    loading.value = false
  }
}
</script>
