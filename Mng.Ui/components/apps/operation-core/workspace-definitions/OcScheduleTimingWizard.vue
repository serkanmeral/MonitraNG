<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  defaultScheduleWizardState,
  formatScheduleHumanSummary,
  getQuartzCronValidationIssue,
  OC_SCHEDULE_TIMEZONE_PRESETS,
  OC_SCHEDULE_WEEKDAY_KEYS,
  validateWizardState,
  wizardStateFromCron,
  wizardStateToCron,
  type OcScheduleWeekdayKey,
  type OcScheduleWizardState,
  type OcScheduleWizardType,
} from '@/utils/ocScheduleCron';

const cronExpression = defineModel<string>('cronExpression', { required: true });
const timezone = defineModel<string>('timezone', { default: 'Europe/Istanbul' });

const { t } = useAppI18n();

const wizard = ref<OcScheduleWizardState>(defaultScheduleWizardState());
const syncingFromCron = ref(false);

const weekdayLabels = computed(() =>
  Object.fromEntries(
    OC_SCHEDULE_WEEKDAY_KEYS.map((key) => [
      key,
      t(`operationCore.workspaceDefinitions.scheduled.weekday.${key}`),
    ])
  ) as Record<OcScheduleWeekdayKey, string>
);

const timezoneItems = computed(() =>
  OC_SCHEDULE_TIMEZONE_PRESETS.map((value) => ({ value, title: value }))
);

const wizardTypes = computed(() => [
  {
    value: 'everyMinutes' as OcScheduleWizardType,
    icon: 'mdi-timer-sand',
    title: t('operationCore.workspaceDefinitions.scheduled.wizardTypeEveryMinutes'),
    hint: t('operationCore.workspaceDefinitions.scheduled.wizardTypeEveryMinutesHint'),
  },
  {
    value: 'everyHours' as OcScheduleWizardType,
    icon: 'mdi-clock-outline',
    title: t('operationCore.workspaceDefinitions.scheduled.wizardTypeEveryHours'),
    hint: t('operationCore.workspaceDefinitions.scheduled.wizardTypeEveryHoursHint'),
  },
  {
    value: 'dailyAt' as OcScheduleWizardType,
    icon: 'mdi-calendar-today',
    title: t('operationCore.workspaceDefinitions.scheduled.wizardTypeDaily'),
    hint: t('operationCore.workspaceDefinitions.scheduled.wizardTypeDailyHint'),
  },
  {
    value: 'weeklyDays' as OcScheduleWizardType,
    icon: 'mdi-calendar-week',
    title: t('operationCore.workspaceDefinitions.scheduled.wizardTypeWeekly'),
    hint: t('operationCore.workspaceDefinitions.scheduled.wizardTypeWeeklyHint'),
  },
  {
    value: 'advanced' as OcScheduleWizardType,
    icon: 'mdi-code-tags',
    title: t('operationCore.workspaceDefinitions.scheduled.wizardTypeAdvanced'),
    hint: t('operationCore.workspaceDefinitions.scheduled.wizardTypeAdvancedHint'),
  },
]);

const selectedTypeHint = computed(
  () => wizardTypes.value.find((x) => x.value === wizard.value.type)?.hint ?? ''
);

const humanSummary = computed(() =>
  formatScheduleHumanSummary(
    cronExpression.value,
    timezone.value || 'Europe/Istanbul',
    weekdayLabels.value,
    (key, params) => t(key, params ?? {})
  )
);

const weeklyDaysInvalid = computed(
  () => wizard.value.type === 'weeklyDays' && wizard.value.weekdays.length === 0
);

function applyWizardToCron() {
  if (syncingFromCron.value) return;
  cronExpression.value = wizardStateToCron(wizard.value);
}

function loadWizardFromCron(expr: string) {
  syncingFromCron.value = true;
  wizard.value = wizardStateFromCron(expr);
  if (wizard.value.type === 'advanced') {
    wizard.value.advancedCron = expr.trim();
  }
  syncingFromCron.value = false;
}

function setWeekdays(keys: OcScheduleWeekdayKey[]) {
  wizard.value.weekdays = keys;
}

function toggleWeekday(key: OcScheduleWeekdayKey) {
  const set = new Set(wizard.value.weekdays);
  if (set.has(key)) set.delete(key);
  else set.add(key);
  wizard.value.weekdays = OC_SCHEDULE_WEEKDAY_KEYS.filter((k) => set.has(k));
}

