<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import DiFilePreviewDialog from '@/components/apps/document-intelligence/DiFilePreviewDialog.vue';
import DiMarkdownPreviewDialog from '@/components/apps/document-intelligence/DiMarkdownPreviewDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAppToast } from '@/composables/useAppToast';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { diCreateMarkdown, diGetById } from '@/services/documentIntelligenceService';
import {
  istanbulYmd,
  listOrgMeetingActionRows,
  meetingTopicColorHex,
  type OrgMeeting,
  type OrgMeetingAction,
  type OrgMeetingTopic,
} from '@/services/meetingsService';
import { DI_RESOURCE_TYPE, type DiResource } from '@/types/apps/documentIntelligence';
import { diScanBlocksContent } from '@/utils/diFilePreview';
import { diPageResourceLabel } from '@/utils/diPageResource';

const NO_TOPIC = '__none__';

const props = defineProps<{
  meetings: OrgMeeting[];
  topics: OrgMeetingTopic[];
}>();

const emit = defineEmits<{ open: [id: string] }>();

const { t } = useAppI18n();
const toast = useAppToast();
const panelError = usePanelErrorNotify('errors.dg.generic');

const topicId = ref<string | null>(null);
const fromDate = ref('');
const toDate = ref('');
const actions = ref<OrgMeetingAction[]>([]);
const loading = ref(false);
const saving = ref(false);
const notice = ref('');
const filePreviewOpen = ref(false);
const filePreviewResource = ref<DiResource | null>(null);
const markdownPreviewOpen = ref(false);
const markdownPreviewResource = ref<DiResource | null>(null);

onMounted(async () => {
  const today = istanbulYmd(new Date());
  const bounds = monthBounds(today);
  fromDate.value = bounds.from;
  toDate.value = bounds.to;
  loading.value = true;
  try {
    actions.value = await listOrgMeetingActionRows();
  } catch (error) {
    panelError(error, 'meetings.report.loadFailed');
  } finally {
    loading.value = false;
  }
});

function monthBounds(today: string): { from: string; to: string } {
  const [year, month] = today.split('-').map(Number);
  if (!year || !month) return { from: today, to: today };
  const from = `${year}-${String(month).padStart(2, '0')}-01`;
  const last = new Date(Date.UTC(year, month, 0)).getUTCDate();
  const to = `${year}-${String(month).padStart(2, '0')}-${String(last).padStart(2, '0')}`;
  return { from, to };
}

function formatWhen(value: string): string {
  const parsed = Date.parse(value);
  if (Number.isNaN(parsed)) return value;
  return new Date(parsed).toLocaleString();
}

function inRange(meeting: OrgMeeting): boolean {
  const start = Date.parse(meeting.startAt);
  const from = Date.parse(`${fromDate.value}T00:00:00`);
  const to = Date.parse(`${toDate.value}T23:59:59`);
  if (Number.isNaN(start) || Number.isNaN(from) || Number.isNaN(to)) return false;
  return start >= from && start <= to;
}

const rangeInvalid = computed(() => Boolean(fromDate.value && toDate.value && fromDate.value > toDate.value));

const topicItems = computed(() => {
  const byName = (left: OrgMeetingTopic, right: OrgMeetingTopic) => left.name.localeCompare(right.name, 'tr');
  const active = props.topics.filter((topic) => !topic.archived).sort(byName);
  const archived = props.topics.filter((topic) => topic.archived).sort(byName);
  return [
    { title: t('meetings.summary.noTopic'), value: NO_TOPIC, color: '#546e7a' },
    ...active.map((topic) => ({
      title: topic.code ? `${topic.name} (${topic.code})` : topic.name,
      value: topic.id,
      color: meetingTopicColorHex(topic.color),
    })),
    ...archived.map((topic) => ({
      title: `${topic.name} (${t('meetings.search.archivedTopic')})`,
      value: topic.id,
      color: meetingTopicColorHex(topic.color),
    })),
  ];
});

const selectedTopic = computed(() => topicItems.value.find((item) => item.value === topicId.value) || null);

