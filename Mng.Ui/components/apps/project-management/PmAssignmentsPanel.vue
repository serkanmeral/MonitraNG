<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useKeeperDirectoryPicker } from '@/composables/useKeeperDirectoryPicker';
import { usePmDate } from '@/composables/usePmDate';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  pmCreateDuty,
  pmDateInput,
  pmDatePayload,
  pmDeleteMeetingAction,
  pmListDuties,
  pmListProjectMeetingPeople,
  pmUpdateMeetingAction,
} from '@/services/projectManagementService';
import type {
  PmMeetingAction,
  PmMeetingActionStatus,
  PmMeetingActionTarget,
  PmMeetingPerson,
  PmWbsItem,
} from '@/types/apps/projectManagement';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  wbs: PmWbsItem[];
  loading?: boolean;
  revision?: number;
}>();

const emit = defineEmits<{
  changed: [];
  openMeeting: [id: string];
}>();

const { t } = useAppI18n();
const { formatPmDateOrDash } = usePmDate();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();
const userPicker = useKeeperDirectoryPicker('user');

const rows = ref<PmMeetingAction[]>([]);
const people = ref<PmMeetingPerson[]>([]);
const listLoading = ref(false);
const saving = ref(false);
const deleting = ref(false);
const dialog = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<PmMeetingAction | null>(null);
const statusBusyId = ref<string | null>(null);
const waiveTarget = ref<PmMeetingAction | null>(null);
const waiveNote = ref('');
type StatusSlice = 'open' | 'inProgress' | 'overdue' | 'done' | 'waived';
type DutyView = 'target' | 'meeting' | 'list';
const statusSlice = ref<StatusSlice | null>(null);
const view = ref<DutyView>('target');
const search = ref('');
const kind = ref<'' | 'user' | 'external' | 'organization'>('');
const userId = ref<string | null>(null);
const personId = ref<string | null>(null);
const firm = ref<string | null>(null);

const form = ref(emptyForm());

const statusItems = computed((): { title: string; value: PmMeetingActionStatus }[] => [
  { title: t('projectManagement.meeting.status.open'), value: 'open' },
  { title: t('projectManagement.meeting.status.inProgress'), value: 'inProgress' },
  { title: t('projectManagement.meeting.status.done'), value: 'done' },
  { title: t('projectManagement.meeting.status.waived'), value: 'waived' },
]);

const wbsItems = computed(() => [
  { title: t('projectManagement.meeting.projectLevel'), value: '' },
  ...props.wbs.map((item) => ({
    title: item.wbsCode ? `${item.wbsCode} · ${item.name}` : item.name,
    value: item.id,
  })),
]);

const personItems = computed(() =>
  [...people.value].sort((a, b) => a.name.localeCompare(b.name, 'tr')),
);

const firmOptions = computed(() => {
  const names = new Map<string, string>();
  const remember = (value: string | null | undefined) => {
    const name = (value || '').trim();
    if (!name) return;
    const key = name.toLocaleLowerCase('tr');
    if (!names.has(key)) names.set(key, name);
  };
  for (const person of people.value) remember(person.organization);
  for (const name of form.value.organizations) remember(name);
  if (firm.value) remember(firm.value);
  return [...names.values()].sort((a, b) => a.localeCompare(b, 'tr'));
});

const canSave = computed(() => {
  if (!form.value.title.trim()) return false;
  if (form.value.status === 'waived' && !form.value.note.trim()) return false;
  return true;
});

function sliceOf(row: PmMeetingAction): StatusSlice {
  if (row.status === 'done') return 'done';
  if (row.status === 'waived') return 'waived';
  if (row.overdue) return 'overdue';
  if (row.status === 'inProgress') return 'inProgress';
  return 'open';
}

const counts = computed(() => {
  const tally: Record<StatusSlice, number> = { open: 0, inProgress: 0, overdue: 0, done: 0, waived: 0 };
  for (const row of rows.value) tally[sliceOf(row)] += 1;
  return tally;
});

