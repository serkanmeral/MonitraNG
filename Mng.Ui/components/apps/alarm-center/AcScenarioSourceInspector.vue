<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { ScenarioSource } from '@/types/apps/scenario';
import AcEventSelectorField from '@/components/apps/alarm-center/AcEventSelectorField.vue';
import {
  fetchDiscoveryHosts,
  type DiscoveryHostDto,
} from '@/services/siemDiscoveryService';
import {
  buildSecEventHostComboItems,
  buildSecEventHostDirectory,
  formatSecEventHostLabel,
  resolveSecEventHostFilterValue,
} from '@/utils/secEventHostLabels';
import type { EventCatalogSelection } from '@/utils/alarm/eventCatalog';
import {
  SIMPLE_METRIC_OPERATORS,
  SIMPLE_SOURCE_ALL_HOSTS,
  applySimpleSourceToConfig,
  channelsForPlatform,
  coerceSimpleSourceState,
  defaultMetricComparison,
  filterDiscoveryHostsForPlatform,
  inferSimpleSourceState,
  normalizeSimpleHosts,
  presetsForSimpleSource,
  simpleSourceSubtitle,
  sourceTypeForPlatform,
  type SimpleMetricOperator,
  type SimpleSourceChannel,
  type SimpleSourcePlatform,
  type SimpleSourceState,
} from '@/utils/alarm/scenarioSimpleSource';

const props = defineProps<{
  source: ScenarioSource;
  simple: SimpleSourceState | null;
  disabled?: boolean;
}>();

const emit = defineEmits<{
  change: [payload: {
    source: ScenarioSource;
    simple: SimpleSourceState;
    subtitle: string;
    sourceType: string | null;
  }];
}>();

const { t } = useAppI18n();
const advancedOpen = ref<string[]>([]);
const hostsLoading = ref(false);
const discoveryHosts = ref<DiscoveryHostDto[]>([]);

const local = ref<SimpleSourceState>(
  coerceSimpleSourceState(
    props.simple ?? inferSimpleSourceState(props.source, props.simple?.hosts ?? [], props.simple?.events ?? []),
  ),
);

watch(
  () => [props.source, props.simple] as const,
  () => {
    local.value = coerceSimpleSourceState(
      props.simple ?? inferSimpleSourceState(props.source, props.simple?.hosts ?? [], props.simple?.events ?? []),
    );
  },
  { deep: true },
);

const platformItems = computed(() => ([
  { value: 'windows', title: t('alarmCenter.scenarioStudio.simpleSource.platform.windows') },
  { value: 'linux', title: t('alarmCenter.scenarioStudio.simpleSource.platform.linux') },
  { value: 'other', title: t('alarmCenter.scenarioStudio.simpleSource.platform.other') },
]));

const channelItems = computed(() =>
  channelsForPlatform(local.value.platform).map(channel => ({
    value: channel,
    title: t(`alarmCenter.scenarioStudio.simpleSource.channel.${channel}`),
  })),
);

const useEventSelector = computed(
  () => local.value.channel === 'eventlog'
    && (local.value.platform === 'windows' || local.value.platform === 'linux'),
);

const useMetricComparison = computed(() => local.value.channel === 'metric');

const eventSelectorPlatform = computed<'windows' | 'linux'>(() =>
  local.value.platform === 'linux' ? 'linux' : 'windows',
);

const matchPresets = computed(() =>
  presetsForSimpleSource(local.value.platform, local.value.channel).map(item => ({
    value: item.value,
    title: t(item.labelKey),
  })),
);

const metricOperatorItems = computed(() =>
  SIMPLE_METRIC_OPERATORS.map(op => ({
    value: op,
    title: t(`alarmCenter.scenarioStudio.simpleSource.metricOperator.${op}`),
  })),
);

