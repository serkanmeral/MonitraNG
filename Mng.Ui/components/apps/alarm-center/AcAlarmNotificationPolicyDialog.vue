<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcPersonPickerAutocomplete from '@/components/apps/operation-core/OcPersonPickerAutocomplete.vue';
import type { AlarmNotificationPolicy } from '@/types/apps/alarmNotificationPolicy';
import type { AlarmRule } from '@/types/apps/alarm';
import {
  AC_ALARM_NOTIFICATION_CHANNELS,
  AC_ALARM_NOTIFICATION_EVENT_TYPES,
  AC_TOAST_SEVERITIES,
  buildCreateAlarmNotificationPolicyPayload,
  buildUpdateAlarmNotificationPolicyPayload,
  defaultEmailTemplateKeyForEvent,
  newAlarmNotificationPolicyDraft,
  parseAlarmNotificationPolicyToDraft,
  validateAlarmNotificationPolicyDraft,
  type AcAlarmNotificationPolicyDraft,
} from '@/utils/acAlarmNotificationPolicies';

const props = defineProps<{
  modelValue: boolean;
  policy: AlarmNotificationPolicy | null;
  ruleItems: { value: string; title: string }[];
  emailTemplateItems: { value: string; title: string }[];
  saving?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  save: [Record<string, unknown>, boolean];
}>();

const { t } = useAppI18n();
const draft = ref<AcAlarmNotificationPolicyDraft>(newAlarmNotificationPolicyDraft());

const isEdit = computed(() => !!props.policy?.id);
const validationError = computed(() => validateAlarmNotificationPolicyDraft(draft.value));
const canSave = computed(() => validationError.value === null);
const wantsEmail = computed(() => draft.value.channels.includes('email'));
const wantsInApp = computed(() => draft.value.channels.includes('inApp'));

const eventTypeItems = computed(() =>
  AC_ALARM_NOTIFICATION_EVENT_TYPES.map((value) => ({
    value,
    title: t(`alarmCenter.notificationPolicies.eventTypes.${value}`),
  }))
);

const channelItems = computed(() =>
  AC_ALARM_NOTIFICATION_CHANNELS.map((value) => ({
    value,
    title: t(`alarmCenter.notificationPolicies.channels.${value}`),
  }))
);

const ruleSelectItems = computed(() => [
  { value: null, title: t('alarmCenter.notificationPolicies.ruleAny') },
  ...props.ruleItems,
]);

const toastSeverityItems = computed(() =>
  AC_TOAST_SEVERITIES.map((value) => ({
    value,
    title: t(`alarmCenter.notificationPolicies.toastSeverityLevels.${value}`),
  }))
);

watch(
  () => [props.modelValue, props.policy?.id] as const,
  ([open]) => {
    if (open) {
      draft.value = props.policy
        ? parseAlarmNotificationPolicyToDraft(props.policy)
        : newAlarmNotificationPolicyDraft();
    }
  }
);

watch(
  () => draft.value.eventType,
  (eventType) => {
    if (!draft.value.channels.includes('email')) return;
    if (draft.value.emailTemplateKey.trim()) return;
    const suggested = defaultEmailTemplateKeyForEvent(eventType);
    if (suggested) draft.value.emailTemplateKey = suggested;
  }
);

function close() {
  emit('update:modelValue', false);
}

