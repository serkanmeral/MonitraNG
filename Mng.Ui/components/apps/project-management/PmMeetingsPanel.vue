<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import FullCalendar from '@fullcalendar/vue3';
import dayGridPlugin from '@fullcalendar/daygrid';
import timeGridPlugin from '@fullcalendar/timegrid';
import interactionPlugin from '@fullcalendar/interaction';
import type { CalendarOptions, DateSelectArg, DatesSetArg, EventClickArg, EventInput } from '@fullcalendar/core';
import PmMeetingDocField from '@/components/apps/project-management/PmMeetingDocField.vue';
import PmPickDocumentDialog from '@/components/apps/project-management/PmPickDocumentDialog.vue';
import PmPickWorkItemDialog from '@/components/apps/project-management/PmPickWorkItemDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePmDate } from '@/composables/usePmDate';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  diCreateMarkdown,
  diGetById,
  diGetMarkdownContent,
  diUpdateMarkdown,
} from '@/services/documentIntelligenceService';
import { ocGetWorkItemProfile } from '@/services/operationCoreService';
import {
  pmCreateMeeting,
  pmCreateMeetingAction,
  pmCreateMeetingSeries,
  pmDateInput,
  pmDatePayload,
  pmDateTimeInput,
  pmDateTimePayload,
  pmDeleteMeeting,
  pmDeleteMeetingAction,
  pmDeleteMeetingSeries,
  pmGetProjectMeetings,
  pmUpdateMeeting,
  pmUpdateMeetingAction,
  pmUpdateMeetingSeries,
} from '@/services/projectManagementService';
import type { DiResource } from '@/types/apps/documentIntelligence';
import type {
  PmMeeting,
  PmMeetingAction,
  PmMeetingActionStatus,
  PmMeetingSeries,
  PmMeetingStatus,
  PmWbsItem,
  PmWorkItemCandidate,
} from '@/types/apps/projectManagement';
import { diPageResourceLabel } from '@/utils/diPageResource';
import {
  ensureProjectMeetingsFolder,
  PM_LIBRARY_MEETINGS_FOLDER,
  resolvePmLibraryAutoTags,
} from '@/utils/pmProjectLibrary';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

type DocSource = 'write' | 'existing';
type MeetingSurface = 'calendar' | 'list' | 'minutes';
type MeetingDocKind = 'agenda' | 'minutes';
type MeetingDialogTab = 'event' | 'agenda' | 'minutes' | 'actions';
type MeetingDoc = {
  source: DocSource;
  resourceId: string;
  resourceName: string;
  body: string;
  canSync: boolean;
  version: number;
  loading: boolean;
};