const matchKeyLabel = computed(() => {
  if (local.value.channel === 'metric') {
    return t('alarmCenter.scenarioStudio.simpleSource.metricLabel');
  }
  if (local.value.channel === 'app') {
    return t('alarmCenter.scenarioStudio.simpleSource.appLabel');
  }
  return t('alarmCenter.scenarioStudio.simpleSource.eventLabel');
});

const metricState = computed(() =>
  local.value.metric ?? defaultMetricComparison(props.source.matchKey || 'cpu_usage'),
);

const hostDirectory = computed(() =>
  buildSecEventHostDirectory(
    filterDiscoveryHostsForPlatform(discoveryHosts.value, local.value.platform),
  ),
);

const hostItems = computed(() => {
  const discoveryItems = buildSecEventHostComboItems(
    hostDirectory.value,
    local.value.hosts,
  );
  return [
    {
      value: SIMPLE_SOURCE_ALL_HOSTS,
      title: t('alarmCenter.scenarioStudio.simpleSource.allHosts'),
    },
    ...discoveryItems,
  ];
});

const hostSelection = computed(() =>
  local.value.hosts.length ? local.value.hosts : [SIMPLE_SOURCE_ALL_HOSTS],
);

function emitChange(nextSimple: SimpleSourceState, matchKey = props.source.matchKey) {
  const source = applySimpleSourceToConfig(props.source, nextSimple, matchKey);
  emit('change', {
    source,
    simple: nextSimple,
    subtitle: simpleSourceSubtitle(nextSimple, source.matchKey, t),
    sourceType: sourceTypeForPlatform(nextSimple.platform, nextSimple.channel),
  });
}

function setPlatform(platform: SimpleSourcePlatform) {
  const channels = channelsForPlatform(platform);
  const channel = channels.includes(local.value.channel)
    ? local.value.channel
    : channels[0];
  const keepEvents = channel === 'eventlog'
    && (platform === 'windows' || platform === 'linux')
    && local.value.platform === platform;
  const next = {
    ...local.value,
    platform,
    channel,
    events: keepEvents ? local.value.events : [],
    metric: channel === 'metric'
      ? (local.value.metric ?? defaultMetricComparison())
      : null,
  };
  local.value = next;
  if ((platform === 'windows' || platform === 'linux') && channel === 'eventlog') {
    emitChange(next, props.source.matchKey);
    return;
  }
  if (channel === 'metric') {
    emitChange(next, next.metric?.key || 'cpu_usage');
    return;
  }
  const presets = presetsForSimpleSource(platform, channel);
  const matchKey = presets.some(item => item.value === props.source.matchKey)
    ? props.source.matchKey
    : (presets[0]?.value ?? '');
  emitChange(next, matchKey);
}

function setChannel(channel: SimpleSourceChannel) {
  const keepEvents = channel === 'eventlog'
    && (local.value.platform === 'windows' || local.value.platform === 'linux');
  const next = {
    ...local.value,
    channel,
    events: keepEvents ? local.value.events : [],
    metric: channel === 'metric'
      ? (local.value.metric ?? defaultMetricComparison(props.source.matchKey || 'cpu_usage'))
      : null,
  };
  local.value = next;
  if (keepEvents) {
    emitChange(next, props.source.matchKey);
    return;
  }
  if (channel === 'metric') {
    emitChange(next, next.metric?.key || 'cpu_usage');
    return;
  }
  const presets = presetsForSimpleSource(next.platform, channel);
  const matchKey = presets.some(item => item.value === props.source.matchKey)
    ? props.source.matchKey
    : (presets[0]?.value ?? '');
  emitChange(next, matchKey);
}

function onEventsUpdate(events: EventCatalogSelection[]) {
  const next = { ...local.value, events };
  local.value = next;
  emitChange(next);
}

function updateMetric(patch: Partial<{ key: string; operator: SimpleMetricOperator; threshold: number }>) {
  const current = metricState.value;
  let nextMetric = { ...current, ...patch };
  if (patch.key && patch.key !== current.key && patch.operator == null && patch.threshold == null) {
    // Switching metric applies sensible defaults for that metric.
    nextMetric = defaultMetricComparison(patch.key);
  }
  const next = { ...local.value, metric: nextMetric, channel: 'metric' as const };
  local.value = next;
  emitChange(next, nextMetric.key);
}

