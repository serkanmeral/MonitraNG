export interface SchedulerJobExecution {
  executionId: string;
  jobId: string;
  status: string;
  executedAt: string;
  responseTimeMs?: number | null;
  responseCode?: number | null;
  responseBody?: string | null;
  errorMessage?: string | null;
  retryCount: number;
  domainId?: string | null;
}

export interface SchedulerJob {
  id?: string;
  jobId: string;
  jobType: number;
  name: string;
  description?: string | null;
  cronExpression: string;
  endpointUrl: string;
  httpMethod: string;
  headers?: Record<string, string> | null;
  payload?: string | null;
  isActive: boolean;
  startDate?: string | null;
  expireDate?: string | null;
  maxExecutionCount?: number | null;
  totalExecutionCount: number;
  successfulExecutionCount: number;
  failedExecutionCount: number;
  timeoutSeconds: number;
  createdAt?: string;
  updatedAt?: string | null;
  createdBy?: string | null;
  domainId?: string | null;
  lastExecution?: SchedulerJobExecution | null;
}

export type OcAdminScheduledJobRunKind = 'oc-execute' | 'http-post' | 'none';

export type OcSchedulerExecutionTone = 'success' | 'warning' | 'error' | 'default';

export interface OcSchedulerExecutionRow {
  executionId: string;
  executedAt: string;
  schedulerStatus: string;
  displayStatus: string;
  statusTone: OcSchedulerExecutionTone;
  responseCode: number | null;
  responseTimeMs: number | null;
  summary: string | null;
  errors: string[];
  responseBody: string | null;
  /** Schedule/row özeti — DG execution geçmişi yokken */
  isFallback?: boolean;
}

export interface OcAdminScheduledJobRow {
  key: string;
  scope: 'system' | 'domain';
  sourceLabel: string;
  jobId: string;
  name: string;
  description?: string | null;
  cronExpression: string;
  isActive: boolean;
  endpointUrl: string;
  httpMethod: string;
  lastStatus?: string | null;
  lastRunAt?: string | null;
  lastError?: string | null;
  ocScheduleId?: string | null;
  canRunManually: boolean;
  runKind: OcAdminScheduledJobRunKind;
}