const countCards = computed(() => [
  { key: 'open' as const, label: t('projectManagement.meeting.status.open'), count: counts.value.open, color: 'info' },
  { key: 'inProgress' as const, label: t('projectManagement.meeting.status.inProgress'), count: counts.value.inProgress, color: 'primary' },
  { key: 'overdue' as const, label: t('projectManagement.meeting.overdue'), count: counts.value.overdue, color: 'error' },
  { key: 'done' as const, label: t('projectManagement.meeting.status.done'), count: counts.value.done, color: 'success' },
  { key: 'waived' as const, label: t('projectManagement.meeting.status.waived'), count: counts.value.waived, color: 'secondary' },
]);

const visibleRows = computed(() => (
  statusSlice.value ? rows.value.filter((row) => sliceOf(row) === statusSlice.value) : rows.value
));

function compareRows(a: PmMeetingAction, b: PmMeetingAction) {
  const rank = (row: PmMeetingAction) => {
    if (row.overdue) return 0;
    if (row.status === 'done' || row.status === 'waived') return 3;
    if (!row.dueDate) return 2;
    return 1;
  };
  const byRank = rank(a) - rank(b);
  if (byRank !== 0) return byRank;
  const aDue = a.dueDate || '';
  const bDue = b.dueDate || '';
  if (aDue !== bDue) return aDue < bDue ? -1 : 1;
  return a.title.localeCompare(b.title, 'tr');
}

function targetBuckets(row: PmMeetingAction): { key: string; label: string }[] {
  const seen = new Set<string>();
  const buckets: { key: string; label: string }[] = [];
  const add = (key: string, label: string) => {
    if (seen.has(key)) return;
    seen.add(key);
    buckets.push({ key, label });
  };
  for (const target of row.targets ?? []) {
    if (target.kind === 'user' && target.userId) {
      add(`user:${target.userId}`, target.name || row.ownerName || target.userId);
      continue;
    }
    if (target.kind === 'external' && target.personId) {
      add(`person:${target.personId}`, target.name || target.personId);
      continue;
    }
    const firmName = (target.organization || target.name || '').trim();
    if (firmName) add(`org:${firmName.toLocaleLowerCase('tr')}`, firmName);
  }
  if (!buckets.length && row.ownerName) add(`owner:${row.ownerName.toLocaleLowerCase('tr')}`, row.ownerName);
  if (!buckets.length) add('none', t('projectManagement.assignment.unassigned'));
  return buckets;
}

function meetingBucket(row: PmMeetingAction) {
  if (row.meetingId) return { key: row.meetingId, label: row.meetingName || t('projectManagement.assignment.openMeeting') };
  return { key: 'none', label: t('projectManagement.assignment.noMeeting') };
}

const groups = computed(() => {
  if (view.value === 'list') {
    if (!visibleRows.value.length) return [];
    return [{
      key: 'all',
      label: '',
      open: 0,
      overdue: 0,
      rows: [...visibleRows.value].sort(compareRows),
    }];
  }
  const stats = new Map<string, { label: string; open: number; overdue: number }>();
  const members = new Map<string, PmMeetingAction[]>();
  const bucketOf = view.value === 'meeting'
    ? (row: PmMeetingAction) => [meetingBucket(row)]
    : targetBuckets;
  for (const row of rows.value) {
    for (const bucket of bucketOf(row)) {
      const stat = stats.get(bucket.key) ?? { label: bucket.label, open: 0, overdue: 0 };
      stat.label = bucket.label;
      if (sliceOf(row) === 'open' || sliceOf(row) === 'inProgress') stat.open += 1;
      if (row.overdue) stat.overdue += 1;
      stats.set(bucket.key, stat);
    }
  }
  for (const row of visibleRows.value) {
    for (const bucket of bucketOf(row)) {
      const list = members.get(bucket.key) ?? [];
      list.push(row);
      members.set(bucket.key, list);
    }
  }
  return [...members.entries()]
    .map(([key, list]) => {
      const stat = stats.get(key) ?? { label: key, open: 0, overdue: 0 };
      return { key, label: stat.label, open: stat.open, overdue: stat.overdue, rows: list.sort(compareRows) };
    })
    .sort((a, b) => {
      if (a.key === 'none') return 1;
      if (b.key === 'none') return -1;
      if (b.overdue !== a.overdue) return b.overdue - a.overdue;
      if (b.open !== a.open) return b.open - a.open;
      return a.label.localeCompare(b.label, 'tr');
    });
});