function submit() {
  if (!canSave.value) return;
  const payload = isEdit.value
    ? buildUpdateAlarmNotificationPolicyPayload(draft.value)
    : buildCreateAlarmNotificationPolicyPayload(draft.value);
  emit('save', payload, isEdit.value);
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="760" persistent @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center py-4">
        <v-icon icon="mdi-bell-ring-outline" color="primary" class="me-2" />
        {{
          isEdit
            ? t('alarmCenter.notificationPolicies.editPolicy')
            : t('alarmCenter.notificationPolicies.addPolicy')
        }}
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" @click="close" />
      </v-card-title>
      <v-divider />
      <v-card-text class="pt-4">
        <v-alert type="info" variant="tonal" density="compact" class="mb-4 rounded-lg">
          {{ t('alarmCenter.notificationPolicies.dialogHint') }}
        </v-alert>

        <v-text-field
          v-model="draft.name"
          :label="t('alarmCenter.notificationPolicies.fieldName')"
          density="comfortable"
          class="mb-3"
        />

        <v-textarea
          v-model="draft.description"
          :label="t('alarmCenter.notificationPolicies.fieldDescription')"
          rows="2"
          auto-grow
          density="comfortable"
          class="mb-3"
        />

        <v-row dense>
          <v-col cols="12" md="6">
            <v-select
              v-model="draft.eventType"
              :items="eventTypeItems"
              item-title="title"
              item-value="value"
              :label="t('alarmCenter.notificationPolicies.fieldEventType')"
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="6">
            <v-select
              v-model="draft.channels"
              :items="channelItems"
              item-title="title"
              item-value="value"
              :label="t('alarmCenter.notificationPolicies.fieldChannels')"
              multiple
              chips
              closable-chips
              density="comfortable"
            />
          </v-col>
        </v-row>

        <v-row dense>
          <v-col cols="12" md="6">
            <v-select
              v-model="draft.ruleId"
              :items="ruleSelectItems"
              item-title="title"
              item-value="value"
              :label="t('alarmCenter.notificationPolicies.fieldRule')"
              density="comfortable"
              clearable
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field
              v-model.number="draft.minSeverity"
              type="number"
              min="1"
              max="10"
              :label="t('alarmCenter.notificationPolicies.fieldMinSeverity')"
              density="comfortable"
              clearable
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field
              v-model.number="draft.maxSeverity"
              type="number"
              min="1"
              max="10"
              :label="t('alarmCenter.notificationPolicies.fieldMaxSeverity')"
              density="comfortable"
              clearable
            />
          </v-col>
        </v-row>

        <v-divider class="my-4" />
        <div class="text-subtitle-2 font-weight-bold mb-2">
          {{ t('alarmCenter.notificationPolicies.sectionDelivery') }}
        </div>

        <OcPersonPickerAutocomplete
          v-model="draft.recipientPersonIds"
          multiple
          show-required-mark
          :label="t('alarmCenter.notificationPolicies.fieldRecipients')"
          density="comfortable"
          class="mb-3"
        />

        <template v-if="wantsEmail">
          <v-combobox
            v-model="draft.emailTemplateKey"
            :items="emailTemplateItems"
            item-title="title"
            item-value="value"
            :label="t('alarmCenter.notificationPolicies.fieldEmailTemplate')"
            :hint="t('alarmCenter.notificationPolicies.fieldEmailTemplateHint')"
            persistent-hint
            density="comfortable"
            class="mb-3"
          />
          <v-text-field
            v-model="draft.emailSubject"
            :label="t('alarmCenter.notificationPolicies.fieldEmailSubject')"
            :hint="t('alarmCenter.notificationPolicies.fieldEmailSubjectHint')"
            persistent-hint
            density="comfortable"
            class="mb-3"
          />
        </template>

        <template v-if="wantsInApp">
          <v-switch
            v-model="draft.pushToast"
            :label="t('alarmCenter.notificationPolicies.fieldPushToast')"
            color="primary"
            density="comfortable"
            hide-details
            class="mb-2"
          />
          <v-select
            v-if="draft.pushToast"
            v-model="draft.toastSeverity"
            :items="toastSeverityItems"
            item-title="title"
            item-value="value"
            :label="t('alarmCenter.notificationPolicies.fieldToastSeverity')"
            density="comfortable"
            class="mb-3"
          />
        </template>

        <v-divider class="my-4" />
        <div class="text-subtitle-2 font-weight-bold mb-2">
          {{ t('alarmCenter.notificationPolicies.sectionBehavior') }}
        </div>

        <v-row dense>
          <v-col cols="12" md="4">
            <v-text-field
              v-model.number="draft.cooldownMinutes"
              type="number"
              min="0"
              :label="t('alarmCenter.notificationPolicies.fieldCooldown')"
              :hint="t('alarmCenter.notificationPolicies.fieldCooldownHint')"
              persistent-hint
              density="comfortable"
              clearable
            />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field
              v-model.number="draft.priority"
              type="number"
              min="0"
              max="100"
              :label="t('alarmCenter.notificationPolicies.fieldPriority')"
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="4" class="d-flex align-center">
            <v-switch
              v-model="draft.isActive"
              :label="t('alarmCenter.notificationPolicies.fieldActive')"
              color="success"
              hide-details
            />
          </v-col>
        </v-row>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="close">
          {{ t('alarmCenter.notificationPolicies.cancel') }}
        </v-btn>
        <v-btn color="primary" :loading="saving" :disabled="!canSave" @click="submit">
          {{ t('alarmCenter.notificationPolicies.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
