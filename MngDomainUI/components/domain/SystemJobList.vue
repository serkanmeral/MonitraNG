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
      @close="error = null"
    />

    <!-- Jobs Table -->
    <UCard v-else>
      <template #header>
        <div class="flex justify-between items-center">
          <h3 class="text-lg font-semibold">System Jobs</h3>
          <div class="flex gap-2">
            <UButton
              color="gray"
              variant="outline"
              icon="i-heroicons-arrow-path"
              @click="fetchJobs"
              :loading="loading"
            >
              Refresh
            </UButton>
            <UButton
              color="primary"
              icon="i-heroicons-plus"
              @click="createJob"
            >
              Create Job
            </UButton>
          </div>
        </div>
      </template>

      <!-- Empty State -->
      <div v-if="jobs.length === 0" class="text-center py-12 text-gray-500">
        <UIcon name="i-heroicons-clock" class="w-16 h-16 mx-auto text-gray-400 mb-4" />
        <p class="text-lg font-semibold mb-2">No system jobs found</p>
        <p class="text-sm">Create your first scheduled job to get started</p>
      </div>

      <!-- Jobs Table -->
      <div v-else class="overflow-x-auto">
        <table class="w-full">
          <thead>
            <tr class="border-b border-gray-200">
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Job ID</th>
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Name</th>
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Cron Expression</th>
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Endpoint</th>
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Status</th>
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Executions</th>
              <th class="text-right py-3 px-4 font-semibold text-gray-700">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="job in jobs"
              :key="job.jobId"
              class="border-b border-gray-100 hover:bg-gray-50"
            >
              <td class="py-3 px-4">
                <code class="text-sm font-mono text-gray-700">{{ job.jobId }}</code>
              </td>
              <td class="py-3 px-4">
                <div class="font-medium text-gray-900">{{ job.name }}</div>
                <div v-if="job.description" class="text-sm text-gray-500 mt-1">
                  {{ job.description }}
                </div>
              </td>
              <td class="py-3 px-4">
                <code class="text-xs font-mono text-gray-600">{{ job.cronExpression }}</code>
              </td>
              <td class="py-3 px-4">
                <div class="text-sm">
                  <span class="font-medium">{{ job.httpMethod }}</span>
                  <span class="text-gray-500 ml-2">{{ job.endpointUrl }}</span>
                </div>
              </td>
              <td class="py-3 px-4">
                <UBadge
                  :color="job.isActive ? 'green' : 'gray'"
                  variant="soft"
                >
                  {{ job.isActive ? 'Active' : 'Inactive' }}
                </UBadge>
                <div v-if="job.maxExecutionCount" class="text-xs text-gray-500 mt-1">
                  {{ job.totalExecutionCount }}/{{ job.maxExecutionCount }}
                </div>
              </td>
              <td class="py-3 px-4">
                <div class="text-sm">
                  <div class="text-green-600">✓ {{ job.successfulExecutionCount }}</div>
                  <div class="text-red-600">✗ {{ job.failedExecutionCount }}</div>
                </div>
              </td>
              <td class="py-3 px-4">
                <div class="flex justify-end gap-2">
                  <UButton
                    color="gray"
                    variant="ghost"
                    size="sm"
                    icon="i-heroicons-eye"
                    @click="viewJob(job)"
                  />
                  <UButton
                    color="gray"
                    variant="ghost"
                    size="sm"
                    icon="i-heroicons-pencil"
                    @click="editJob(job)"
                  />
                  <UButton
                    color="red"
                    variant="ghost"
                    size="sm"
                    icon="i-heroicons-trash"
                    @click="confirmDelete(job)"
                  />
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>

    <!-- Create/Edit Job Modal -->
    <UModal v-model="showJobModal" :ui="{ width: 'max-w-4xl' }">
      <UCard>
        <template #header>
          <div class="flex justify-between items-center">
            <h3 class="text-lg font-semibold">
              {{ editingJob ? 'Edit System Job' : 'Create System Job' }}
            </h3>
            <UButton
              color="gray"
              variant="ghost"
              icon="i-heroicons-x-mark"
              @click="closeJobModal"
            />
          </div>
        </template>

        <DomainSystemJobForm
          v-if="showJobModal"
          :job="editingJob"
          @success="handleJobSuccess"
          @cancel="closeJobModal"
        />
      </UCard>
    </UModal>

    <!-- View Job Modal -->
    <UModal v-model="showViewModal" :ui="{ width: 'max-w-4xl' }">
      <UCard>
        <template #header>
          <div class="flex justify-between items-center">
            <h3 class="text-lg font-semibold">Job Details</h3>
            <UButton
              color="gray"
              variant="ghost"
              icon="i-heroicons-x-mark"
              @click="showViewModal = false"
            />
          </div>
        </template>

        <DomainSystemJobView
          v-if="viewingJob"
          :job="viewingJob"
          @edit="editJob(viewingJob)"
        />
      </UCard>
    </UModal>

    <!-- Delete Confirmation Modal -->
    <UModal v-model="showDeleteModal">
      <UCard>
        <template #header>
          <h3 class="text-lg font-semibold">Delete Job</h3>
        </template>
        <div class="space-y-4">
          <p>Are you sure you want to delete job <strong>{{ jobToDelete?.name }}</strong>?</p>
          <p class="text-sm text-gray-500">This action cannot be undone.</p>
          <div class="flex justify-end gap-2">
            <UButton
              color="gray"
              variant="outline"
              @click="showDeleteModal = false"
            >
              Cancel
            </UButton>
            <UButton
              color="red"
              :loading="deleting"
              @click="handleDelete"
            >
              Delete
            </UButton>
          </div>
        </div>
      </UCard>
    </UModal>
  </div>
