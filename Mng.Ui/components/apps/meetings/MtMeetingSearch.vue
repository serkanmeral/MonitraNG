<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { diContentSearch } from '@/services/documentIntelligenceService';
import {
  listOrgMeetingActionRows,
  listOrgMeetingAttachmentRows,
  listOrgMeetingAttendanceRows,
  type OrgAttendancePresence,
  type OrgMeeting,
  type OrgMeetingAction,
  type OrgMeetingAttendance,
  type OrgMeetingPerson,
  type OrgMeetingStatus,
  type OrgMeetingTopic,
} from '@/services/meetingsService';

const props = defineProps<{
  meetings: OrgMeeting[];
  topics: OrgMeetingTopic[];
  people: OrgMeetingPerson[];
  personNames: Record<string, string>;
}>();

const emit = defineEmits<{ open: [id: string] }>();

type Mode = 'meeting' | 'content';
type Role = 'agenda' | 'minutes' | 'attachment';
type SearchField =
  | 'title'
  | 'location'
  | 'topic'
  | 'status'
  | 'organizer'
  | 'user'
  | 'person'
  | 'presence'
  | 'series'
  | 'minutes'
  | 'agendaDoc'
  | 'attachment'
  | 'action';
type FieldKind = 'text' | 'catalog' | 'user' | 'flag';

interface SearchRow {
  id: number;
  field: SearchField | '';
  operator: string;
  value: unknown;
}

interface ContentHit {
  meeting: OrgMeeting;
  role: Role;
  snippet: string;
  resourceName: string;
}

const FIELD_KIND: Record<SearchField, FieldKind> = {
  title: 'text',
  location: 'text',
  topic: 'catalog',
  status: 'catalog',
  organizer: 'user',
  user: 'user',
  person: 'user',
  presence: 'catalog',
  series: 'catalog',
  minutes: 'flag',
  agendaDoc: 'flag',
  attachment: 'flag',
  action: 'catalog',
};

const FIELD_ORDER: SearchField[] = [
  'title', 'location', 'topic', 'status', 'organizer', 'user', 'person',
  'presence', 'series', 'minutes', 'agendaDoc', 'attachment', 'action',
];

const PRESENCE_VALUES: OrgAttendancePresence[] = ['invited', 'confirmed', 'attended', 'absent', 'excused'];

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const mode = ref<Mode>('meeting');
const titleQuery = ref('');
const fromDate = ref('');
const toDate = ref('');
const query = ref('');
const panelOpen = ref(false);
const advancedRows = ref<SearchRow[]>([]);
let rowSeq = 0;
const searching = ref(false);
const ran = ref(false);
const notice = ref('');
const capped = ref(false);
const meetingHits = ref<OrgMeeting[]>([]);
const contentHits = ref<ContentHit[]>([]);

const topicItems = computed(() => props.topics.map((topic) => ({
  title: topicLabel(topic),
  value: topic.id,
})));
const personItems = computed(() => props.people.map((person) => ({
  title: person.organization ? `${person.name} (${person.organization})` : person.name,
  value: person.id,
})));
const statusItems = computed(() => (['scheduled', 'held', 'cancelled'] as OrgMeetingStatus[]).map((value) => ({
  title: t(`meetings.status.${value}`),
  value,
})));
const presenceItems = computed(() => PRESENCE_VALUES.map((value) => ({
  title: t(`meetings.presence.${value}`),
  value,
})));
const seriesItems = computed(() => (['member', 'single', 'exception'] as const).map((value) => ({
  title: t(`meetings.search.seriesKind.${value}`),
  value,
})));
const actionItems = computed(() => (['open', 'any', 'none'] as const).map((value) => ({
  title: t(`meetings.search.actionKind.${value}`),
  value,
})));
const fieldOptions = computed(() => FIELD_ORDER.map((value) => ({ value, title: fieldLabel(value) })));
const canClear = computed(() => Boolean(
  titleQuery.value.trim() || fromDate.value || toDate.value || advancedRows.value.length,
));

function topicLabel(topic: OrgMeetingTopic): string {
  const name = topic.code ? `${topic.name} (${topic.code})` : topic.name;
  return topic.archived ? `${name} · ${t('meetings.search.archivedTopic')}` : name;
}