function isWeekdaySelected(key: OcScheduleWeekdayKey): boolean {
  return wizard.value.weekdays.includes(key);
}

function applyPresetWeekdays(preset: 'weekdays' | 'weekend') {
  if (preset === 'weekdays') {
    setWeekdays(['mon', 'tue', 'wed', 'thu', 'fri']);
  } else {
    setWeekdays(['sat', 'sun']);
  }
}

watch(
  () => cronExpression.value,
  (expr) => {
    const fromWizard = wizardStateToCron(wizard.value);
    if (expr.trim() === fromWizard.trim()) return;
    loadWizardFromCron(expr);
  },
  { immediate: true }
);

watch(
  wizard,
  () => {
    applyWizardToCron();
  },
  { deep: true }
);

function onWizardTypeChange(next: OcScheduleWizardType) {
  if (next === 'advanced' && wizard.value.type !== 'advanced') {
    wizard.value.advancedCron = cronExpression.value.trim();
  }
  wizard.value.type = next;
  if (next === 'weeklyDays' && wizard.value.weekdays.length === 0) {
    wizard.value.weekdays = ['mon'];
  }
}

function validate(): string | null {
  const wizardIssue = validateWizardState(wizard.value);
  if (wizardIssue === 'weeklyDaysEmpty') {
    return t('operationCore.workspaceDefinitions.scheduled.validationWeekdaysEmpty');
  }
  const cronIssue = getQuartzCronValidationIssue(cronExpression.value);
  if (cronIssue === 'empty') return t('operationCore.workspaceDefinitions.scheduled.validationCronEmpty');
  if (cronIssue === 'tooFew') return t('operationCore.workspaceDefinitions.scheduled.validationCronTooFew');
  if (cronIssue === 'tooMany') return t('operationCore.workspaceDefinitions.scheduled.validationCronTooMany');
  return null;
}

defineExpose({ humanSummary, validate });
</script>