const rows = computed(() => {
  if (!topicId.value || rangeInvalid.value) return [];
  const wanted = topicId.value === NO_TOPIC ? '' : topicId.value;
  const byMeeting = new Map<string, OrgMeetingAction[]>();
  for (const action of actions.value) {
    const list = byMeeting.get(action.meetingId);
    if (list) list.push(action);
    else byMeeting.set(action.meetingId, [action]);
  }
  return props.meetings
    .filter((meeting) => meeting.status !== 'cancelled' && (meeting.topicId || '') === wanted && inRange(meeting))
    .sort((left, right) => Date.parse(right.startAt) - Date.parse(left.startAt))
    .map((meeting) => ({
      meeting,
      actions: (byMeeting.get(meeting.id) || []).slice().sort((left, right) => {
        if (left.status !== right.status) return left.status === 'open' ? -1 : 1;
        const leftDue = left.dueAt ? Date.parse(left.dueAt) : Number.POSITIVE_INFINITY;
        const rightDue = right.dueAt ? Date.parse(right.dueAt) : Number.POSITIVE_INFINITY;
        if (leftDue !== rightDue) return leftDue - rightDue;
        return left.title.localeCompare(right.title, 'tr');
      }),
    }));
});

function scanBlockText(status: string | null | undefined): string {
  if (status === 'threat') return t('documentIntelligence.scanThreatMessage');
  if (status === 'failed') return t('documentIntelligence.scanFailedMessage');
  return t('documentIntelligence.scanPendingMessage');
}

async function openMinutes(meeting: OrgMeeting) {
  if (!meeting.minutesResourceId) return;
  notice.value = '';
  try {
    const resource = await diGetById(meeting.minutesResourceId);
    if (diScanBlocksContent(resource)) {
      notice.value = scanBlockText(resource.scanStatus);
      return;
    }
    if (resource.type === DI_RESOURCE_TYPE.markdown) {
      markdownPreviewResource.value = resource;
      markdownPreviewOpen.value = true;
      return;
    }
    filePreviewResource.value = resource;
    filePreviewOpen.value = true;
  } catch (error) {
    panelError(error, 'meetings.report.previewFailed');
  }
}

function oneLine(value: string): string {
  return value.replace(/\s+/g, ' ').trim();
}

async function minutesName(id: string): Promise<string> {
  try {
    const resource = await diGetById(id);
    return diPageResourceLabel(resource) || t('meetings.fields.minutes');
  } catch {
    return t('meetings.fields.minutes');
  }
}

