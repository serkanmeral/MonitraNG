// System Jobs management composable

export interface ScheduledJob {
  id?: string
  jobId: string
  jobType: number // 0 = System, 1 = User
  name: string
  description?: string
  cronExpression: string
  endpointUrl: string
  httpMethod: string
  headers?: Record<string, string>
  payload?: string
  isActive: boolean
  startDate?: string | null
  expireDate?: string | null
  maxExecutionCount?: number | null
  totalExecutionCount: number
  successfulExecutionCount: number
  failedExecutionCount: number
  timeoutSeconds: number
  createdAt: string
  updatedAt?: string | null
  createdBy?: string | null
  domainId?: string | null
  lastExecution?: JobExecution | null
}

export interface JobExecution {
  id?: string
  executionId: string
  jobId: string
  status: string
  executedAt: string
  responseTimeMs?: number | null
  responseCode?: number | null
  responseBody?: string | null
  errorMessage?: string | null
  retryCount: number
  domainId?: string | null
}

export interface CreateJobRequest {
  jobId: string
  jobType: number
  name: string
  description?: string
  cronExpression: string
  endpointUrl: string
  httpMethod: string
  headers?: Record<string, string>
  payload?: string
  isActive: boolean
  startDate?: string | null
  expireDate?: string | null
  maxExecutionCount?: number | null
  timeoutSeconds: number
}

export interface UpdateJobRequest extends CreateJobRequest {}

export const useScheduler = () => {
  // Use server-side proxy for API calls
  // The proxy expects: /api/scheduler/v1/system/jobs
  // It will then call: http://localhost:5090/api/v1/system/jobs

  // Get all system jobs
  const getAllSystemJobs = async (): Promise<ScheduledJob[]> => {
    try {
      const response = await $fetch<ScheduledJob[]>(`/api/scheduler/v1/system/jobs`, {
        method: 'GET',
      })
      return response
    } catch (error: any) {
      const errorMessage = error.message || error.data?.message || 'Failed to fetch system jobs'
      throw new Error(errorMessage)
    }
  }

  // Get active system jobs
  const getActiveSystemJobs = async (): Promise<ScheduledJob[]> => {
    try {
      const response = await $fetch<ScheduledJob[]>(`/api/scheduler/v1/system/jobs/active`, {
        method: 'GET',
      })
      return response
    } catch (error: any) {
      const errorMessage = error.message || error.data?.message || 'Failed to fetch active system jobs'
      throw new Error(errorMessage)
    }
  }

  // Get system job by ID
  const getSystemJobById = async (jobId: string): Promise<ScheduledJob> => {
    try {
      const response = await $fetch<ScheduledJob>(`/api/scheduler/v1/system/jobs/${jobId}`, {
        method: 'GET',
      })
      return response
    } catch (error: any) {
      const errorMessage = error.message || error.data?.message || 'Failed to fetch system job'
      throw new Error(errorMessage)
    }
  }

  // Create system job
  const createSystemJob = async (job: CreateJobRequest): Promise<ScheduledJob> => {
    try {
      const response = await $fetch<ScheduledJob>(`/api/scheduler/v1/system/jobs`, {
        method: 'POST',
        body: job,
      })
      return response
    } catch (error: any) {
      const errorMessage = error.message || error.data?.message || 'Failed to create system job'
      throw new Error(errorMessage)
    }
  }

  // Update system job
  const updateSystemJob = async (jobId: string, job: UpdateJobRequest): Promise<ScheduledJob> => {
    try {
      const response = await $fetch<ScheduledJob>(`/api/scheduler/v1/system/jobs/${jobId}`, {
        method: 'PUT',
        body: job,
      })
      return response
    } catch (error: any) {
      const errorMessage = error.message || error.data?.message || 'Failed to update system job'
      throw new Error(errorMessage)
    }
  }

  // Delete system job
  const deleteSystemJob = async (jobId: string): Promise<void> => {
    try {
      await $fetch(`/api/scheduler/v1/system/jobs/${jobId}`, {
        method: 'DELETE',
      })
    } catch (error: any) {
      const errorMessage = error.message || error.data?.message || 'Failed to delete system job'
      throw new Error(errorMessage)
    }
  }

  // Get job executions
  const getJobExecutions = async (jobId: string, limit: number = 100): Promise<JobExecution[]> => {
    try {
      const response = await $fetch<JobExecution[]>(`/api/scheduler/v1/system/jobs/${jobId}/executions?limit=${limit}`, {
        method: 'GET',
      })
      return response
    } catch (error: any) {
      const errorMessage = error.message || error.data?.message || 'Failed to fetch job executions'
      throw new Error(errorMessage)
    }
  }

  return {
    getAllSystemJobs,
    getActiveSystemJobs,
    getSystemJobById,
    createSystemJob,
    updateSystemJob,
    deleteSystemJob,
    getJobExecutions,
  }
}
