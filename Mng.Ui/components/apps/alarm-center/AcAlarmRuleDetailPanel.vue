<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { AlarmRule } from '@/types/apps/alarm';
import {
  buildRuleConditionSummary,
  classifyRuleSource,
  getRuleScenarioId,
  isDuplicateRule,
  ruleSourceColor,
  ruleSourceLabelKey,
} from '@/composables/useAlarmRuleList';
import { severityBand, typeLabelKey } from '@/composables/useAlarmRuleFormCatalog';

const props = defineProps<{
  rule: AlarmRule;
  duplicateKeys: Set<string>;
  toggling?: boolean;
}>();

const emit = defineEmits<{
  edit: [];
  delete: [];
  'toggle-enabled': [];
  'open-alarms': [];
}>();

const { t } = useAppI18n();

const source = computed(() => classifyRuleSource(props.rule));
const scenarioId = computed(() => getRuleScenarioId(props.rule));
const conditionSummary = computed(() => buildRuleConditionSummary(props.rule, t));
const isDuplicate = computed(() => isDuplicateRule(props.rule, props.duplicateKeys));

function severityColor(severity: number): string {
  if (severity >= 8) return 'error';
  if (severity >= 5) return 'warning';
  return 'info';
}
</script>

<template>
  <v-card variant="outlined" class="rounded-lg ac-rule-detail h-100">
    <v-card-title class="text-subtitle-1 font-weight-bold d-flex align-start gap-2 pa-4 pb-2">
      <v-icon icon="mdi-shield-search" size="22" color="primary" class="mt-1" />
      <div class="min-w-0">
        <div class="text-truncate">{{ rule.name }}</div>
        <div class="text-caption text-medium-emphasis font-weight-regular mt-1">
          {{ t('alarmCenter.rules.detailTitle') }}
        </div>
      </div>
    </v-card-title>

    <v-card-text class="pa-4 pt-2">
      <v-alert
        v-if="isDuplicate"
        type="warning"
        variant="tonal"
        density="compact"
        class="mb-3"
        icon="mdi-alert-circle-outline"
      >
        {{ t('alarmCenter.rules.duplicateWarning') }}
      </v-alert>

      <div class="d-flex flex-wrap gap-2 mb-4">
        <v-chip v-if="scenarioId" size="small" color="primary" variant="tonal">{{ scenarioId }}</v-chip>
        <v-chip size="small" :color="ruleSourceColor(source)" variant="tonal">
          {{ t(ruleSourceLabelKey(source)) }}
        </v-chip>
        <v-chip size="small" variant="tonal">{{ t(typeLabelKey(rule.type)) }}</v-chip>
        <v-chip size="small" :color="severityColor(rule.severity)" variant="tonal">
          {{ rule.severity }} · {{ t(`alarmCenter.rules.severityBand.${severityBand(rule.severity)}`) }}
        </v-chip>
      </div>

      <v-alert type="info" variant="tonal" density="compact" class="mb-4" icon="mdi-eye-outline">
        <div class="text-caption text-medium-emphasis mb-1">{{ t('alarmCenter.rules.previewTitle') }}</div>
        <div class="text-body-2">{{ conditionSummary }}</div>
      </v-alert>

      <v-list density="compact" class="bg-transparent pa-0">
        <v-list-item class="px-0">
          <v-list-item-title class="text-caption text-medium-emphasis">{{ t('alarmCenter.rules.fieldMatchKey') }}</v-list-item-title>
          <v-list-item-subtitle class="text-body-2">{{ rule.matchKey }}</v-list-item-subtitle>
        </v-list-item>
        <v-list-item v-if="rule.groupByFields?.length" class="px-0">
          <v-list-item-title class="text-caption text-medium-emphasis">{{ t('alarmCenter.rules.fieldGroupBy') }}</v-list-item-title>
          <v-list-item-subtitle class="text-body-2">{{ rule.groupByFields.join(', ') }}</v-list-item-subtitle>
        </v-list-item>
        <v-list-item class="px-0">
          <v-list-item-title class="text-caption text-medium-emphasis">{{ t('alarmCenter.rules.fieldCooldown') }}</v-list-item-title>
          <v-list-item-subtitle class="text-body-2">{{ rule.cooldownMinutes }} {{ t('alarmCenter.rules.minutesShort') }}</v-list-item-subtitle>
        </v-list-item>
        <v-list-item class="px-0">
          <v-list-item-title class="text-caption text-medium-emphasis">{{ t('alarmCenter.rules.colEnabled') }}</v-list-item-title>
          <v-list-item-subtitle>
            <v-switch
              :model-value="rule.enabled"
              :loading="toggling"
              color="primary"
              density="compact"
              hide-details
              :label="rule.enabled ? t('alarmCenter.rules.enabledYes') : t('alarmCenter.rules.enabledNo')"
              @update:model-value="emit('toggle-enabled')"
            />
          </v-list-item-subtitle>
        </v-list-item>
      </v-list>

      <div class="d-flex flex-wrap gap-2 mt-4">
        <v-btn size="small" variant="tonal" prepend-icon="mdi-pencil" @click="emit('edit')">
          {{ t('alarmCenter.rules.editTitle') }}
        </v-btn>
        <v-btn size="small" variant="tonal" color="primary" prepend-icon="mdi-bell-ring-outline" :to="`/apps/alarm-center/alarms?ruleId=${rule.id}`">
          {{ t('alarmCenter.rules.openAlarms') }}
        </v-btn>
        <v-btn size="small" variant="text" color="error" prepend-icon="mdi-delete" @click="emit('delete')">
          {{ t('alarmCenter.rules.delete') }}
        </v-btn>
      </div>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.ac-rule-detail {
  position: sticky;
  top: 12px;
}
</style>
