import { getAccessToken } from '@/services/apiService';
import { ocListAllWorkItemSchedules, ocRunWorkItemScheduleNow } from '@/services/operationCoreService';
import type { OpWorkItemSchedule } from '@/types/apps/operationCore';
import type { SchedulerJob, SchedulerJobExecution } from '@/types/apps/scheduler';

/** Admin execution history dialog — scheduler API `limit` query param. */
export const SCHEDULER_EXECUTION_HISTORY_LIMIT = 20;
import { useAuthStore } from '@/stores/auth';

function normalizeExecution(raw: unknown): SchedulerJobExecution | null {
  if (!raw || typeof raw !== 'object') return null;
  const r = raw as Record<string, unknown>;
  const executedAt = r.executedAt ?? r.ExecutedAt;
  if (!executedAt) return null;
  return {
    executionId: String(r.executionId ?? r.ExecutionId ?? ''),
    jobId: String(r.jobId ?? r.JobId ?? ''),
    status: String(r.status ?? r.Status ?? ''),
    executedAt: String(executedAt),
    responseTimeMs: (r.responseTimeMs ?? r.ResponseTimeMs ?? null) as number | null,
    responseCode: (r.responseCode ?? r.ResponseCode ?? null) as number | null,
    responseBody: (r.responseBody ?? r.ResponseBody ?? null) as string | null,
    errorMessage: (r.errorMessage ?? r.ErrorMessage ?? null) as string | null,
    retryCount: Number(r.retryCount ?? r.RetryCount ?? 0),
    domainId: (r.domainId ?? r.DomainId ?? null) as string | null,
  };
}

function normalizeSchedulerJob(raw: unknown): SchedulerJob {
  const r = (raw && typeof raw === 'object' ? raw : {}) as Record<string, unknown>;
  const lastExecution = normalizeExecution(r.lastExecution ?? r.LastExecution);
  return {
    ...(raw as SchedulerJob),
    jobId: String(r.jobId ?? r.JobId ?? ''),
    name: String(r.name ?? r.Name ?? r.jobId ?? ''),
    cronExpression: String(r.cronExpression ?? r.CronExpression ?? ''),
    endpointUrl: String(r.endpointUrl ?? r.EndpointUrl ?? ''),
    httpMethod: String(r.httpMethod ?? r.HttpMethod ?? 'POST'),
    isActive: Boolean(r.isActive ?? r.IsActive ?? false),
    totalExecutionCount: Number(r.totalExecutionCount ?? r.TotalExecutionCount ?? 0),
    successfulExecutionCount: Number(r.successfulExecutionCount ?? r.SuccessfulExecutionCount ?? 0),
    failedExecutionCount: Number(r.failedExecutionCount ?? r.FailedExecutionCount ?? 0),
    jobType: Number(r.jobType ?? r.JobType ?? 0),
    timeoutSeconds: Number(r.timeoutSeconds ?? r.TimeoutSeconds ?? 0),
    description: (r.description ?? r.Description ?? null) as string | null,
    lastExecution,
  };
}

function normalizeSchedulerJobs(raw: unknown): SchedulerJob[] {
  if (!Array.isArray(raw)) return [];
  return raw.map(normalizeSchedulerJob);
}

async function enrichJobLastExecution(
  job: SchedulerJob,
  scope: 'system' | 'domain'
): Promise<SchedulerJob> {
  if (job.lastExecution?.executedAt) return job;
  try {
    const execs =
      scope === 'system'
        ? await schedulerGetSystemJobExecutions(job.jobId, 1)
        : await schedulerGetUserJobExecutions(job.jobId, 1);
    const latest = execs[0] ? normalizeExecution(execs[0]) : null;
    if (latest?.executedAt) {
      return { ...job, lastExecution: latest };
    }
  } catch {
    // execution history optional
  }
  return job;
}

async function enrichJobsLastExecution(
  jobs: SchedulerJob[],
  scope: 'system' | 'domain'
): Promise<SchedulerJob[]> {
  return Promise.all(jobs.map((job) => enrichJobLastExecution(job, scope)));
}

