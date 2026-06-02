<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useOcPersonPicker } from '@/composables/useOcPersonPicker';
import { useOcWorkspaceCatalogInject } from '@/composables/useOcWorkspaceCatalog';
import { useUserStore } from '@/stores/apps/user';
import OcWorkspaceScheduleDialog, {
  type OcScheduleFormModel,
} from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceScheduleDialog.vue';
import {
  ocCreateWorkItemSchedule,
  ocDeleteWorkItemSchedule,
  ocExtractDgErrorMessage,
  ocListSchedulesForWorkspace,
  ocRunWorkItemScheduleNow,
  ocSyncWorkItemScheduleScheduler,
  ocUnlinkWorkItemScheduleScheduler,
  ocUpdateWorkItemSchedule,
} from '@/services/operationCoreService';
import type { OpBoard, OpPriority, OpWorkItemSchedule, OpWorkItemType } from '@/types/apps/operationCore';
import {
  buildMultiWeeklyQuartzCron,
  formatScheduleHumanSummary,
  formatScheduleLastRun,
  OC_SCHEDULE_WEEKDAY_KEYS,
  type OcScheduleWeekdayKey,
} from '@/utils/ocScheduleCron';
import { mapUserToOcPersonPickerItem } from '@/utils/ocPersonPicker';

const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();
const personPicker = useOcPersonPicker();
const catalog = useOcWorkspaceCatalogInject();
const userStore = useUserStore();

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const runningId = ref<string | null>(null);
const errorLocal = ref<string | null>(null);
const infoLocal = ref<string | null>(null);

const schedules = ref<OpWorkItemSchedule[]>([]);
const boards = ref<OpBoard[]>([]);
const types = ref<OpWorkItemType[]>([]);
const priorities = ref<OpPriority[]>([]);

const dialog = ref(false);
const editId = ref<string | null>(null);
const scheduleDialogRef = ref<InstanceType<typeof OcWorkspaceScheduleDialog> | null>(null);
const deleteDialog = ref(false);
const deleteTarget = ref<OpWorkItemSchedule | null>(null);

const weekdayLabels = computed(() =>
  Object.fromEntries(
    OC_SCHEDULE_WEEKDAY_KEYS.map((key) => [
      key,
      t(`operationCore.workspaceDefinitions.scheduled.weekday.${key}`),
    ])
  ) as Record<OcScheduleWeekdayKey, string>
);

const howItWorksSteps = computed(() => [
  {
    icon: 'mdi-clock-outline',
    color: 'primary',
    title: t('operationCore.workspaceDefinitions.scheduled.howStep1Title'),
    body: t('operationCore.workspaceDefinitions.scheduled.howStep1Body'),
  },
  {
    icon: 'mdi-view-dashboard-outline',
    color: 'secondary',
    title: t('operationCore.workspaceDefinitions.scheduled.howStep2Title'),
    body: t('operationCore.workspaceDefinitions.scheduled.howStep2Body'),
  },
  {
    icon: 'mdi-auto-fix',
    color: 'success',
    title: t('operationCore.workspaceDefinitions.scheduled.howStep3Title'),
    body: t('operationCore.workspaceDefinitions.scheduled.howStep3Body'),
  },
]);

const boardItems = computed(() =>
  boards.value.map((b) => ({ value: b.__dataId, title: b.name }))
);
const typeItems = computed(() =>
  types.value.map((ty) => ({ value: ty.__dataId, title: ty.name }))
);
const priorityItems = computed(() => [
  { value: '', title: t('operationCore.workspaceDefinitions.scheduled.priorityNone') },
  ...priorities.value.map((p) => ({ value: p.__dataId, title: p.name })),
]);

const boardNameById = computed(() => new Map(boards.value.map((b) => [b.__dataId, b.name])));
const typeNameById = computed(() => new Map(types.value.map((ty) => [ty.__dataId, ty.name])));

const activeCount = computed(() => schedules.value.filter((s) => s.isActive).length);

