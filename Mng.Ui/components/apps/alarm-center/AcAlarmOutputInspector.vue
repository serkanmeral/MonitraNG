<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { ScenarioNodeConfig } from '@/types/apps/scenario';
import {
  ALARM_GROUP_BY_FIELD_OPTIONS,
  applyMergeScopeToConfig,
  inferMergeScope,
  normalizeAlarmDedup,
  type AlarmMergeScope,
} from '@/utils/alarm/alarmOutputMerge';

const props = defineProps<{
  config: ScenarioNodeConfig;
  disabled?: boolean;
  severity?: number;
}>();

const emit = defineEmits<{
  change: [config: ScenarioNodeConfig];
  'update:severity': [value: number];
}>();

const { t } = useAppI18n();
const showAdvanced = ref(false);

const dedup = computed(() => normalizeAlarmDedup(props.config.dedup));
const mergeScope = computed(() =>
  inferMergeScope(dedup.value.mergeEnabled !== false, props.config.groupBy ?? []),
);

const mergeScopeItems = computed(() => ([
  { value: 'all' as AlarmMergeScope, title: t('alarmCenter.scenarioStudio.alarmMerge.scopeAll') },
  { value: 'host' as AlarmMergeScope, title: t('alarmCenter.scenarioStudio.alarmMerge.scopeHost') },
  { value: 'user' as AlarmMergeScope, title: t('alarmCenter.scenarioStudio.alarmMerge.scopeUser') },
  { value: 'hostUser' as AlarmMergeScope, title: t('alarmCenter.scenarioStudio.alarmMerge.scopeHostUser') },
  { value: 'custom' as AlarmMergeScope, title: t('alarmCenter.scenarioStudio.alarmMerge.scopeCustom') },
]));

const groupByItems = computed(() =>
  ALARM_GROUP_BY_FIELD_OPTIONS.map(item => ({
    value: item.value,
    title: t(item.labelKey),
  })),
);

const mergeEnabled = computed({
  get: () => dedup.value.mergeEnabled !== false,
  set: (value: boolean) => {
    const scope: AlarmMergeScope = value
      ? (mergeScope.value === 'none' ? 'all' : mergeScope.value)
      : 'none';
    emit('change', applyMergeScopeToConfig(props.config, scope));
  },
});

const scopeModel = computed({
  get: () => (mergeScope.value === 'none' ? 'all' : mergeScope.value),
  set: (value: AlarmMergeScope) => {
    emit('change', applyMergeScopeToConfig(props.config, value));
  },
});

const customGroupBy = computed({
  get: () => [...(props.config.groupBy ?? [])],
  set: (fields: Array<string | { value?: string; title?: string }>) => {
    const normalized = fields
      .map((field) => (typeof field === 'string' ? field : String(field?.value ?? field?.title ?? '')))
      .map(value => value.trim())
      .filter(Boolean);
    emit('change', applyMergeScopeToConfig(props.config, 'custom', normalized));
  },
});

const cooldownMinutes = computed({
  get: () => Math.round((dedup.value.cooldownSeconds || 0) / 60),
  set: (minutes: number) => {
    const next = normalizeAlarmDedup(props.config.dedup);
    next.cooldownSeconds = Math.max(0, Number(minutes) || 0) * 60;
    emit('change', { ...props.config, dedup: next });
  },
});

const keyTemplate = computed({
  get: () => dedup.value.keyTemplate,
  set: (value: string) => {
    const next = normalizeAlarmDedup(props.config.dedup);
    next.keyTemplate = value;
    emit('change', { ...props.config, dedup: next });
  },
});

const severityModel = computed({
  get: () => props.severity ?? props.config.severity ?? 5,
  set: (value: number) => emit('update:severity', value),
});
</script>

<template>
  <div class="alarm-output-inspector">
    <v-text-field
      v-model.number="severityModel"
      type="number"
      min="1"
      max="10"
      :disabled="disabled"
      :label="t('alarmCenter.scenarioStudio.severity')"
      density="compact"
      hide-details="auto"
      class="mb-3"
    />

    <v-switch
      v-model="mergeEnabled"
      :disabled="disabled"
      color="primary"
      density="compact"
      hide-details
      class="mb-1"
      :label="t('alarmCenter.scenarioStudio.alarmMerge.enabled')"
    />
    <p class="text-caption text-medium-emphasis mb-3">
      {{ mergeEnabled
        ? t('alarmCenter.scenarioStudio.alarmMerge.enabledHint')
        : t('alarmCenter.scenarioStudio.alarmMerge.disabledHint') }}
    </p>

    <template v-if="mergeEnabled">
      <v-select
        v-model="scopeModel"
        :items="mergeScopeItems"
        item-title="title"
        item-value="value"
        :disabled="disabled"
        :label="t('alarmCenter.scenarioStudio.alarmMerge.scope')"
        density="compact"
        hide-details="auto"
        class="mb-2"
      />
      <p class="text-caption text-medium-emphasis mb-3">
        {{ t('alarmCenter.scenarioStudio.alarmMerge.scopeHint') }}
      </p>

      <v-combobox
        v-if="scopeModel === 'custom'"
        v-model="customGroupBy"
        :items="groupByItems"
        item-title="title"
        item-value="value"
        :disabled="disabled"
        :label="t('alarmCenter.scenarioStudio.groupBy')"
        density="compact"
        chips
        multiple
        closable-chips
        hide-details="auto"
        class="mb-3"
        :hint="t('alarmCenter.scenarioStudio.alarmMerge.customHint')"
        persistent-hint
      />

      <v-text-field
        v-model.number="cooldownMinutes"
        type="number"
        min="0"
        :disabled="disabled"
        :label="t('alarmCenter.scenarioStudio.cooldownMinutes')"
        density="compact"
        hide-details="auto"
        class="mb-2"
        :hint="t('alarmCenter.scenarioStudio.alarmMerge.cooldownHint')"
        persistent-hint
      />
    </template>

    <v-btn
      size="x-small"
      variant="text"
      class="px-0 mb-1"
      :disabled="disabled && !showAdvanced"
      @click="showAdvanced = !showAdvanced"
    >
      {{ showAdvanced
        ? t('alarmCenter.scenarioStudio.alarmMerge.hideAdvanced')
        : t('alarmCenter.scenarioStudio.alarmMerge.showAdvanced') }}
    </v-btn>
    <v-expand-transition>
      <div v-if="showAdvanced" class="mt-1">
        <v-text-field
          v-model="keyTemplate"
          :disabled="disabled"
          :label="t('alarmCenter.scenarioStudio.dedupTemplate')"
          density="compact"
          hide-details="auto"
          :hint="t('alarmCenter.scenarioStudio.alarmMerge.templateHint')"
          persistent-hint
        />
      </div>
    </v-expand-transition>
  </div>
</template>
