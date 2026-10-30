<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import DiPickResourceDialog from '@/components/apps/document-intelligence/DiPickResourceDialog.vue';
import PmMeetingDocField from '@/components/apps/project-management/PmMeetingDocField.vue';
import MtMeetingCalendar from '@/components/apps/meetings/MtMeetingCalendar.vue';
import MtMinutesImportDialog from '@/components/apps/meetings/MtMinutesImportDialog.vue';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { diCreateMarkdown, diGetById, diGetMarkdownContent, diUpdateMarkdown } from '@/services/documentIntelligenceService';
import {
  createOrgMeeting,
  createOrgMeetingAction,
  createOrgMeetingPerson,
  createOrgMeetingSeries,
  deleteOrgMeeting,
  deleteOrgMeetingAction,
  deleteOrgMeetingPerson,
  deleteOrgMeetingSeries,
  endOrgMeetingSeries,
  extendOpenSeries,
  istanbulYmd,
  listOrgMeetingActions,
  listOrgMeetingAttendance,
  listOrgMeetingPeople,
  listOrgMeetingSeries,
  listOrgMeetings,
  syncOrgMeetingAttendance,
  updateOrgMeeting,
  updateOrgMeetingAction,
  updateOrgMeetingSeries,
  type OrgAttendanceDraft,
  type OrgAttendancePresence,
  type OrgMeeting,
  type OrgMeetingAttendance,
  type OrgMeetingAction,
  type OrgMeetingActionStatus,
  type OrgMeetingPerson,
  type OrgMeetingSeries,
  type OrgMeetingStatus,
} from '@/services/meetingsService';
import { useUserStore, type User } from '@/stores/apps/user';
import type { DiResource } from '@/types/apps/documentIntelligence';
import { diPageResourceLabel } from '@/utils/diPageResource';
import { TrashIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

type ListFilter = 'upcoming' | 'past' | 'all';
type Surface = 'list' | 'calendar';
type DocTarget = 'minutes' | 'agenda' | 'seriesAgenda';
type DocSource = 'write' | 'existing';
type MeetingDoc = {
  source: DocSource;
  body: string;
  resourceId: string;
  resourceName: string;
  loading: boolean;
  canSync: boolean;
  version: number;
};

function emptyDoc(): MeetingDoc {
  return { source: 'write', body: '', resourceId: '', resourceName: '', loading: false, canSync: false, version: 0 };
}

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const userStore = useUserStore();

const loading = ref(false);
const saving = ref(false);
const meetings = ref<OrgMeeting[]>([]);
const people = ref<OrgMeetingPerson[]>([]);
const seriesRows = ref<OrgMeetingSeries[]>([]);
const personNames = ref<Record<string, string>>({});
const filter = ref<ListFilter>('upcoming');
const surface = ref<Surface>('list');

const dialogOpen = ref(false);
const editingId = ref<string | null>(null);
const title = ref('');
const startAt = ref('');
const endAt = ref('');
const status = ref<OrgMeetingStatus>('scheduled');
const location = ref('');
const meetingUrl = ref('');
const organizerId = ref<string | null>(null);
const agenda = ref('');
const agendaResourceId = ref<string | null>(null);
const minutesResourceId = ref<string | null>(null);
const agendaDoc = ref<MeetingDoc>(emptyDoc());
const minutesDoc = ref<MeetingDoc>(emptyDoc());
const seriesId = ref<string | null>(null);
const occurrenceDate = ref<string | null>(null);
const detached = ref(false);
const originalStart = ref('');
const originalEnd = ref('');
const pickerOpen = ref(false);
const pickerTarget = ref<DocTarget>('minutes');
const attendance = ref<OrgAttendanceDraft[]>([]);
const attendanceExisting = ref<OrgMeetingAttendance[]>([]);
const pendingUserIds = ref<string[]>([]);
const pendingPersonIds = ref<string[]>([]);
const actions = ref<OrgMeetingAction[]>([]);
const actionsLoading = ref(false);
const actionTitle = ref('');
const actionAssigneeId = ref<string | null>(null);
const actionDueAt = ref('');
const deleteOpen = ref(false);
const deleting = ref(false);
const deleteSeriesOpen = ref(false);
const importOpen = ref(false);

const seriesOpen = ref(false);
const seriesSaving = ref(false);
const editingSeriesId = ref<string | null>(null);
const seriesName = ref('');
const seriesPattern = ref<'once' | 'weekdays'>('once');
const seriesOpenEnded = ref(false);
const seriesWeekday = ref(1);
const seriesTime = ref('09:00');
const seriesDuration = ref(60);
const seriesFirst = ref('');
const seriesUntil = ref('');
const seriesLocation = ref('');
const seriesUrl = ref('');
const seriesOrganizerId = ref<string | null>(null);
const seriesAgenda = ref('');
const seriesAgendaResourceId = ref<string | null>(null);
const seriesAgendaName = ref('');
const seriesUserIds = ref<string[]>([]);
const seriesPersonIds = ref<string[]>([]);
const seriesFrom = ref('');

const personOpen = ref(false);
const personName = ref('');
const personOrganization = ref('');
const personEmail = ref('');
const personNote = ref('');
const personSaving = ref(false);

const page = computed(() => ({ title: t('meetings.title') }));
const breadcrumbs = computed(() => [
  { text: t('breadcrumbs.home'), disabled: false, href: '/' },
  { text: t('meetings.title'), disabled: true, href: '#' },
]);
const statusItems = computed(() => [
  { title: t('meetings.status.scheduled'), value: 'scheduled' as OrgMeetingStatus },
  { title: t('meetings.status.held'), value: 'held' as OrgMeetingStatus },
  { title: t('meetings.status.cancelled'), value: 'cancelled' as OrgMeetingStatus },
]);
const presenceItems = computed(() =>
  (['invited', 'confirmed', 'attended', 'absent', 'excused'] as OrgAttendancePresence[]).map((value) => ({
    title: t(`meetings.presence.${value}`),
    value,
  })),
);
const weekdayItems = computed(() =>
  [1, 2, 3, 4, 5, 6, 7].map((value) => ({ title: t(`meetings.weekday.${value}`), value })),
);
const personItems = computed(() =>
  people.value.map((person) => ({
    title: person.organization ? `${person.name} (${person.organization})` : person.name,
    value: person.id,
  })),
);
const headers = computed(() => [
  { title: t('meetings.columns.title'), key: 'title' },
  { title: t('meetings.columns.when'), key: 'when', width: 220 },
  { title: t('meetings.columns.duration'), key: 'duration', width: 110 },
  { title: t('meetings.columns.status'), key: 'status', width: 140 },
  { title: t('meetings.columns.organizer'), key: 'organizer', width: 180 },
  { title: t('meetings.columns.place'), key: 'place', width: 180 },
  { title: '', key: 'actions', width: 64, sortable: false },
]);
const visibleMeetings = computed(() => {
  const now = Date.now();
  const rows = meetings.value.filter((meeting) => {
    const start = Date.parse(meeting.startAt);
    if (filter.value === 'all' || Number.isNaN(start)) return filter.value === 'all' || filter.value === 'upcoming';
    return filter.value === 'upcoming' ? start >= now : start < now;
  });
  return [...rows].sort((a, b) => {
    const left = Date.parse(a.startAt);
    const right = Date.parse(b.startAt);
    if (Number.isNaN(left) || Number.isNaN(right)) return a.title.localeCompare(b.title, 'tr');
    return filter.value === 'upcoming' ? left - right : right - left;
  });
});

function rowOf(item: OrgMeeting | { raw?: OrgMeeting }): OrgMeeting {
  if (item && typeof item === 'object' && 'raw' in item && item.raw) return item.raw;
  return item as OrgMeeting;
}

function personLabel(id: string | null | undefined): string {
  if (!id) return '—';
  return personNames.value[id] || id;
}

function attendanceLabel(row: OrgAttendanceDraft): string {
  if (row.kind === 'user') return personLabel(row.userId) === row.userId ? row.displayName : personLabel(row.userId);
  return row.displayName;
}

function formatWhen(value: string): string {
  const parsed = Date.parse(value);
  if (Number.isNaN(parsed)) return value || '—';
  return new Date(parsed).toLocaleString();
}

function formatDuration(meeting: OrgMeeting): string {
  const start = Date.parse(meeting.startAt);
  const end = meeting.endAt ? Date.parse(meeting.endAt) : NaN;
  if (Number.isNaN(start) || Number.isNaN(end) || end <= start) return '—';
  const minutes = Math.round((end - start) / 60000);
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours && rest) return t('meetings.durationHoursMinutes', { hours, minutes: rest });
  if (hours) return t('meetings.durationHours', { hours });
  return t('meetings.durationMinutes', { minutes: rest });
}

