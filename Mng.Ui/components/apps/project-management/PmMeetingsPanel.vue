<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  pmCreateMeeting,
  pmCreateMeetingAction,
  pmDateInput,
  pmDatePayload,
  pmDeleteMeeting,
  pmDeleteMeetingAction,
  pmUpdateMeeting,
  pmUpdateMeetingAction,
} from '@/services/projectManagementService';
import type {
  PmMeeting,
  PmMeetingAction,
  PmMeetingActionStatus,
  PmWbsItem,
} from '@/types/apps/projectManagement';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  items: PmMeeting[];
  wbs: PmWbsItem[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  changed: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const statusFilter = ref<'all' | 'open' | 'overdue' | 'unbound'>('all');
const meetingDialog = ref(false);
const actionDialog = ref(false);
const saving = ref(false);
const editingMeetingId = ref<string | null>(null);
const editingActionId = ref<string | null>(null);
const actionMeetingId = ref<string | null>(null);
const deleteMeetingTarget = ref<PmMeeting | null>(null);
const deleteActionTarget = ref<PmMeetingAction | null>(null);
const deleting = ref(false);
const closingId = ref<string | null>(null);

const meetingForm = ref({
  name: '',
  heldAt: '',
  minutesResourceId: '',
  wbsId: '',
  attendees: '',
  note: '',
});

const actionForm = ref({
  title: '',
  ownerName: '',
  dueDate: '',
  status: 'open' as PmMeetingActionStatus,
  workItemId: '',
  wbsId: '',
  note: '',
});

const statusItems = computed(() => [
  { title: t('projectManagement.meeting.status.open'), value: 'open' },
  { title: t('projectManagement.meeting.status.inProgress'), value: 'inProgress' },
  { title: t('projectManagement.meeting.status.done'), value: 'done' },
  { title: t('projectManagement.meeting.status.waived'), value: 'waived' },
]);

const wbsItems = computed(() => [
  { title: t('projectManagement.meeting.projectLevel'), value: '' },
  ...props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
]);

const allActions = computed(() => props.items.flatMap((meeting) => meeting.actions ?? []));
const openCount = computed(() => allActions.value.filter((row) => row.open).length);
const overdueCount = computed(() => allActions.value.filter((row) => row.overdue).length);
const unboundCount = computed(() => allActions.value.filter((row) => row.unbound).length);

const visibleMeetings = computed(() => {
  if (statusFilter.value === 'all') return props.items;
  return props.items
    .map((meeting) => ({
      ...meeting,
      actions: (meeting.actions ?? []).filter((row) => {
        if (statusFilter.value === 'open') return row.open;
        if (statusFilter.value === 'overdue') return row.overdue;
        return row.unbound;
      }),
    }))
    .filter((meeting) => meeting.actions.length > 0 || statusFilter.value === 'all');
});

const canSaveMeeting = computed(() => Boolean(meetingForm.value.name.trim()));
const canSaveAction = computed(() => {
  if (!actionForm.value.title.trim()) return false;
  if (actionForm.value.status === 'waived' && !actionForm.value.note.trim()) return false;
  return true;
});

function wbsName(id?: string | null) {
  if (!id) return t('projectManagement.meeting.projectLevel');
  const row = props.wbs.find((item) => item.id === id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
}

function statusLabel(status?: string | null) {
  const key = `projectManagement.meeting.status.${status || 'open'}`;
  const label = t(key);
  return label === key ? status || 'open' : label;
}

function statusColor(row: PmMeetingAction) {
  if (row.status === 'waived') return 'warning';
  if (row.status === 'done') return 'success';
  if (row.overdue) return 'error';
  if (row.unbound) return 'warning';
  return 'info';
}

function minutesHref(id: string) {
  return `/apps/document-intelligence/r/${encodeURIComponent(id)}`;
}

function openCreateMeeting() {
  editingMeetingId.value = null;
  meetingForm.value = { name: '', heldAt: '', minutesResourceId: '', wbsId: '', attendees: '', note: '' };
  meetingDialog.value = true;
}

function openEditMeeting(row: PmMeeting) {
  editingMeetingId.value = row.id;
  meetingForm.value = {
    name: row.name,
    heldAt: pmDateInput(row.heldAt),
    minutesResourceId: row.minutesResourceId || '',
    wbsId: row.wbsId || '',
    attendees: row.attendees || '',
    note: row.note || '',
  };
  meetingDialog.value = true;
}

function openCreateAction(meeting: PmMeeting) {
  editingActionId.value = null;
  actionMeetingId.value = meeting.id;
  actionForm.value = {
    title: '',
    ownerName: '',
    dueDate: '',
    status: 'open',
    workItemId: '',
    wbsId: meeting.wbsId || '',
    note: '',
  };
  actionDialog.value = true;
}

function openEditAction(row: PmMeetingAction) {
  editingActionId.value = row.id;
  actionMeetingId.value = row.meetingId;
  actionForm.value = {
    title: row.title,
    ownerName: row.ownerName || '',
    dueDate: pmDateInput(row.dueDate),
    status: (row.status as PmMeetingActionStatus) || 'open',
    workItemId: row.workItemId || '',
    wbsId: row.wbsId || '',
    note: row.note || '',
  };
  actionDialog.value = true;
}

function meetingPayload() {
  return {
    name: meetingForm.value.name.trim(),
    heldAt: pmDatePayload(meetingForm.value.heldAt),
    minutesResourceId: meetingForm.value.minutesResourceId.trim() || null,
    wbsId: meetingForm.value.wbsId || null,
    attendees: meetingForm.value.attendees.trim() || null,
    note: meetingForm.value.note.trim() || null,
  };
}

function actionPayload() {
  return {
    title: actionForm.value.title.trim(),
    ownerName: actionForm.value.ownerName.trim() || null,
    dueDate: pmDatePayload(actionForm.value.dueDate),
    status: actionForm.value.status,
    workItemId: actionForm.value.workItemId.trim() || null,
    wbsId: actionForm.value.wbsId || null,
    note: actionForm.value.note.trim() || null,
  };
}

async function saveMeeting() {
  if (!canSaveMeeting.value) return;
  saving.value = true;
  try {
    if (editingMeetingId.value) await pmUpdateMeeting(editingMeetingId.value, meetingPayload());
    else await pmCreateMeeting(props.projectId, meetingPayload());
    meetingDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.meetingSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

async function saveAction() {
  if (!canSaveAction.value || !actionMeetingId.value) return;
  saving.value = true;
  try {
    if (editingActionId.value) await pmUpdateMeetingAction(editingActionId.value, actionPayload());
    else await pmCreateMeetingAction(actionMeetingId.value, actionPayload());
    actionDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.meetingActionSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

async function markDone(row: PmMeetingAction) {
  closingId.value = row.id;
  try {
    await pmUpdateMeetingAction(row.id, { status: 'done', workItemId: row.workItemId });
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.meetingActionSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    closingId.value = null;
  }
}

async function executeDeleteMeeting() {
  if (!deleteMeetingTarget.value) return;
  deleting.value = true;
  try {
    await pmDeleteMeeting(deleteMeetingTarget.value.id);
    deleteMeetingTarget.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.meetingDeleted'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.deleteFailed');
  } finally {
    deleting.value = false;
  }
}

async function executeDeleteAction() {
  if (!deleteActionTarget.value) return;
  deleting.value = true;
  try {
    await pmDeleteMeetingAction(deleteActionTarget.value.id);
    deleteActionTarget.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.meetingActionDeleted'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.deleteFailed');
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between flex-wrap ga-3 mb-4">
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.meeting.hint') }}</div>
      <v-btn color="primary" @click="openCreateMeeting">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.meeting.new') }}
      </v-btn>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip size="small" color="info" variant="tonal">
        {{ t('projectManagement.meeting.open') }} · {{ openCount }}
      </v-chip>
      <v-chip size="small" :color="overdueCount ? 'error' : 'success'" variant="tonal">
        {{ t('projectManagement.meeting.overdue') }} · {{ overdueCount }}
      </v-chip>
      <v-chip size="small" :color="unboundCount ? 'warning' : 'default'" variant="tonal">
        {{ t('projectManagement.meeting.unbound') }} · {{ unboundCount }}
      </v-chip>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip :variant="statusFilter === 'all' ? 'flat' : 'tonal'" @click="statusFilter = 'all'">
        {{ t('projectManagement.meeting.filterAll') }}
      </v-chip>
      <v-chip color="info" :variant="statusFilter === 'open' ? 'flat' : 'tonal'" @click="statusFilter = 'open'">
        {{ t('projectManagement.meeting.open') }}
      </v-chip>
      <v-chip color="error" :variant="statusFilter === 'overdue' ? 'flat' : 'tonal'" @click="statusFilter = 'overdue'">
        {{ t('projectManagement.meeting.overdue') }}
      </v-chip>
      <v-chip color="warning" :variant="statusFilter === 'unbound' ? 'flat' : 'tonal'" @click="statusFilter = 'unbound'">
        {{ t('projectManagement.meeting.unbound') }}
      </v-chip>
    </div>

    <v-alert v-if="!items.length && !loading" type="info" variant="tonal" class="mb-4">
      {{ t('projectManagement.meeting.empty') }}
    </v-alert>

    <v-expansion-panels v-else multiple variant="accordion">
      <v-expansion-panel v-for="meeting in visibleMeetings" :key="meeting.id">
        <v-expansion-panel-title>
          <div class="d-flex align-center justify-space-between flex-grow-1 flex-wrap ga-2 pr-4">
            <div>
              <div class="font-weight-medium">{{ meeting.name }}</div>
              <div class="text-caption text-medium-emphasis">
                {{ pmDateInput(meeting.heldAt) || t('projectManagement.meeting.noDate') }}
                · {{ wbsName(meeting.wbsId) }}
              </div>
            </div>
            <v-chip size="small" :color="meeting.openActionCount ? 'info' : 'success'" variant="tonal">
              {{ t('projectManagement.meeting.open') }} · {{ meeting.openActionCount }}
            </v-chip>
          </div>
        </v-expansion-panel-title>
        <v-expansion-panel-text>
          <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-3">
            <div class="text-body-2">
              <NuxtLink
                v-if="meeting.minutesResourceId"
                :to="minutesHref(meeting.minutesResourceId)"
                class="text-primary"
              >
                {{ t('projectManagement.meeting.minutes') }}
              </NuxtLink>
              <span v-else class="text-medium-emphasis">{{ t('projectManagement.meeting.noMinutes') }}</span>
            </div>
            <div class="d-flex ga-1">
              <v-btn size="small" color="primary" variant="tonal" @click="openCreateAction(meeting)">
                <PlusIcon size="16" class="mr-1" />
                {{ t('projectManagement.meeting.newAction') }}
              </v-btn>
              <v-btn size="small" variant="text" @click="openEditMeeting(meeting)">{{ t('projectManagement.edit') }}</v-btn>
              <v-btn icon size="small" variant="text" color="error" @click="deleteMeetingTarget = meeting">
                <TrashIcon size="18" />
              </v-btn>
            </div>
          </div>

          <v-table density="comfortable" class="rounded-lg border">
            <thead>
              <tr>
                <th>{{ t('projectManagement.meeting.action') }}</th>
                <th>{{ t('projectManagement.fields.status') }}</th>
                <th>{{ t('projectManagement.meeting.owner') }}</th>
                <th>{{ t('projectManagement.meeting.dueDate') }}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="action in meeting.actions" :key="action.id">
                <td>
                  <div>{{ action.title }}</div>
                  <div v-if="action.workItemId" class="text-caption text-medium-emphasis">{{ action.workItemId }}</div>
                </td>
                <td>
                  <v-chip size="small" :color="statusColor(action)" variant="tonal">
                    {{ statusLabel(action.status) }}
                  </v-chip>
                </td>
                <td>{{ action.ownerName || '—' }}</td>
                <td>{{ pmDateInput(action.dueDate) || '—' }}</td>
                <td class="text-right">
                  <v-btn
                    v-if="action.open"
                    size="small"
                    variant="text"
                    color="success"
                    :loading="closingId === action.id"
                    @click="markDone(action)"
                  >
                    {{ t('projectManagement.meeting.markDone') }}
                  </v-btn>
                  <v-btn size="small" variant="text" @click="openEditAction(action)">{{ t('projectManagement.edit') }}</v-btn>
                  <v-btn icon size="small" variant="text" color="error" @click="deleteActionTarget = action">
                    <TrashIcon size="16" />
                  </v-btn>
                </td>
              </tr>
              <tr v-if="!(meeting.actions || []).length">
                <td colspan="5" class="text-center text-medium-emphasis py-4">
                  {{ t('projectManagement.meeting.emptyActions') }}
                </td>
              </tr>
            </tbody>
          </v-table>
        </v-expansion-panel-text>
      </v-expansion-panel>
    </v-expansion-panels>

    <v-dialog v-model="meetingDialog" max-width="560">
      <v-card rounded="lg">
        <v-card-title>
          {{ editingMeetingId ? t('projectManagement.meeting.edit') : t('projectManagement.meeting.new') }}
        </v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="meetingForm.name" :label="t('projectManagement.fields.name')" density="comfortable" />
          <v-select v-model="meetingForm.wbsId" :items="wbsItems" :label="t('projectManagement.fields.wbsCode')" density="comfortable" />
          <v-text-field v-model="meetingForm.heldAt" type="date" :label="t('projectManagement.meeting.heldAt')" density="comfortable" />
          <v-text-field
            v-model="meetingForm.minutesResourceId"
            :label="t('projectManagement.meeting.minutesId')"
            density="comfortable"
          />
          <v-textarea v-model="meetingForm.attendees" :label="t('projectManagement.meeting.attendees')" density="comfortable" rows="2" auto-grow />
          <v-textarea v-model="meetingForm.note" :label="t('projectManagement.meeting.note')" density="comfortable" rows="2" auto-grow />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="meetingDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" :disabled="!canSaveMeeting" @click="saveMeeting">
            {{ t('projectManagement.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="actionDialog" max-width="560">
      <v-card rounded="lg">
        <v-card-title>
          {{ editingActionId ? t('projectManagement.meeting.editAction') : t('projectManagement.meeting.newAction') }}
        </v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="actionForm.title" :label="t('projectManagement.meeting.action')" density="comfortable" />
          <v-text-field v-model="actionForm.ownerName" :label="t('projectManagement.meeting.owner')" density="comfortable" />
          <v-select v-model="actionForm.wbsId" :items="wbsItems" :label="t('projectManagement.fields.wbsCode')" density="comfortable" />
          <v-select v-model="actionForm.status" :items="statusItems" :label="t('projectManagement.fields.status')" density="comfortable" />
          <v-text-field v-model="actionForm.dueDate" type="date" :label="t('projectManagement.meeting.dueDate')" density="comfortable" />
          <v-text-field v-model="actionForm.workItemId" :label="t('projectManagement.meeting.workItemId')" density="comfortable" />
          <v-textarea v-model="actionForm.note" :label="t('projectManagement.meeting.actionNote')" density="comfortable" rows="2" auto-grow />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="actionDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" :disabled="!canSaveAction" @click="saveAction">
            {{ t('projectManagement.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="Boolean(deleteMeetingTarget)" max-width="440" @update:model-value="(open: boolean) => { if (!open) deleteMeetingTarget = null; }">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.meeting.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.meeting.deleteConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteMeetingTarget = null">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="error" :loading="deleting" @click="executeDeleteMeeting">{{ t('projectManagement.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="Boolean(deleteActionTarget)" max-width="440" @update:model-value="(open: boolean) => { if (!open) deleteActionTarget = null; }">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.meeting.deleteActionTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.meeting.deleteActionConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteActionTarget = null">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="error" :loading="deleting" @click="executeDeleteAction">{{ t('projectManagement.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