function emptyForm() {
  return {
    code: '',
    title: '',
    userIds: [] as string[],
    personIds: [] as string[],
    organizations: [] as string[],
    wbsId: '',
    status: 'open' as PmMeetingActionStatus,
    dueDate: '',
    note: '',
  };
}

function targetValue() {
  if (kind.value === 'user') return (userId.value || '').trim();
  if (kind.value === 'external') return (personId.value || '').trim();
  if (kind.value === 'organization') return (firm.value || '').trim();
  return '';
}

async function loadPeople() {
  if (!props.projectId) return;
  try {
    people.value = await pmListProjectMeetingPeople(props.projectId);
  } catch {
    people.value = [];
  }
}

async function load() {
  if (!props.projectId) return;
  listLoading.value = true;
  try {
    const target = targetValue();
    const pack = await pmListDuties(props.projectId, {
      kind: target ? kind.value : undefined,
      target: target || undefined,
      q: search.value.trim() || undefined,
    });
    rows.value = pack.items ?? [];
  } catch (error) {
    rows.value = [];
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    listLoading.value = false;
  }
}

function openCreate() {
  editingId.value = null;
  form.value = emptyForm();
  dialog.value = true;
}

async function openEdit(row: PmMeetingAction) {
  editingId.value = row.id;
  const targets = row.targets ?? [];
  const userIds = targets.filter((item) => item.kind === 'user' && item.userId).map((item) => item.userId as string);
  form.value = {
    code: row.code || '',
    title: row.title,
    userIds,
    personIds: targets.filter((item) => item.kind === 'external' && item.personId).map((item) => item.personId as string),
    organizations: targets
      .filter((item) => item.kind === 'organization')
      .map((item) => (item.organization || item.name || '').trim())
      .filter(Boolean),
    wbsId: row.wbsId || '',
    status: (row.status as PmMeetingActionStatus) || 'open',
    dueDate: pmDateInput(row.dueDate),
    note: row.note || '',
  };
  dialog.value = true;
  await userPicker.ensureSelectedLabels(userIds);
}

function payloadTargets() {
  const targets: PmMeetingActionTarget[] = [];
  for (const id of form.value.userIds) {
    targets.push({ kind: 'user', userId: id, name: userPicker.labelFor(id) || id });
  }
  for (const id of form.value.personIds) {
    const person = people.value.find((item) => item.id === id);
    targets.push({ kind: 'external', personId: id, name: person?.name || id });
  }
  for (const name of form.value.organizations) {
    const firmName = name.trim();
    if (!firmName) continue;
    targets.push({ kind: 'organization', organization: firmName, name: firmName });
  }
  return targets;
}

async function save() {
  if (!canSave.value) return;
  saving.value = true;
  try {
    await userPicker.ensureSelectedLabels(form.value.userIds);
    const body = {
      code: form.value.code.trim() || null,
      title: form.value.title.trim(),
      targets: payloadTargets(),
      wbsId: form.value.wbsId || null,
      status: form.value.status,
      dueDate: pmDatePayload(form.value.dueDate),
      note: form.value.note.trim() || null,
    };
    if (editingId.value) await pmUpdateMeetingAction(editingId.value, body);
    else await pmCreateDuty(props.projectId, body);
    dialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.assignment.saved'),
      severity: 'success',
    });
    await load();
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

async function remove() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  try {
    await pmDeleteMeetingAction(deleteTarget.value.id);
    deleteTarget.value = null;
    dialog.value = false;
    await load();
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.deleteFailed');
  } finally {
    deleting.value = false;
  }
}

function statusLabel(row: PmMeetingAction) {
  return statusItems.value.find((item) => item.value === row.status)?.title || row.status;
}

function chipColor(row: PmMeetingAction) {
  if (row.overdue) return 'error';
  if (row.status === 'inProgress') return 'primary';
  if (row.status === 'done') return 'success';
  if (row.status === 'waived') return 'secondary';
  return 'info';
}

function toggleSlice(key: StatusSlice) {
  statusSlice.value = statusSlice.value === key ? null : key;
}