const props = defineProps<{
  projectId: string;
  projectCode: string;
  hubFolderId?: string | null;
  wbs: PmWbsItem[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  changed: [];
  hubReady: [id: string];
}>();

const { t, locale } = useAppI18n();
const { formatPmDateOrDash, formatPmDateTime } = usePmDate();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const calendarPlugins = [dayGridPlugin, timeGridPlugin, interactionPlugin];

const surface = ref<MeetingSurface>('calendar');
const pickTarget = ref<MeetingDocKind>('minutes');
const meetingTab = ref<MeetingDialogTab>('event');
const agendaDoc = ref<MeetingDoc>(emptyMeetingDoc());
const minutesDoc = ref<MeetingDoc>(emptyMeetingDoc());

const meetingDialog = ref(false);
const seriesDialog = ref(false);
const actionDialog = ref(false);
const pickOpen = ref(false);
const workPickOpen = ref(false);
const saving = ref(false);
const deleting = ref(false);
const statusBusyId = ref<string | null>(null);
const closingId = ref<string | null>(null);
const editingMeetingId = ref<string | null>(null);
const editingSeriesId = ref<string | null>(null);
const editingActionId = ref<string | null>(null);
const actionMeetingId = ref<string | null>(null);
const deleteMeetingTarget = ref<PmMeeting | null>(null);
const deleteSeriesTarget = ref<PmMeetingSeries | null>(null);
const deleteActionTarget = ref<PmMeetingAction | null>(null);
const cancelTarget = ref<PmMeeting | null>(null);

const meetingForm = ref(emptyMeetingForm());
const seriesForm = ref(emptySeriesForm());
const actionForm = ref(emptyActionForm());
const loadedMeeting = ref<PmMeeting | null>(null);
const seriesRows = ref<PmMeetingSeries[]>([]);
const calendarItems = ref<PmMeeting[]>([]);
const adhocItems = ref<PmMeeting[]>([]);
const occurrenceItems = ref<PmMeeting[]>([]);
const openCount = ref(0);
const overdueCount = ref(0);
const unboundCount = ref(0);
const summaryLoading = ref(false);
const calendarLoading = ref(false);
const adhocLoading = ref(false);
const occurrenceLoading = ref(false);
const calendarRange = ref<{ from: string; to: string } | null>(null);
const adhocSearch = ref('');
const adhocFrom = ref(defaultAdhocFrom());
const adhocTo = ref('');
const adhocPage = ref(1);
const adhocItemsPerPage = ref(25);
const adhocTotal = ref(0);
const occurrencePage = ref(1);
const occurrenceItemsPerPage = ref(25);
const occurrenceTotal = ref(0);
const occurrenceSeries = ref<PmMeetingSeries | null>(null);
const occurrenceDialog = ref(false);
const minutesItems = ref<PmMeeting[]>([]);
const minutesLoading = ref(false);
const minutesSearch = ref('');
const minutesFrom = ref(defaultAdhocFrom());
const minutesTo = ref('');
const minutesPage = ref(1);
const minutesItemsPerPage = ref(25);
const minutesTotal = ref(0);
const minutesPile = ref<'present' | 'missing'>('present');
const minutesSeriesId = ref('');
const pageSizeOptions = [
  { value: 10, title: '10' },
  { value: 25, title: '25' },
  { value: 50, title: '50' },
  { value: 100, title: '100' },
];

const EVENT_STATUS_META: Record<PmMeetingStatus, { icon: string; color: string; hex: string }> = {
  scheduled: { icon: 'mdi-calendar-clock', color: 'primary', hex: '#5D87FF' },
  held: { icon: 'mdi-check-circle-outline', color: 'success', hex: '#13DEB9' },
  cancelled: { icon: 'mdi-calendar-remove', color: 'grey', hex: '#90A4AE' },
};

function emptyMeetingForm() {
  return {
    name: '',
    startAt: '',
    endAt: '',
    wbsId: '',
    location: '',
    meetingUrl: '',
    attendees: '',
    note: '',
    status: 'scheduled' as PmMeetingStatus,
  };
}

function emptyMeetingDoc(body = ''): MeetingDoc {
  return {
    source: 'write',
    resourceId: '',
    resourceName: '',
    body,
    canSync: false,
    version: 0,
    loading: false,
  };
}

function emptySeriesForm() {
  return {
    name: '',
    wbsId: '',
    weekday: 1,
    startTime: '09:00',
    durationMinutes: 60,
    firstStart: '',
    until: '',
    location: '',
    meetingUrl: '',
    agenda: '',
    attendees: '',
    note: '',
  };
}

function emptyActionForm() {
  return {
    title: '',
    ownerName: '',
    dueDate: '',
    status: 'open' as PmMeetingActionStatus,
    workItemId: '',
    workItemKey: '',
    workItemTitle: '',
    wbsId: '',
    note: '',
  };
}

function defaultAdhocFrom() {
  const date = new Date();
  date.setDate(date.getDate() - 30);
  return pmDateInput(date.toISOString());
}

const actionStatusItems = computed(() => [
  { title: t('projectManagement.meeting.status.open'), value: 'open' },
  { title: t('projectManagement.meeting.status.inProgress'), value: 'inProgress' },
  { title: t('projectManagement.meeting.status.done'), value: 'done' },
  { title: t('projectManagement.meeting.status.waived'), value: 'waived' },
]);

const eventStatusChoices = computed(() =>
  (['scheduled', 'held', 'cancelled'] as PmMeetingStatus[]).map((value) => ({
    value,
    title: t(`projectManagement.meeting.eventStatus.${value}`),
    icon: EVENT_STATUS_META[value].icon,
    color: EVENT_STATUS_META[value].color,
  })),
);

const weekdayItems = computed(() =>
  [1, 2, 3, 4, 5, 6, 7].map((value) => ({
    value,
    title: t(`projectManagement.meeting.weekdayName.${value}`),
  })),
);

const wbsItems = computed(() => [
  { title: t('projectManagement.meeting.projectLevel'), value: '' },
  ...props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
]);

const seriesHeaders = computed(() => [
  { title: t('projectManagement.fields.name'), key: 'name', minWidth: 220 },
  { title: t('projectManagement.meeting.weekday'), key: 'weekday', width: 140 },
  { title: t('projectManagement.meeting.startTime'), key: 'startTime', width: 120 },
  { title: t('projectManagement.meeting.seriesUntil'), key: 'until', width: 130 },
  { title: t('projectManagement.meeting.occurrences'), key: 'occurrenceCount', width: 110 },
  { title: t('projectManagement.meeting.open'), key: 'openActionCount', width: 130 },
  { title: t('projectManagement.actions'), key: 'actions', width: 160, sortable: false, align: 'end' as const },
]);

const adhocHeaders = computed(() => [
  { title: t('projectManagement.meeting.startAt'), key: 'startAt', width: 180 },
  { title: t('projectManagement.fields.name'), key: 'name', minWidth: 220 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 130 },
  { title: t('projectManagement.meeting.agenda'), key: 'agenda', width: 110, sortable: false },
  { title: t('projectManagement.meeting.minutes'), key: 'minutes', width: 110, sortable: false },
  { title: t('projectManagement.meeting.open'), key: 'openActionCount', width: 130 },
  { title: t('projectManagement.actions'), key: 'actions', width: 140, sortable: false, align: 'end' as const },
]);

const occurrenceHeaders = computed(() => [
  { title: t('projectManagement.meeting.startAt'), key: 'startAt', width: 180 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 130 },
  { title: t('projectManagement.meeting.agenda'), key: 'agenda', width: 110, sortable: false },
  { title: t('projectManagement.meeting.minutes'), key: 'minutes', width: 110, sortable: false },
  { title: t('projectManagement.meeting.open'), key: 'openActionCount', width: 130 },
  { title: t('projectManagement.actions'), key: 'actions', width: 140, sortable: false, align: 'end' as const },
]);

const minutesHeaders = computed(() => [
  { title: t('projectManagement.meeting.startAt'), key: 'startAt', width: 180 },
  { title: t('projectManagement.fields.name'), key: 'name', minWidth: 220 },
  { title: t('projectManagement.meeting.fromSeries'), key: 'series', width: 160 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 130 },
  { title: t('projectManagement.meeting.minutes'), key: 'minutes', width: 130, sortable: false },
  { title: t('projectManagement.meeting.open'), key: 'openActionCount', width: 130 },
  { title: t('projectManagement.actions'), key: 'actions', width: 160, sortable: false, align: 'end' as const },
]);

const minutesSeriesItems = computed(() => [
  { title: t('projectManagement.meeting.allSeries'), value: '' },
  ...seriesRows.value.map((row) => ({ title: row.name, value: row.id })),
]);

function findMeeting(id?: string | null): PmMeeting | null {
  if (!id) return null;
  if (loadedMeeting.value?.id === id) return loadedMeeting.value;
  return (
    calendarItems.value.find((row) => row.id === id) ||
    adhocItems.value.find((row) => row.id === id) ||
    occurrenceItems.value.find((row) => row.id === id) ||
    minutesItems.value.find((row) => row.id === id) ||
    null
  );
}

const editingMeeting = computed(() => findMeeting(editingMeetingId.value));
const isSeriesOccurrence = computed(() => Boolean(editingMeeting.value?.seriesId) && !editingMeeting.value?.detached);

const derivedWeekday = computed(() => isoWeekdayFromInput(seriesForm.value.firstStart) || seriesForm.value.weekday);

const canSaveMeeting = computed(() => Boolean(meetingForm.value.name.trim() && meetingForm.value.startAt));
const canSaveSeries = computed(() => {
  if (!seriesForm.value.name.trim()) return false;
  if (editingSeriesId.value) return Boolean(seriesForm.value.until && seriesForm.value.startTime);
  return Boolean(seriesForm.value.firstStart && seriesForm.value.until);
});
const canSaveAction = computed(() => {
  if (!actionForm.value.title.trim()) return false;
  if (actionForm.value.status === 'waived' && !actionForm.value.note.trim()) return false;
  return true;
});

const calendarEvents = computed<EventInput[]>(() =>
  calendarItems.value
    .map((row) => toCalendarEvent(row))
    .filter((row): row is EventInput => Boolean(row)),
);

const calendarOptions = computed<CalendarOptions>(() => ({
  plugins: calendarPlugins,
  initialView: 'timeGridWeek',
  headerToolbar: {
    left: 'prev,next today',
    center: 'title',
    right: 'dayGridMonth,timeGridWeek,timeGridDay',
  },
  buttonText: {
    today: locale().toLowerCase().startsWith('en') ? 'Today' : 'Bugün',
    month: locale().toLowerCase().startsWith('en') ? 'Month' : 'Ay',
    week: locale().toLowerCase().startsWith('en') ? 'Week' : 'Hafta',
    day: locale().toLowerCase().startsWith('en') ? 'Day' : 'Gün',
  },
  firstDay: 1,
  height: 680,
  nowIndicator: true,
  selectable: true,
  selectMirror: true,
  editable: false,
  allDaySlot: false,
  slotMinTime: '07:00:00',
  slotMaxTime: '20:00:00',
  scrollTime: '08:00:00',
  dayMaxEvents: true,
  events: calendarEvents.value,
  datesSet: handleDatesSet,
  select: handleDateSelect,
  eventClick: handleEventClick,
}));

function meetingStart(row: PmMeeting): string | null {
  return row.startAt || row.heldAt || null;
}

function meetingEnd(row: PmMeeting): string | null {
  if (row.endAt) return row.endAt;
  const start = meetingStart(row);
  if (!start) return null;
  const date = new Date(start);
  if (Number.isNaN(date.getTime())) return null;
  date.setMinutes(date.getMinutes() + 60);
  return date.toISOString();
}

function meetingStatus(row: PmMeeting): PmMeetingStatus {
  const raw = String(row.status || '').toLowerCase();
  if (raw === 'held' || raw === 'cancelled' || raw === 'scheduled') return raw;
  const start = meetingStart(row);
  if (!start) return 'scheduled';
  return new Date(start).getTime() < Date.now() ? 'held' : 'scheduled';
}

function toCalendarEvent(row: PmMeeting): EventInput | null {
  const start = meetingStart(row);
  if (!start) return null;
  const status = meetingStatus(row);
  const meta = EVENT_STATUS_META[status];
  const classes = ['pm-cal-event', `pm-cal-${status}`];
  if (row.seriesId && !row.detached) classes.push('pm-cal-series');
  if (row.detached) classes.push('pm-cal-detached');
  return {
    id: row.id,
    title: row.name,
    start,
    end: meetingEnd(row) || undefined,
    backgroundColor: meta.hex,
    borderColor: meta.hex,
    textColor: status === 'cancelled' ? '#546E7A' : '#fff',
    classNames: classes,
  };
}

function isoWeekdayFromInput(value?: string | null): number {
  if (!value) return 0;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 0;
  const day = date.getDay();
  return day === 0 ? 7 : day;
}

function padTime(date: Date): string {
  const hours = String(date.getHours()).padStart(2, '0');
  const minutes = String(date.getMinutes()).padStart(2, '0');
  return `${hours}:${minutes}`;
}

function nextHourRange(): { start: Date; end: Date } {
  const start = new Date();
  start.setMinutes(0, 0, 0);
  start.setHours(start.getHours() + 1);
  const end = new Date(start);
  end.setHours(end.getHours() + 1);
  return { start, end };
}

function defaultUntil(from: Date): string {
  const until = new Date(from);
  until.setDate(until.getDate() + 12 * 7);
  return pmDateInput(until.toISOString());
}

function durationMinutes(start: string, end: string): number {
  const from = new Date(start);
  const to = new Date(end);
  if (Number.isNaN(from.getTime()) || Number.isNaN(to.getTime())) return 60;
  const mins = Math.round((to.getTime() - from.getTime()) / 60000);
  if (mins < 15) return 15;
  if (mins > 480) return 480;
  return mins;
}

function timesChanged(row: PmMeeting): boolean {
  const nextStart = pmDateTimePayload(meetingForm.value.startAt);
  const nextEnd = pmDateTimePayload(meetingForm.value.endAt);
  const prevStart = meetingStart(row);
  const prevEnd = meetingEnd(row);
  const startDiff = nextStart && prevStart ? new Date(nextStart).getTime() !== new Date(prevStart).getTime() : Boolean(nextStart) !== Boolean(prevStart);
  const endDiff = nextEnd && prevEnd ? new Date(nextEnd).getTime() !== new Date(prevEnd).getTime() : Boolean(nextEnd) !== Boolean(prevEnd);
  return startDiff || endDiff;
}

function wbsName(id?: string | null) {
  if (!id) return t('projectManagement.meeting.projectLevel');
  const row = props.wbs.find((item) => item.id === id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
}

function seriesName(id?: string | null) {
  if (!id) return '';
  return seriesRows.value.find((row) => row.id === id)?.name || t('projectManagement.meeting.fromSeries');
}

function weekdayLabel(value: number) {
  return t(`projectManagement.meeting.weekdayName.${value}`);
}

function eventStatusLabel(status?: string | null) {
  const key = `projectManagement.meeting.eventStatus.${status || 'scheduled'}`;
  const label = t(key);
  return label === key ? status || 'scheduled' : label;
}

function eventStatusColor(status?: string | null) {
  const key = (status || 'scheduled') as PmMeetingStatus;
  return EVENT_STATUS_META[key]?.color || 'primary';
}

function actionStatusLabel(status?: string | null) {
  const key = `projectManagement.meeting.status.${status || 'open'}`;
  const label = t(key);
  return label === key ? status || 'open' : label;
}

function actionStatusColor(row: PmMeetingAction) {
  if (row.status === 'waived') return 'warning';
  if (row.status === 'done') return 'success';
  if (row.overdue) return 'error';
  if (row.unbound) return 'warning';
  return 'info';
}

function resourceHref(id: string) {
  return `/apps/document-intelligence/r/${encodeURIComponent(id)}`;
}

function workItemHref(id: string) {
  return `/apps/operation-core/work-items/${encodeURIComponent(id)}/profile`;
}

function workItemLabel(id?: string | null, key?: string | null, title?: string | null) {
  if (key && title) return `${key} · ${title}`;
  if (title) return title;
  if (key) return key;
  if (!id) return '';
  return id.slice(0, 8);
}

async function resolveDocumentLabel(id: string) {
  if (!id) return '';
  try {
    const resource = await diGetById(id);
    return diPageResourceLabel(resource) || id;
  } catch {
    return id;
  }
}

async function resolveWorkItemLabel(id: string) {
  if (!id) return { key: '', title: '' };
  try {
    const profile = await ocGetWorkItemProfile(id);
    return {
      key: profile.workItem?.key || '',
      title: profile.workItem?.title || '',
    };
  } catch {
    return { key: '', title: '' };
  }
}

async function loadSummary() {
  if (!props.projectId) return;
  summaryLoading.value = true;
  try {
    const pack = await pmGetProjectMeetings(props.projectId, {
      kind: 'all',
      skip: 0,
      take: 0,
      includeSeries: true,
    });
    seriesRows.value = pack.series ?? [];
    openCount.value = pack.openActionCount ?? 0;
    overdueCount.value = pack.overdueActionCount ?? 0;
    unboundCount.value = pack.unboundActionCount ?? 0;
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    summaryLoading.value = false;
  }
}

async function loadCalendar() {
  if (!props.projectId || !calendarRange.value) return;
  calendarLoading.value = true;
  try {
    const pack = await pmGetProjectMeetings(props.projectId, {
      kind: 'all',
      from: calendarRange.value.from,
      to: calendarRange.value.to,
      skip: 0,
      take: 200,
      includeSeries: false,
    });
    calendarItems.value = pack.items ?? [];
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    calendarLoading.value = false;
  }
}

async function loadAdhoc() {
  if (!props.projectId) return;
  adhocLoading.value = true;
  try {
    const skip = (adhocPage.value - 1) * adhocItemsPerPage.value;
    const pack = await pmGetProjectMeetings(props.projectId, {
      kind: 'adhoc',
      from: adhocFrom.value || undefined,
      to: adhocTo.value || undefined,
      q: adhocSearch.value.trim() || undefined,
      skip,
      take: adhocItemsPerPage.value,
      includeSeries: false,
    });
    adhocItems.value = pack.items ?? [];
    adhocTotal.value = pack.total ?? 0;
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    adhocLoading.value = false;
  }
}

async function loadOccurrences() {
  const series = occurrenceSeries.value;
  if (!props.projectId || !series) return;
  occurrenceLoading.value = true;
  try {
    const skip = (occurrencePage.value - 1) * occurrenceItemsPerPage.value;
    const pack = await pmGetProjectMeetings(props.projectId, {
      seriesId: series.id,
      skip,
      take: occurrenceItemsPerPage.value,
      includeSeries: false,
    });
    occurrenceItems.value = pack.items ?? [];
    occurrenceTotal.value = pack.total ?? 0;
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    occurrenceLoading.value = false;
  }
}

async function loadMinutes() {
  if (!props.projectId) return;
  minutesLoading.value = true;
  try {
    const skip = (minutesPage.value - 1) * minutesItemsPerPage.value;
    const pack = await pmGetProjectMeetings(props.projectId, {
      kind: 'all',
      from: minutesFrom.value || undefined,
      to: minutesTo.value || undefined,
      q: minutesSearch.value.trim() || undefined,
      seriesId: minutesSeriesId.value || undefined,
      minutes: minutesPile.value,
      skip,
      take: minutesItemsPerPage.value,
      includeSeries: false,
    });
    minutesItems.value = pack.items ?? [];
    minutesTotal.value = pack.total ?? 0;
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    minutesLoading.value = false;
  }
}

async function reloadViews() {
  await Promise.all([
    loadSummary(),
    calendarRange.value ? loadCalendar() : Promise.resolve(),
    surface.value === 'list' ? loadAdhoc() : Promise.resolve(),
    surface.value === 'minutes' ? loadMinutes() : Promise.resolve(),
    occurrenceDialog.value && occurrenceSeries.value ? loadOccurrences() : Promise.resolve(),
  ]);
}

function notifyChanged() {
  emit('changed');
  void reloadViews();
}

function handleDatesSet(info: DatesSetArg) {
  const from = info.start.toISOString();
  const to = info.end.toISOString();
  if (calendarRange.value?.from === from && calendarRange.value?.to === to) return;
  calendarRange.value = { from, to };
  void loadCalendar();
}

function handleDateSelect(info: DateSelectArg) {
  info.view.calendar.unselect();
  openCreateMeeting(info.start, info.end);
}

function handleEventClick(info: EventClickArg) {
  info.jsEvent.preventDefault();
  const row = findMeeting(info.event.id);
  if (row) void openEditMeeting(row);
}

function openOccurrenceDialog(row: PmMeetingSeries) {
  occurrenceSeries.value = row;
  occurrenceDialog.value = true;
  if (occurrencePage.value !== 1) occurrencePage.value = 1;
  else void loadOccurrences();
}

function onSeriesRow(_event: Event, row: { item: PmMeetingSeries }) {
  openOccurrenceDialog(row.item);
}

function onAdhocRow(_event: Event, row: { item: PmMeeting }) {
  void openEditMeeting(row.item);
}

function onOccurrenceRow(_event: Event, row: { item: PmMeeting }) {
  void openEditMeeting(row.item);
}

function onMinutesRow(_event: Event, row: { item: PmMeeting }) {
  void openEditMeeting(row.item, 'minutes');
}

function startWriteMinutes() {
  minutesPile.value = 'missing';
  if (surface.value !== 'minutes') surface.value = 'minutes';
  else if (minutesPage.value !== 1) minutesPage.value = 1;
  else void loadMinutes();
}

function openCreateMeeting(start?: Date, end?: Date) {
  editingMeetingId.value = null;
  meetingTab.value = 'event';
  const range = start && end ? { start, end } : nextHourRange();
  const form = emptyMeetingForm();
  form.startAt = pmDateTimeInput(range.start.toISOString());
  form.endAt = pmDateTimeInput(range.end.toISOString());
  meetingForm.value = form;
  agendaDoc.value = emptyMeetingDoc();
  minutesDoc.value = emptyMeetingDoc();
  meetingDialog.value = true;
}

function openCreateSeries(start?: Date, end?: Date) {
  editingSeriesId.value = null;
  const range = start && end ? { start, end } : nextHourRange();
  const form = emptySeriesForm();
  form.firstStart = pmDateTimeInput(range.start.toISOString());
  form.startTime = padTime(range.start);
  form.weekday = isoWeekdayFromInput(form.firstStart) || 1;
  form.durationMinutes = durationMinutes(range.start.toISOString(), range.end.toISOString());
  form.until = defaultUntil(range.start);
  seriesForm.value = form;
  seriesDialog.value = true;
}

async function openEditMeeting(row: PmMeeting, tab: MeetingDialogTab = 'event') {
  loadedMeeting.value = row;
  editingMeetingId.value = row.id;
  meetingTab.value = tab === 'actions' && !row.id ? 'event' : tab;
  meetingForm.value = {
    name: row.name,
    startAt: pmDateTimeInput(meetingStart(row)),
    endAt: pmDateTimeInput(meetingEnd(row)),
    wbsId: row.wbsId || '',
    location: row.location || '',
    meetingUrl: row.meetingUrl || '',
    attendees: row.attendees || '',
    note: row.note || '',
    status: meetingStatus(row),
  };
  agendaDoc.value = emptyMeetingDoc(row.agenda || '');
  minutesDoc.value = emptyMeetingDoc();
  meetingDialog.value = true;
  await Promise.all([
    hydrateMeetingDoc(agendaDoc, row.agendaResourceId || '', row.id),
    hydrateMeetingDoc(minutesDoc, row.minutesResourceId || '', row.id),
  ]);
}

function openEditSeries(row: PmMeetingSeries) {
  editingSeriesId.value = row.id;
  seriesForm.value = {
    name: row.name,
    wbsId: row.wbsId || '',
    weekday: row.weekday || 1,
    startTime: row.startTime || '09:00',
    durationMinutes: row.durationMinutes || 60,
    firstStart: pmDateTimeInput(row.anchorStart),
    until: pmDateInput(row.until),
    location: row.location || '',
    meetingUrl: row.meetingUrl || '',
    agenda: row.agenda || '',
    attendees: row.attendees || '',
    note: row.note || '',
  };
  seriesDialog.value = true;
}

function openCreateAction(meeting: PmMeeting) {
  editingActionId.value = null;
  actionMeetingId.value = meeting.id;
  actionForm.value = {
    ...emptyActionForm(),
    wbsId: meeting.wbsId || '',
  };
  actionDialog.value = true;
}

async function openEditAction(row: PmMeetingAction) {
  editingActionId.value = row.id;
  actionMeetingId.value = row.meetingId;
  actionForm.value = {
    title: row.title,
    ownerName: row.ownerName || '',
    dueDate: pmDateInput(row.dueDate),
    status: (row.status as PmMeetingActionStatus) || 'open',
    workItemId: row.workItemId || '',
    workItemKey: '',
    workItemTitle: row.workItemId || '',
    wbsId: row.wbsId || '',
    note: row.note || '',
  };
  actionDialog.value = true;
  const work = await resolveWorkItemLabel(row.workItemId || '');
  if (editingActionId.value === row.id) {
    actionForm.value.workItemKey = work.key;
    actionForm.value.workItemTitle = work.title;
  }
}

function pickDocument(resource: DiResource) {
  const doc = pickTarget.value === 'agenda' ? agendaDoc : minutesDoc;
  doc.value.resourceId = resource.id;
  doc.value.resourceName = diPageResourceLabel(resource) || resource.id;
  doc.value.canSync = false;
  doc.value.version = 0;
}

function clearMeetingDoc(kind: MeetingDocKind) {
  const doc = kind === 'agenda' ? agendaDoc : minutesDoc;
  doc.value.resourceId = '';
  doc.value.resourceName = '';
  doc.value.canSync = false;
  doc.value.version = 0;
}

function openPick(kind: MeetingDocKind) {
  pickTarget.value = kind;
  pickOpen.value = true;
}

function pickWorkItem(row: PmWorkItemCandidate) {
  actionForm.value.workItemId = row.id;
  actionForm.value.workItemKey = row.key || '';
  actionForm.value.workItemTitle = row.title || '';
}

function clearWorkItem() {
  actionForm.value.workItemId = '';
  actionForm.value.workItemKey = '';
  actionForm.value.workItemTitle = '';
}

function excerpt(text: string): string | null {
  const value = text.trim();
  if (!value) return null;
  return value.length > 400 ? `${value.slice(0, 397)}...` : value;
}

function meetingPayload(agendaResourceId: string | null, minutesResourceId: string | null) {
  const start = pmDateTimePayload(meetingForm.value.startAt);
  const end = pmDateTimePayload(meetingForm.value.endAt);
  const row = editingMeeting.value;
  const detach = Boolean(row?.seriesId) && !row?.detached && timesChanged(row);
  return {
    name: meetingForm.value.name.trim(),
    startAt: start,
    endAt: end,
    heldAt: start,
    status: meetingForm.value.status,
    agendaResourceId: agendaResourceId ?? '',
    minutesResourceId: minutesResourceId ?? '',
    wbsId: meetingForm.value.wbsId || null,
    attendees: meetingForm.value.attendees.trim() || null,
    note: meetingForm.value.note.trim() || null,
    location: meetingForm.value.location.trim() || null,
    meetingUrl: meetingForm.value.meetingUrl.trim() || null,
    agenda: excerpt(agendaDoc.value.body),
    detached: detach ? true : undefined,
  };
}

function seriesPayload() {
  const firstStart = pmDateTimePayload(seriesForm.value.firstStart) || new Date().toISOString();
  const weekday = editingSeriesId.value ? seriesForm.value.weekday : derivedWeekday.value || 1;
  const startTime = editingSeriesId.value ? seriesForm.value.startTime : padTime(new Date(firstStart));
  const until = pmDatePayload(seriesForm.value.until) || firstStart;
  return {
    name: seriesForm.value.name.trim(),
    wbsId: seriesForm.value.wbsId || null,
    weekday,
    startTime,
    durationMinutes: Number(seriesForm.value.durationMinutes) || 60,
    firstStart,
    until,
    location: seriesForm.value.location.trim() || null,
    meetingUrl: seriesForm.value.meetingUrl.trim() || null,
    attendees: seriesForm.value.attendees.trim() || null,
    agenda: seriesForm.value.agenda.trim() || null,
    note: seriesForm.value.note.trim() || null,
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

async function hydrateMeetingDoc(
  doc: { value: MeetingDoc },
  resourceId: string,
  meetingId: string,
) {
  if (!resourceId) return;
  doc.value.resourceId = resourceId;
  doc.value.resourceName = resourceId;
  doc.value.loading = true;
  try {
    const md = await diGetMarkdownContent(resourceId);
    if (editingMeetingId.value !== meetingId) return;
    doc.value.body = md.content || doc.value.body;
    if (md.title) doc.value.resourceName = md.title;
    doc.value.canSync = true;
    doc.value.version = md.currentVersionNumber || 0;
    doc.value.source = 'write';
  } catch {
    if (editingMeetingId.value !== meetingId) return;
    doc.value.source = 'existing';
    const title = await resolveDocumentLabel(resourceId);
    if (editingMeetingId.value === meetingId) doc.value.resourceName = title;
  } finally {
    if (editingMeetingId.value === meetingId) doc.value.loading = false;
  }
}

async function ensureOfficialPage(doc: MeetingDoc, title: string): Promise<{ id: string; name: string }> {
  if (doc.canSync && doc.resourceId) {
    const updated = await diUpdateMarkdown(doc.resourceId, {
      title,
      content: doc.body,
      expectedVersionNumber: doc.version,
      isDraft: false,
    });
    doc.version = updated.currentVersionNumber || doc.version;
    return { id: updated.id, name: diPageResourceLabel(updated) || title };
  }

  const { hubId, folderId } = await ensureProjectMeetingsFolder(
    props.projectId,
    props.projectCode,
    props.hubFolderId,
  );
  if (hubId && hubId !== (props.hubFolderId || '').trim()) emit('hubReady', hubId);
  const created = await diCreateMarkdown({
    parentId: folderId,
    title,
    content: doc.body,
    isDraft: false,
    tags: await resolvePmLibraryAutoTags([PM_LIBRARY_MEETINGS_FOLDER]),
  });
  doc.canSync = true;
  doc.version = created.currentVersionNumber || 0;
  return { id: created.id, name: diPageResourceLabel(created) || title };
}

async function persistMeetingDoc(kind: MeetingDocKind): Promise<string | null> {
  const doc = kind === 'agenda' ? agendaDoc.value : minutesDoc.value;
  if (doc.source === 'existing') return doc.resourceId.trim() || null;
  if (!doc.body.trim()) return doc.resourceId.trim() || null;
  const name = meetingForm.value.name.trim();
  const title = t(
    kind === 'agenda' ? 'projectManagement.meeting.agendaPageTitle' : 'projectManagement.meeting.minutesPageTitle',
    { name },
  );
  const page = await ensureOfficialPage(doc, title);
  doc.resourceId = page.id;
  doc.resourceName = page.name;
  return page.id;
}

async function saveMeeting() {
  if (!canSaveMeeting.value) return;
  saving.value = true;
  try {
    const agendaResourceId = await persistMeetingDoc('agenda');
    const minutesResourceId = await persistMeetingDoc('minutes');
    const payload = meetingPayload(agendaResourceId, minutesResourceId);
    if (editingMeetingId.value) await pmUpdateMeeting(editingMeetingId.value, payload);
    else await pmCreateMeeting(props.projectId, payload);
    meetingDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.meetingSaved'),
      severity: 'success',
    });
    notifyChanged();
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

async function saveSeries() {
  if (!canSaveSeries.value) return;
  saving.value = true;
  try {
    const body = seriesPayload();
    if (editingSeriesId.value) {
      const { firstStart, ...update } = body;
      void firstStart;
      await pmUpdateMeetingSeries(editingSeriesId.value, update);
    } else {
      await pmCreateMeetingSeries(props.projectId, body);
    }
    seriesDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.meetingSeriesSaved'),
      severity: 'success',
    });
    notifyChanged();
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
    notifyChanged();
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
    notifyChanged();
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    closingId.value = null;
  }
}

async function setMeetingStatus(row: PmMeeting, status: PmMeetingStatus) {
  statusBusyId.value = row.id;
  try {
    await pmUpdateMeeting(row.id, { status });
    meetingForm.value.status = status;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message:
        status === 'cancelled'
          ? t('projectManagement.notify.meetingCancelled')
          : status === 'held'
            ? t('projectManagement.notify.meetingHeld')
            : t('projectManagement.notify.meetingSaved'),
      severity: 'success',
    });
    notifyChanged();
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    statusBusyId.value = null;
  }
}