function fieldLabel(field: SearchField): string {
  if (field === 'title') return t('meetings.fields.title');
  if (field === 'location') return t('meetings.fields.location');
  if (field === 'topic') return t('meetings.fields.topic');
  if (field === 'status') return t('meetings.fields.status');
  if (field === 'organizer') return t('meetings.fields.organizer');
  if (field === 'user') return t('meetings.search.participant');
  if (field === 'person') return t('meetings.search.external');
  if (field === 'presence') return t('meetings.search.fields.presence');
  if (field === 'series') return t('meetings.search.fields.series');
  if (field === 'minutes') return t('meetings.fields.minutes');
  if (field === 'agendaDoc') return t('meetings.fields.agendaDoc');
  if (field === 'attachment') return t('meetings.tabs.attachments');
  return t('meetings.tabs.actions');
}

function operatorsFor(field: SearchField): string[] {
  if (field === 'presence') return ['eq', 'in'];
  if (field === 'user' || field === 'person') return ['in', 'all', 'nin'];
  if (field === 'organizer') return ['eq', 'ne'];
  if (field === 'minutes' || field === 'agendaDoc' || field === 'attachment') return ['exists', 'missing'];
  if (field === 'title' || field === 'location') return ['contains', 'eq', 'startsWith'];
  return ['eq', 'ne', 'in', 'nin'];
}

function operatorOptions(field: SearchField | ''): { value: string; title: string }[] {
  if (!field) return [];
  return operatorsFor(field).map((value) => ({ value, title: t(`meetings.search.operators.${value}`) }));
}

function isMulti(row: SearchRow): boolean {
  return row.operator === 'in' || row.operator === 'nin' || row.operator === 'all';
}

function showsText(row: SearchRow): boolean {
  return row.field === 'title' || row.field === 'location';
}

function showsUser(row: SearchRow): boolean {
  return row.field === 'organizer' || row.field === 'user';
}

function showsCatalog(row: SearchRow): boolean {
  return Boolean(row.field) && !showsText(row) && !showsUser(row) && FIELD_KIND[row.field] !== 'flag';
}

function valueItems(row: SearchRow): { title: string; value: string }[] {
  if (row.field === 'topic') return topicItems.value;
  if (row.field === 'status') return statusItems.value;
  if (row.field === 'person') return personItems.value;
  if (row.field === 'presence') return presenceItems.value;
  if (row.field === 'series') return seriesItems.value;
  if (row.field === 'action') return actionItems.value;
  return [];
}

function defaultValue(row: SearchRow): unknown {
  if (!row.field || FIELD_KIND[row.field] === 'flag') return null;
  if (isMulti(row)) return [];
  if (FIELD_KIND[row.field] === 'text') return '';
  return null;
}

function onFieldChange(row: SearchRow) {
  row.operator = row.field ? operatorsFor(row.field)[0] : '';
  row.value = defaultValue(row);
}

function onOperatorChange(row: SearchRow) {
  const next = defaultValue(row);
  if (Array.isArray(row.value) && Array.isArray(next)) return;
  row.value = next;
}

function addRow() {
  panelOpen.value = true;
  advancedRows.value.push({ id: ++rowSeq, field: '', operator: '', value: null });
}

function removeRow(id: number) {
  advancedRows.value = advancedRows.value.filter((row) => row.id !== id);
}

function clearSearch() {
  titleQuery.value = '';
  fromDate.value = '';
  toDate.value = '';
  advancedRows.value = [];
  meetingHits.value = [];
  notice.value = '';
  ran.value = false;
}

function selectedValues(raw: unknown): string[] {
  const list = Array.isArray(raw) ? raw : [raw];
  return list.map((item) => String(item ?? '').trim()).filter((item) => item.length > 0);
}

function rowReady(row: SearchRow): row is SearchRow & { field: SearchField } {
  if (!row.field || !row.operator) return false;
  if (FIELD_KIND[row.field] === 'flag') return true;
  return selectedValues(row.value).length > 0;
}

function dayStart(ymd: string): number {
  return Date.parse(`${ymd}T00:00:00`);
}

function dayEnd(ymd: string): number {
  return Date.parse(`${ymd}T23:59:59`);
}

function inRange(meeting: OrgMeeting): boolean {
  const start = Date.parse(meeting.startAt);
  if (Number.isNaN(start)) return false;
  if (fromDate.value && start < dayStart(fromDate.value)) return false;
  if (toDate.value && start > dayEnd(toDate.value)) return false;
  return true;
}

