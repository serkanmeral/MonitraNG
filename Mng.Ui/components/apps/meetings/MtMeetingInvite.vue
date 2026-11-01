<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAppToast } from '@/composables/useAppToast';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { getAccessToken } from '@/services/apiService';
import {
  createOrgMeetingNoticeLog,
  ensureOrgMeetingNotices,
  listOrgMeetingNoticeLogs,
  listOrgMeetingNoticeSettings,
  type MeetingNoticeKind,
  type OrgMeetingNoticeLog,
  type OrgMeetingNoticeRecipient,
  type OrgMeetingNoticeSetting,
  type OrgAttendanceDraft,
  type OrgMeetingPerson,
  type OrgMeetingStatus,
} from '@/services/meetingsService';
import { useAuthStore } from '@/stores/auth';
import { useUserStore } from '@/stores/apps/user';

const props = defineProps<{
  meetingId: string | null;
  title: string;
  startAt: string;
  endAt: string;
  location: string;
  meetingUrl: string;
  agenda: string;
  status: OrgMeetingStatus;
  attendance: OrgAttendanceDraft[];
  people: OrgMeetingPerson[];
  personNames: Record<string, string>;
}>();

const { t } = useAppI18n();
const toast = useAppToast();
const panelError = usePanelErrorNotify('errors.dg.generic');
const auth = useAuthStore();
const userStore = useUserStore();

const loading = ref(false);
const sending = ref(false);
const settings = ref<OrgMeetingNoticeSetting[]>([]);
const logs = ref<OrgMeetingNoticeLog[]>([]);
const recipients = ref<OrgMeetingNoticeRecipient[]>([]);

watch(() => props.meetingId, () => {
  void load();
}, { immediate: true });

watch(() => props.attendance, () => {
  void resolveRecipients();
}, { deep: true });

function sameInstant(left: string, right: string): boolean {
  if (!left && !right) return true;
  const a = Date.parse(left);
  const b = Date.parse(right);
  if (Number.isNaN(a) || Number.isNaN(b)) return left === right;
  return a === b;
}

function formatWhen(value: string): string {
  const parsed = Date.parse(value);
  if (Number.isNaN(parsed)) return value;
  return new Date(parsed).toLocaleString();
}

function personName(row: OrgAttendanceDraft): string {
  if (row.kind === 'user' && row.userId && props.personNames[row.userId]) return props.personNames[row.userId];
  return row.displayName || row.userId || row.personId || '';
}

const lastSent = computed(() => logs.value.find((log) => log.recipients.some((row) => row.result === 'sent')) || null);

const kind = computed<MeetingNoticeKind>(() => {
  const last = lastSent.value;
  if (!last) return 'invite';
  if (props.status === 'cancelled') return 'cancel';
  const changed = last.title !== props.title.trim()
    || !sameInstant(last.startAt, props.startAt)
    || !sameInstant(last.endAt, props.endAt || '')
    || last.location !== props.location.trim();
  return changed ? 'update' : 'invite';
});

const setting = computed(() => settings.value.find((row) => row.kind === kind.value) || null);

const blockReason = computed(() => {
  if (props.status === 'cancelled' && !lastSent.value) return 'noInvite';
  if (!setting.value?.enabled) return 'disabled';
  if (!setting.value.templateKey) return 'noTemplate';
  if (!recipients.value.length) return 'noPeople';
  return '';
});

async function resolveRecipients() {
  const rows = props.attendance;
  const userIds = rows.filter((row) => row.kind === 'user' && row.userId).map((row) => row.userId as string);
  let emails = new Map<string, string>();
  if (userIds.length) {
    try {
      const users = await userStore.fetchUsersByIds(userIds);
      emails = new Map(users.flatMap((user) => {
        const email = user.email?.trim();
        if (!email) return [];
        return [user.id, user.userId].filter(Boolean).map((id) => [id as string, email] as const);
      }));
    } catch {
      emails = new Map();
    }
  }
  recipients.value = rows.map((row) => {
    const name = personName(row);
    if (row.kind === 'external') {
      const person = props.people.find((item) => item.id === row.personId);
      return { name, email: (row.email || person?.email || '').trim(), result: 'skipped' as const };
    }
    return { name, email: (row.userId ? emails.get(row.userId) || '' : ''), result: 'skipped' as const };
  });
}

async function load() {
  if (!props.meetingId) {
    logs.value = [];
    recipients.value = [];
    return;
  }
  loading.value = true;
  try {
    await ensureOrgMeetingNotices();
    const [noticeSettings, noticeLogs] = await Promise.all([
      listOrgMeetingNoticeSettings(),
      listOrgMeetingNoticeLogs(props.meetingId),
    ]);
    settings.value = noticeSettings;
    logs.value = noticeLogs;
    await resolveRecipients();
  } catch (error) {
    panelError(error, 'meetings.notice.loadFailed');
  } finally {
    loading.value = false;
  }
}

function meetingWhen(): string {
  const start = formatWhen(props.startAt);
  if (!props.endAt) return start;
  return `${start} – ${formatWhen(props.endAt)}`;
}