const tableHeaders = computed(() => [
  { title: t('operationCore.workspaceDefinitions.scheduled.colName'), key: 'name', sortable: true },
  { title: t('operationCore.workspaceDefinitions.scheduled.colSchedule'), key: 'schedule', sortable: false },
  { title: t('operationCore.workspaceDefinitions.scheduled.colTarget'), key: 'target', sortable: false },
  { title: t('operationCore.workspaceDefinitions.scheduled.colAssignee'), key: 'assignee', sortable: false },
  { title: t('operationCore.workspaceDefinitions.scheduled.colStatus'), key: 'isActive', sortable: true },
  { title: t('operationCore.workspaceDefinitions.scheduled.colLastRun'), key: 'lastRunAt', sortable: true },
  { title: t('operationCore.workspaceDefinitions.scheduled.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

function scheduleSummary(row: OpWorkItemSchedule): string {
  return formatScheduleHumanSummary(
    row.cronExpression,
    row.timezone || 'Europe/Istanbul',
    weekdayLabels.value,
    (key, params) => t(key, params ?? {})
  );
}

function assigneeLabel(userId: string): string {
  const fromPicker = personPicker.items.value.find((i) => i.value === userId);
  if (fromPicker?.title) return fromPicker.title;
  const user = userStore.getUserById(userId);
  const mapped = user ? mapUserToOcPersonPickerItem(user) : null;
  if (mapped?.title) return mapped.title;
  return userId;
}

function workItemProfileHref(id: string): string {
  return `/apps/operation-core/work-items/${encodeURIComponent(id)}/profile?workspaceId=${encodeURIComponent(props.workspaceId)}`;
}

function formatLastRun(iso: string | null | undefined): string {
  if (!iso?.trim()) return t('operationCore.workspaceDefinitions.scheduled.neverRun');
  return formatScheduleLastRun(iso, 'tr-TR');
}

async function loadAll() {
  if (!props.workspaceId) {
    schedules.value = [];
    return;
  }
  loading.value = true;
  errorLocal.value = null;
  try {
    const [scheduleRows] = await Promise.all([
      ocListSchedulesForWorkspace(props.workspaceId),
      catalog.whenReady(),
    ]);
    schedules.value = scheduleRows;
    boards.value = catalog.boards.value;
    types.value = catalog.types.value;
    priorities.value = catalog.priorities.value;

    const assigneeIds = [...new Set(scheduleRows.map((s) => s.assignee).filter(Boolean))];
    if (assigneeIds.length) await personPicker.ensureSelectedIds(assigneeIds);
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.scheduled.loadError')
    );
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.workspaceId,
  () => {
    void loadAll();
  },
  { immediate: true }
);

function defaultFormModel(): OcScheduleFormModel {
  return {
    name: '',
    description: '',
    isActive: true,
    cronExpression: buildMultiWeeklyQuartzCron(['mon'], 9, 0),
    timezone: 'Europe/Istanbul',
    boardId: boards.value.length === 1 ? boards.value[0]!.__dataId : '',
    typeId: types.value.length === 1 ? types.value[0]!.__dataId : '',
    assignee: null,
    priorityId: '',
    title: '',
    templateDescription: '',
  };
}

function openCreate() {
  editId.value = null;
  errorLocal.value = null;
  dialog.value = true;
  requestAnimationFrame(() => {
    scheduleDialogRef.value?.resetForm(defaultFormModel());
  });
}

function openEdit(row: OpWorkItemSchedule) {
  editId.value = row.__dataId;
  errorLocal.value = null;
  dialog.value = true;
  requestAnimationFrame(() => {
    scheduleDialogRef.value?.setForm({
      name: row.name,
      description: row.description ?? '',
      isActive: row.isActive,
      cronExpression: row.cronExpression,
      timezone: row.timezone || 'Europe/Istanbul',
      boardId: row.boardId,
      typeId: row.typeId,
      assignee: row.assignee || null,
      priorityId: row.priorityId ?? '',
      title: row.title,
      templateDescription: row.templateDescription ?? '',
    });
  });
}

function openDelete(row: OpWorkItemSchedule) {
  deleteTarget.value = row;
  deleteDialog.value = true;
}

function buildPayload(form: OcScheduleFormModel): Record<string, unknown> {
  return {
    workspaceId: props.workspaceId,
    name: form.name.trim(),
    description: form.description.trim() || null,
    isActive: form.isActive,
    cronExpression: form.cronExpression.trim(),
    timezone: form.timezone.trim() || 'Europe/Istanbul',
    boardId: form.boardId,
    typeId: form.typeId,
    assignee: form.assignee,
    priorityId: form.priorityId.trim() || null,
    title: form.title.trim(),
    templateDescription: form.templateDescription.trim() || null,
  };
}

async function onSaveForm(form: OcScheduleFormModel) {
  saving.value = true;
  errorLocal.value = null;
  infoLocal.value = null;
  try {
    const body = buildPayload(form);
    let scheduleId = editId.value;
    if (scheduleId) {
      await ocUpdateWorkItemSchedule(scheduleId, body);
    } else {
      scheduleId = (await ocCreateWorkItemSchedule(body)) ?? null;
      if (!scheduleId) {
        throw new Error(t('operationCore.workspaceDefinitions.scheduled.saveError'));
      }
    }
    try {
      await ocSyncWorkItemScheduleScheduler(scheduleId);
      infoLocal.value = t('operationCore.workspaceDefinitions.scheduled.saveSuccess');
    } catch (syncErr: unknown) {
      infoLocal.value = t('operationCore.workspaceDefinitions.scheduled.saveSuccess');
      errorLocal.value = ocExtractDgErrorMessage(
        syncErr,
        t('operationCore.workspaceDefinitions.scheduled.schedulerSyncWarning')
      );
    }
    dialog.value = false;
    await loadAll();
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.scheduled.saveError')
    );
  } finally {
    saving.value = false;
  }
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    try {
      await ocUnlinkWorkItemScheduleScheduler(deleteTarget.value.__dataId);
    } catch (unlinkErr: unknown) {
      errorLocal.value = ocExtractDgErrorMessage(
        unlinkErr,
        t('operationCore.workspaceDefinitions.scheduled.schedulerUnlinkWarning')
      );
    }
    await ocDeleteWorkItemSchedule(deleteTarget.value.__dataId);
    deleteDialog.value = false;
    deleteTarget.value = null;
    await loadAll();
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.scheduled.deleteError')
    );
    deleteDialog.value = false;
  } finally {
    deleting.value = false;
  }
}

async function runNow(row: OpWorkItemSchedule) {
  runningId.value = row.__dataId;
  errorLocal.value = null;
  infoLocal.value = null;
  try {
    await ocRunWorkItemScheduleNow(row.__dataId);
    infoLocal.value = t('operationCore.workspaceDefinitions.scheduled.runNowSuccess');
    await loadAll();
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.scheduled.runNowError')
    );
  } finally {
    runningId.value = null;
  }
}
</script>