function fold(value: string): string {
  return value.toLocaleLowerCase('tr');
}

function textPasses(hay: string, operator: string, raw: unknown): boolean {
  const needle = selectedValues(raw)[0] || '';
  if (!needle) return true;
  const value = fold(hay);
  const wanted = fold(needle);
  if (operator === 'eq') return value === wanted;
  if (operator === 'startsWith') return value.startsWith(wanted);
  return value.includes(wanted);
}

function catalogPasses(actual: string[], operator: string, raw: unknown): boolean {
  const selected = selectedValues(raw);
  if (!selected.length) return true;
  const hit = selected.some((value) => actual.includes(value));
  if (operator === 'ne' || operator === 'nin') return !hit;
  return hit;
}

function seriesLabels(meeting: OrgMeeting): string[] {
  const labels: string[] = [];
  if (meeting.seriesId && !meeting.detached) labels.push('member');
  if (!meeting.seriesId) labels.push('single');
  if (meeting.detached) labels.push('exception');
  return labels;
}

function actionLabels(actions: OrgMeetingAction[]): string[] {
  if (!actions.length) return ['none'];
  const labels = ['any'];
  if (actions.some((action) => action.status === 'open')) labels.push('open');
  return labels;
}

function catalogActual(meeting: OrgMeeting, field: SearchField, actions: Map<string, OrgMeetingAction[]>): string[] {
  if (field === 'topic') return meeting.topicId ? [meeting.topicId] : [];
  if (field === 'status') return [meeting.status];
  if (field === 'organizer') return meeting.organizerId ? [meeting.organizerId] : [];
  if (field === 'series') return seriesLabels(meeting);
  if (field === 'action') return actionLabels(actions.get(meeting.id) || []);
  return [];
}

function flagPresent(meeting: OrgMeeting, field: SearchField, attachments: Set<string>): boolean {
  if (field === 'minutes') return Boolean(meeting.minutesResourceId);
  if (field === 'agendaDoc') return Boolean(meeting.agendaResourceId);
  return attachments.has(meeting.id);
}

function attendanceHit(rows: OrgMeetingAttendance[], field: 'user' | 'person', id: string): boolean {
  return rows.some((row) => (field === 'user' ? row.userId : row.personId) === id);
}

function peoplePass(
  rows: OrgMeetingAttendance[],
  field: 'user' | 'person',
  operator: string,
  ids: string[],
): boolean {
  const present = ids.filter((id) => attendanceHit(rows, field, id));
  if (operator === 'all') return present.length === ids.length;
  if (operator === 'nin' || operator === 'ne') return present.length === 0;
  return present.length > 0;
}

function presencePasses(
  attendance: OrgMeetingAttendance[],
  conditions: Array<SearchRow & { field: SearchField }>,
  people: Array<SearchRow & { field: SearchField }>,
): boolean {
  if (!conditions.length) return true;
  const positive = people.filter((row) => row.operator === 'in' || row.operator === 'all' || row.operator === 'eq');
  return conditions.every((condition) => {
    const matches = (presence: string) => catalogPasses([presence], condition.operator, condition.value);
    if (!positive.length) return attendance.some((row) => matches(row.presence));
    return positive.every((person) => {
      if (person.field !== 'user' && person.field !== 'person') return true;
      const ids = selectedValues(person.value);
      const matched = ids.filter((id) => {
        const row = attendance.find((item) => (person.field === 'user' ? item.userId : item.personId) === id);
        return Boolean(row && matches(row.presence));
      });
      return person.operator === 'all' || person.operator === 'eq' ? matched.length === ids.length : matched.length > 0;
    });
  });
}

function formatWhen(value: string): string {
  const parsed = Date.parse(value);
  if (Number.isNaN(parsed)) return value;
  return new Date(parsed).toLocaleString();
}

function topicName(id: string | null): string {
  if (!id) return '';
  return props.topics.find((topic) => topic.id === id)?.name || '';
}

function organizerName(id: string | null): string {
  if (!id) return '';
  return props.personNames[id] || '';
}

