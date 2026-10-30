<script setup lang="ts">
import { computed } from 'vue';
import FullCalendar from '@fullcalendar/vue3';
import dayGridPlugin from '@fullcalendar/daygrid';
import timeGridPlugin from '@fullcalendar/timegrid';
import interactionPlugin from '@fullcalendar/interaction';
import trLocale from '@fullcalendar/core/locales/tr';
import type { CalendarOptions, DateSelectArg, DatesSetArg, EventClickArg, EventInput } from '@fullcalendar/core';
import { useAppI18n } from '@/composables/useAppI18n';
import { useLocaleStore } from '@/stores/locale';
import type { OrgMeeting, OrgMeetingStatus } from '@/services/meetingsService';

const props = defineProps<{ meetings: OrgMeeting[] }>();
const emit = defineEmits<{
  create: [payload: { start: string; end: string }];
  open: [id: string];
  range: [through: string];
}>();

const { locale } = useAppI18n();
const localeStore = useLocaleStore();

const STATUS_COLOR: Record<OrgMeetingStatus, string> = {
  scheduled: '#1565c0',
  held: '#2e7d32',
  cancelled: '#9e9e9e',
};

const english = computed(() => (localeStore.locale || locale() || 'tr').toLowerCase().startsWith('en'));

function localInput(date: Date): string {
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function eventEnd(meeting: OrgMeeting): string | undefined {
  if (meeting.endAt) return meeting.endAt;
  const start = Date.parse(meeting.startAt);
  if (Number.isNaN(start)) return undefined;
  return new Date(start + 60 * 60000).toISOString();
}

const events = computed<EventInput[]>(() =>
  props.meetings
    .filter((meeting) => meeting.startAt)
    .map((meeting) => ({
      id: meeting.id,
      title: meeting.title,
      start: meeting.startAt,
      end: eventEnd(meeting),
      backgroundColor: STATUS_COLOR[meeting.status],
      borderColor: STATUS_COLOR[meeting.status],
      classNames: [
        meeting.seriesId && !meeting.detached ? 'mt-cal-series' : '',
        meeting.detached ? 'mt-cal-detached' : '',
      ].filter(Boolean),
    })),
);

const options = computed<CalendarOptions>(() => ({
  plugins: [dayGridPlugin, timeGridPlugin, interactionPlugin],
  locale: english.value ? 'en' : trLocale,
  initialView: 'timeGridWeek',
  headerToolbar: {
    left: 'prev,next today',
    center: 'title',
    right: 'dayGridMonth,timeGridWeek,timeGridDay',
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
  events: events.value,
  select: (arg: DateSelectArg) => {
    emit('create', { start: localInput(arg.start), end: localInput(arg.end) });
    arg.view.calendar.unselect();
  },
  eventClick: (arg: EventClickArg) => emit('open', arg.event.id),
  datesSet: (arg: DatesSetArg) => emit('range', localInput(arg.end).slice(0, 10)),
}));
</script>

<template>
  <FullCalendar :options="options" />
</template>

<style scoped>
:deep(.mt-cal-series) {
  box-shadow: inset 4px 0 0 #0d47a1;
}
:deep(.mt-cal-detached) {
  box-shadow: inset 4px 0 0 #ef6c00;
}
</style>