<template>
  <div class="oc-schedule-timing-wizard">
    <p class="text-body-2 text-medium-emphasis mb-3">
      {{ t('operationCore.workspaceDefinitions.scheduled.wizardIntro') }}
    </p>

    <v-item-group
      :model-value="wizard.type"
      mandatory
      class="mb-3"
      @update:model-value="onWizardTypeChange"
    >
      <v-row dense>
        <v-col
          v-for="opt in wizardTypes"
          :key="opt.value"
          cols="12"
          sm="6"
        >
          <v-item v-slot="{ isSelected, toggle }" :value="opt.value">
            <v-card
              :variant="isSelected ? 'tonal' : 'outlined'"
              :color="isSelected ? 'primary' : undefined"
              rounded="lg"
              class="oc-schedule-timing-wizard__type-card h-100 cursor-pointer"
              @click="toggle"
            >
              <v-card-text class="d-flex align-start gap-3 py-3">
                <v-icon :icon="opt.icon" size="22" class="mt-1 flex-shrink-0" />
                <div>
                  <div class="text-body-2 font-weight-bold">{{ opt.title }}</div>
                  <div class="text-caption text-medium-emphasis mt-1">{{ opt.hint }}</div>
                </div>
              </v-card-text>
            </v-card>
          </v-item>
        </v-col>
      </v-row>
    </v-item-group>

    <v-alert type="info" variant="tonal" density="compact" class="mb-4 rounded-lg">
      {{ selectedTypeHint }}
    </v-alert>

    <div v-if="wizard.type === 'everyMinutes'" class="mb-4">
      <v-text-field
        v-model.number="wizard.everyN"
        type="number"
        min="1"
        max="59"
        :label="t('operationCore.workspaceDefinitions.scheduled.wizardEveryMinutesLabel')"
        :suffix="t('operationCore.workspaceDefinitions.scheduled.fieldMinuteSuffix')"
        variant="outlined"
        density="comfortable"
        style="max-width: 240px"
      />
    </div>

    <div v-else-if="wizard.type === 'everyHours'" class="mb-4">
      <v-text-field
        v-model.number="wizard.everyN"
        type="number"
        min="1"
        max="23"
        :label="t('operationCore.workspaceDefinitions.scheduled.wizardEveryHoursLabel')"
        :suffix="t('operationCore.workspaceDefinitions.scheduled.fieldHourSuffix')"
        variant="outlined"
        density="comfortable"
        style="max-width: 240px"
      />
    </div>

    <div v-else-if="wizard.type === 'dailyAt'" class="mb-4">
      <div class="d-flex flex-wrap ga-3">
        <v-text-field
          v-model.number="wizard.hour"
          type="number"
          min="0"
          max="23"
          :label="t('operationCore.workspaceDefinitions.scheduled.fieldHour')"
          variant="outlined"
          density="comfortable"
          style="max-width: 140px"
        />
        <v-text-field
          v-model.number="wizard.minute"
          type="number"
          min="0"
          max="59"
          :label="t('operationCore.workspaceDefinitions.scheduled.fieldMinute')"
          variant="outlined"
          density="comfortable"
          style="max-width: 140px"
        />
      </div>
    </div>

    <div v-else-if="wizard.type === 'weeklyDays'" class="mb-4">
      <div class="d-flex flex-wrap ga-2 mb-3">
        <v-btn
          size="small"
          variant="tonal"
          class="text-none"
          @click="applyPresetWeekdays('weekdays')"
        >
          {{ t('operationCore.workspaceDefinitions.scheduled.wizardPresetWeekdays') }}
        </v-btn>
        <v-btn
          size="small"
          variant="tonal"
          class="text-none"
          @click="applyPresetWeekdays('weekend')"
        >
          {{ t('operationCore.workspaceDefinitions.scheduled.wizardPresetWeekend') }}
        </v-btn>
      </div>

      <div class="text-caption text-medium-emphasis mb-2">
        {{ t('operationCore.workspaceDefinitions.scheduled.wizardPickDays') }}
      </div>
      <div class="d-flex flex-wrap ga-2 mb-3">
        <v-chip
          v-for="key in OC_SCHEDULE_WEEKDAY_KEYS"
          :key="key"
          :color="isWeekdaySelected(key) ? 'primary' : undefined"
          :variant="isWeekdaySelected(key) ? 'flat' : 'outlined'"
          class="text-none"
          @click="toggleWeekday(key)"
        >
          {{ weekdayLabels[key] }}
        </v-chip>
      </div>
      <v-alert
        v-if="weeklyDaysInvalid"
        type="warning"
        variant="tonal"
        density="compact"
        class="mb-3 rounded-lg"
      >
        {{ t('operationCore.workspaceDefinitions.scheduled.validationWeekdaysEmpty') }}
      </v-alert>

      <div class="d-flex flex-wrap ga-3">
        <v-text-field
          v-model.number="wizard.hour"
          type="number"
          min="0"
          max="23"
          :label="t('operationCore.workspaceDefinitions.scheduled.fieldHour')"
          variant="outlined"
          density="comfortable"
          style="max-width: 140px"
        />
        <v-text-field
          v-model.number="wizard.minute"
          type="number"
          min="0"
          max="59"
          :label="t('operationCore.workspaceDefinitions.scheduled.fieldMinute')"
          variant="outlined"
          density="comfortable"
          style="max-width: 140px"
        />
      </div>
    </div>

    <div v-else-if="wizard.type === 'advanced'" class="mb-4">
      <v-text-field
        v-model="wizard.advancedCron"
        :label="t('operationCore.workspaceDefinitions.scheduled.fieldCron')"
        :hint="t('operationCore.workspaceDefinitions.scheduled.fieldCronHint')"
        persistent-hint
        variant="outlined"
        density="comfortable"
        autocomplete="off"
      />
    </div>

    <v-sheet variant="outlined" rounded="lg" class="pa-3 mb-4 oc-schedule-timing-wizard__summary">
      <div class="text-caption text-medium-emphasis mb-1">
        {{ t('operationCore.workspaceDefinitions.scheduled.wizardSummaryLabel') }}
      </div>
      <div class="text-body-2 font-weight-medium">{{ humanSummary }}</div>
    </v-sheet>

    <v-select
      v-model="timezone"
      :items="timezoneItems"
      item-title="title"
      item-value="value"
      :label="t('operationCore.workspaceDefinitions.scheduled.fieldTimezone')"
      :hint="t('operationCore.workspaceDefinitions.scheduled.fieldTimezoneHint')"
      persistent-hint
      variant="outlined"
      density="comfortable"
    />
  </div>
</template>

<style scoped>
.oc-schedule-timing-wizard__type-card {
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}

.oc-schedule-timing-wizard__type-card:hover {
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}

.oc-schedule-timing-wizard__summary {
  background: rgba(var(--v-theme-primary), 0.04);
}
</style>