async function runMeeting() {
  notice.value = '';
  capped.value = false;
  contentHits.value = [];
  const ready = advancedRows.value.filter(rowReady);
  const hasFilter = Boolean(titleQuery.value.trim() || fromDate.value || toDate.value || ready.length);
  if (!hasFilter) {
    ran.value = true;
    meetingHits.value = [];
    notice.value = t('meetings.search.needFilter');
    return;
  }
  searching.value = true;
  try {
    const needsAttendance = ready.some((row) => row.field === 'user' || row.field === 'person' || row.field === 'presence');
    const needsAttachments = ready.some((row) => row.field === 'attachment');
    const needsActions = ready.some((row) => row.field === 'action');
    const [attendanceRows, attachmentRows, actionRows] = await Promise.all([
      needsAttendance ? listOrgMeetingAttendanceRows() : Promise.resolve([]),
      needsAttachments ? listOrgMeetingAttachmentRows() : Promise.resolve([]),
      needsActions ? listOrgMeetingActionRows() : Promise.resolve([]),
    ]);
    const attendance = new Map<string, OrgMeetingAttendance[]>();
    for (const row of attendanceRows) {
      const list = attendance.get(row.meetingId) || [];
      list.push(row);
      attendance.set(row.meetingId, list);
    }
    const attachments = new Set(attachmentRows.map((row) => row.meetingId));
    const actions = new Map<string, OrgMeetingAction[]>();
    for (const row of actionRows) {
      const list = actions.get(row.meetingId) || [];
      list.push(row);
      actions.set(row.meetingId, list);
    }
    const people = ready.filter((row) => row.field === 'user' || row.field === 'person');
    const presence = ready.filter((row) => row.field === 'presence');
    meetingHits.value = props.meetings
      .filter((meeting) => {
        if (titleQuery.value.trim() && !textPasses(meeting.title, 'contains', titleQuery.value)) return false;
        if ((fromDate.value || toDate.value) && !inRange(meeting)) return false;
        const rows = attendance.get(meeting.id) || [];
        for (const condition of ready) {
          if (condition.field === 'title' && !textPasses(meeting.title, condition.operator, condition.value)) return false;
          if (condition.field === 'location' && !textPasses(meeting.location, condition.operator, condition.value)) return false;
          if (condition.field === 'user' || condition.field === 'person') {
            if (!peoplePass(rows, condition.field, condition.operator, selectedValues(condition.value))) return false;
          }
          if (condition.field === 'minutes' || condition.field === 'agendaDoc' || condition.field === 'attachment') {
            const present = flagPresent(meeting, condition.field, attachments);
            if (condition.operator === 'missing' ? present : !present) return false;
          }
          if (condition.field === 'topic' || condition.field === 'status' || condition.field === 'organizer' || condition.field === 'series' || condition.field === 'action') {
            if (!catalogPasses(catalogActual(meeting, condition.field, actions), condition.operator, condition.value)) return false;
          }
        }
        return presencePasses(rows, presence, people);
      })
      .sort((left, right) => Date.parse(right.startAt) - Date.parse(left.startAt));
    ran.value = true;
  } catch (error) {
    panelError(error, 'meetings.loadFailed');
  } finally {
    searching.value = false;
  }
}

function excerpt(text: string, needle: string): string {
  const hay = text.replace(/\s+/g, ' ').trim();
  const index = hay.toLocaleLowerCase('tr').indexOf(needle.toLocaleLowerCase('tr'));
  if (index < 0) return hay.slice(0, 180);
  const start = Math.max(0, index - 40);
  const end = Math.min(hay.length, index + needle.length + 80);
  return `${start > 0 ? '…' : ''}${hay.slice(start, end)}${end < hay.length ? '…' : ''}`;
}