function statusColor(value: OrgMeetingStatus): string {
  if (value === 'held') return 'success';
  if (value === 'cancelled') return 'error';
  return 'info';
}

function toLocalInput(value: string | null | undefined): string {
  if (!value) return '';
  const parsed = Date.parse(value);
  if (Number.isNaN(parsed)) return value.slice(0, 16);
  const date = new Date(parsed);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function idsOf(value: unknown): string[] {
  if (Array.isArray(value)) return value.map((item) => String(item).trim()).filter(Boolean);
  if (typeof value === 'string' && value.trim()) return [value.trim()];
  return [];
}

function keeperUserLabel(user: User): string {
  const name = `${user.firstName || ''} ${user.lastName || ''}`.trim();
  return name || user.username || user.email || user.id;
}

async function rememberPeople(ids: string[]) {
  const missing = [...new Set(ids.map((id) => id.trim()).filter(Boolean))].filter((id) => !personNames.value[id]);
  if (!missing.length) return;
  try {
    const users = await userStore.fetchUsersByIds(missing);
    const next = { ...personNames.value };
    for (const user of users) {
      const label = keeperUserLabel(user);
      if (user.id) next[user.id] = label;
      if (user.userId) next[user.userId] = label;
    }
    personNames.value = next;
  } catch {
    // Names stay as ids when Keeper cannot resolve them.
  }
}

async function load() {
  loading.value = true;
  try {
    const [meetingRows, personRows, series] = await Promise.all([
      listOrgMeetings(),
      listOrgMeetingPeople(),
      listOrgMeetingSeries(),
    ]);
    meetings.value = meetingRows;
    people.value = personRows;
    seriesRows.value = series;
    const ids = meetingRows.flatMap((meeting) => [meeting.organizerId].filter(Boolean) as string[]);
    await rememberPeople(ids);
  } catch (error) {
    panelError(error, 'meetings.loadFailed');
  } finally {
    loading.value = false;
  }
}

function resetForm() {
  editingId.value = null;
  title.value = '';
  startAt.value = '';
  endAt.value = '';
  status.value = 'scheduled';
  location.value = '';
  meetingUrl.value = '';
  organizerId.value = null;
  agenda.value = '';
  agendaResourceId.value = null;
  minutesResourceId.value = null;
  agendaDoc.value = emptyDoc();
  minutesDoc.value = emptyDoc();
  seriesId.value = null;
  occurrenceDate.value = null;
  detached.value = false;
  originalStart.value = '';
  originalEnd.value = '';
  attendance.value = [];
  attendanceExisting.value = [];
  pendingUserIds.value = [];
  pendingPersonIds.value = [];
  actions.value = [];
  actionTitle.value = '';
  actionAssigneeId.value = null;
  actionDueAt.value = '';
}

function openCreate(range?: { start: string; end: string }) {
  resetForm();
  if (range) {
    startAt.value = range.start;
    endAt.value = range.end;
  }
  dialogOpen.value = true;
}

async function labelResource(id: string | null): Promise<string> {
  if (!id) return '';
  try {
    const resource = await diGetById(id);
    return diPageResourceLabel(resource);
  } catch {
    return id;
  }
}

async function openEdit(meeting: OrgMeeting) {
  resetForm();
  editingId.value = meeting.id;
  title.value = meeting.title;
  startAt.value = toLocalInput(meeting.startAt);
  endAt.value = toLocalInput(meeting.endAt);
  originalStart.value = startAt.value;
  originalEnd.value = endAt.value;
  status.value = meeting.status;
  location.value = meeting.location;
  meetingUrl.value = meeting.meetingUrl;
  organizerId.value = meeting.organizerId;
  agenda.value = meeting.agenda;
  agendaResourceId.value = meeting.agendaResourceId;
  minutesResourceId.value = meeting.minutesResourceId;
  seriesId.value = meeting.seriesId;
  occurrenceDate.value = meeting.occurrenceDate;
  detached.value = meeting.detached;
  dialogOpen.value = true;
  agendaDoc.value = { ...emptyDoc(), body: meeting.agenda };
  minutesDoc.value = emptyDoc();
  const series = seriesRows.value.find((row) => row.id === meeting.seriesId);
  const sharedAgenda = Boolean(series && meeting.agendaResourceId && meeting.agendaResourceId === series.agendaResourceId);
  await hydrateMeetingDoc(agendaDoc, meeting.agendaResourceId, meeting.id, meeting.agenda, sharedAgenda);
  await hydrateMeetingDoc(minutesDoc, meeting.minutesResourceId, meeting.id, '', false);
  try {
    const rows = await listOrgMeetingAttendance(meeting.id);
    attendance.value = rows.map((row) => ({ ...row }));
    attendanceExisting.value = rows.map((row) => ({ ...row }));
    await rememberPeople(rows.map((row) => row.userId).filter(Boolean) as string[]);
  } catch (error) {
    panelError(error, 'meetings.attendanceLoadFailed');
  }
  await loadActions(meeting.id);
}

function currentInput(detachedNow = detached.value) {
  return {
    title: title.value.trim(),
    startAt: startAt.value,
    endAt: endAt.value || null,
    status: status.value,
    location: location.value,
    meetingUrl: meetingUrl.value,
    organizerId: organizerId.value,
    agenda: agenda.value,
    agendaResourceId: agendaResourceId.value,
    minutesResourceId: minutesResourceId.value,
    seriesId: seriesId.value,
    occurrenceDate: occurrenceDate.value,
    detached: detachedNow,
  };
}

function sameIds(left: Array<string | null | undefined>, right: string[]): boolean {
  const a = left.filter((id): id is string => Boolean(id)).slice().sort();
  const b = right.slice().sort();
  return a.length === b.length && a.every((id, index) => id === b[index]);
}

function occurrenceIsException(agendaText: string, agendaId: string | null, minutesId: string | null): boolean {
  if (!seriesId.value) return false;
  const series = seriesRows.value.find((row) => row.id === seriesId.value);
  const timeChanged = startAt.value !== originalStart.value || endAt.value !== originalEnd.value;
  if (timeChanged || minutesId || actions.value.length) return true;
  if (attendance.value.some((row) => row.presence !== 'invited')) return true;
  if (!series) return true;
  const ownAgenda = agendaText.trim() !== (series.agenda || '').trim()
    || (agendaId || null) !== (series.agendaResourceId || null);
  if (ownAgenda) return true;
  return !sameIds(attendance.value.filter((row) => row.kind === 'user').map((row) => row.userId), series.memberUserIds)
    || !sameIds(attendance.value.filter((row) => row.kind === 'external').map((row) => row.personId), series.memberPersonIds);
}

async function hydrateMeetingDoc(
  doc: { value: MeetingDoc },
  resourceId: string | null,
  meetingId: string,
  fallbackBody: string,
  sharedSeriesPage: boolean,
) {
  doc.value.body = fallbackBody;
  if (!resourceId) return;
  doc.value.loading = true;
  try {
    const md = await diGetMarkdownContent(resourceId);
    if (editingId.value !== meetingId) return;
    doc.value.body = md.content || fallbackBody;
    doc.value.source = 'write';
    if (sharedSeriesPage) return;
    doc.value.resourceId = resourceId;
    if (md.title) doc.value.resourceName = md.title;
    doc.value.canSync = true;
    doc.value.version = md.currentVersionNumber || 0;
  } catch {
    if (editingId.value !== meetingId) return;
    doc.value.resourceId = resourceId;
    doc.value.source = 'existing';
    doc.value.resourceName = await labelResource(resourceId);
  } finally {
    if (editingId.value === meetingId) doc.value.loading = false;
  }
}

async function persistWrittenDoc(doc: MeetingDoc, pageTitle: string): Promise<string | null> {
  const body = doc.body.trim();
  if (doc.source === 'existing') return doc.resourceId.trim() || null;
  if (!body) return null;
  if (doc.canSync && doc.resourceId) {
    const updated = await diUpdateMarkdown(doc.resourceId, {
      title: pageTitle,
      content: body,
      expectedVersionNumber: doc.version,
      isDraft: false,
    });
    doc.version = updated.currentVersionNumber || doc.version;
    doc.resourceName = diPageResourceLabel(updated) || pageTitle;
    return updated.id;
  }
  const created = await diCreateMarkdown({ parentId: null, title: pageTitle, content: body, isDraft: false });
  doc.canSync = true;
  doc.resourceId = created.id;
  doc.version = created.currentVersionNumber || 0;
  doc.resourceName = diPageResourceLabel(created) || pageTitle;
  return created.id;
}

async function persistedDocs(): Promise<{ agendaText: string; agendaId: string | null; minutesId: string | null }> {
  const name = title.value.trim();
  const series = seriesRows.value.find((row) => row.id === seriesId.value) || null;
  let agendaText = agendaDoc.value.body.trim();
  let agendaId: string | null = null;
  if (agendaDoc.value.source === 'existing') {
    agendaId = agendaDoc.value.resourceId.trim() || null;
    agendaText = agenda.value.trim();
  } else if (!agendaText) {
    agendaText = '';
  } else if (series && !agendaDoc.value.resourceId && agendaText === (series.agenda || '').trim()) {
    agendaId = series.agendaResourceId;
    agendaText = series.agenda;
  } else {
    agendaId = await persistWrittenDoc(agendaDoc.value, t('meetings.agendaPageTitle', { name }));
    agendaText = agendaDoc.value.body.trim().slice(0, 4000);
  }
  const minutesId = await persistWrittenDoc(minutesDoc.value, t('meetings.minutesPageTitle', { name }));
  return { agendaText, agendaId, minutesId };
}

function formErrorKey(): string | null {
  if (!title.value.trim()) return 'meetings.validation.title';
  if (!startAt.value) return 'meetings.validation.start';
  if (endAt.value && Date.parse(endAt.value) <= Date.parse(startAt.value)) return 'meetings.validation.end';
  return null;
}

async function save() {
  const problem = formErrorKey();
  if (problem) {
    panelError(new Error(problem), problem);
    return;
  }
  saving.value = true;
  try {
    const docs = await persistedDocs();
    const input = currentInput(occurrenceIsException(docs.agendaText, docs.agendaId, docs.minutesId));
    input.agenda = docs.agendaText;
    input.agendaResourceId = docs.agendaId;
    input.minutesResourceId = docs.minutesId;
    if (editingId.value) {
      await updateOrgMeeting(editingId.value, input);
    } else {
      editingId.value = await createOrgMeeting(input);
    }
    await syncOrgMeetingAttendance(editingId.value, attendance.value, attendanceExisting.value);
    dialogOpen.value = false;
    await load();
  } catch (error) {
    panelError(error, 'meetings.saveFailed');
  } finally {
    saving.value = false;
  }
}

function openPicker(target: DocTarget) {
  pickerTarget.value = target;
  pickerOpen.value = true;
}

function onPicked(resource: DiResource) {
  const label = diPageResourceLabel(resource);
  if (pickerTarget.value === 'agenda') {
    agendaDoc.value.resourceId = resource.id;
    agendaDoc.value.resourceName = label;
    agendaDoc.value.source = 'existing';
    agendaDoc.value.canSync = false;
  } else if (pickerTarget.value === 'minutes') {
    minutesDoc.value.resourceId = resource.id;
    minutesDoc.value.resourceName = label;
    minutesDoc.value.source = 'existing';
    minutesDoc.value.canSync = false;
  } else if (pickerTarget.value === 'seriesAgenda') {
    seriesAgendaResourceId.value = resource.id;
    seriesAgendaName.value = label;
  }
  pickerOpen.value = false;
}

watch(pendingUserIds, (ids) => {
  const list = idsOf(ids);
  if (!list.length) return;
  pendingUserIds.value = [];
  const fresh = list.filter((id) => !attendance.value.some((row) => row.kind === 'user' && row.userId === id));
  void rememberPeople(fresh).then(() => {
    for (const id of fresh) {
      attendance.value.push({
        meetingId: editingId.value || '',
        kind: 'user',
        userId: id,
        displayName: personNames.value[id] || id,
        presence: 'invited',
      });
    }
  });
});

function addExternal() {
  const ids = idsOf(pendingPersonIds.value);
  pendingPersonIds.value = [];
  for (const id of ids) {
    if (attendance.value.some((row) => row.kind === 'external' && row.personId === id)) continue;
    const person = people.value.find((row) => row.id === id);
    if (!person) continue;
    attendance.value.push({
      meetingId: editingId.value || '',
      kind: 'external',
      personId: person.id,
      displayName: person.name,
      organization: person.organization,
      email: person.email,
      presence: 'invited',
    });
  }
}

function removeAttendance(index: number) {
  attendance.value.splice(index, 1);
}

async function loadActions(meetingId: string) {
  actionsLoading.value = true;
  try {
    actions.value = await listOrgMeetingActions(meetingId);
    await rememberPeople(actions.value.map((action) => action.assigneeId).filter(Boolean) as string[]);
  } catch (error) {
    panelError(error, 'meetings.actionsLoadFailed');
  } finally {
    actionsLoading.value = false;
  }
}

async function addAction() {
  if (!editingId.value || !actionTitle.value.trim()) return;
  actionsLoading.value = true;
  try {
    await createOrgMeetingAction({
      meetingId: editingId.value,
      title: actionTitle.value.trim(),
      assigneeId: actionAssigneeId.value,
      dueAt: actionDueAt.value || null,
      status: 'open',
    });
    actionTitle.value = '';
    actionAssigneeId.value = null;
    actionDueAt.value = '';
    await loadActions(editingId.value);
  } catch (error) {
    panelError(error, 'meetings.actionsSaveFailed');
  } finally {
    actionsLoading.value = false;
  }
}

async function toggleAction(action: OrgMeetingAction) {
  if (!editingId.value) return;
  const next: OrgMeetingActionStatus = action.status === 'done' ? 'open' : 'done';
  try {
    await updateOrgMeetingAction(action.id, {
      meetingId: action.meetingId,
      title: action.title,
      assigneeId: action.assigneeId,
      dueAt: action.dueAt,
      status: next,
      note: action.note,
    });
    await loadActions(editingId.value);
  } catch (error) {
    panelError(error, 'meetings.actionsSaveFailed');
  }
}

async function removeAction(action: OrgMeetingAction) {
  if (!editingId.value) return;
  try {
    await deleteOrgMeetingAction(action.id);
    await loadActions(editingId.value);
  } catch (error) {
    panelError(error, 'meetings.actionsSaveFailed');
  }
}

function askDelete(meeting: OrgMeeting) {
  editingId.value = meeting.id;
  title.value = meeting.title;
  deleteOpen.value = true;
}

async function confirmDelete() {
  if (!editingId.value) return;
  deleting.value = true;
  try {
    await deleteOrgMeeting(editingId.value);
    deleteOpen.value = false;
    dialogOpen.value = false;
    await load();
  } catch (error) {
    panelError(error, 'meetings.deleteFailed');
  } finally {
    deleting.value = false;
  }
}

function resetSeries() {
  editingSeriesId.value = null;
  seriesName.value = '';
  seriesPattern.value = 'once';
  seriesOpenEnded.value = false;
  seriesWeekday.value = 1;
  seriesTime.value = '09:00';
  seriesDuration.value = 60;
  seriesFirst.value = '';
  seriesUntil.value = '';
  seriesLocation.value = '';
  seriesUrl.value = '';
  seriesOrganizerId.value = null;
  seriesAgenda.value = '';
  seriesAgendaResourceId.value = null;
  seriesAgendaName.value = '';
  seriesUserIds.value = [];
  seriesPersonIds.value = [];
  seriesFrom.value = '';
}

function seriesFromMin(): string {
  const today = istanbulYmd(new Date());
  if (seriesFirst.value && seriesFirst.value > today) return seriesFirst.value;
  return today;
}

function clampSeriesFrom(value: string): string {
  const min = seriesFromMin();
  return value && value > min ? value : min;
}

function openSeries() {
  resetSeries();
  seriesOpen.value = true;
}

async function editSeries(row: OrgMeetingSeries, fromDate?: string) {
  editingSeriesId.value = row.id;
  seriesName.value = row.name;
  const days = row.weekdays.length ? row.weekdays : [row.weekday];
  seriesPattern.value = [1, 2, 3, 4, 5].every((day) => days.includes(day)) && days.length === 5 ? 'weekdays' : 'once';
  seriesOpenEnded.value = !row.until;
  seriesWeekday.value = row.weekday;
  seriesTime.value = row.startTime;
  seriesDuration.value = row.durationMinutes;
  seriesFirst.value = row.anchorStart ? toLocalInput(row.anchorStart).slice(0, 10) : '';
  seriesUntil.value = row.until;
  seriesLocation.value = row.location;
  seriesUrl.value = row.meetingUrl;
  seriesOrganizerId.value = row.organizerId;
  seriesAgenda.value = row.agenda;
  seriesAgendaResourceId.value = row.agendaResourceId;
  seriesAgendaName.value = await labelResource(row.agendaResourceId);
  seriesUserIds.value = [...row.memberUserIds];
  seriesPersonIds.value = [...row.memberPersonIds];
  seriesFrom.value = clampSeriesFrom(fromDate || istanbulYmd(new Date()));
}

const seriesForwardPreview = computed(() => {
  if (!editingSeriesId.value || !seriesFrom.value) return null;
  const rows = meetings.value.filter((meeting) => meeting.seriesId === editingSeriesId.value);
  const update = rows.filter((meeting) => (
    meeting.occurrenceDate
    && meeting.occurrenceDate >= seriesFrom.value
    && !meeting.detached
    && meeting.status === 'scheduled'
    && !meeting.minutesResourceId
  )).length;
  return { update, keep: rows.length - update };
});

async function cancelThisOccurrence() {
  status.value = 'cancelled';
  await save();
}

async function editSeriesFromHere() {
  const row = seriesRows.value.find((item) => item.id === seriesId.value);
  if (!row) return;
  const day = occurrenceDate.value || toLocalInput(startAt.value).slice(0, 10);
  dialogOpen.value = false;
  await editSeries(row, day);
  seriesOpen.value = true;
}

function seriesErrorKey(): string | null {
  if (!seriesName.value.trim()) return 'meetings.validation.title';
  if (!seriesFirst.value) return 'meetings.validation.seriesRange';
  if (!seriesOpenEnded.value && (!seriesUntil.value || seriesUntil.value < seriesFirst.value)) return 'meetings.validation.seriesRange';
  return null;
}

function seriesInput() {
  return {
    name: seriesName.value.trim(),
    weekday: seriesPattern.value === 'weekdays' ? 1 : seriesWeekday.value,
    weekdays: seriesPattern.value === 'weekdays' ? [1, 2, 3, 4, 5] : [seriesWeekday.value],
    startTime: seriesTime.value,
    durationMinutes: Number(seriesDuration.value) || 60,
    firstDate: seriesFirst.value,
    until: seriesOpenEnded.value ? null : seriesUntil.value,
    location: seriesLocation.value,
    meetingUrl: seriesUrl.value,
    organizerId: seriesOrganizerId.value,
    agenda: seriesAgenda.value,
    agendaResourceId: seriesAgendaResourceId.value,
    memberUserIds: idsOf(seriesUserIds.value),
    memberPersonIds: idsOf(seriesPersonIds.value),
  };
}

async function saveSeries() {
  const problem = seriesErrorKey();
  if (problem) {
    panelError(new Error(problem), problem);
    return;
  }
  seriesSaving.value = true;
  try {
    const input = seriesInput();
    if (editingSeriesId.value) {
      const existing = seriesRows.value.find((row) => row.id === editingSeriesId.value);
      if (!existing) return;
      await updateOrgMeetingSeries(editingSeriesId.value, input, existing, clampSeriesFrom(seriesFrom.value));
    } else {
      await createOrgMeetingSeries(input);
    }
    seriesOpen.value = false;
    await load();
  } catch (error) {
    panelError(error, 'meetings.seriesSaveFailed');
  } finally {
    seriesSaving.value = false;
  }
}

async function removeSeries() {
  if (!editingSeriesId.value) return;
  seriesSaving.value = true;
  try {
    await deleteOrgMeetingSeries(editingSeriesId.value);
    deleteSeriesOpen.value = false;
    seriesOpen.value = false;
    await load();
  } catch (error) {
    panelError(error, 'meetings.seriesSaveFailed');
  } finally {
    seriesSaving.value = false;
  }
}

async function finishSeries() {
  if (!editingSeriesId.value) return;
  seriesSaving.value = true;
  try {
    await endOrgMeetingSeries(editingSeriesId.value);
    seriesOpen.value = false;
    await load();
  } catch (error) {
    panelError(error, 'meetings.seriesSaveFailed');
  } finally {
    seriesSaving.value = false;
  }
}

function openPerson() {
  personName.value = '';
  personOrganization.value = '';
  personEmail.value = '';
  personNote.value = '';
  personOpen.value = true;
}

async function savePerson() {
  if (!personName.value.trim()) {
    panelError(new Error('name'), 'meetings.validation.personName');
    return;
  }
  personSaving.value = true;
  try {
    const created = await createOrgMeetingPerson({
      name: personName.value.trim(),
      organization: personOrganization.value,
      email: personEmail.value,
      note: personNote.value,
    });
    people.value = [...people.value, created].sort((a, b) => a.name.localeCompare(b.name, 'tr'));
    pendingPersonIds.value = [...pendingPersonIds.value, created.id];
    personOpen.value = false;
  } catch (error) {
    panelError(error, 'meetings.personSaveFailed');
  } finally {
    personSaving.value = false;
  }
}

async function removePerson(id: string) {
  try {
    await deleteOrgMeetingPerson(id);
    people.value = people.value.filter((row) => row.id !== id);
  } catch (error) {
    const message = error instanceof Error ? error.message : '';
    panelError(error, message === 'PERSON_IN_USE' ? 'meetings.personInUse' : 'meetings.personSaveFailed');
  }
}

const coveredThrough = ref('');

async function onCalendarRange(through: string) {
  if (!through || through <= coveredThrough.value) return;
  const previous = coveredThrough.value;
  coveredThrough.value = through;
  try {
    await extendOpenSeries(through);
    await load();
  } catch (error) {
    coveredThrough.value = previous;
    panelError(error, 'meetings.seriesSaveFailed');
  }
}

onMounted(() => {
  void load();
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-card>
      <v-card-text class="d-flex flex-wrap align-center ga-3">
        <v-btn-toggle v-model="surface" mandatory density="comfortable" divided>
          <v-btn value="list">{{ t('meetings.surface.list') }}</v-btn>
          <v-btn value="calendar">{{ t('meetings.surface.calendar') }}</v-btn>
        </v-btn-toggle>
        <v-btn-toggle v-if="surface === 'list'" v-model="filter" mandatory density="comfortable" divided>
          <v-btn value="upcoming">{{ t('meetings.filters.upcoming') }}</v-btn>
          <v-btn value="past">{{ t('meetings.filters.past') }}</v-btn>
          <v-btn value="all">{{ t('meetings.filters.all') }}</v-btn>
        </v-btn-toggle>
        <v-spacer />
        <v-btn variant="tonal" @click="importOpen = true">{{ t('meetings.import.open') }}</v-btn>
        <v-btn variant="tonal" @click="openSeries">{{ t('meetings.series.open') }}</v-btn>
        <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate()">{{ t('meetings.new') }}</v-btn>
      </v-card-text>

      <MtMeetingCalendar
        v-if="surface === 'calendar'"
        class="pa-4"
        :meetings="meetings"
        @create="openCreate"
        @range="onCalendarRange"
        @open="(id) => { const row = meetings.find((item) => item.id === id); if (row) void openEdit(row); }"
      />

      <v-data-table
        v-else
        :headers="headers"
        :items="visibleMeetings"
        :loading="loading"
        item-value="id"
        density="comfortable"
      >
        <template #item.title="{ item }">
          <button type="button" class="text-primary text-start" @click="openEdit(rowOf(item))">
            {{ rowOf(item).title }}
          </button>
          <v-chip v-if="rowOf(item).seriesId && !rowOf(item).detached" size="x-small" class="ms-2" variant="tonal">
            {{ t('meetings.series.badge') }}
          </v-chip>
          <v-chip v-else-if="rowOf(item).detached" size="x-small" class="ms-2" color="warning" variant="tonal">
            {{ t('meetings.series.detached') }}
          </v-chip>
        </template>
        <template #item.when="{ item }">{{ formatWhen(rowOf(item).startAt) }}</template>
        <template #item.duration="{ item }">{{ formatDuration(rowOf(item)) }}</template>
        <template #item.status="{ item }">
          <v-chip size="small" :color="statusColor(rowOf(item).status)" variant="tonal">
            {{ t(`meetings.status.${rowOf(item).status}`) }}
          </v-chip>
        </template>
        <template #item.organizer="{ item }">{{ personLabel(rowOf(item).organizerId) }}</template>
        <template #item.place="{ item }">{{ rowOf(item).location || rowOf(item).meetingUrl || '—' }}</template>
        <template #item.actions="{ item }">
          <v-btn icon variant="text" :aria-label="t('meetings.delete')" @click="askDelete(rowOf(item))">
            <TrashIcon size="18" />
          </v-btn>
        </template>
        <template #no-data>{{ t('meetings.empty') }}</template>
      </v-data-table>
    </v-card>

    <v-dialog v-model="dialogOpen" max-width="920" scrollable>
      <v-card>
        <v-card-title>{{ editingId ? t('meetings.edit') : t('meetings.new') }}</v-card-title>
        <v-card-text>
          <v-alert v-if="seriesId && !detached" type="info" variant="tonal" class="mb-3">
            {{ t('meetings.series.detachHint') }}
          </v-alert>
          <v-row>
            <v-col cols="12">
              <v-text-field v-model="title" :label="t('meetings.fields.title')" variant="outlined" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="startAt" :label="t('meetings.fields.start')" type="datetime-local" variant="outlined" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="endAt" :label="t('meetings.fields.end')" type="datetime-local" variant="outlined" />
            </v-col>
            <v-col cols="12" md="6">
              <v-select v-model="status" :items="statusItems" :label="t('meetings.fields.status')" variant="outlined" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="location" :label="t('meetings.fields.location')" variant="outlined" />
            </v-col>
            <v-col cols="12">
              <v-text-field v-model="meetingUrl" :label="t('meetings.fields.url')" variant="outlined" />
            </v-col>
            <v-col cols="12">
              <MngDirectoryPickerField v-model="organizerId" entity="user" :label="t('meetings.fields.organizer')" />
            </v-col>
            <v-col cols="12" md="6">
              <div class="text-subtitle-2 mb-2">{{ t('meetings.fields.agenda') }}</div>
              <PmMeetingDocField
                :source="agendaDoc.source"
                :body="agendaDoc.body"
                :resource-id="agendaDoc.resourceId"
                :resource-name="agendaDoc.resourceName"
                :loading="agendaDoc.loading"
                :write-hint="t('meetings.writeAgenda')"
                :empty-label="t('meetings.agendaEmpty')"
                @update:source="agendaDoc.source = $event"
                @update:body="agendaDoc.body = $event"
                @pick="openPicker('agenda')"
                @clear="agendaDoc.resourceId = ''; agendaDoc.resourceName = ''; agendaDoc.canSync = false"
              />
            </v-col>
            <v-col cols="12" md="6">
              <div class="text-subtitle-2 mb-2">{{ t('meetings.fields.minutes') }}</div>
              <PmMeetingDocField
                :source="minutesDoc.source"
                :body="minutesDoc.body"
                :resource-id="minutesDoc.resourceId"
                :resource-name="minutesDoc.resourceName"
                :loading="minutesDoc.loading"
                :write-hint="t('meetings.writeMinutes')"
                :empty-label="t('meetings.minutesEmpty')"
                @update:source="minutesDoc.source = $event"
                @update:body="minutesDoc.body = $event"
                @pick="openPicker('minutes')"
                @clear="minutesDoc.resourceId = ''; minutesDoc.resourceName = ''; minutesDoc.canSync = false"
              />
            </v-col>
          </v-row>

          <v-divider class="my-4" />
          <div class="text-subtitle-1 mb-2">{{ t('meetings.attendanceTitle') }}</div>
          <v-row>
            <v-col cols="12" md="6">
              <MngDirectoryPickerField v-model="pendingUserIds" entity="user" multiple :label="t('meetings.addUser')" />
            </v-col>
            <v-col cols="12" md="6" class="d-flex ga-2 align-start">
              <v-select
                v-model="pendingPersonIds"
                :items="personItems"
                :label="t('meetings.addExternal')"
                variant="outlined"
                multiple
                chips
                clearable
                class="flex-grow-1"
              />
              <v-btn class="mt-1" variant="tonal" @click="addExternal">{{ t('meetings.add') }}</v-btn>
              <v-btn class="mt-1" variant="text" @click="openPerson">{{ t('meetings.newPerson') }}</v-btn>
            </v-col>
          </v-row>
          <v-list v-if="attendance.length" density="compact">
            <v-list-item v-for="(row, index) in attendance" :key="row.id || `${row.kind}-${row.userId || row.personId}`">
              <v-list-item-title>{{ attendanceLabel(row) }}</v-list-item-title>
              <v-list-item-subtitle>{{ row.kind === 'external' ? t('meetings.kind.external') : t('meetings.kind.user') }}</v-list-item-subtitle>
              <template #append>
                <v-select
                  v-model="row.presence"
                  :items="presenceItems"
                  density="compact"
                  variant="outlined"
                  hide-details
                  style="min-width: 150px"
                />
                <v-btn icon variant="text" :aria-label="t('meetings.delete')" @click="removeAttendance(index)">
                  <TrashIcon size="18" />
                </v-btn>
              </template>
            </v-list-item>
          </v-list>
          <p v-else class="text-medium-emphasis">{{ t('meetings.attendanceEmpty') }}</p>

          <v-divider class="my-4" />
          <div class="text-subtitle-1 mb-2">{{ t('meetings.actionsTitle') }}</div>
          <p v-if="!editingId" class="text-medium-emphasis">{{ t('meetings.actionsAfterSave') }}</p>
          <template v-else>
            <v-row>
              <v-col cols="12" md="5">
                <v-text-field v-model="actionTitle" :label="t('meetings.actionTitle')" variant="outlined" density="comfortable" />
              </v-col>
              <v-col cols="12" md="4">
                <MngDirectoryPickerField v-model="actionAssigneeId" entity="user" :label="t('meetings.actionAssignee')" />
              </v-col>
              <v-col cols="12" md="3">
                <v-text-field v-model="actionDueAt" :label="t('meetings.actionDue')" type="date" variant="outlined" density="comfortable" />
              </v-col>
            </v-row>
            <v-btn size="small" color="primary" variant="tonal" :loading="actionsLoading" :disabled="!actionTitle.trim()" @click="addAction">
              {{ t('meetings.addAction') }}
            </v-btn>
            <v-list v-if="actions.length" class="mt-2" density="compact">
              <v-list-item v-for="action in actions" :key="action.id">
                <template #prepend>
                  <v-checkbox-btn :model-value="action.status === 'done'" @update:model-value="toggleAction(action)" />
                </template>
                <v-list-item-title :class="{ 'text-decoration-line-through': action.status === 'done' }">{{ action.title }}</v-list-item-title>
                <v-list-item-subtitle>
                  {{ personLabel(action.assigneeId) }}
                  <span v-if="action.dueAt"> · {{ action.dueAt }}</span>
                  <span v-if="action.note"> · {{ action.note }}</span>
                </v-list-item-subtitle>
                <template #append>
                  <v-btn icon variant="text" :aria-label="t('meetings.delete')" @click="removeAction(action)">
                    <TrashIcon size="18" />
                  </v-btn>
                </template>
              </v-list-item>
            </v-list>
            <p v-else-if="!actionsLoading" class="text-medium-emphasis mt-2">{{ t('meetings.actionsEmpty') }}</p>
          </template>
        </v-card-text>
        <v-card-actions>
          <v-btn v-if="editingId && seriesId && status === 'scheduled'" color="error" variant="text" :loading="saving" @click="cancelThisOccurrence">
            {{ t('meetings.series.cancelOne') }}
          </v-btn>
          <v-btn v-if="seriesId" variant="tonal" @click="editSeriesFromHere">{{ t('meetings.series.editFromHere') }}</v-btn>
          <v-spacer />
          <v-btn variant="text" @click="dialogOpen = false">{{ t('meetings.cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" @click="save">{{ t('meetings.save') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="seriesOpen" max-width="920" scrollable>
      <v-card>
        <v-card-title>{{ editingSeriesId ? t('meetings.series.edit') : t('meetings.series.new') }}</v-card-title>
        <v-card-text>
          <div v-if="seriesRows.length" class="d-flex flex-wrap ga-2 mb-4">
            <v-chip
              v-for="row in seriesRows"
              :key="row.id"
              :color="row.id === editingSeriesId ? 'primary' : undefined"
              variant="tonal"
              @click="editSeries(row)"
            >
              {{ row.name }}
            </v-chip>
            <v-btn size="small" variant="text" @click="resetSeries">{{ t('meetings.series.new') }}</v-btn>
          </div>
          <v-row>
            <v-col cols="12" md="6">
              <v-text-field v-model="seriesName" :label="t('meetings.fields.title')" variant="outlined" />
            </v-col>
            <v-col cols="12" md="6">
              <v-btn-toggle v-model="seriesPattern" mandatory density="comfortable" divided>
                <v-btn value="once">{{ t('meetings.series.once') }}</v-btn>
                <v-btn value="weekdays">{{ t('meetings.series.weekdays') }}</v-btn>
              </v-btn-toggle>
            </v-col>
            <v-col v-if="seriesPattern === 'once'" cols="12" md="6">
              <v-select v-model="seriesWeekday" :items="weekdayItems" :label="t('meetings.series.weekday')" variant="outlined" />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field v-model="seriesTime" :label="t('meetings.series.time')" type="time" variant="outlined" />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field v-model.number="seriesDuration" :label="t('meetings.series.duration')" type="number" min="15" max="480" variant="outlined" />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field v-model="seriesFirst" :label="t('meetings.series.first')" type="date" variant="outlined" :disabled="Boolean(editingSeriesId)" />
            </v-col>
            <v-col v-if="editingSeriesId" cols="12" md="4">
              <v-text-field v-model="seriesFrom" :label="t('meetings.series.from')" type="date" :min="seriesFromMin()" variant="outlined" />
            </v-col>
            <v-col cols="12" md="4">
              <v-switch v-model="seriesOpenEnded" :label="t('meetings.series.openEnded')" color="primary" hide-details />
            </v-col>
            <v-col v-if="!seriesOpenEnded" cols="12" md="4">
              <v-text-field v-model="seriesUntil" :label="t('meetings.series.until')" type="date" variant="outlined" />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field v-model="seriesLocation" :label="t('meetings.fields.location')" variant="outlined" />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field v-model="seriesUrl" :label="t('meetings.fields.url')" variant="outlined" />
            </v-col>
            <v-col cols="12">
              <MngDirectoryPickerField v-model="seriesOrganizerId" entity="user" :label="t('meetings.fields.organizer')" />
            </v-col>
            <v-col cols="12" md="6">
              <MngDirectoryPickerField v-model="seriesUserIds" entity="user" multiple :label="t('meetings.series.users')" />
            </v-col>
            <v-col cols="12" md="6">
              <v-select v-model="seriesPersonIds" :items="personItems" :label="t('meetings.series.people')" multiple chips variant="outlined" />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="seriesAgenda" :label="t('meetings.fields.agenda')" variant="outlined" rows="3" auto-grow />
            </v-col>
            <v-col cols="12">
              <div class="d-flex flex-wrap align-center ga-2">
                <span>{{ seriesAgendaName || t('meetings.agendaEmpty') }}</span>
                <v-btn size="small" variant="tonal" @click="openPicker('seriesAgenda')">{{ t('meetings.pickAgenda') }}</v-btn>
                <v-btn v-if="seriesAgendaResourceId" size="small" variant="text" @click="seriesAgendaResourceId = null; seriesAgendaName = ''">{{ t('meetings.clearDoc') }}</v-btn>
              </div>
            </v-col>
          </v-row>
          <p v-if="seriesForwardPreview" class="text-body-2 mt-2">
            {{ t('meetings.series.preview', seriesForwardPreview) }}
          </p>
          <p class="text-medium-emphasis mt-2">{{ t('meetings.series.hint') }}</p>
        </v-card-text>
        <v-card-actions>
          <v-btn v-if="editingSeriesId" color="error" variant="text" :loading="seriesSaving" @click="finishSeries">
            {{ t('meetings.series.end') }}
          </v-btn>
          <v-btn v-if="editingSeriesId" color="error" variant="text" :loading="seriesSaving" @click="deleteSeriesOpen = true">
            {{ t('meetings.series.remove') }}
          </v-btn>
          <v-spacer />
          <v-btn variant="text" @click="seriesOpen = false">{{ t('meetings.cancel') }}</v-btn>
          <v-btn color="primary" :loading="seriesSaving" @click="saveSeries">{{ t('meetings.save') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="personOpen" max-width="520">
      <v-card>
        <v-card-title>{{ t('meetings.newPerson') }}</v-card-title>
        <v-card-text>
          <v-text-field v-model="personName" :label="t('meetings.person.name')" variant="outlined" />
          <v-text-field v-model="personOrganization" :label="t('meetings.person.organization')" variant="outlined" />
          <v-text-field v-model="personEmail" :label="t('meetings.person.email')" variant="outlined" />
          <v-textarea v-model="personNote" :label="t('meetings.person.note')" variant="outlined" rows="2" />
          <div v-if="people.length" class="d-flex flex-wrap ga-2">
            <v-chip v-for="person in people" :key="person.id" closable @click:close="removePerson(person.id)">
              {{ person.name }}
            </v-chip>
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="personOpen = false">{{ t('meetings.cancel') }}</v-btn>
          <v-btn color="primary" :loading="personSaving" @click="savePerson">{{ t('meetings.save') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <DiPickResourceDialog v-model="pickerOpen" :title="t('meetings.pickMinutes')" @pick="onPicked" />
    <MtMinutesImportDialog v-model="importOpen" @saved="load" />

    <v-dialog v-model="deleteSeriesOpen" max-width="480">
      <v-card>
        <v-card-title>{{ t('meetings.series.removeTitle') }}</v-card-title>
        <v-card-text>{{ t('meetings.series.removeBody') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteSeriesOpen = false">{{ t('meetings.cancel') }}</v-btn>
          <v-btn color="error" :loading="seriesSaving" @click="removeSeries">{{ t('meetings.series.remove') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteOpen" max-width="420">
      <v-card>
        <v-card-title>{{ t('meetings.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('meetings.deleteBody', { title }) }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteOpen = false">{{ t('meetings.cancel') }}</v-btn>
          <v-btn color="error" :loading="deleting" @click="confirmDelete">{{ t('meetings.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
