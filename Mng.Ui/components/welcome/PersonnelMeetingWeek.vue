<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import MtMeetingCalendar from '@/components/apps/meetings/MtMeetingCalendar.vue';
import { fetchPersonnelWeekMeetings } from '@/services/personnelActivityFetch';
import type { OrgMeeting, OrgMeetingStatus } from '@/services/meetingsService';

const props = defineProps<{
  personId?: string | null;
  from?: string | null;
  to?: string | null;
}>();

const meetings = ref<OrgMeeting[]>([]);

const weekStart = computed(() => {
  const value = props.from?.trim() ?? '';
  return /^\d{4}-\d{2}-\d{2}/.test(value) ? value.slice(0, 10) : '';
});

function asStatus(value: string): OrgMeetingStatus {
  if (value === 'held' || value === 'cancelled') return value;
  return 'scheduled';
}

watch(
  () => [props.personId?.trim() || '', props.from || '', props.to || ''] as const,
  async ([personId, from, to]) => {
    if (!personId || !from || !to) {
      meetings.value = [];
      return;
    }
    try {
      const rows = await fetchPersonnelWeekMeetings(personId, from, to);
      meetings.value = rows.map((row) => ({
        id: row.id,
        title: row.title,
        startAt: row.startAt,
        endAt: row.endAt,
        status: asStatus(row.status),
        location: '',
        meetingUrl: '',
        organizerId: null,
        attendeeIds: [],
        agenda: '',
        agendaResourceId: null,
        minutesResourceId: null,
        seriesId: null,
        occurrenceDate: null,
        detached: false,
        topicId: null,
        topicCustom: false,
        roomId: null,
        roomCustom: false,
      }));
    } catch {
      meetings.value = [];
    }
  },
  { immediate: true },
);

function openMeeting(id: string) {
  if (!id) return;
  window.open(`/apps/meetings?open=${encodeURIComponent(id)}`, '_blank', 'noopener');
}
</script>

<template>
  <v-card variant="outlined" class="my-4">
    <v-card-title class="text-subtitle-1">Toplantılar</v-card-title>
    <v-card-text>
      <MtMeetingCalendar :meetings="meetings" :week-start="weekStart" @open="openMeeting" />
    </v-card-text>
  </v-card>
</template>