async function runContent() {
  notice.value = '';
  capped.value = false;
  meetingHits.value = [];
  const needle = query.value.trim();
  if (needle.length < 3) {
    ran.value = true;
    contentHits.value = [];
    notice.value = t('meetings.search.contentShort');
    return;
  }
  searching.value = true;
  try {
    const attachments = await listOrgMeetingAttachmentRows();
    const links = new Map<string, { meeting: OrgMeeting; role: Role }[]>();
    const add = (resourceId: string | null, meeting: OrgMeeting, role: Role) => {
      if (!resourceId) return;
      const key = resourceId.toLowerCase();
      const list = links.get(key) || [];
      if (!list.some((item) => item.meeting.id === meeting.id && item.role === role)) list.push({ meeting, role });
      links.set(key, list);
    };
    const byId = new Map(props.meetings.map((meeting) => [meeting.id, meeting]));
    for (const meeting of props.meetings) {
      add(meeting.agendaResourceId, meeting, 'agenda');
      add(meeting.minutesResourceId, meeting, 'minutes');
    }
    for (const row of attachments) {
      const meeting = byId.get(row.meetingId);
      if (meeting) add(row.resourceId, meeting, 'attachment');
    }
    const hits: ContentHit[] = [];
    const seen = new Set<string>();
    const push = (meeting: OrgMeeting, role: Role, snippet: string, resourceName: string, resourceId: string) => {
      const key = `${meeting.id}|${role}|${resourceId}`;
      if (seen.has(key)) return;
      seen.add(key);
      hits.push({ meeting, role, snippet, resourceName });
    };
    for (const meeting of props.meetings) {
      if (meeting.agenda.toLocaleLowerCase('tr').includes(needle.toLocaleLowerCase('tr'))) {
        push(meeting, 'agenda', excerpt(meeting.agenda, needle), '', meeting.agendaResourceId || 'agenda-text');
      }
    }
    try {
      const found = await diContentSearch({ q: needle, scope: 'all', match: 'content', sort: 'relevance' });
      capped.value = found.capped;
      for (const hit of found.items) {
        const linked = links.get(hit.resource.id.toLowerCase()) || [];
        const snippet = (hit.snippets[0] || hit.resource.name).replace(/<[^>]+>/g, '');
        for (const link of linked) push(link.meeting, link.role, snippet, hit.resource.name, hit.resource.id);
      }
    } catch (error) {
      panelError(error, 'meetings.loadFailed');
    }
    contentHits.value = hits.sort((left, right) => Date.parse(right.meeting.startAt) - Date.parse(left.meeting.startAt));
    ran.value = true;
  } catch (error) {
    panelError(error, 'meetings.loadFailed');
  } finally {
    searching.value = false;
  }
}

function run() {
  if (mode.value === 'meeting') void runMeeting();
  else void runContent();
}

watch(mode, () => {
  ran.value = false;
  notice.value = '';
  capped.value = false;
  meetingHits.value = [];
  contentHits.value = [];
});
</script>

