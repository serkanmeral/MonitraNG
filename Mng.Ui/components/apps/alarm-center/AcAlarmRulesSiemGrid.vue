<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { AlarmRule } from '@/types/apps/alarm';
import {
  buildRuleConditionSummary,
  buildSiemScenarioRows,
  classifyRuleSource,
  ruleSourceColor,
  ruleSourceLabelKey,
} from '@/composables/useAlarmRuleList';
import { severityBand } from '@/composables/useAlarmRuleFormCatalog';
import { scenarioEventsLink, SIEM_SCENARIO_CATALOG } from '@/composables/useSiemScenarioCatalog';

const props = defineProps<{
  rules: AlarmRule[];
  selectedId: string | null;
}>();

const emit = defineEmits<{
  select: [rule: AlarmRule];
  edit: [rule: AlarmRule];
  create: [];
}>();

const { t } = useAppI18n();

const rows = computed(() => buildSiemScenarioRows(props.rules));

function scenarioDef(id: string) {
  return SIEM_SCENARIO_CATALOG.find((s) => s.id === id);
}

function severityColor(severity: number): string {
  if (severity >= 8) return 'error';
  if (severity >= 5) return 'warning';
  return 'info';
}

function cardClass(rule: AlarmRule | null, scenarioId: string): Record<string, boolean> {
  return {
    'ac-siem-card--defined': !!rule,
    'ac-siem-card--missing': !rule,
    'ac-siem-card--selected': !!rule && rule.id === props.selectedId,
    'ac-siem-card--disabled': !!rule && !rule.enabled,
  };
}
</script>

<template>
  <v-row dense>
    <v-col v-for="row in rows" :key="row.scenarioId" cols="12" sm="6" lg="4">
      <v-card
        variant="outlined"
        class="ac-siem-card h-100"
        :class="cardClass(row.rule, row.scenarioId)"
        @click="row.rule ? emit('select', row.rule) : undefined"
      >
        <v-card-text class="pa-4">
          <div class="d-flex align-center justify-space-between gap-2 mb-2">
            <v-chip size="small" color="primary" variant="flat">{{ row.scenarioId }}</v-chip>
            <v-chip v-if="row.rule" size="x-small" :color="row.rule.enabled ? 'success' : 'default'" variant="tonal">
              {{ row.rule.enabled ? t('alarmCenter.rules.enabledYes') : t('alarmCenter.rules.enabledNo') }}
            </v-chip>
            <v-chip v-else size="x-small" variant="tonal" color="default">
              {{ t('alarmCenter.rules.siemNotDefined') }}
            </v-chip>
          </div>

          <template v-if="row.rule">
            <div class="text-subtitle-2 font-weight-bold mb-1 text-truncate">{{ row.rule.name }}</div>
            <div class="text-caption text-medium-emphasis mb-3">
              {{ buildRuleConditionSummary(row.rule, t) }}
            </div>
            <div class="d-flex flex-wrap gap-2 mb-3">
              <v-chip size="x-small" :color="ruleSourceColor(classifyRuleSource(row.rule))" variant="tonal">
                {{ t(ruleSourceLabelKey(classifyRuleSource(row.rule))) }}
              </v-chip>
              <v-chip size="x-small" :color="severityColor(row.rule.severity)" variant="tonal">
                {{ row.rule.severity }} · {{ t(`alarmCenter.rules.severityBand.${severityBand(row.rule.severity)}`) }}
              </v-chip>
            </div>
            <div class="d-flex flex-wrap gap-2">
              <v-btn size="x-small" variant="tonal" @click.stop="emit('edit', row.rule!)">
                {{ t('alarmCenter.rules.editTitle') }}
              </v-btn>
              <v-btn
                size="x-small"
                variant="text"
                :to="`/apps/alarm-center/alarms?ruleId=${row.rule!.id}`"
                @click.stop
              >
                {{ t('alarmCenter.rules.openAlarms') }}
              </v-btn>
            </div>
          </template>

          <template v-else>
            <div class="text-body-2 text-medium-emphasis mb-2">
              {{ t('alarmCenter.rules.siemMissingHint', { id: row.scenarioId, matchKey: row.matchKey }) }}
            </div>
            <v-btn
              v-if="scenarioDef(row.scenarioId)"
              size="x-small"
              variant="text"
              :to="scenarioEventsLink(scenarioDef(row.scenarioId)!)"
            >
              {{ t('alarmCenter.rules.viewEvents') }}
            </v-btn>
          </template>
        </v-card-text>
      </v-card>
    </v-col>
  </v-row>
</template>

<style scoped>
.ac-siem-card {
  cursor: default;
  transition: border-color 0.15s ease, background-color 0.15s ease;
}

.ac-siem-card--defined {
  cursor: pointer;
}

.ac-siem-card--defined:hover {
  border-color: rgba(var(--v-theme-primary), 0.45);
}

.ac-siem-card--selected {
  border-color: rgb(var(--v-theme-primary)) !important;
  background: rgba(var(--v-theme-primary), 0.05);
}

.ac-siem-card--missing {
  border-style: dashed;
  opacity: 0.88;
}

.ac-siem-card--disabled {
  opacity: 0.72;
}
</style>
