import type {
  OcAdminScheduledJobRow,
  OcSchedulerExecutionRow,
  OcSchedulerExecutionTone,
  SchedulerJob,
  SchedulerJobExecution,
} from '@/types/apps/scheduler';
import type { OpWorkItemSchedule } from '@/types/apps/operationCore';

export const OC_SCHEDULE_JOB_PREFIX = 'oc-schedule-';

export function parseOcScheduleId(jobId: string): string | null {
  if (!jobId.startsWith(OC_SCHEDULE_JOB_PREFIX)) return null;
  const id = jobId.slice(OC_SCHEDULE_JOB_PREFIX.length).trim();
  return id || null;
}

function isHttpEndpoint(url: string): boolean {
  return /^https?:\/\//i.test(url);
}

function isOrchestrationEndpoint(url: string): boolean {
  return url.startsWith('orchestration://');
}

export function mapSystemJobToAdminRow(job: SchedulerJob): OcAdminScheduledJobRow {
  const endpointUrl = job.endpointUrl ?? '';
  let runKind: OcAdminScheduledJobRow['runKind'] = 'none';
  let canRunManually = false;

  if (isHttpEndpoint(endpointUrl) && job.httpMethod?.toUpperCase() === 'POST') {
    runKind = 'http-post';
    canRunManually = true;
  }

  const lastStatus = resolveJobLastStatus(job);
  const ex = job.lastExecution;

  return {
    key: `system:${job.jobId}`,
    scope: 'system',
    sourceLabel: job.name || job.jobId,
    jobId: job.jobId,
    name: job.name,
    description: job.description,
    cronExpression: job.cronExpression,
    isActive: job.isActive,
    endpointUrl,
    httpMethod: job.httpMethod || 'POST',
    lastStatus,
    lastRunAt: ex?.executedAt ?? null,
    lastError: ex?.errorMessage ?? extractBackupFailureMessages(ex?.responseBody),
    canRunManually,
    runKind,
  };
}

function resolveJobLastStatus(job: SchedulerJob): string | null {
  const ex = job.lastExecution;
  if (!ex) return null;
  const backupSummary = summarizeBackupRunResponse(ex.responseBody);
  if (backupSummary) return backupSummary;
  return ex.status || null;
}

export function mapUserJobToAdminRow(
  job: SchedulerJob,
  scheduleById?: Map<string, OpWorkItemSchedule>
): OcAdminScheduledJobRow {
  const endpointUrl = job.endpointUrl ?? '';
  const ocScheduleId = parseOcScheduleId(job.jobId);
  let runKind: OcAdminScheduledJobRow['runKind'] = 'none';
  let canRunManually = false;
  let sourceLabel = job.name || job.jobId;

  if (ocScheduleId) {
    runKind = 'oc-execute';
    canRunManually = true;
    sourceLabel = `OC · ${job.name || ocScheduleId}`;
  } else if (isHttpEndpoint(endpointUrl) && job.httpMethod?.toUpperCase() === 'POST') {
    runKind = 'http-post';
    canRunManually = true;
    sourceLabel = `Domain · ${job.name || job.jobId}`;
  } else if (isOrchestrationEndpoint(endpointUrl)) {
    sourceLabel = `Domain · ${job.name || job.jobId}`;
  }

  const ex = job.lastExecution;
  let lastRunAt = ex?.executedAt ?? null;
  let lastStatus = resolveJobLastStatus(job);
  let lastError = ex?.errorMessage ?? extractBackupFailureMessages(ex?.responseBody) ?? null;

  // OC schedule: MO `lastRunAt` on op_work_item_schedules is authoritative when scheduler job doc is stale.
  let dgIsActive: boolean | null | undefined;
  if (ocScheduleId && scheduleById?.has(ocScheduleId)) {
    const schedule = scheduleById.get(ocScheduleId)!;
    dgIsActive = schedule.isActive;
    if (schedule.lastRunAt) {
      lastRunAt = schedule.lastRunAt;
      if (!lastStatus) lastStatus = 'success';
    }
  }

  const schedulerDgMismatch =
    dgIsActive != null && dgIsActive !== job.isActive;

  return {
    key: `domain:${job.jobId}`,
    scope: 'domain',
    sourceLabel,
    jobId: job.jobId,
    name: job.name,
    description: job.description,
    cronExpression: job.cronExpression,
    isActive: job.isActive,
    endpointUrl,
    httpMethod: job.httpMethod || 'POST',
    lastStatus,
    lastRunAt,
    lastError,
    ocScheduleId,
    dgIsActive,
    schedulerDgMismatch,
    canRunManually,
    runKind,
  };
}

