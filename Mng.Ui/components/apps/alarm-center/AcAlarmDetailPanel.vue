<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useDisplay } from 'vuetify';
import { useAppI18n } from '@/composables/useAppI18n';
import type { AlarmRule, AlarmSummary } from '@/types/apps/alarm';
import type { SecEventListItem } from '@/types/apps/secEvent';
import { secEventQuery } from '@/services/secEventService';
import {
  buildRelatedEventsQuery,
  copyTextToClipboard,
  eventsLinkForAlarm,
  extractContextFields,
  formatAlarmRelativeTime,
  formatAlarmScenarioLabel,
  formatAlarmSummary,
  getScenarioIdForAlarm,
  isAlarmActionable,
  lifecycleActionLabel,
  parseLifecycleHistory,
  ruleLinkForAlarm,
  severityColor,
  statusColor,
  statusLabel,
} from '@/composables/useAlarmList';
import { buildRuleConditionSummary } from '@/composables/useAlarmRuleList';

const props = defineProps<{
  alarm: AlarmSummary | null;
  rule?: AlarmRule | null;
  ruleName?: string | null;
  loading?: boolean;
  actionLoading?: boolean;
}>();

const emit = defineEmits<{
  close: [];
  acknowledge: [];
  suppress: [];
  resolve: [];
}>();

const { t, locale } = useAppI18n();
const { mdAndUp } = useDisplay();
const copyHint = ref<string | null>(null);
const relatedEvents = ref<SecEventListItem[]>([]);
const relatedLoading = ref(false);

const scenarioId = computed(() => (props.alarm ? getScenarioIdForAlarm(props.alarm) : null));
const scenarioTitle = computed(() =>
  scenarioId.value ? t(`siemCenter.scenarios.${scenarioId.value}.title`) : null,
);
const scenarioDesc = computed(() =>
  scenarioId.value ? t(`siemCenter.scenarios.${scenarioId.value}.desc`) : null,
);
const contextFields = computed(() => (props.alarm ? extractContextFields(props.alarm) : []));
const eventsLink = computed(() => (props.alarm ? eventsLinkForAlarm(props.alarm) : null));
const actionable = computed(() => (props.alarm ? isAlarmActionable(props.alarm.status) : false));
const lifecycleHistory = computed(() => (props.alarm ? parseLifecycleHistory(props.alarm) : []));
const lastManualBy = computed(() => {
  const ctx = props.alarm?.context;
  if (!ctx) return null;
  const by = ctx.manualActionBy ?? ctx.ManualActionBy;
  return typeof by === 'string' && by.trim() ? by.trim() : null;
});
const ruleSummary = computed(() => (props.rule ? buildRuleConditionSummary(props.rule, t) : null));
const displayRuleName = computed(() => props.ruleName ?? props.rule?.name ?? null);

watch(
  () => props.alarm?.id,
  async (id) => {
    if (!id || !props.alarm) {
      relatedEvents.value = [];
      return;
    }
    relatedLoading.value = true;
    try {
      const q = buildRelatedEventsQuery(props.alarm);
      const res = await secEventQuery({
        from: q.from,
        to: q.to,
        eventAction: q.eventAction,
        search: q.search,
        srcIp: q.srcIp,
        actorUser: q.actorUser,
        limit: q.limit,
        excludeUnknown: true,
      });
      relatedEvents.value = res.items;
    } catch {
      relatedEvents.value = [];
    } finally {
      relatedLoading.value = false;
    }
  },
  { immediate: true },
);

function formatDate(value?: string | null): string {
  if (!value) return '—';
  try {
    return new Intl.DateTimeFormat(locale.value === 'tr' ? 'tr-TR' : 'en-GB', {
      dateStyle: 'short',
      timeStyle: 'medium',
    }).format(new Date(value));
  } catch {
    return value ?? '—';
  }
}

function eventActionLabel(action: string): string {
  const key = `siemCenter.events.actions.${action}`;
  const translated = t(key);
  return translated !== key ? translated : action;
}

function autoReasonLabel(reason: string): string {
  const key = `alarmCenter.alarms.autoReason.${reason}`;
  const translated = t(key);
  return translated !== key ? translated : reason;
}

async function copyValue(label: string, value?: string | null) {
  if (!value) return;
  const ok = await copyTextToClipboard(value);
  copyHint.value = ok ? t('alarmCenter.alarms.copied', { label }) : t('alarmCenter.alarms.copyFailed');
  window.setTimeout(() => {
    copyHint.value = null;
  }, 2000);
}
</script>