function coerceComboValue(value: unknown): string | null {
  if (value == null) return null;
  if (typeof value === 'string') return value;
  if (typeof value === 'object' && value !== null && 'value' in value) {
    return String((value as { value: unknown }).value ?? '');
  }
  return String(value);
}

function onHostsUpdate(value: unknown) {
  const raw = Array.isArray(value) ? value : [];
  const selected = raw
    .map(item => coerceComboValue(item))
    .filter((item): item is string => !!item);

  if (!selected.length || selected.includes(SIMPLE_SOURCE_ALL_HOSTS)) {
    const last = selected[selected.length - 1];
    if (last === SIMPLE_SOURCE_ALL_HOSTS || !selected.length) {
      const next = { ...local.value, hosts: [] as string[] };
      local.value = next;
      emitChange(next);
      return;
    }
  }

  const hosts = normalizeSimpleHosts(
    selected
      .filter(item => item !== SIMPLE_SOURCE_ALL_HOSTS)
      .map(item => resolveSecEventHostFilterValue(item, hostDirectory.value)),
  );
  const next = { ...local.value, hosts };
  local.value = next;
  emitChange(next);
}

function hostChipTitle(host: string): string {
  if (host === SIMPLE_SOURCE_ALL_HOSTS) {
    return t('alarmCenter.scenarioStudio.simpleSource.allHosts');
  }
  return formatSecEventHostLabel(host, hostDirectory.value);
}

function setMatchKey(value: string | null) {
  emitChange(local.value, String(value ?? '').trim());
}

async function loadHosts() {
  hostsLoading.value = true;
  try {
    const res = await fetchDiscoveryHosts({ limit: 2000 });
    discoveryHosts.value = res.items ?? [];
  } catch {
    discoveryHosts.value = [];
  } finally {
    hostsLoading.value = false;
  }
}

onMounted(() => {
  void loadHosts();
});
</script>