async function sendOne(email: string, name: string, templateKey: string) {
  await auth.ensureValidToken();
  const token = getAccessToken();
  if (!token) throw new Error('token');
  await $fetch('/api/notifier/v1/notifications/send-template', {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}` },
    body: {
      to: [email],
      templateKey,
      context: {
        meeting: {
          title: props.title.trim(),
          when: meetingWhen(),
          location: props.location.trim(),
          url: props.meetingUrl.trim(),
          agenda: props.agenda.trim().slice(0, 2000),
        },
        person: { name },
      },
    },
  });
}

async function send() {
  if (!props.meetingId || blockReason.value || sending.value) return;
  const templateKey = setting.value?.templateKey || '';
  sending.value = true;
  try {
    const outcomes: OrgMeetingNoticeRecipient[] = [];
    const ready = recipients.value.filter((row) => row.email);
    for (const row of recipients.value.filter((item) => !item.email)) {
      outcomes.push({ name: row.name, email: '', result: 'skipped' });
    }
    for (let index = 0; index < ready.length; index += 5) {
      const slice = ready.slice(index, index + 5);
      await Promise.all(slice.map(async (row) => {
        try {
          await sendOne(row.email, row.name, templateKey);
          outcomes.push({ name: row.name, email: row.email, result: 'sent' });
        } catch {
          outcomes.push({ name: row.name, email: row.email, result: 'failed' });
        }
      }));
    }
    await createOrgMeetingNoticeLog({
      meetingId: props.meetingId,
      kind: kind.value,
      sentAt: new Date().toISOString(),
      title: props.title.trim(),
      startAt: props.startAt,
      endAt: props.endAt,
      location: props.location.trim(),
      recipients: outcomes,
    });
    logs.value = await listOrgMeetingNoticeLogs(props.meetingId);
    const sent = outcomes.filter((row) => row.result === 'sent').length;
    const skipped = outcomes.filter((row) => row.result === 'skipped').length;
    const failed = outcomes.filter((row) => row.result === 'failed').length;
    toast.push({
      title: t('meetings.notice.sentTitle'),
      message: t('meetings.notice.sentSummary', { sent, skipped, failed }),
      severity: failed ? 'warning' : 'success',
    });
  } catch (error) {
    panelError(error, 'meetings.notice.sendFailed');
  } finally {
    sending.value = false;
  }
}

function namesFor(log: OrgMeetingNoticeLog, result: OrgMeetingNoticeRecipient['result']): string {
  return log.recipients.filter((row) => row.result === result).map((row) => row.name || row.email).filter(Boolean).join(', ');
}
</script>

<template>
  <div class="pt-2">
    <p v-if="!meetingId" class="text-medium-emphasis">{{ t('meetings.notice.afterSave') }}</p>
    <template v-else>
      <p class="text-body-2 text-medium-emphasis mb-4">{{ t('meetings.notice.hint') }}</p>
      <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />
      <div class="text-subtitle-2 mb-2">{{ t('meetings.notice.recipients') }}</div>
      <v-list v-if="recipients.length" density="compact" class="pa-0 mb-4">
        <v-list-item v-for="(row, index) in recipients" :key="`${row.name}-${index}`" class="px-0">
          <v-list-item-title>{{ row.name }}</v-list-item-title>
          <v-list-item-subtitle>{{ row.email || t('meetings.notice.noAddress') }}</v-list-item-subtitle>
        </v-list-item>
      </v-list>
      <p v-else class="text-medium-emphasis mb-4">{{ t('meetings.notice.noPeople') }}</p>
      <div class="d-flex flex-wrap align-center ga-3 mb-6">
        <v-btn color="primary" :loading="sending" :disabled="Boolean(blockReason) || loading" @click="send">
          {{ t(`meetings.notice.send.${kind}`) }}
        </v-btn>
        <span v-if="blockReason" class="text-caption text-medium-emphasis">{{ t(`meetings.notice.block.${blockReason}`) }}</span>
      </div>
      <div class="text-subtitle-2 mb-2">{{ t('meetings.notice.log') }}</div>
      <div v-if="logs.length" class="d-flex flex-column ga-3">
        <section v-for="log in logs" :key="log.id" class="mt-invite-log rounded-lg pa-3">
          <div class="d-flex flex-wrap align-center ga-2 mb-2">
            <v-chip size="small" variant="tonal">{{ t(`meetings.notice.kind.${log.kind}`) }}</v-chip>
            <span class="text-caption text-medium-emphasis">{{ formatWhen(log.sentAt) }}</span>
          </div>
          <div v-if="namesFor(log, 'sent')" class="text-body-2">{{ t('meetings.notice.went') }}: {{ namesFor(log, 'sent') }}</div>
          <div v-if="namesFor(log, 'skipped')" class="text-body-2">{{ t('meetings.notice.skipped') }}: {{ namesFor(log, 'skipped') }}</div>
          <div v-if="namesFor(log, 'failed')" class="text-body-2">{{ t('meetings.notice.failed') }}: {{ namesFor(log, 'failed') }}</div>
        </section>
      </div>
      <p v-else class="text-medium-emphasis">{{ t('meetings.notice.logEmpty') }}</p>
    </template>
  </div>
</template>

<style scoped>
.mt-invite-log {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