async function schedulerFetch<T>(
  path: string,
  method: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE' = 'GET',
  body?: unknown
): Promise<T> {
  const authStore = useAuthStore();
  try {
    await authStore.ensureValidToken();
  } catch {
    // Scheduler system jobs may work without token; user jobs need it.
  }

  const token = getAccessToken();
  const clean = path.replace(/^\/+/, '');
  return $fetch<T>(`/api/scheduler/${clean}`, {
    method,
    ...(body != null && { body }),
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  });
}

export async function schedulerListSystemJobs(): Promise<SchedulerJob[]> {
  const raw = await schedulerFetch<unknown>('v1/system/jobs', 'GET');
  return normalizeSchedulerJobs(raw);
}

export async function schedulerListUserJobs(): Promise<SchedulerJob[]> {
  const raw = await schedulerFetch<unknown>('v1/user/jobs', 'GET');
  return normalizeSchedulerJobs(raw);
}

export async function schedulerGetSystemJobExecutions(
  jobId: string,
  limit = SCHEDULER_EXECUTION_HISTORY_LIMIT
): Promise<SchedulerJobExecution[]> {
  const raw = await schedulerFetch<unknown>(
    `v1/system/jobs/${encodeURIComponent(jobId)}/executions?limit=${limit}`,
    'GET'
  );
  if (!Array.isArray(raw)) return [];
  return raw.map((e) => normalizeExecution(e)).filter((e): e is SchedulerJobExecution => e != null);
}

export async function schedulerGetUserJobExecutions(
  jobId: string,
  limit = SCHEDULER_EXECUTION_HISTORY_LIMIT
): Promise<SchedulerJobExecution[]> {
  const raw = await schedulerFetch<unknown>(
    `v1/user/jobs/${encodeURIComponent(jobId)}/executions?limit=${limit}`,
    'GET'
  );
  if (!Array.isArray(raw)) return [];
  return raw.map((e) => normalizeExecution(e)).filter((e): e is SchedulerJobExecution => e != null);
}

/** System HTTP job — POST hedef endpoint (MngAdmin backup vb.) */
export async function schedulerRunHttpPostJob(endpointUrl: string, payload = '{}'): Promise<unknown> {
  const authStore = useAuthStore();
  await authStore.ensureValidToken();
  const token = getAccessToken();
  if (!token) throw new Error('Access token bulunamadı');

  let body: unknown = {};
  if (payload.trim()) {
    try {
      body = JSON.parse(payload);
    } catch {
      body = payload;
    }
  }

  // MngAdmin yedekleme — mevcut admin proxy
  const adminMatch = endpointUrl.match(/^(https?:\/\/[^/]+)\/api\/v1\/(.+)$/i);
  if (adminMatch) {
    const adminPath = adminMatch[2];
    return $fetch(`/api/admin/${adminPath}`, {
      method: 'POST',
      body,
      headers: { Authorization: `Bearer ${token}` },
    });
  }

  // Diğer internal HTTP uçları — doğrudan (geliştirme / test)
  return $fetch(endpointUrl, {
    method: 'POST',
    body,
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
  });
}

export async function schedulerRunOcSchedule(scheduleId: string): Promise<unknown> {
  return ocRunWorkItemScheduleNow(scheduleId);
}

export async function schedulerLoadAdminJobExplorerRows(): Promise<{
  systemJobs: SchedulerJob[];
  userJobs: SchedulerJob[];
  scheduleById: Map<string, OpWorkItemSchedule>;
}> {
  const [systemRaw, userRaw, schedules] = await Promise.all([
    schedulerListSystemJobs(),
    schedulerListUserJobs().catch(() => [] as SchedulerJob[]),
    ocListAllWorkItemSchedules().catch(() => [] as OpWorkItemSchedule[]),
  ]);
  const [systemJobs, userJobs] = await Promise.all([
    enrichJobsLastExecution(systemRaw, 'system'),
    enrichJobsLastExecution(userRaw, 'domain'),
  ]);
  const scheduleById = new Map(schedules.map((s) => [s.__dataId, s]));
  return { systemJobs, userJobs, scheduleById };
}