<template>
  <div class="oc-ws-scheduled-tab pa-4 pa-md-5">
    <div class="oc-ws-scheduled-tab__hero mb-5">
      <div class="d-flex flex-wrap align-start justify-space-between gap-4 mb-4">
        <div class="flex-grow-1 min-width-0" style="max-width: 720px">
          <h3 class="text-h6 font-weight-bold mb-2">
            {{ t('operationCore.workspaceDefinitions.scheduled.pageTitle') }}
          </h3>
          <p class="text-body-2 text-medium-emphasis mb-0">
            {{ t('operationCore.workspaceDefinitions.scheduled.pageSubtitle') }}
          </p>
        </div>
        <v-btn color="primary" rounded="lg" size="large" class="text-none flex-shrink-0" @click="openCreate">
          <v-icon icon="mdi-plus" start />
          {{ t('operationCore.workspaceDefinitions.scheduled.addSchedule') }}
        </v-btn>
      </div>

      <v-row dense>
        <v-col v-for="(step, idx) in howItWorksSteps" :key="idx" cols="12" md="4">
          <v-card variant="outlined" rounded="lg" class="h-100 oc-ws-scheduled-tab__how-card">
            <v-card-text class="pa-4">
              <div class="d-flex align-start gap-3">
                <v-avatar :color="step.color" variant="tonal" size="40" rounded="lg">
                  <v-icon :icon="step.icon" size="22" />
                </v-avatar>
                <div>
                  <div class="text-body-1 font-weight-bold mb-1">{{ step.title }}</div>
                  <p class="text-body-2 text-medium-emphasis mb-0">{{ step.body }}</p>
                </div>
              </div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </div>

    <v-alert
      v-if="errorLocal"
      type="error"
      variant="tonal"
      class="mb-4 rounded-lg"
      closable
      @click:close="errorLocal = null"
    >
      {{ errorLocal }}
    </v-alert>

    <v-alert
      v-if="infoLocal"
      type="success"
      variant="tonal"
      class="mb-4 rounded-lg"
      closable
      @click:close="infoLocal = null"
    >
      {{ infoLocal }}
    </v-alert>

    <div v-if="!loading && schedules.length" class="d-flex flex-wrap ga-3 mb-4">
      <v-chip variant="tonal" color="primary" class="text-none">
        <v-icon icon="mdi-format-list-bulleted" start size="16" />
        {{
          t('operationCore.workspaceDefinitions.scheduled.statsTotal', { count: schedules.length })
        }}
      </v-chip>
      <v-chip variant="tonal" color="success" class="text-none">
        <v-icon icon="mdi-check-circle-outline" start size="16" />
        {{
          t('operationCore.workspaceDefinitions.scheduled.statsActive', { count: activeCount })
        }}
      </v-chip>
    </div>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4 rounded-pill" />

    <v-card
      v-if="!loading && !schedules.length"
      variant="outlined"
      rounded="lg"
      class="oc-ws-scheduled-tab__empty text-center pa-8 pa-md-12"
    >
      <v-avatar color="primary" variant="tonal" size="72" rounded="lg" class="mb-4">
        <v-icon icon="mdi-calendar-plus" size="36" />
      </v-avatar>
      <h4 class="text-h6 font-weight-bold mb-2">
        {{ t('operationCore.workspaceDefinitions.scheduled.emptyTitle') }}
      </h4>
      <p class="text-body-2 text-medium-emphasis mx-auto mb-6" style="max-width: 480px">
        {{ t('operationCore.workspaceDefinitions.scheduled.emptyBody') }}
      </p>
      <v-btn color="primary" rounded="lg" size="large" class="text-none" @click="openCreate">
        <v-icon icon="mdi-plus" start />
        {{ t('operationCore.workspaceDefinitions.scheduled.emptyCta') }}
      </v-btn>
    </v-card>

    <v-card v-else-if="!loading" variant="outlined" rounded="lg">
      <v-data-table
        :headers="tableHeaders"
        :items="schedules"
        :loading="loading"
        class="oc-ws-scheduled-table"
      >
        <template #[`item.name`]="{ item }">
          <div>
            <div class="text-body-2 font-weight-medium">{{ item.name }}</div>
            <div
              v-if="item.title"
              class="text-caption text-medium-emphasis text-truncate"
              style="max-width: 220px"
            >
              «{{ item.title }}»
            </div>
          </div>
        </template>

        <template #[`item.schedule`]="{ item }">
          <div class="d-flex align-start gap-2 py-1">
            <v-icon icon="mdi-calendar-clock" size="18" class="text-primary mt-1 flex-shrink-0" />
            <div>
              <div class="text-body-2 font-weight-medium">{{ scheduleSummary(item) }}</div>
              <div class="text-caption text-medium-emphasis">{{ item.timezone }}</div>
            </div>
          </div>
        </template>

        <template #[`item.target`]="{ item }">
          <div class="py-1">
            <div class="d-flex align-center gap-1 text-body-2">
              <v-icon icon="mdi-view-dashboard-outline" size="16" class="text-medium-emphasis" />
              {{ boardNameById.get(item.boardId) ?? '—' }}
            </div>
            <div class="d-flex align-center gap-1 text-caption text-medium-emphasis mt-1">
              <v-icon icon="mdi-shape-outline" size="14" />
              {{ typeNameById.get(item.typeId) ?? '—' }}
            </div>
          </div>
        </template>

        <template #[`item.assignee`]="{ item }">
          <div v-if="item.assignee" class="d-flex align-center gap-2">
            <v-icon icon="mdi-account-outline" size="18" class="text-medium-emphasis" />
            <span class="text-body-2">{{ assigneeLabel(item.assignee) }}</span>
          </div>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <template #[`item.isActive`]="{ item }">
          <v-chip
            size="small"
            variant="tonal"
            :color="item.isActive ? 'success' : 'default'"
            class="text-none"
          >
            <v-icon
              :icon="item.isActive ? 'mdi-play-circle-outline' : 'mdi-pause-circle-outline'"
              start
              size="14"
            />
            {{
              item.isActive
                ? t('operationCore.workspaceDefinitions.scheduled.activeYes')
                : t('operationCore.workspaceDefinitions.scheduled.activeNo')
            }}
          </v-chip>
          <div
            v-if="item.schedulerJobId"
            class="text-caption text-medium-emphasis mt-1 d-flex align-center gap-1"
            :title="item.schedulerJobId"
          >
            <v-icon icon="mdi-clock-check-outline" size="12" />
            {{ t('operationCore.workspaceDefinitions.scheduled.schedulerLinked') }}
          </div>
          <div
            v-else
            class="text-caption text-warning mt-1 d-flex align-center gap-1"
          >
            <v-icon icon="mdi-clock-alert-outline" size="12" />
            {{ t('operationCore.workspaceDefinitions.scheduled.schedulerPending') }}
          </div>
        </template>

        <template #[`item.lastRunAt`]="{ item }">
          <div>
            <div class="text-body-2">{{ formatLastRun(item.lastRunAt) }}</div>
            <NuxtLink
              v-if="item.lastWorkItemId"
              :to="workItemProfileHref(item.lastWorkItemId)"
              class="text-caption text-primary text-decoration-none d-inline-flex align-center gap-1 mt-1"
            >
              <v-icon icon="mdi-open-in-new" size="12" />
              {{ t('operationCore.workspaceDefinitions.scheduled.viewLastWorkItem') }}
            </NuxtLink>
          </div>
        </template>

        <template #[`item.actions`]="{ item }">
          <div class="d-flex justify-end align-center ga-1">
            <v-tooltip :text="t('operationCore.workspaceDefinitions.scheduled.runNowTooltip')" location="top">
              <template #activator="{ props: tipProps }">
                <v-btn
                  v-bind="tipProps"
                  size="small"
                  variant="text"
                  icon="mdi-play-circle-outline"
                  :loading="runningId === item.__dataId"
                  @click="runNow(item)"
                />
              </template>
            </v-tooltip>
            <v-tooltip :text="t('operationCore.workspaceDefinitions.scheduled.editTooltip')" location="top">
              <template #activator="{ props: tipProps }">
                <v-btn
                  v-bind="tipProps"
                  size="small"
                  variant="text"
                  icon="mdi-pencil-outline"
                  @click="openEdit(item)"
                />
              </template>
            </v-tooltip>
            <v-tooltip :text="t('operationCore.workspaceDefinitions.scheduled.deleteTooltip')" location="top">
              <template #activator="{ props: tipProps }">
                <v-btn
                  v-bind="tipProps"
                  size="small"
                  variant="text"
                  icon="mdi-delete-outline"
                  color="error"
                  @click="openDelete(item)"
                />
              </template>
            </v-tooltip>
          </div>
        </template>
      </v-data-table>
    </v-card>

    <p v-if="!loading" class="text-caption text-medium-emphasis mt-4 mb-0">
      {{ t('operationCore.workspaceDefinitions.scheduled.technicalFootnote') }}
    </p>

    <OcWorkspaceScheduleDialog
      ref="scheduleDialogRef"
      v-model="dialog"
      :edit-id="editId"
      :workspace-id="workspaceId"
      :board-items="boardItems"
      :type-items="typeItems"
      :priority-items="priorityItems"
      :board-name-by-id="boardNameById"
      :type-name-by-id="typeNameById"
      :saving="saving"
      @save="onSaveForm"
    />

    <v-dialog v-model="deleteDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="d-flex align-center gap-2 pt-5 px-5">
          <v-icon icon="mdi-alert-circle-outline" color="warning" />
          {{ t('operationCore.workspaceDefinitions.scheduled.deleteTitle') }}
        </v-card-title>
        <v-card-text class="px-5 pb-2">
          <p class="mb-2">{{ t('operationCore.workspaceDefinitions.scheduled.deleteBody') }}</p>
          <v-chip v-if="deleteTarget" variant="tonal" class="text-none font-weight-medium">
            {{ deleteTarget.name }}
          </v-chip>
          <p class="text-caption text-medium-emphasis mt-3 mb-0">
            {{ t('operationCore.workspaceDefinitions.scheduled.deleteFootnote') }}
          </p>
        </v-card-text>
        <v-card-actions class="pa-4 px-5">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteDialog = false">
            {{ t('operationCore.definitions.cancel') }}
          </v-btn>
          <v-btn color="error" variant="flat" rounded="lg" class="text-none" :loading="deleting" @click="confirmDelete">
            {{ t('operationCore.definitions.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.oc-ws-scheduled-tab__how-card {
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}

.oc-ws-scheduled-tab__how-card:hover {
  border-color: rgba(var(--v-theme-primary), 0.35);
}

.oc-ws-scheduled-tab__empty {
  border-style: dashed;
}
</style>