async function savePage() {
  const topic = selectedTopic.value;
  if (!topic || !rows.value.length || saving.value) return;
  saving.value = true;
  try {
    const ids = [...new Set(rows.value.map((row) => row.meeting.minutesResourceId).filter((id): id is string => Boolean(id)))];
    const names = new Map<string, string>();
    await Promise.all(ids.map(async (id) => names.set(id, await minutesName(id))));
    const lines = [
      `# ${oneLine(topic.title)}`,
      '',
      `${fromDate.value} – ${toDate.value}`,
      '',
    ];
    for (const row of rows.value) {
      lines.push(`## ${oneLine(row.meeting.title)}`);
      lines.push(formatWhen(row.meeting.startAt));
      lines.push(t(`meetings.status.${row.meeting.status}`));
      const minutesId = row.meeting.minutesResourceId;
      lines.push(minutesId ? `${t('meetings.fields.minutes')}: ${names.get(minutesId) || t('meetings.fields.minutes')}` : t('meetings.report.noMinutes'));
      if (!row.actions.length) lines.push(t('meetings.report.noActions'));
      for (const action of row.actions) {
        const mark = action.status === 'done' ? t('meetings.report.actionDone') : t('meetings.report.actionOpen');
        const due = action.dueAt ? ` · ${action.dueAt}` : '';
        lines.push(`- ${mark} — ${oneLine(action.title)}${due}`);
      }
      lines.push('');
    }
    const title = oneLine(`${topic.title} ${fromDate.value} ${toDate.value}`).slice(0, 180);
    await diCreateMarkdown({ parentId: null, title, content: lines.join('\n'), isDraft: false });
    toast.push({
      title: t('meetings.report.savedTitle'),
      message: t('meetings.report.saved'),
      severity: 'success',
    });
  } catch (error) {
    panelError(error, 'meetings.report.saveFailed');
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <div>
    <p class="text-body-2 text-medium-emphasis mb-4">{{ t('meetings.report.hint') }}</p>
    <v-row dense>
      <v-col cols="12" md="4">
        <v-select
          v-model="topicId"
          :items="topicItems"
          item-title="title"
          item-value="value"
          :label="t('meetings.report.topic')"
          variant="outlined"
          density="comfortable"
          hide-details
        >
          <template #item="{ props: itemProps, item }">
            <v-list-item v-bind="itemProps">
              <template #prepend>
                <span class="mt-report-dot me-2" :style="{ background: item.raw.color }" />
              </template>
            </v-list-item>
          </template>
          <template #selection="{ item }">
            <span class="d-inline-flex align-center">
              <span class="mt-report-dot me-2" :style="{ background: item.raw.color }" />
              {{ item.title }}
            </span>
          </template>
        </v-select>
      </v-col>
      <v-col cols="12" sm="6" md="3">
        <v-text-field v-model="fromDate" :label="t('meetings.report.from')" type="date" variant="outlined" density="comfortable" hide-details />
      </v-col>
      <v-col cols="12" sm="6" md="3">
        <v-text-field v-model="toDate" :label="t('meetings.report.to')" type="date" variant="outlined" density="comfortable" hide-details />
      </v-col>
      <v-col cols="12" md="2" class="d-flex align-center">
        <v-btn color="primary" variant="tonal" block :loading="saving" :disabled="!rows.length" @click="savePage">
          {{ t('meetings.report.savePage') }}
        </v-btn>
      </v-col>
    </v-row>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="my-4" />
    <v-alert v-if="notice" type="warning" variant="tonal" class="mt-4" closable @click:close="notice = ''">{{ notice }}</v-alert>
    <p v-if="!topicId" class="text-medium-emphasis mt-4">{{ t('meetings.report.pickTopic') }}</p>
    <p v-else-if="rangeInvalid" class="text-medium-emphasis mt-4">{{ t('meetings.report.range') }}</p>
    <p v-else-if="!loading && !rows.length" class="text-medium-emphasis mt-4">{{ t('meetings.report.empty') }}</p>

    <div v-else class="d-flex flex-column ga-3 mt-4">
      <section v-for="row in rows" :key="row.meeting.id" class="mt-report-day rounded-lg pa-4">
        <div class="d-flex flex-wrap align-center ga-2">
          <button type="button" class="text-subtitle-1 font-weight-medium text-primary text-start" @click="emit('open', row.meeting.id)">
            {{ row.meeting.title }}
          </button>
          <v-chip size="x-small" variant="tonal">{{ t(`meetings.status.${row.meeting.status}`) }}</v-chip>
          <v-spacer />
          <span class="text-caption text-medium-emphasis">{{ formatWhen(row.meeting.startAt) }}</span>
        </div>
        <div class="mt-2">
          <v-btn v-if="row.meeting.minutesResourceId" size="small" variant="text" class="px-0" @click="openMinutes(row.meeting)">
            {{ t('meetings.fields.minutes') }}
          </v-btn>
          <span v-else class="text-caption text-medium-emphasis">{{ t('meetings.report.noMinutes') }}</span>
        </div>
        <v-list v-if="row.actions.length" density="compact" class="pa-0 mt-1">
          <v-list-item v-for="action in row.actions" :key="action.id" class="px-0">
            <template #prepend>
              <v-chip size="x-small" :color="action.status === 'done' ? 'success' : 'info'" variant="tonal" class="me-2">
                {{ action.status === 'done' ? t('meetings.report.actionDone') : t('meetings.report.actionOpen') }}
              </v-chip>
            </template>
            <v-list-item-title :class="{ 'text-decoration-line-through': action.status === 'done' }">{{ action.title }}</v-list-item-title>
            <v-list-item-subtitle v-if="action.dueAt || action.note">
              <span v-if="action.dueAt">{{ action.dueAt }}</span>
              <span v-if="action.dueAt && action.note"> · </span>
              <span v-if="action.note">{{ action.note }}</span>
            </v-list-item-subtitle>
          </v-list-item>
        </v-list>
        <p v-else class="text-caption text-medium-emphasis mb-0 mt-1">{{ t('meetings.report.noActions') }}</p>
      </section>
    </div>

    <DiFilePreviewDialog v-model="filePreviewOpen" :resource="filePreviewResource" />
    <DiMarkdownPreviewDialog v-model="markdownPreviewOpen" :resource="markdownPreviewResource" />
  </div>
</template>

<style scoped>
.mt-report-day {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.mt-report-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  display: inline-block;
  flex: 0 0 auto;
}
</style>