function askDeleteEditing() {
  const row = rows.value.find((item) => item.id === editingId.value);
  if (row) deleteTarget.value = row;
}

async function applyStatus(row: PmMeetingAction, status: PmMeetingActionStatus, note?: string) {
  if (row.status === status && note === undefined) return;
  statusBusyId.value = row.id;
  try {
    await pmUpdateMeetingAction(row.id, {
      status,
      ...(note !== undefined ? { note } : {}),
    });
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.assignment.statusSaved'),
      severity: 'success',
    });
    waiveTarget.value = null;
    waiveNote.value = '';
    await load();
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    statusBusyId.value = null;
  }
}

function chooseStatus(row: PmMeetingAction, status: PmMeetingActionStatus) {
  if (status === 'waived' && !(row.note || '').trim()) {
    waiveTarget.value = row;
    waiveNote.value = '';
    return;
  }
  void applyStatus(row, status);
}

let searchTimer: ReturnType<typeof setTimeout> | undefined;
watch([kind, userId, personId, firm], () => {
  void load();
});
watch(search, () => {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => void load(), 300);
});
watch(
  () => props.projectId,
  () => {
    void loadPeople();
    void load();
  },
);

watch(
  () => props.revision,
  () => {
    void load();
  },
);

onMounted(() => {
  void loadPeople();
  void load();
});
</script>