</template>

<script setup lang="ts">
import type { ScheduledJob } from '~/composables/useScheduler'
import { useScheduler } from '~/composables/useScheduler'
import DomainSystemJobForm from '~/components/domain/SystemJobForm.vue'
import DomainSystemJobView from '~/components/domain/SystemJobView.vue'

const {
  getAllSystemJobs,
  deleteSystemJob,
} = useScheduler()

const jobs = ref<ScheduledJob[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
const showJobModal = ref(false)
const showViewModal = ref(false)
const showDeleteModal = ref(false)
const editingJob = ref<ScheduledJob | null>(null)
const viewingJob = ref<ScheduledJob | null>(null)
const jobToDelete = ref<ScheduledJob | null>(null)
const deleting = ref(false)

// Fetch jobs on mount
onMounted(() => {
  fetchJobs()
})

const fetchJobs = async () => {
  loading.value = true
  error.value = null
  try {
    jobs.value = await getAllSystemJobs()
  } catch (err: any) {
    error.value = err.message || 'Failed to fetch system jobs'
    console.error('Failed to fetch system jobs:', err)
  } finally {
    loading.value = false
  }
}

const viewJob = (job: ScheduledJob) => {
  viewingJob.value = job
  showViewModal.value = true
}

const editJob = (job: ScheduledJob) => {
  editingJob.value = job
  showJobModal.value = true
  showViewModal.value = false
}

const confirmDelete = (job: ScheduledJob) => {
  jobToDelete.value = job
  showDeleteModal.value = true
}

const handleDelete = async () => {
  if (!jobToDelete.value) return

  deleting.value = true
  try {
    await deleteSystemJob(jobToDelete.value.jobId)
    showDeleteModal.value = false
    jobToDelete.value = null
    await fetchJobs()
  } catch (err: any) {
    error.value = err.message || 'Failed to delete job'
    console.error('Failed to delete job:', err)
  } finally {
    deleting.value = false
  }
}

const handleJobSuccess = () => {
  showJobModal.value = false
  editingJob.value = null
  fetchJobs()
}

const createJob = () => {
  editingJob.value = null
  showJobModal.value = true
}

const closeJobModal = () => {
  showJobModal.value = false
  editingJob.value = null
}
</script>
