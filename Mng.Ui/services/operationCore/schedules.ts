import { fetchFromOperations } from '@/services/apiService';
import {
  OC_DATASETS,
  ocCreateRecordId,
  ocDelete,
  ocListDataset,
  ocUpdate,
  resolveRelationId,
} from '@/services/operationCoreService';
import type { OpWorkItemSchedule } from '@/types/apps/operationCore';

export function mapOpWorkItemSchedule(raw: Record<string, unknown>): OpWorkItemSchedule {
  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    workspaceId: resolveRelationId(raw.workspaceId ?? raw.WorkspaceId) ?? '',
    name: String(raw.name ?? ''),
    description:
      raw.description != null
        ? String(raw.description)
        : raw.Description != null
          ? String(raw.Description)
          : null,
    isActive: raw.isActive !== false && raw.IsActive !== false,
    cronExpression: String(raw.cronExpression ?? raw.CronExpression ?? ''),
    timezone: String(raw.timezone ?? raw.Timezone ?? 'Europe/Istanbul'),
    boardId: resolveRelationId(raw.boardId ?? raw.BoardId) ?? '',
    typeId: resolveRelationId(raw.typeId ?? raw.TypeId) ?? '',
    assignee: String(raw.assignee ?? raw.Assignee ?? ''),
    priorityId: resolveRelationId(raw.priorityId ?? raw.PriorityId) || null,
    title: String(raw.title ?? raw.Title ?? ''),
    templateDescription:
      raw.templateDescription != null
        ? String(raw.templateDescription)
        : raw.TemplateDescription != null
          ? String(raw.TemplateDescription)
          : null,
    fields:
      raw.fields && typeof raw.fields === 'object' && !Array.isArray(raw.fields)
        ? (raw.fields as Record<string, unknown>)
        : raw.Fields && typeof raw.Fields === 'object' && !Array.isArray(raw.Fields)
          ? (raw.Fields as Record<string, unknown>)
          : null,
    initialTransitionKey:
      raw.initialTransitionKey != null
        ? String(raw.initialTransitionKey)
        : raw.InitialTransitionKey != null
          ? String(raw.InitialTransitionKey)
          : null,
    schedulerJobId:
      raw.schedulerJobId != null
        ? String(raw.schedulerJobId)
        : raw.SchedulerJobId != null
          ? String(raw.SchedulerJobId)
          : null,
    lastRunAt:
      raw.lastRunAt != null
        ? String(raw.lastRunAt)
        : raw.LastRunAt != null
          ? String(raw.LastRunAt)
          : null,
    lastWorkItemId: resolveRelationId(raw.lastWorkItemId ?? raw.LastWorkItemId) || null,
  };
}

export async function ocListSchedulesForWorkspace(
  workspaceId: string
): Promise<OpWorkItemSchedule[]> {
  const rows = await ocListDataset(OC_DATASETS.workItemSchedules, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'name:asc',
    limit: 200,
  });
  return rows
    .map((r) => mapOpWorkItemSchedule(r as Record<string, unknown>))
    .filter((s) => s.__dataId && s.name && s.workspaceId === workspaceId);
}

/** Admin job explorer — tüm workspace schedule kayıtları (lastRunAt birleştirmesi). */
export async function ocListAllWorkItemSchedules(limit = 500): Promise<OpWorkItemSchedule[]> {
  const rows = await ocListDataset(OC_DATASETS.workItemSchedules, {
    sort: 'lastRunAt:desc',
    limit,
  });
  return rows
    .map((r) => mapOpWorkItemSchedule(r as Record<string, unknown>))
    .filter((s) => s.__dataId && s.name);
}

export async function ocCreateWorkItemSchedule(
  payload: Record<string, unknown>
): Promise<string | null> {
  return ocCreateRecordId(OC_DATASETS.workItemSchedules, payload);
}

export async function ocUpdateWorkItemSchedule(
  scheduleId: string,
  payload: Record<string, unknown>
) {
  await ocUpdate(OC_DATASETS.workItemSchedules, scheduleId, payload);
}

export async function ocDeleteWorkItemSchedule(scheduleId: string) {
  await ocDelete(OC_DATASETS.workItemSchedules, scheduleId);
}

/** SW-3b: DG kaydı sonrası MngScheduler User Job senkronu. */
export async function ocSyncWorkItemScheduleScheduler(scheduleId: string) {
  return fetchFromOperations(
    `/api/v1/work-item-schedules/${encodeURIComponent(scheduleId)}/sync-scheduler`,
    'POST'
  );
}

/** SW-3b: DG silmeden önce Scheduler job kaldırma. */
export async function ocUnlinkWorkItemScheduleScheduler(scheduleId: string) {
  return fetchFromOperations(
    `/api/v1/work-item-schedules/${encodeURIComponent(scheduleId)}/unlink-scheduler`,
    'POST'
  );
}

/** SW-2: MO execute endpoint — henüz yoksa hata fırlatır. */
export async function ocRunWorkItemScheduleNow(scheduleId: string) {
  return fetchFromOperations(
    `/api/v1/work-item-schedules/${encodeURIComponent(scheduleId)}/execute`,
    'POST'
  );
}