<template>
  <v-card v-if="alarm" variant="outlined" class="rounded-lg ac-alarm-detail h-100">
    <v-card-title class="d-flex align-start gap-2 pa-4 pb-2">
      <v-icon icon="mdi-bell-alert" color="error" size="22" class="mt-1" />
      <div class="min-w-0 flex-grow-1">
        <div class="text-subtitle-1 font-weight-bold">{{ t('alarmCenter.alarms.detailTitle') }}</div>
        <div v-if="scenarioTitle" class="text-body-2 text-medium-emphasis mt-1">{{ scenarioTitle }}</div>
      </div>
      <v-btn v-if="!mdAndUp" icon="mdi-close" variant="text" size="small" @click="emit('close')" />
    </v-card-title>

    <v-card-text class="pa-4 pt-0">
      <v-skeleton-loader v-if="loading" type="article" />

      <template v-else>
        <v-alert v-if="copyHint" type="success" variant="tonal" density="compact" class="mb-3">
          {{ copyHint }}
        </v-alert>

        <div class="d-flex flex-wrap gap-2 mb-4">
          <v-chip v-if="scenarioId" size="small" color="primary" variant="flat">{{ scenarioId }}</v-chip>
          <v-chip size="small" :color="severityColor(alarm.severity)" variant="flat">
            {{ alarm.severity }}
          </v-chip>
          <v-chip size="small" :color="statusColor(alarm.status)" variant="tonal">
            {{ statusLabel(alarm.status, t) }}
          </v-chip>
          <v-chip size="small" variant="tonal">
            {{ t('alarmCenter.alarms.colCount') }}: {{ alarm.count.toLocaleString() }}
          </v-chip>
        </div>

        <div v-if="actionable" class="d-flex flex-wrap gap-2 mb-4">
          <v-btn
            size="small"
            variant="tonal"
            color="warning"
            prepend-icon="mdi-check-circle-outline"
            :loading="actionLoading"
            @click="emit('acknowledge')"
          >
            {{ t('alarmCenter.alarms.actionAcknowledge') }}
          </v-btn>
          <v-btn
            size="small"
            variant="tonal"
            prepend-icon="mdi-bell-off-outline"
            :loading="actionLoading"
            @click="emit('suppress')"
          >
            {{ t('alarmCenter.alarms.actionSuppress') }}
          </v-btn>
          <v-btn
            size="small"
            variant="tonal"
            color="success"
            prepend-icon="mdi-check-all"
            :loading="actionLoading"
            @click="emit('resolve')"
          >
            {{ t('alarmCenter.alarms.actionResolve') }}
          </v-btn>
        </div>

        <v-alert v-if="scenarioDesc" type="info" variant="tonal" density="compact" class="mb-4" icon="mdi-information-outline">
          {{ scenarioDesc }}
        </v-alert>

        <v-alert v-if="ruleSummary" type="info" variant="tonal" density="compact" class="mb-4" icon="mdi-shield-crown-outline">
          <div class="text-caption text-medium-emphasis mb-1">{{ t('alarmCenter.alarms.ruleSummary') }}</div>
          <div class="text-body-2">{{ ruleSummary }}</div>
        </v-alert>

        <div class="text-caption text-medium-emphasis text-uppercase mb-2">
          {{ t('alarmCenter.alarms.sectionOverview') }}
        </div>
        <v-list density="compact" class="bg-transparent mb-4 pa-0">
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('siemCenter.dashboard.alarmColScenario') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ formatAlarmScenarioLabel(alarm, t) }}</v-list-item-subtitle>
          </v-list-item>
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('siemCenter.dashboard.alarmColSummary') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ formatAlarmSummary(alarm) }}</v-list-item-subtitle>
          </v-list-item>
          <v-list-item v-if="displayRuleName" class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('alarmCenter.alarms.colRule') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ displayRuleName }}</v-list-item-subtitle>
          </v-list-item>
          <v-list-item v-if="lastManualBy" class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('alarmCenter.alarms.lastManualBy') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">{{ lastManualBy }}</v-list-item-subtitle>
          </v-list-item>
        </v-list>

        <div class="text-caption text-medium-emphasis text-uppercase mb-2">
          {{ t('alarmCenter.alarms.sectionLifecycle') }}
        </div>
        <v-timeline
          v-if="lifecycleHistory.length"
          side="end"
          density="compact"
          truncate-line="both"
          align="start"
          class="mb-4 ac-alarm-lifecycle"
        >
          <v-timeline-item
            v-for="(entry, idx) in lifecycleHistory"
            :key="`${entry.at}-${entry.action}-${idx}`"
            :dot-color="entry.source === 'manual' ? 'primary' : 'grey'"
            size="x-small"
          >
            <div class="text-body-2 font-weight-medium">
              {{ lifecycleActionLabel(entry.action, t) }}
            </div>
            <div class="text-caption text-medium-emphasis">
              {{ formatDate(entry.at) }}
              · {{ entry.byUserName || t('alarmCenter.alarms.actorSystem') }}
              <span v-if="entry.source === 'automatic' && entry.reason">
                · {{ autoReasonLabel(entry.reason) }}
              </span>
            </div>
          </v-timeline-item>
        </v-timeline>
        <p v-else class="text-body-2 text-medium-emphasis mb-4">
          {{ t('alarmCenter.alarms.noLifecycleHistory') }}
        </p>

        <div class="text-caption text-medium-emphasis text-uppercase mb-2">
          {{ t('alarmCenter.alarms.sectionRelatedEvents') }}
        </div>
        <v-skeleton-loader v-if="relatedLoading" type="list-item@3" class="mb-4" />
        <v-list v-else-if="relatedEvents.length" density="compact" class="bg-transparent mb-4 pa-0 rounded-lg border">
          <v-list-item
            v-for="ev in relatedEvents"
            :key="ev.id"
            :to="`/apps/siem-center/events?eventAction=${encodeURIComponent(ev.eventAction)}`"
            class="px-3"
          >
            <v-list-item-title class="text-body-2">{{ eventActionLabel(ev.eventAction) }}</v-list-item-title>
            <v-list-item-subtitle class="text-caption">
              {{ formatDate(ev.timestamp) }}
              <span v-if="ev.actorUser || ev.networkSrcIp"> · {{ ev.actorUser || ev.networkSrcIp }}</span>
            </v-list-item-subtitle>
          </v-list-item>
        </v-list>
        <p v-else class="text-body-2 text-medium-emphasis mb-4">{{ t('alarmCenter.alarms.noRelatedEvents') }}</p>

        <div class="text-caption text-medium-emphasis text-uppercase mb-2">
          {{ t('alarmCenter.alarms.sectionTimeline') }}
        </div>
        <v-list density="compact" class="bg-transparent mb-4 pa-0">
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('alarmCenter.alarms.colFirstSeen') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">
              {{ formatDate(alarm.firstSeenAt) }}
              <span class="text-caption text-medium-emphasis">
                · {{ formatAlarmRelativeTime(alarm.firstSeenAt, locale, t) }}
              </span>
            </v-list-item-subtitle>
          </v-list-item>
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('alarmCenter.alarms.colLastSeen') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">
              {{ formatDate(alarm.lastSeenAt) }}
              <span class="text-caption text-medium-emphasis">
                · {{ formatAlarmRelativeTime(alarm.lastSeenAt, locale, t) }}
              </span>
            </v-list-item-subtitle>
          </v-list-item>
        </v-list>

        <template v-if="contextFields.length">
          <div class="text-caption text-medium-emphasis text-uppercase mb-2">
            {{ t('alarmCenter.alarms.sectionContext') }}
          </div>
          <v-list density="compact" class="bg-transparent mb-4 pa-0">
            <v-list-item v-for="field in contextFields" :key="field.key" class="px-0">
              <v-list-item-title class="text-caption text-medium-emphasis">
                {{ field.labelKey.startsWith('alarmCenter.') ? t(field.labelKey) : field.labelKey }}
              </v-list-item-title>
              <v-list-item-subtitle class="text-body-2 text-break">{{ field.value }}</v-list-item-subtitle>
              <template #append>
                <v-btn
                  icon="mdi-content-copy"
                  variant="text"
                  size="x-small"
                  @click="copyValue(field.key, field.value)"
                />
              </template>
            </v-list-item>
          </v-list>
        </template>

        <div class="text-caption text-medium-emphasis text-uppercase mb-2">
          {{ t('alarmCenter.alarms.sectionTechnical') }}
        </div>
        <v-list density="compact" class="bg-transparent mb-4 pa-0">
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('alarmCenter.alarms.colDedupKey') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2 text-break">{{ alarm.dedupKey }}</v-list-item-subtitle>
            <template #append>
              <v-btn icon="mdi-content-copy" variant="text" size="x-small" @click="copyValue('dedupKey', alarm.dedupKey)" />
            </template>
          </v-list-item>
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">ID</v-list-item-title>
            <v-list-item-subtitle class="text-body-2 text-break">{{ alarm.id }}</v-list-item-subtitle>
            <template #append>
              <v-btn icon="mdi-content-copy" variant="text" size="x-small" @click="copyValue('ID', alarm.id)" />
            </template>
          </v-list-item>
          <v-list-item class="px-0">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('alarmCenter.alarms.colCorrelationId') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2 text-break">{{ alarm.correlationId }}</v-list-item-subtitle>
          </v-list-item>
        </v-list>

        <div class="d-flex flex-wrap gap-2">
          <v-btn size="small" variant="tonal" color="primary" prepend-icon="mdi-shield-crown-outline" :to="ruleLinkForAlarm(alarm)">
            {{ t('alarmCenter.alarms.viewRule') }}
          </v-btn>
          <v-btn
            v-if="eventsLink"
            size="small"
            variant="tonal"
            prepend-icon="mdi-shield-search"
            :to="eventsLink"
          >
            {{ t('alarmCenter.alarms.relatedEvents') }}
          </v-btn>
          <v-btn size="small" variant="text" prepend-icon="mdi-view-dashboard-outline" to="/apps/siem-center">
            {{ t('alarmCenter.alarms.backToDashboard') }}
          </v-btn>
        </div>
      </template>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.ac-alarm-detail {
  position: sticky;
  top: 1rem;
  max-height: calc(100vh - 2rem);
  overflow-y: auto;
}
</style>