export function mergeAdminScheduledJobRows(
  systemJobs: SchedulerJob[],
  userJobs: SchedulerJob[],
  scheduleById?: Map<string, OpWorkItemSchedule>
): OcAdminScheduledJobRow[] {
  const rows = [
    ...systemJobs.map(mapSystemJobToAdminRow),
    ...userJobs.map((job) => mapUserJobToAdminRow(job, scheduleById)),
  ];
  return rows.sort((a, b) => {
    if (a.scope !== b.scope) return a.scope === 'system' ? -1 : 1;
    return a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });
  });
}

export function summarizeBackupRunResponse(body: string | null | undefined): string | null {
  if (!body) return null;
  try {
    const parsed = JSON.parse(body) as {
      status?: string;
      successfulBackups?: number;
      totalBackups?: number;
      failedBackups?: number;
    };
    if (parsed.status) {
      const counts =
        parsed.totalBackups != null
          ? ` (${parsed.successfulBackups ?? 0}/${parsed.totalBackups})`
          : '';
      return `${parsed.status}${counts}`;
    }
  } catch {
    // ignore
  }
  return null;
}

/** Full backup JSON gövdesindeki failed bileşenlerin errorMessage listesi. */
export function extractBackupFailureList(body: string | null | undefined): string[] {
  if (!body) return [];
  try {
    const parsed = JSON.parse(body) as {
      systemBackups?: Array<{
        databaseName?: string;
        status?: string;
        errorMessage?: string | null;
      }>;
      domainBackups?: Array<{
        domainName?: string;
        databaseName?: string;
        status?: string;
        errorMessage?: string | null;
      }>;
    };
    const failedLines: string[] = [];
    for (const row of parsed.systemBackups ?? []) {
      if (row.status === 'failed') {
        failedLines.push(`${row.databaseName ?? 'system'}: ${row.errorMessage ?? 'failed'}`);
      }
    }
    for (const row of parsed.domainBackups ?? []) {
      if (row.status === 'failed') {
        const label = row.domainName ?? row.databaseName ?? 'domain';
        failedLines.push(`${label}: ${row.errorMessage ?? 'failed'}`);
      }
    }
    return failedLines;
  } catch {
    return [];
  }
}

/** Full backup JSON gövdesindeki failed bileşenlerin errorMessage özetleri. */
export function extractBackupFailureMessages(body: string | null | undefined): string | null {
  const failedLines = extractBackupFailureList(body);
  return failedLines.length > 0 ? failedLines.join(' · ') : null;
}

export function resolveExecutionDisplayStatus(ex: {
  status?: string | null;
  responseBody?: string | null;
  errorMessage?: string | null;
}): string {
  const backupSummary = summarizeBackupRunResponse(ex.responseBody);
  if (backupSummary) return backupSummary;
  if (ex.errorMessage) return 'failed';
  return ex.status || 'unknown';
}

export function executionStatusTone(displayStatus: string, schedulerStatus?: string | null): OcSchedulerExecutionTone {
  if (displayStatus.includes('completed_with_errors')) return 'warning';
  if (displayStatus === 'failed' || displayStatus === 'timeout' || schedulerStatus === 'failed') return 'error';
  if (displayStatus === 'success' || displayStatus === 'completed') return 'success';
  if (schedulerStatus === 'timeout') return 'error';
  return 'default';
}

export function mapExecutionToAdminRow(ex: SchedulerJobExecution): OcSchedulerExecutionRow {
  const backupErrors = extractBackupFailureList(ex.responseBody);
  const displayStatus = resolveExecutionDisplayStatus(ex);
  const summary =
    backupErrors.length === 0 && !ex.errorMessage
      ? summarizeBackupRunResponse(ex.responseBody)
      : null;
  const errors = ex.errorMessage ? [ex.errorMessage, ...backupErrors] : backupErrors;

  return {
    executionId: ex.executionId,
    executedAt: ex.executedAt,
    schedulerStatus: ex.status,
    displayStatus,
    statusTone: executionStatusTone(displayStatus, ex.status),
    responseCode: ex.responseCode ?? null,
    responseTimeMs: ex.responseTimeMs ?? null,
    summary,
    errors,
    responseBody: ex.responseBody ?? null,
  };
}

/** OC schedule veya API hatasında tek satırlık son çalışma özeti. */
export function buildAdminJobFallbackExecutionRows(job: OcAdminScheduledJobRow): OcSchedulerExecutionRow[] {
  if (!job.lastRunAt) return [];

  const displayStatus = job.lastStatus ?? 'success';
  return [
    {
      executionId: `fallback-${job.key}`,
      executedAt: job.lastRunAt,
      schedulerStatus: displayStatus,
      displayStatus,
      statusTone: executionStatusTone(displayStatus),
      responseCode: null,
      responseTimeMs: null,
      summary: null,
      errors: job.lastError ? [job.lastError] : [],
      responseBody: null,
      isFallback: true,
    },
  ];
}