<template>
  <div class="simple-source">
    <p class="simple-source__intro mb-3">
      {{ t('alarmCenter.scenarioStudio.simpleSource.intro') }}
    </p>

    <v-select
      :model-value="local.platform"
      :items="platformItems"
      item-title="title"
      item-value="value"
      :disabled="disabled"
      :label="t('alarmCenter.scenarioStudio.simpleSource.platformLabel')"
      density="compact"
      class="mb-2"
      @update:model-value="setPlatform"
    />

    <v-select
      :model-value="local.channel"
      :items="channelItems"
      item-title="title"
      item-value="value"
      :disabled="disabled"
      :label="t('alarmCenter.scenarioStudio.simpleSource.channelLabel')"
      density="compact"
      class="mb-2"
      @update:model-value="setChannel"
    />

    <AcEventSelectorField
      v-if="useEventSelector"
      :model-value="local.events"
      :platform="eventSelectorPlatform"
      :disabled="disabled"
      :label="t('alarmCenter.scenarioStudio.simpleSource.eventLabel')"
      class="mb-3"
      @update:model-value="onEventsUpdate"
    />

    <template v-else-if="useMetricComparison">
      <v-select
        :model-value="metricState.key"
        :items="matchPresets"
        item-title="title"
        item-value="value"
        :disabled="disabled"
        :label="t('alarmCenter.scenarioStudio.simpleSource.metricLabel')"
        density="compact"
        class="mb-2"
        @update:model-value="updateMetric({ key: String($event ?? '') })"
      />
      <div class="d-flex flex-wrap ga-2 mb-2">
        <v-select
          :model-value="metricState.operator"
          :items="metricOperatorItems"
          item-title="title"
          item-value="value"
          :disabled="disabled"
          :label="t('alarmCenter.scenarioStudio.simpleSource.metricOperatorLabel')"
          density="compact"
          class="ac-metric-op"
          @update:model-value="updateMetric({ operator: $event })"
        />
        <v-text-field
          :model-value="metricState.threshold"
          type="number"
          :disabled="disabled"
          :label="t('alarmCenter.scenarioStudio.simpleSource.metricThresholdLabel')"
          density="compact"
          class="ac-metric-threshold"
          @update:model-value="updateMetric({ threshold: Number($event) })"
        />
      </div>
      <p class="text-caption text-medium-emphasis mb-3">
        {{ t('alarmCenter.scenarioStudio.simpleSource.metricHint') }}
      </p>
    </template>

    <template v-else>
      <v-combobox
        :model-value="source.matchKey"
        :items="matchPresets.map(item => item.value)"
        :disabled="disabled"
        :label="matchKeyLabel"
        density="compact"
        clearable
        class="mb-1"
        @update:model-value="setMatchKey"
      />
      <div v-if="matchPresets.length" class="mb-3">
        <v-chip
          v-for="item in matchPresets.slice(0, 8)"
          :key="`${local.platform}:${local.channel}:${item.value}`"
          size="x-small"
          variant="tonal"
          class="ma-1"
          :disabled="disabled"
          @click="setMatchKey(item.value)"
        >
          {{ item.title }}
        </v-chip>
      </div>
    </template>

    <v-combobox
      :model-value="hostSelection"
      :items="hostItems"
      item-title="title"
      item-value="value"
      :disabled="disabled"
      :loading="hostsLoading"
      :label="t('alarmCenter.scenarioStudio.simpleSource.hostLabel')"
      :hint="t('alarmCenter.scenarioStudio.simpleSource.hostHint')"
      persistent-hint
      density="compact"
      clearable
      multiple
      chips
      closable-chips
      class="mb-2"
      @update:model-value="onHostsUpdate"
    >
      <template #chip="{ props: chipProps, item }">
        <v-chip v-bind="chipProps" size="small">
          {{ hostChipTitle(String(item?.value ?? item?.title ?? item ?? '')) }}
        </v-chip>
      </template>
    </v-combobox>

    <v-expansion-panels v-model="advancedOpen" class="mt-2" variant="accordion">
      <v-expansion-panel value="advanced">
        <v-expansion-panel-title class="text-caption">
          {{ t('alarmCenter.scenarioStudio.simpleSource.advanced') }}
        </v-expansion-panel-title>
        <v-expansion-panel-text>
          <v-text-field
            :model-value="source.kind"
            :label="t('alarmCenter.scenarioStudio.sourceKind')"
            density="compact"
            disabled
            class="mb-2"
          />
          <v-text-field
            :model-value="source.observationKind"
            :label="t('alarmCenter.scenarioStudio.observationKind')"
            density="compact"
            disabled
            class="mb-2"
          />
          <v-text-field
            :model-value="source.matchKey"
            :label="t('alarmCenter.scenarioStudio.matchKey')"
            density="compact"
            disabled
            class="mb-2"
          />
          <v-text-field
            :model-value="(source.matchKeys ?? []).join(', ')"
            :label="t('alarmCenter.scenarioStudio.eventSelector.matchKeys')"
            density="compact"
            disabled
          />
          <p class="text-caption text-medium-emphasis mt-2 mb-0">
            {{ t('alarmCenter.scenarioStudio.simpleSource.advancedHint') }}
          </p>
        </v-expansion-panel-text>
      </v-expansion-panel>
    </v-expansion-panels>
  </div>
</template>

<style scoped>
.simple-source__intro {
  font-size: 0.75rem;
  color: rgba(var(--v-theme-on-surface), 0.65);
  line-height: 1.35;
}

.ac-metric-op {
  flex: 1 1 140px;
  min-width: 130px;
}

.ac-metric-threshold {
  flex: 1 1 120px;
  min-width: 110px;
}
</style>
