<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import {
  ODAK_SIPARIS_NOTIFICATION_EVENT_TYPES,
  newOdakNotificationPolicyDraft,
  parseOdakNotificationPolicyToDraft,
  validateOdakNotificationPolicyDraft,
  type OdakNotificationPolicyDraft,
  type OdakSiparisNotificationPolicy,
} from '@/utils/odakSiparisNotificationPolicies';
import { ODAK_PACKAGE_POLICY_FIELD_KEYS } from '@/utils/odakSiparisFieldPolicies';
import { odakNotificationEventLabelTr, odakPackageSettingsFieldLabelTr } from '@/utils/odakSiparisSettingsLabels';
import { ODAK_SHIPMENT_STATUS_OPTIONS } from '@/utils/odakSiparisConfig';

const props = defineProps<{
  modelValue: boolean;
  policy: OdakSiparisNotificationPolicy | null;
  emailTemplateItems: { value: string; title: string }[];
  allowedEventTypes?: string[];
  defaultEventType?: string;
  defaultTemplateKey?: string;
  i18nPrefix?: string;
  saving?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  save: [OdakNotificationPolicyDraft];
}>();

const { t } = useAppI18n();
const draft = ref<OdakNotificationPolicyDraft>(newOdakNotificationPolicyDraft());

const i18nPrefix = computed(() => props.i18nPrefix ?? 'odakSiparis.packages.settings.notifications');

function label(key: string): string {
  return t(`${i18nPrefix.value}.${key}`);
}

const isEdit = computed(() => !!props.policy?.__dataId);
const validationError = computed(() => validateOdakNotificationPolicyDraft(draft.value));
const canSave = computed(() => validationError.value === null);

const eventItems = computed(() => {
  const types = props.allowedEventTypes?.length
    ? props.allowedEventTypes
    : [...ODAK_SIPARIS_NOTIFICATION_EVENT_TYPES];
  return types.map((value) => ({
    value,
    title: odakNotificationEventLabelTr(value),
  }));
});

const singleEventType = computed(() => eventItems.value.length === 1);

const watchedFieldItems = computed(() =>
  ODAK_PACKAGE_POLICY_FIELD_KEYS.filter((k) => !['customerPo', 'projectNo'].includes(k)).map((k) => ({
    value: k,
    title: odakPackageSettingsFieldLabelTr(k),
  }))
);

const shipmentStatusItems = ODAK_SHIPMENT_STATUS_OPTIONS.map((o) => ({ value: o.value, title: o.title }));

watch(
  () => [props.modelValue, props.policy?.__dataId] as const,
  ([open]) => {
    if (open) {
      draft.value = props.policy
        ? parseOdakNotificationPolicyToDraft(props.policy)
        : newOdakNotificationPolicyDraft({
            eventType: props.defaultEventType ?? props.allowedEventTypes?.[0] ?? 'PackageCreated',
            emailTemplateKey: props.defaultTemplateKey ?? '',
          });
      if (singleEventType.value && props.defaultEventType) {
        draft.value.eventType = props.defaultEventType;
      }
    }
  }
);

function close() {
  emit('update:modelValue', false);
}

function submit() {
  if (!canSave.value) return;
  emit('save', draft.value);
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="760" persistent @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="py-4">
        {{ isEdit ? label('editPolicy') : label('addPolicy') }}
      </v-card-title>
      <v-divider />
      <v-card-text class="pt-4">
        <v-text-field v-model="draft.name" :label="label('fieldName')" class="mb-3" />
        <v-textarea v-model="draft.description" :label="label('fieldDescription')" rows="2" auto-grow class="mb-3" />

        <v-row dense>
          <v-col cols="12" :md="singleEventType ? 12 : 6">
            <v-select
              v-if="!singleEventType"
              v-model="draft.eventType"
              :items="eventItems"
              item-title="title"
              item-value="value"
              :label="label('fieldEvent')"
            />
            <v-text-field
              v-else
              :model-value="eventItems[0]?.title ?? draft.eventType"
              :label="label('fieldEvent')"
              readonly
              variant="outlined"
            />
          </v-col>
          <v-col cols="12" md="6">
            <v-text-field v-model.number="draft.priority" type="number" :label="label('fieldPriority')" />
          </v-col>
        </v-row>

        <MngDirectoryPickerField
          v-model="draft.recipientPersonIds"
          entity="user"
          :label="label('fieldRecipients')"
          multiple
          class="mb-3"
        />

        <v-select
          v-model="draft.emailTemplateKey"
          :items="emailTemplateItems"
          item-title="title"
          item-value="value"
          :label="label('fieldTemplate')"
          class="mb-3"
        />
        <v-text-field v-model="draft.emailSubject" :label="label('fieldSubject')" class="mb-3" />
        <v-switch v-model="draft.excludeActor" :label="label('excludeActor')" hide-details class="mb-3" />
        <v-switch v-model="draft.isActive" :label="label('fieldActive')" hide-details class="mb-4" />

        <template v-if="draft.eventType === 'PackageUpdated'">
          <v-select
            v-model="draft.updateTriggerMode"
            :items="[
              { value: 'always', title: t('odakSiparis.packages.settings.notifications.updateAlways') },
              { value: 'fields', title: t('odakSiparis.packages.settings.notifications.updateFields') },
            ]"
            item-title="title"
            item-value="value"
            :label="t('odakSiparis.packages.settings.notifications.updateTrigger')"
            class="mb-3"
          />
          <v-select
            v-if="draft.updateTriggerMode === 'fields'"
            v-model="draft.watchedFields"
            :items="watchedFieldItems"
            item-title="title"
            item-value="value"
            :label="t('odakSiparis.packages.settings.notifications.watchedFields')"
            multiple
            chips
            closable-chips
          />
        </template>

        <template v-if="draft.eventType === 'ShipmentCompleted'">
          <v-select
            v-model="draft.shipmentTriggerMode"
            :items="[
              { value: 'transition', title: t('odakSiparis.packages.settings.notifications.shipmentTransition') },
              { value: 'toStatus', title: t('odakSiparis.packages.settings.notifications.shipmentToStatus') },
              { value: 'always', title: t('odakSiparis.packages.settings.notifications.shipmentAlways') },
            ]"
            item-title="title"
            item-value="value"
            :label="t('odakSiparis.packages.settings.notifications.shipmentTrigger')"
            class="mb-3"
          />
          <v-row v-if="draft.shipmentTriggerMode === 'transition'" dense>
            <v-col cols="6">
              <v-select v-model="draft.fromStatus" :items="shipmentStatusItems" item-title="title" item-value="value" :label="t('odakSiparis.packages.settings.notifications.fromStatus')" />
            </v-col>
            <v-col cols="6">
              <v-select v-model="draft.toStatus" :items="shipmentStatusItems" item-title="title" item-value="value" :label="t('odakSiparis.packages.settings.notifications.toStatus')" />
            </v-col>
          </v-row>
          <v-select
            v-if="draft.shipmentTriggerMode === 'toStatus'"
            v-model="draft.targetStatus"
            :items="shipmentStatusItems"
            item-title="title"
            item-value="value"
            :label="t('odakSiparis.packages.settings.notifications.targetStatus')"
          />
        </template>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="close">{{ t('odakSiparis.packages.cancel') }}</v-btn>
        <v-btn color="primary" variant="flat" :disabled="!canSave" :loading="saving" @click="submit">
          {{ t('odakSiparis.packages.settings.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