async function executeCancelOccurrence() {
  if (!cancelTarget.value) return;
  const row = cancelTarget.value;
  cancelTarget.value = null;
  await setMeetingStatus(row, 'cancelled');
}

async function executeDeleteMeeting() {
  if (!deleteMeetingTarget.value) return;
  deleting.value = true;
  try {
    await pmDeleteMeeting(deleteMeetingTarget.value.id);
    deleteMeetingTarget.value = null;
    meetingDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.meetingDeleted'),
      severity: 'success',
    });
    notifyChanged();
  } catch (error) {
    panelError(error, 'projectManagement.errors.deleteFailed');
  } finally {
    deleting.value = false;
  }
}

async function executeDeleteSeries() {
  if (!deleteSeriesTarget.value) return;
  deleting.value = true;
  try {
    await pmDeleteMeetingSeries(deleteSeriesTarget.value.id);
    deleteSeriesTarget.value = null;
    seriesDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.meetingSeriesDeleted'),
      severity: 'success',
    });
    notifyChanged();
  } catch (error) {
    panelError(error, 'projectManagement.errors.deleteFailed');
  } finally {
    deleting.value = false;
  }
}

function addActionFromDialog() {
  if (editingMeeting.value) openCreateAction(editingMeeting.value);
}

function markEditingHeld() {
  if (editingMeeting.value) void setMeetingStatus(editingMeeting.value, 'held');
}