<template>
  <div>
    <div class="d-flex align-center ga-3 mb-4">
      <div class="text-h6">{{ t('projectManagement.assignment.title') }}</div>
      <v-spacer />
      <v-btn color="primary" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.assignment.add') }}
      </v-btn>
    </div>

    <div class="duty-counts mb-4">
      <button
        v-for="card in countCards"
        :key="card.key"
        type="button"
        class="duty-count"
        :class="[`duty-count--${card.color}`, { 'is-on': statusSlice === card.key }]"
        @click="toggleSlice(card.key)"
      >
        <span class="duty-count__n">{{ card.count }}</span>
        <span class="duty-count__label">{{ card.label }}</span>
      </button>
    </div>

    <div class="d-flex flex-wrap align-center ga-3 mb-4">
      <v-btn-toggle v-model="view" mandatory density="comfortable" divided>
        <v-btn value="target">{{ t('projectManagement.assignment.viewTarget') }}</v-btn>
        <v-btn value="meeting">{{ t('projectManagement.assignment.viewMeeting') }}</v-btn>
        <v-btn value="list">{{ t('projectManagement.assignment.viewList') }}</v-btn>
      </v-btn-toggle>
      <v-text-field
        v-model="search"
        :label="t('projectManagement.assignment.search')"
        density="comfortable"
        hide-details
        clearable
        style="max-width: 280px"
      />
      <v-select
        v-model="kind"
        :items="[
          { title: t('projectManagement.assignment.target'), value: '' },
          { title: t('projectManagement.meeting.targetUsers'), value: 'user' },
          { title: t('projectManagement.meeting.targetPeople'), value: 'external' },
          { title: t('projectManagement.meeting.targetFirms'), value: 'organization' },
        ]"
        density="comfortable"
        hide-details
        style="max-width: 200px"
      />
      <MngDirectoryPickerField
        v-if="kind === 'user'"
        v-model="userId"
        entity="user"
        :external-picker="userPicker"
        :label="t('projectManagement.meeting.targetUsers')"
        style="max-width: 260px"
      />
      <v-autocomplete
        v-else-if="kind === 'external'"
        v-model="personId"
        :items="personItems"
        item-title="name"
        item-value="id"
        :label="t('projectManagement.meeting.targetPeople')"
        density="comfortable"
        hide-details
        clearable
        style="max-width: 260px"
      />
      <v-combobox
        v-else-if="kind === 'organization'"
        v-model="firm"
        :items="firmOptions"
        :label="t('projectManagement.meeting.targetFirms')"
        density="comfortable"
        hide-details
        clearable
        style="max-width: 260px"
      />
    </div>

    <v-progress-linear v-if="loading || listLoading" indeterminate class="mb-3" />

    <div v-if="groups.length" class="d-flex flex-column ga-5">
      <section v-for="group in groups" :key="group.key">
        <div v-if="view !== 'list'" class="d-flex align-center justify-space-between ga-3 mb-2">
          <div class="text-subtitle-1">{{ group.label }}</div>
          <div v-if="group.open || group.overdue" class="text-caption text-medium-emphasis">
            {{ t('projectManagement.assignment.groupMeta', { open: group.open, overdue: group.overdue }) }}
          </div>
        </div>
        <v-card variant="outlined" rounded="lg">
          <div
            v-for="row in group.rows"
            :key="`${group.key}-${row.id}`"
            class="duty-row"
            @click="openEdit(row)"
          >
            <div class="duty-row__main">
              <div class="duty-row__title">
                <span v-if="row.code" class="text-caption text-medium-emphasis mr-2">{{ row.code }}</span>
                {{ row.title }}
              </div>
              <div class="text-caption text-medium-emphasis">
                <span :class="{ 'text-error': row.overdue }">{{ formatPmDateOrDash(row.dueDate) }}</span>
                <span v-if="row.overdue"> · {{ t('projectManagement.meeting.overdue') }}</span>
                <template v-if="view !== 'target'">
                  <span v-if="row.ownerName"> · {{ row.ownerName }}</span>
                </template>
                <template v-if="view !== 'meeting'">
                  <span> · </span>
                  <button
                    v-if="row.meetingId"
                    type="button"
                    class="duty-link"
                    @click.stop="emit('openMeeting', row.meetingId)"
                  >
                    {{ row.meetingName || t('projectManagement.assignment.openMeeting') }}
                  </button>
                  <span v-else>{{ t('projectManagement.assignment.noMeeting') }}</span>
                </template>
              </div>
            </div>
            <v-menu location="bottom end">
              <template #activator="{ props: menuProps }">
                <v-chip
                  v-bind="menuProps"
                  size="small"
                  variant="tonal"
                  :color="chipColor(row)"
                  :disabled="statusBusyId === row.id"
                  @click.stop
                >
                  {{ statusLabel(row) }}
                  <v-icon icon="mdi-menu-down" end size="16" />
                </v-chip>
              </template>
              <v-list density="compact" min-width="160">
                <v-list-item
                  v-for="item in statusItems"
                  :key="item.value"
                  :active="row.status === item.value"
                  @click="chooseStatus(row, item.value)"
                >
                  <v-list-item-title>{{ item.title }}</v-list-item-title>
                </v-list-item>
              </v-list>
            </v-menu>
          </div>
        </v-card>
      </section>
    </div>
    <div v-else-if="!(loading || listLoading)" class="text-medium-emphasis">
      {{ t('projectManagement.assignment.empty') }}
    </div>

    <v-dialog v-model="dialog" max-width="720">
      <v-card rounded="lg">
        <v-card-title>
          {{ editingId ? t('projectManagement.assignment.edit') : t('projectManagement.assignment.add') }}
        </v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <div class="d-flex flex-wrap ga-3">
            <v-text-field
              v-model="form.code"
              :label="t('projectManagement.meeting.actionCode')"
              maxlength="32"
              density="comfortable"
              style="max-width: 140px"
            />
            <v-text-field
              v-model="form.title"
              :label="t('projectManagement.meeting.action')"
              density="comfortable"
              class="flex-grow-1"
            />
          </div>
          <p class="text-caption text-medium-emphasis mb-0">{{ t('projectManagement.meeting.ownerHint') }}</p>
          <MngDirectoryPickerField
            v-model="form.userIds"
            entity="user"
            multiple
            :external-picker="userPicker"
            :label="t('projectManagement.meeting.targetUsers')"
          />
          <v-autocomplete
            v-model="form.personIds"
            :items="personItems"
            item-title="name"
            item-value="id"
            :label="t('projectManagement.meeting.targetPeople')"
            multiple
            chips
            closable-chips
            clearable
            density="comfortable"
          />
          <v-combobox
            v-model="form.organizations"
            :items="firmOptions"
            :label="t('projectManagement.meeting.targetFirms')"
            :hint="t('projectManagement.meeting.targetFirmsHint')"
            persistent-hint
            multiple
            chips
            closable-chips
            clearable
            density="comfortable"
          />
          <v-select
            v-model="form.wbsId"
            :items="wbsItems"
            :label="t('projectManagement.fields.wbsCode')"
            density="comfortable"
          />
          <v-select
            v-model="form.status"
            :items="statusItems"
            :label="t('projectManagement.fields.status')"
            density="comfortable"
          />
          <v-text-field
            v-model="form.dueDate"
            type="date"
            :label="t('projectManagement.meeting.dueDate')"
            density="comfortable"
          />
          <v-textarea
            v-model="form.note"
            :label="t('projectManagement.meeting.actionNote')"
            density="comfortable"
            rows="2"
            auto-grow
          />
        </v-card-text>
        <v-card-actions>
          <v-btn v-if="editingId" variant="text" color="error" @click="askDeleteEditing">
            <TrashIcon size="16" class="mr-1" />
            {{ t('projectManagement.delete') }}
          </v-btn>
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" :disabled="!canSave" @click="save">
            {{ t('projectManagement.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="Boolean(waiveTarget)" max-width="480" @update:model-value="waiveTarget = null">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.assignment.waiveTitle') }}</v-card-title>
        <v-card-text>
          <p class="text-body-2 text-medium-emphasis mb-3">{{ t('projectManagement.assignment.waiveHint') }}</p>
          <v-textarea v-model="waiveNote" :label="t('projectManagement.meeting.actionNote')" rows="3" auto-grow />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="waiveTarget = null">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn
            color="primary"
            :disabled="!waiveNote.trim()"
            :loading="statusBusyId === waiveTarget?.id"
            @click="waiveTarget && applyStatus(waiveTarget, 'waived', waiveNote.trim())"
          >
            {{ t('projectManagement.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="Boolean(deleteTarget)" max-width="440" @update:model-value="deleteTarget = null">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.assignment.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.assignment.deleteConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteTarget = null">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="error" :loading="deleting" @click="remove">{{ t('projectManagement.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.duty-counts {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 12px;
}

.duty-count {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 2px;
  width: 100%;
  padding: 12px 14px;
  border-radius: 12px;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.12);
  background: rgb(var(--v-theme-surface));
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
}

.duty-count__n {
  font-size: 1.5rem;
  font-weight: 600;
  line-height: 1.1;
}

.duty-count__label {
  font-size: 0.8125rem;
  color: rgba(var(--v-theme-on-surface), 0.7);
}

.duty-count--info .duty-count__n { color: rgb(var(--v-theme-info)); }
.duty-count--primary .duty-count__n { color: rgb(var(--v-theme-primary)); }
.duty-count--error .duty-count__n { color: rgb(var(--v-theme-error)); }
.duty-count--success .duty-count__n { color: rgb(var(--v-theme-success)); }
.duty-count--secondary .duty-count__n { color: rgb(var(--v-theme-secondary)); }

.duty-count--info.is-on { border-color: rgb(var(--v-theme-info)); background: rgba(var(--v-theme-info), 0.08); }
.duty-count--primary.is-on { border-color: rgb(var(--v-theme-primary)); background: rgba(var(--v-theme-primary), 0.08); }
.duty-count--error.is-on { border-color: rgb(var(--v-theme-error)); background: rgba(var(--v-theme-error), 0.08); }
.duty-count--success.is-on { border-color: rgb(var(--v-theme-success)); background: rgba(var(--v-theme-success), 0.08); }
.duty-count--secondary.is-on { border-color: rgb(var(--v-theme-secondary)); background: rgba(var(--v-theme-secondary), 0.08); }

.duty-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  cursor: pointer;
}

.duty-row + .duty-row {
  border-top: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}

.duty-row:hover {
  background: rgba(var(--v-theme-on-surface), 0.04);
}

.duty-row__main {
  flex: 1;
  min-width: 0;
}

.duty-row__title {
  line-height: 1.35;
}

.duty-link {
  padding: 0;
  border: 0;
  background: none;
  color: rgb(var(--v-theme-primary));
  font: inherit;
  cursor: pointer;
}

@media (max-width: 720px) {
  .duty-counts {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