<template>
  <div>
    <v-btn-toggle v-model="mode" mandatory density="comfortable" divided class="mb-4">
      <v-btn value="meeting">{{ t('meetings.search.meeting') }}</v-btn>
      <v-btn value="content">{{ t('meetings.search.content') }}</v-btn>
    </v-btn-toggle>

    <template v-if="mode === 'meeting'">
      <v-row>
        <v-col cols="12" md="6">
          <v-text-field
            v-model="titleQuery"
            :label="t('meetings.fields.title')"
            :placeholder="t('meetings.search.titleContains')"
            variant="outlined"
            density="comfortable"
            hide-details
            @keydown.enter="run"
          />
        </v-col>
        <v-col cols="12" md="3">
          <v-text-field v-model="fromDate" type="date" :label="t('meetings.search.from')" variant="outlined" density="comfortable" hide-details />
        </v-col>
        <v-col cols="12" md="3">
          <v-text-field v-model="toDate" type="date" :label="t('meetings.search.to')" variant="outlined" density="comfortable" hide-details />
        </v-col>
      </v-row>

      <div class="d-flex flex-wrap ga-2 mt-3 mb-2">
        <v-btn size="small" variant="tonal" class="text-none" @click="panelOpen = !panelOpen">
          {{ panelOpen ? t('meetings.search.advanced.hide') : t('meetings.search.advanced.show') }}
          <v-icon :icon="panelOpen ? 'mdi-chevron-up' : 'mdi-chevron-down'" end />
        </v-btn>
        <v-btn v-if="panelOpen" size="small" variant="tonal" color="primary" class="text-none" prepend-icon="mdi-plus" @click="addRow">
          {{ t('meetings.search.advanced.add') }}
        </v-btn>
        <v-btn v-if="canClear" size="small" variant="text" class="text-none" @click="clearSearch">
          {{ t('meetings.search.advanced.clear') }}
        </v-btn>
      </div>

      <v-expand-transition>
        <div v-show="panelOpen" class="mt-advanced-search rounded-lg pa-3">
          <p class="text-caption text-medium-emphasis mb-2">{{ t('meetings.search.advanced.andHint') }}</p>
          <p v-if="!advancedRows.length" class="text-caption text-disabled mb-0">{{ t('meetings.search.advanced.empty') }}</p>
          <v-row v-for="row in advancedRows" :key="row.id" dense align="center" class="mb-1">
            <v-col cols="12" md="3">
              <v-select
                v-model="row.field"
                :items="fieldOptions"
                :label="t('meetings.search.advanced.field')"
                variant="outlined"
                density="compact"
                hide-details
                @update:model-value="onFieldChange(row)"
              />
            </v-col>
            <v-col cols="12" md="3">
              <v-select
                v-model="row.operator"
                :items="operatorOptions(row.field)"
                :label="t('meetings.search.advanced.operator')"
                variant="outlined"
                density="compact"
                hide-details
                :disabled="!row.field"
                @update:model-value="onOperatorChange(row)"
              />
            </v-col>
            <v-col cols="11" md="5">
              <v-text-field
                v-if="showsText(row)"
                v-model="row.value"
                :label="t('meetings.search.advanced.value')"
                variant="outlined"
                density="compact"
                hide-details
              />
              <MngDirectoryPickerField
                v-else-if="showsUser(row)"
                :key="`${row.id}-${row.field}`"
                v-model="row.value"
                entity="user"
                density="compact"
                hide-details
                :multiple="row.field === 'user'"
                :label="t('meetings.search.advanced.value')"
              />
              <v-select
                v-else-if="showsCatalog(row)"
                v-model="row.value"
                :items="valueItems(row)"
                :label="t('meetings.search.advanced.value')"
                variant="outlined"
                density="compact"
                hide-details
                :multiple="isMulti(row)"
                :chips="isMulti(row)"
                clearable
              />
            </v-col>
            <v-col cols="1" class="text-right">
              <v-btn icon="mdi-close" variant="text" size="small" @click="removeRow(row.id)" />
            </v-col>
          </v-row>
        </div>
      </v-expand-transition>
    </template>
    <template v-else>
      <p class="text-caption text-medium-emphasis mb-2">{{ t('meetings.search.contentHint') }}</p>
      <v-text-field v-model="query" :label="t('meetings.search.query')" variant="outlined" density="comfortable" @keydown.enter="run" />
    </template>

    <v-btn color="primary" class="mt-4" :loading="searching" @click="run">{{ t('meetings.search.run') }}</v-btn>
    <v-alert v-if="notice" type="info" variant="tonal" class="mt-4">{{ notice }}</v-alert>
    <v-alert v-if="capped" type="warning" variant="tonal" class="mt-4">{{ t('meetings.search.capped') }}</v-alert>

    <v-list v-if="mode === 'meeting' && meetingHits.length" class="mt-4" density="compact">
      <v-list-item v-for="meeting in meetingHits" :key="meeting.id" @click="emit('open', meeting.id)">
        <v-list-item-title>{{ meeting.title }}</v-list-item-title>
        <v-list-item-subtitle>
          {{ formatWhen(meeting.startAt) }}
          <span v-if="topicName(meeting.topicId)"> · {{ topicName(meeting.topicId) }}</span>
          <span v-if="organizerName(meeting.organizerId)"> · {{ organizerName(meeting.organizerId) }}</span>
          · {{ t(`meetings.status.${meeting.status}`) }}
        </v-list-item-subtitle>
      </v-list-item>
    </v-list>

    <v-list v-else-if="mode === 'content' && contentHits.length" class="mt-4" density="compact">
      <v-list-item v-for="(hit, index) in contentHits" :key="`${hit.meeting.id}-${hit.role}-${index}`" @click="emit('open', hit.meeting.id)">
        <template #prepend>
          <v-chip size="small" variant="tonal" class="mr-2">{{ t(`meetings.search.role.${hit.role}`) }}</v-chip>
        </template>
        <v-list-item-title>
          {{ hit.meeting.title }}
          <span class="text-medium-emphasis"> · {{ formatWhen(hit.meeting.startAt) }}</span>
        </v-list-item-title>
        <v-list-item-subtitle>{{ hit.snippet }}</v-list-item-subtitle>
      </v-list-item>
    </v-list>
    <p v-else-if="ran && !notice && !searching" class="text-medium-emphasis mt-4">{{ t('meetings.search.empty') }}</p>
  </div>
</template>

<style scoped>
.mt-advanced-search {
  background-color: rgba(var(--v-theme-primary), 0.04);
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