function askCancelEditing() {
  cancelTarget.value = editingMeeting.value;
}

function askDeleteEditing() {
  deleteMeetingTarget.value = editingMeeting.value;
}

function onDeleteMeetingDialog(open: boolean) {
  if (!open) deleteMeetingTarget.value = null;
}

function onDeleteSeriesDialog(open: boolean) {
  if (!open) deleteSeriesTarget.value = null;
}

function onCancelDialog(open: boolean) {
  if (!open) cancelTarget.value = null;
}

function onDeleteActionDialog(open: boolean) {
  if (!open) deleteActionTarget.value = null;
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
    notifyChanged();
  } catch (error) {
    panelError(error, 'projectManagement.errors.deleteFailed');
  } finally {
    deleting.value = false;
  }
}

onMounted(() => {
  void loadSummary();
  if (surface.value === 'list') void loadAdhoc();
  if (surface.value === 'minutes') void loadMinutes();
});

watch(
  () => props.projectId,
  () => {
    void reloadViews();
  },
);

watch(surface, (value) => {
  if (value === 'list') void loadAdhoc();
  if (value === 'minutes') void loadMinutes();
});

watch([adhocPage, adhocItemsPerPage], () => {
  if (surface.value === 'list') void loadAdhoc();
});

watch([adhocSearch, adhocFrom, adhocTo], () => {
  if (surface.value !== 'list') return;
  if (adhocPage.value !== 1) adhocPage.value = 1;
  else void loadAdhoc();
});

watch([occurrencePage, occurrenceItemsPerPage], () => {
  if (occurrenceDialog.value) void loadOccurrences();
});

watch([minutesPage, minutesItemsPerPage], () => {
  if (surface.value === 'minutes') void loadMinutes();
});

watch([minutesSearch, minutesFrom, minutesTo, minutesPile, minutesSeriesId], () => {
  if (surface.value !== 'minutes') return;
  if (minutesPage.value !== 1) minutesPage.value = 1;
  else void loadMinutes();
});
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between flex-wrap ga-3 mb-4">
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.meeting.hint') }}</div>
      <div class="d-flex flex-wrap ga-2 align-center">
        <v-btn-toggle v-model="surface" mandatory density="compact" color="primary" divided>
          <v-btn value="calendar" class="text-none" size="small">
            {{ t('projectManagement.meeting.viewCalendar') }}
          </v-btn>
          <v-btn value="list" class="text-none" size="small">
            {{ t('projectManagement.meeting.viewList') }}
          </v-btn>
          <v-btn value="minutes" class="text-none" size="small">
            {{ t('projectManagement.meeting.viewMinutes') }}
          </v-btn>
        </v-btn-toggle>
        <v-btn variant="tonal" @click="openCreateSeries()">
          <PlusIcon size="18" class="mr-1" />
          {{ t('projectManagement.meeting.newSeries') }}
        </v-btn>
        <v-btn color="primary" @click="openCreateMeeting()">
          <PlusIcon size="18" class="mr-1" />
          {{ t('projectManagement.meeting.new') }}
        </v-btn>
      </div>
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

    <div v-if="surface === 'calendar'" class="pm-calendar rounded-lg border mb-6">
      <v-progress-linear v-if="calendarLoading || summaryLoading" indeterminate />
      <ClientOnly>
        <FullCalendar :options="calendarOptions" />
      </ClientOnly>
    </div>

    <template v-else-if="surface === 'list'">
      <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-3">
        <div class="text-subtitle-1">{{ t('projectManagement.meeting.seriesRegister') }}</div>
      </div>
      <v-data-table
        :headers="seriesHeaders"
        :items="seriesRows"
        :loading="summaryLoading"
        item-value="id"
        density="comfortable"
        hover
        class="rounded-lg border mb-8"
        :items-per-page="25"
        :items-per-page-options="pageSizeOptions"
        @click:row="onSeriesRow"
      >
        <template #item.name="{ item }">
          <div class="font-weight-medium">{{ item.name }}</div>
          <div class="text-caption text-medium-emphasis">{{ wbsName(item.wbsId) }}</div>
        </template>
        <template #item.weekday="{ item }">
          {{ weekdayLabel(item.weekday) }}
        </template>
        <template #item.startTime="{ item }">
          {{ item.startTime }} · {{ item.durationMinutes }}′
        </template>
        <template #item.until="{ item }">
          {{ formatPmDateOrDash(item.until) }}
        </template>
        <template #item.openActionCount="{ item }">
          <v-chip size="small" :color="item.openActionCount ? 'info' : 'success'" variant="tonal">
            {{ item.openActionCount || 0 }}
          </v-chip>
        </template>
        <template #item.actions="{ item }">
          <div class="d-flex justify-end ga-1" @click.stop>
            <v-btn size="small" variant="text" @click="openOccurrenceDialog(item)">
              {{ t('projectManagement.meeting.openOccurrences') }}
            </v-btn>
            <v-btn size="small" variant="text" @click="openEditSeries(item)">{{ t('projectManagement.edit') }}</v-btn>
            <v-btn icon size="small" variant="text" color="error" @click="deleteSeriesTarget = item">
              <TrashIcon size="18" />
            </v-btn>
          </div>
        </template>
        <template #no-data>
          <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.meeting.emptySeries') }}</div>
        </template>
      </v-data-table>

      <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-3">
        <div class="text-subtitle-1">{{ t('projectManagement.meeting.adhocRegister') }}</div>
      </div>
      <div class="d-flex flex-wrap ga-2 mb-3 align-center">
        <v-text-field
          v-model="adhocSearch"
          :label="t('projectManagement.meeting.search')"
          density="comfortable"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          style="max-width: 280px"
        />
        <v-text-field
          v-model="adhocFrom"
          type="date"
          :label="t('projectManagement.meeting.windowFrom')"
          density="comfortable"
          hide-details
          style="max-width: 180px"
        />
        <v-text-field
          v-model="adhocTo"
          type="date"
          :label="t('projectManagement.meeting.windowTo')"
          density="comfortable"
          hide-details
          clearable
          style="max-width: 180px"
        />
      </div>
      <v-data-table-server
        v-model:page="adhocPage"
        v-model:items-per-page="adhocItemsPerPage"
        :headers="adhocHeaders"
        :items="adhocItems"
        :items-length="adhocTotal"
        :items-per-page-options="pageSizeOptions"
        :loading="adhocLoading"
        item-value="id"
        density="comfortable"
        hover
        class="rounded-lg border mb-6"
        @click:row="onAdhocRow"
      >
        <template #item.startAt="{ item }">
          {{ formatPmDateTime(meetingStart(item)) || t('projectManagement.meeting.noDate') }}
        </template>
        <template #item.name="{ item }">
          <div class="font-weight-medium">{{ item.name }}</div>
          <div class="text-caption text-medium-emphasis">{{ wbsName(item.wbsId) }}</div>
        </template>
        <template #item.status="{ item }">
          <v-chip size="small" :color="eventStatusColor(item.status)" variant="tonal">
            {{ eventStatusLabel(item.status) }}
          </v-chip>
        </template>
        <template #item.agenda="{ item }">
          <NuxtLink
            v-if="item.agendaResourceId"
            :to="resourceHref(item.agendaResourceId)"
            class="text-primary"
            @click.stop
          >
            {{ t('projectManagement.meeting.agenda') }}
          </NuxtLink>
          <span v-else class="text-medium-emphasis">{{ t('projectManagement.meeting.noAgenda') }}</span>
        </template>
        <template #item.minutes="{ item }">
          <NuxtLink
            v-if="item.minutesResourceId"
            :to="resourceHref(item.minutesResourceId)"
            class="text-primary"
            @click.stop
          >
            {{ t('projectManagement.meeting.minutes') }}
          </NuxtLink>
          <span v-else class="text-medium-emphasis">{{ t('projectManagement.meeting.noMinutes') }}</span>
        </template>
        <template #item.openActionCount="{ item }">
          <v-chip size="small" :color="item.openActionCount ? 'info' : 'success'" variant="tonal">
            {{ item.openActionCount || 0 }}
          </v-chip>
        </template>
        <template #item.actions="{ item }">
          <div class="d-flex justify-end ga-1" @click.stop>
            <v-btn size="small" variant="text" @click="openEditMeeting(item)">{{ t('projectManagement.edit') }}</v-btn>
            <v-btn icon size="small" variant="text" color="error" @click="deleteMeetingTarget = item">
              <TrashIcon size="18" />
            </v-btn>
          </div>
        </template>
        <template #no-data>
          <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.meeting.emptyAdhoc') }}</div>
        </template>
      </v-data-table-server>
    </template>

    <template v-else-if="surface === 'minutes'">
      <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-3">
        <div class="text-subtitle-1">{{ t('projectManagement.meeting.minutesRegister') }}</div>
        <v-btn color="primary" variant="tonal" @click="startWriteMinutes">
          {{ t('projectManagement.meeting.writeMinutes') }}
        </v-btn>
      </div>
      <div class="d-flex flex-wrap ga-2 mb-3 align-center">
        <v-chip :variant="minutesPile === 'present' ? 'flat' : 'tonal'" @click="minutesPile = 'present'">
          {{ t('projectManagement.meeting.minutesPresent') }}
        </v-chip>
        <v-chip
          color="warning"
          :variant="minutesPile === 'missing' ? 'flat' : 'tonal'"
          @click="minutesPile = 'missing'"
        >
          {{ t('projectManagement.meeting.minutesMissing') }}
        </v-chip>
        <v-text-field
          v-model="minutesSearch"
          :label="t('projectManagement.meeting.search')"
          density="comfortable"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          style="max-width: 280px"
        />
        <v-text-field
          v-model="minutesFrom"
          type="date"
          :label="t('projectManagement.meeting.windowFrom')"
          density="comfortable"
          hide-details
          style="max-width: 180px"
        />
        <v-text-field
          v-model="minutesTo"
          type="date"
          :label="t('projectManagement.meeting.windowTo')"
          density="comfortable"
          hide-details
          clearable
          style="max-width: 180px"
        />
        <v-select
          v-model="minutesSeriesId"
          :items="minutesSeriesItems"
          :label="t('projectManagement.meeting.seriesFilter')"
          density="comfortable"
          hide-details
          style="max-width: 240px"
        />
      </div>
      <v-data-table-server
        v-model:page="minutesPage"
        v-model:items-per-page="minutesItemsPerPage"
        :headers="minutesHeaders"
        :items="minutesItems"
        :items-length="minutesTotal"
        :items-per-page-options="pageSizeOptions"
        :loading="minutesLoading"
        item-value="id"
        density="comfortable"
        hover
        class="rounded-lg border mb-6"
        @click:row="onMinutesRow"
      >
        <template #item.startAt="{ item }">
          {{ formatPmDateTime(meetingStart(item)) || t('projectManagement.meeting.noDate') }}
        </template>
        <template #item.name="{ item }">
          <div class="font-weight-medium">{{ item.name }}</div>
          <div class="text-caption text-medium-emphasis">{{ wbsName(item.wbsId) }}</div>
        </template>
        <template #item.series="{ item }">
          {{ item.seriesId && !item.detached ? seriesName(item.seriesId) : '—' }}
        </template>
        <template #item.status="{ item }">
          <v-chip size="small" :color="eventStatusColor(item.status)" variant="tonal">
            {{ eventStatusLabel(item.status) }}
          </v-chip>
        </template>
        <template #item.minutes="{ item }">
          <NuxtLink
            v-if="item.minutesResourceId"
            :to="resourceHref(item.minutesResourceId)"
            class="text-primary"
            @click.stop
          >
            {{ t('projectManagement.meeting.minutes') }}
          </NuxtLink>
          <span v-else class="text-medium-emphasis">{{ t('projectManagement.meeting.noMinutes') }}</span>
        </template>
        <template #item.openActionCount="{ item }">
          <v-chip size="small" :color="item.openActionCount ? 'info' : 'success'" variant="tonal">
            {{ item.openActionCount || 0 }}
          </v-chip>
        </template>
        <template #item.actions="{ item }">
          <div class="d-flex justify-end ga-1" @click.stop>
            <v-btn size="small" variant="text" @click="openEditMeeting(item, 'minutes')">
              {{
                item.minutesResourceId
                  ? t('projectManagement.edit')
                  : t('projectManagement.meeting.writeMinutes')
              }}
            </v-btn>
            <v-btn icon size="small" variant="text" color="error" @click="deleteMeetingTarget = item">
              <TrashIcon size="18" />
            </v-btn>
          </div>
        </template>
        <template #no-data>
          <div class="text-center py-8 text-medium-emphasis">
            {{
              minutesPile === 'missing'
                ? t('projectManagement.meeting.emptyMinutesMissing')
                : t('projectManagement.meeting.emptyMinutesPresent')
            }}
          </div>
        </template>
      </v-data-table-server>
    </template>

    <v-dialog v-model="occurrenceDialog" max-width="980" scrollable>
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.meeting.occurrencesTitle') }}</v-card-title>
        <v-card-subtitle>{{ occurrenceSeries?.name }}</v-card-subtitle>
        <v-card-text>
          <v-data-table-server
            v-model:page="occurrencePage"
            v-model:items-per-page="occurrenceItemsPerPage"
            :headers="occurrenceHeaders"
            :items="occurrenceItems"
            :items-length="occurrenceTotal"
            :items-per-page-options="pageSizeOptions"
            :loading="occurrenceLoading"
            item-value="id"
            density="comfortable"
            hover
            class="rounded-lg border"
            @click:row="onOccurrenceRow"
          >
            <template #item.startAt="{ item }">
              {{ formatPmDateTime(meetingStart(item)) || t('projectManagement.meeting.noDate') }}
            </template>
            <template #item.status="{ item }">
              <v-chip size="small" :color="eventStatusColor(item.status)" variant="tonal">
                {{ eventStatusLabel(item.status) }}
              </v-chip>
            </template>
            <template #item.agenda="{ item }">
              <NuxtLink
                v-if="item.agendaResourceId"
                :to="resourceHref(item.agendaResourceId)"
                class="text-primary"
                @click.stop
              >
                {{ t('projectManagement.meeting.agenda') }}
              </NuxtLink>
              <span v-else class="text-medium-emphasis">{{ t('projectManagement.meeting.noAgenda') }}</span>
            </template>
            <template #item.minutes="{ item }">
              <NuxtLink
                v-if="item.minutesResourceId"
                :to="resourceHref(item.minutesResourceId)"
                class="text-primary"
                @click.stop
              >
                {{ t('projectManagement.meeting.minutes') }}
              </NuxtLink>
              <span v-else class="text-medium-emphasis">{{ t('projectManagement.meeting.noMinutes') }}</span>
            </template>
            <template #item.openActionCount="{ item }">
              <v-chip size="small" :color="item.openActionCount ? 'info' : 'success'" variant="tonal">
                {{ item.openActionCount || 0 }}
              </v-chip>
            </template>
            <template #item.actions="{ item }">
              <div class="d-flex justify-end ga-1" @click.stop>
                <v-btn size="small" variant="text" @click="openEditMeeting(item)">{{ t('projectManagement.edit') }}</v-btn>
                <v-btn icon size="small" variant="text" color="error" @click="deleteMeetingTarget = item">
                  <TrashIcon size="18" />
                </v-btn>
              </div>
            </template>
            <template #no-data>
              <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.meeting.empty') }}</div>
            </template>
          </v-data-table-server>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="occurrenceDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="meetingDialog" max-width="960" scrollable>
      <v-card rounded="lg">
        <v-card-title>
          {{ editingMeetingId ? t('projectManagement.meeting.edit') : t('projectManagement.meeting.new') }}
        </v-card-title>
        <v-card-subtitle class="pb-0">
          {{
            editingMeetingId
              ? t('projectManagement.meeting.dialog.subtitleEdit')
              : t('projectManagement.meeting.dialog.subtitleNew')
          }}
        </v-card-subtitle>
        <v-card-text class="d-flex flex-column ga-3">
          <div v-if="editingMeeting" class="d-flex flex-wrap ga-2">
            <v-chip size="small" :color="eventStatusColor(meetingForm.status)" variant="tonal">
              {{ eventStatusLabel(meetingForm.status) }}
            </v-chip>
            <v-chip v-if="editingMeeting.seriesId" size="small" variant="tonal">
              {{ t('projectManagement.meeting.fromSeries') }} · {{ seriesName(editingMeeting.seriesId) }}
            </v-chip>
            <v-chip v-if="editingMeeting.detached" size="small" color="warning" variant="tonal">
              {{ t('projectManagement.meeting.detached') }}
            </v-chip>
          </div>

          <v-tabs v-model="meetingTab" color="primary" class="mb-2">
            <v-tab value="event">{{ t('projectManagement.meeting.tabEvent') }}</v-tab>
            <v-tab value="agenda">{{ t('projectManagement.meeting.tabAgenda') }}</v-tab>
            <v-tab value="minutes">{{ t('projectManagement.meeting.tabMinutes') }}</v-tab>
            <v-tab v-if="editingMeetingId" value="actions">{{ t('projectManagement.meeting.tabActions') }}</v-tab>
          </v-tabs>

          <v-window v-model="meetingTab">
            <v-window-item value="event">
              <div class="d-flex flex-column ga-4 pt-2">
                <v-text-field v-model="meetingForm.name" :label="t('projectManagement.fields.name')" density="comfortable" />
                <section>
                  <div class="text-subtitle-2 mb-2">{{ t('projectManagement.meeting.dialog.sectionWhen') }}</div>
                  <div class="d-flex flex-wrap ga-3">
                    <v-text-field
                      v-model="meetingForm.startAt"
                      type="datetime-local"
                      :label="t('projectManagement.meeting.startAt')"
                      density="comfortable"
                      class="flex-grow-1"
                    />
                    <v-text-field
                      v-model="meetingForm.endAt"
                      type="datetime-local"
                      :label="t('projectManagement.meeting.endAt')"
                      density="comfortable"
                      class="flex-grow-1"
                    />
                  </div>
                  <p v-if="isSeriesOccurrence" class="text-caption text-medium-emphasis mb-0">
                    {{ t('projectManagement.meeting.thisOnlyHint') }}
                  </p>
                </section>
                <section>
                  <div class="text-subtitle-2 mb-2">{{ t('projectManagement.meeting.dialog.sectionWhere') }}</div>
                  <v-select v-model="meetingForm.wbsId" :items="wbsItems" :label="t('projectManagement.fields.wbsCode')" density="comfortable" class="mb-3" />
                  <v-text-field v-model="meetingForm.location" :label="t('projectManagement.meeting.location')" density="comfortable" class="mb-3" />
                  <v-text-field v-model="meetingForm.meetingUrl" :label="t('projectManagement.meeting.meetingUrl')" density="comfortable" />
                </section>
                <v-textarea
                  v-model="meetingForm.attendees"
                  :label="t('projectManagement.meeting.attendees')"
                  density="comfortable"
                  rows="2"
                  auto-grow
                />
                <v-textarea v-model="meetingForm.note" :label="t('projectManagement.meeting.note')" density="comfortable" rows="2" auto-grow />
                <section v-if="editingMeetingId">
                  <div class="text-subtitle-2 mb-2">{{ t('projectManagement.meeting.dialog.sectionStatus') }}</div>
                  <div class="d-flex flex-wrap ga-2">
                    <v-card
                      v-for="choice in eventStatusChoices"
                      :key="choice.value"
                      :variant="meetingForm.status === choice.value ? 'tonal' : 'outlined'"
                      :color="meetingForm.status === choice.value ? choice.color : undefined"
                      rounded="lg"
                      class="pa-3"
                      role="button"
                      @click="meetingForm.status = choice.value"
                    >
                      <div class="d-flex align-center ga-2">
                        <v-icon :icon="choice.icon" size="20" />
                        <span class="font-weight-medium">{{ choice.title }}</span>
                      </div>
                    </v-card>
                  </div>
                </section>
              </div>
            </v-window-item>

            <v-window-item value="agenda">
              <div class="pt-2">
                <PmMeetingDocField
                  :source="agendaDoc.source"
                  :body="agendaDoc.body"
                  :resource-id="agendaDoc.resourceId"
                  :resource-name="agendaDoc.resourceName"
                  :loading="agendaDoc.loading"
                  :write-hint="t('projectManagement.meeting.writeAgendaHint')"
                  :empty-label="t('projectManagement.meeting.noAgenda')"
                  @update:source="agendaDoc.source = $event"
                  @update:body="agendaDoc.body = $event"
                  @pick="openPick('agenda')"
                  @clear="clearMeetingDoc('agenda')"
                />
              </div>
            </v-window-item>

            <v-window-item value="minutes">
              <div class="pt-2">
                <p class="text-caption text-medium-emphasis mb-2">{{ t('projectManagement.meeting.minutesHint') }}</p>
                <PmMeetingDocField
                  :source="minutesDoc.source"
                  :body="minutesDoc.body"
                  :resource-id="minutesDoc.resourceId"
                  :resource-name="minutesDoc.resourceName"
                  :loading="minutesDoc.loading"
                  :write-hint="t('projectManagement.meeting.writeMinutesHint')"
                  :empty-label="t('projectManagement.meeting.noMinutes')"
                  @update:source="minutesDoc.source = $event"
                  @update:body="minutesDoc.body = $event"
                  @pick="openPick('minutes')"
                  @clear="clearMeetingDoc('minutes')"
                />
              </div>
            </v-window-item>

            <v-window-item v-if="editingMeeting" value="actions">
              <div class="pt-2">
                <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-2">
                  <div class="text-subtitle-2">{{ t('projectManagement.meeting.dialog.sectionActions') }}</div>
                  <v-btn size="small" color="primary" variant="tonal" @click="addActionFromDialog">
                    <PlusIcon size="16" class="mr-1" />
                    {{ t('projectManagement.meeting.newAction') }}
                  </v-btn>
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
                    <tr v-for="action in editingMeeting.actions" :key="action.id">
                      <td>
                        <div>{{ action.title }}</div>
                        <div v-if="action.workItemId" class="text-caption text-medium-emphasis">{{ action.workItemId }}</div>
                      </td>
                      <td>
                        <v-chip size="small" :color="actionStatusColor(action)" variant="tonal">
                          {{ actionStatusLabel(action.status) }}
                        </v-chip>
                      </td>
                      <td>{{ action.ownerName || '—' }}</td>
                      <td>{{ formatPmDateOrDash(action.dueDate) }}</td>
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
                    <tr v-if="!(editingMeeting.actions || []).length">
                      <td colspan="5" class="text-center text-medium-emphasis py-4">
                        {{ t('projectManagement.meeting.emptyActions') }}
                      </td>
                    </tr>
                  </tbody>
                </v-table>
              </div>
            </v-window-item>
          </v-window>
        </v-card-text>
        <v-card-actions>
          <v-btn
            v-if="editingMeeting"
            icon
            variant="text"
            color="error"
            @click="askDeleteEditing"
          >
            <TrashIcon size="18" />
          </v-btn>
          <v-btn
            v-if="editingMeeting && meetingForm.status !== 'cancelled'"
            variant="text"
            color="warning"
            :loading="statusBusyId === editingMeeting.id"
            @click="askCancelEditing"
          >
            {{ t('projectManagement.meeting.cancelOccurrence') }}
          </v-btn>
          <v-btn
            v-if="editingMeeting && meetingForm.status === 'scheduled'"
            variant="text"
            color="success"
            :loading="statusBusyId === editingMeeting.id"
            @click="markEditingHeld"
          >
            {{ t('projectManagement.meeting.markHeld') }}
          </v-btn>
          <v-spacer />
          <v-btn variant="text" @click="meetingDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" :disabled="!canSaveMeeting" @click="saveMeeting">
            {{ t('projectManagement.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="seriesDialog" max-width="640">
      <v-card rounded="lg">
        <v-card-title>
          {{ editingSeriesId ? t('projectManagement.meeting.editSeries') : t('projectManagement.meeting.newSeries') }}
        </v-card-title>
        <v-card-subtitle class="pb-0">
          {{
            editingSeriesId
              ? t('projectManagement.meeting.dialog.subtitleSeriesEdit')
              : t('projectManagement.meeting.dialog.subtitleSeriesNew')
          }}
        </v-card-subtitle>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="seriesForm.name" :label="t('projectManagement.fields.name')" density="comfortable" />
          <v-select v-model="seriesForm.wbsId" :items="wbsItems" :label="t('projectManagement.fields.wbsCode')" density="comfortable" />
          <v-text-field
            v-if="!editingSeriesId"
            v-model="seriesForm.firstStart"
            type="datetime-local"
            :label="t('projectManagement.meeting.firstStart')"
            density="comfortable"
          />
          <v-select
            v-if="editingSeriesId"
            v-model="seriesForm.weekday"
            :items="weekdayItems"
            :label="t('projectManagement.meeting.weekday')"
            density="comfortable"
          />
          <div v-else class="text-body-2">
            {{ t('projectManagement.meeting.weekday') }}:
            {{ weekdayLabel(derivedWeekday || seriesForm.weekday) }}
          </div>
          <div class="d-flex flex-wrap ga-3">
            <v-text-field
              v-if="editingSeriesId"
              v-model="seriesForm.startTime"
              type="time"
              :label="t('projectManagement.meeting.startTime')"
              density="comfortable"
              class="flex-grow-1"
            />
            <v-text-field
              v-model.number="seriesForm.durationMinutes"
              type="number"
              min="15"
              max="480"
              step="15"
              :label="t('projectManagement.meeting.duration')"
              density="comfortable"
              class="flex-grow-1"
            />
          </div>
          <v-text-field
            v-model="seriesForm.until"
            type="date"
            :label="t('projectManagement.meeting.seriesUntil')"
            density="comfortable"
          />
          <p class="text-caption text-medium-emphasis mb-0">{{ t('projectManagement.meeting.horizonHint') }}</p>
          <v-text-field v-model="seriesForm.location" :label="t('projectManagement.meeting.location')" density="comfortable" />
          <v-text-field v-model="seriesForm.meetingUrl" :label="t('projectManagement.meeting.meetingUrl')" density="comfortable" />
          <v-textarea v-model="seriesForm.agenda" :label="t('projectManagement.meeting.agenda')" density="comfortable" rows="2" auto-grow />
          <v-textarea v-model="seriesForm.attendees" :label="t('projectManagement.meeting.attendees')" density="comfortable" rows="2" auto-grow />
          <v-textarea v-model="seriesForm.note" :label="t('projectManagement.meeting.note')" density="comfortable" rows="2" auto-grow />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="seriesDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" :disabled="!canSaveSeries" @click="saveSeries">
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
          <v-select v-model="actionForm.status" :items="actionStatusItems" :label="t('projectManagement.fields.status')" density="comfortable" />
          <v-text-field v-model="actionForm.dueDate" type="date" :label="t('projectManagement.meeting.dueDate')" density="comfortable" />
          <div>
            <div class="text-body-2 font-weight-medium mb-1">{{ t('projectManagement.meeting.workItemId') }}</div>
            <div v-if="actionForm.workItemId" class="d-flex align-center ga-2 flex-wrap mb-2">
              <NuxtLink :to="workItemHref(actionForm.workItemId)" class="text-decoration-none" @click.stop>
                <v-chip size="small" color="primary" variant="tonal">
                  {{ workItemLabel(actionForm.workItemId, actionForm.workItemKey, actionForm.workItemTitle) }}
                </v-chip>
              </NuxtLink>
              <v-btn size="small" variant="text" @click="clearWorkItem">
                {{ t('projectManagement.obligation.clearWorkItem') }}
              </v-btn>
            </div>
            <div v-else class="text-caption text-medium-emphasis mb-2">{{ t('projectManagement.obligation.noWorkItem') }}</div>
            <v-btn variant="tonal" class="text-none" @click="workPickOpen = true">
              {{ t('projectManagement.pickWorkItem.open') }}
            </v-btn>
          </div>
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

    <v-dialog :model-value="Boolean(deleteMeetingTarget)" max-width="440" @update:model-value="onDeleteMeetingDialog">
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

    <v-dialog :model-value="Boolean(deleteSeriesTarget)" max-width="440" @update:model-value="onDeleteSeriesDialog">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.meeting.deleteSeriesTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.meeting.deleteSeriesConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteSeriesTarget = null">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="error" :loading="deleting" @click="executeDeleteSeries">{{ t('projectManagement.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="Boolean(cancelTarget)" max-width="440" @update:model-value="onCancelDialog">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.meeting.cancelTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.meeting.cancelConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="cancelTarget = null">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="warning" :loading="Boolean(statusBusyId)" @click="executeCancelOccurrence">
            {{ t('projectManagement.meeting.cancelOccurrence') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="Boolean(deleteActionTarget)" max-width="440" @update:model-value="onDeleteActionDialog">
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

    <PmPickDocumentDialog
      v-model="pickOpen"
      :project-id="projectId"
      :project-code="projectCode"
      :hub-folder-id="hubFolderId"
      markdown-only
      @pick="pickDocument"
      @hub-ready="emit('hubReady', $event)"
    />
    <PmPickWorkItemDialog v-model="workPickOpen" :project-id="projectId" @pick="pickWorkItem" />
  </div>
</template>

<style scoped>
.pm-calendar {
  background: rgb(var(--v-theme-surface));
  padding: 12px;
}
.pm-calendar :deep(.pm-cal-cancelled) {
  opacity: 0.55;
  text-decoration: line-through;
}
.pm-calendar :deep(.fc-event) {
  cursor: pointer;
}
</style>
