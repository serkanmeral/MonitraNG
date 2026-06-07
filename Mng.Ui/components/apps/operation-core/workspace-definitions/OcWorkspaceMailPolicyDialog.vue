<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OpNotificationPolicy } from '@/types/apps/operationCore';
import {
  OC_NOTIFICATION_CHANNELS,
  OC_NOTIFICATION_EVENT_TYPES,
  OC_NOTIFICATION_RECIPIENT_KEYS,
  OC_TOAST_SEVERITIES,
  buildNotificationPolicyPayload,
  defaultInAppTemplateKeyForEvent,
  newNotificationPolicyDraft,
  parseOpNotificationPolicyToDraft,
  validateNotificationPolicyDraft,
  type OcNotificationPolicyDraft,
} from '@/utils/ocNotificationPolicies';

const props = defineProps<{
  modelValue: boolean;
  policy: OpNotificationPolicy | null;
  workspaceId: string;
  typeItems: { value: string; title: string }[];
  boardItems: { value: string; title: string }[];
  stateItems: { value: string; title: string }[];
  transitionItems: { value: string; title: string; fromStateId: string; toStateId: string }[];
  personFieldItems: { value: string; title: string }[];
  emailTemplateItems: { value: string; title: string }[];
  inAppTemplateItems: { value: string; title: string }[];
  saving?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  save: [Record<string, unknown>];
}>();

const { t } = useAppI18n();
const draft = ref<OcNotificationPolicyDraft>(newNotificationPolicyDraft());

const isEdit = computed(() => !!props.policy?.__dataId);
const canSave = computed(() => validateNotificationPolicyDraft(draft.value) === null);
const isTransitionEvent = computed(() => draft.value.eventType === 'WorkItemTransitioned');
const wantsEmail = computed(() => draft.value.channels.includes('email'));
const wantsInApp = computed(() => draft.value.channels.includes('inApp'));

const eventTypeItems = computed(() =>
  OC_NOTIFICATION_EVENT_TYPES.map((value) => ({
    value,
    title: t(`operationCore.workspaceDefinitions.mail.eventTypes.${value}`),
  }))
);

const channelItems = computed(() =>
  OC_NOTIFICATION_CHANNELS.map((value) => ({
    value,
    title: t(`operationCore.workspaceDefinitions.mail.channels.${value}`),
  }))
);

const recipientItems = computed(() => {
  const core = OC_NOTIFICATION_RECIPIENT_KEYS.map((value) => ({
    value,
    title: t(`operationCore.workspaceDefinitions.mail.recipients.${value}`),
  }));
  const fields = props.personFieldItems.map((f) => ({
    value: `field:${f.value}`,
    title: t('operationCore.workspaceDefinitions.mail.recipients.field', { field: f.title }),
  }));
  return [...core, ...fields];
});

const typeSelectItems = computed(() => [
  { value: null, title: t('operationCore.workspaceDefinitions.mail.scopeAny') },
  ...props.typeItems,
]);

const boardSelectItems = computed(() => [
  { value: null, title: t('operationCore.workspaceDefinitions.mail.scopeAny') },
  ...props.boardItems,
]);

const stateSelectItems = computed(() => [
  { value: null, title: t('operationCore.workspaceDefinitions.mail.scopeAny') },
  ...props.stateItems,
]);

const transitionSelectItems = computed(() => [
  { value: null, title: t('operationCore.workspaceDefinitions.mail.anyTransition') },
  ...props.transitionItems,
]);

const toastSeverityItems = computed(() =>
  OC_TOAST_SEVERITIES.map((value) => ({
    value,
    title: t(`operationCore.workspaceDefinitions.mail.toastSeverityLevels.${value}`),
  }))
);

watch(
  () => [props.modelValue, props.policy?.__dataId] as const,
  ([open]) => {
    if (open) {
      draft.value = props.policy
        ? parseOpNotificationPolicyToDraft(props.policy)
        : newNotificationPolicyDraft();
    }
  }
);

watch(
  () => draft.value.transitionKey,
  (key) => {
    if (!key) return;
    const match = props.transitionItems.find((tr) => tr.value === key);
    if (match) {
      if (!draft.value.fromStateId) draft.value.fromStateId = match.fromStateId;
      if (!draft.value.toStateId) draft.value.toStateId = match.toStateId;
    }
  }
);

watch(
  () => draft.value.eventType,
  (eventType) => {
    if (!draft.value.channels.includes('inApp')) return;
    if (draft.value.notificationTemplateKey.trim()) return;
    const suggested = defaultInAppTemplateKeyForEvent(eventType);
    if (suggested) draft.value.notificationTemplateKey = suggested;
  }
);

function close() {
  emit('update:modelValue', false);
}

function submit() {
  if (!canSave.value) return;
  emit('save', buildNotificationPolicyPayload(draft.value, props.workspaceId));
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="760" persistent @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center py-4">
        <v-icon icon="mdi-bell-outline" color="primary" class="me-2" />
        {{
          isEdit
            ? t('operationCore.workspaceDefinitions.mail.editPolicy')
            : t('operationCore.workspaceDefinitions.mail.addPolicy')
        }}
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" @click="close" />
      </v-card-title>
      <v-divider />
      <v-card-text class="pt-4">
        <v-alert type="info" variant="tonal" density="compact" class="mb-4 rounded-lg">
          {{ t('operationCore.workspaceDefinitions.mail.dialogHint') }}
        </v-alert>

        <v-text-field
          v-model="draft.name"
          :label="t('operationCore.workspaceDefinitions.mail.fieldName')"
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
              :label="t('operationCore.workspaceDefinitions.mail.fieldEventType')"
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="6">
            <v-select
              v-model="draft.channels"
              :items="channelItems"
              item-title="title"
              item-value="value"
              :label="t('operationCore.workspaceDefinitions.mail.fieldChannels')"
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
              v-model="draft.typeId"
              :items="typeSelectItems"
              item-title="title"
              item-value="value"
              :label="t('operationCore.workspaceDefinitions.mail.scopeType')"
              density="comfortable"
              clearable
            />
          </v-col>
          <v-col cols="12" md="6">
            <v-select
              v-model="draft.boardId"
              :items="boardSelectItems"
              item-title="title"
              item-value="value"
              :label="t('operationCore.workspaceDefinitions.mail.scopeBoard')"
              density="comfortable"
              clearable
            />
          </v-col>
        </v-row>

        <template v-if="isTransitionEvent">
          <v-divider class="my-4" />
          <div class="text-subtitle-2 font-weight-bold mb-2">
            {{ t('operationCore.workspaceDefinitions.mail.sectionTransition') }}
          </div>
          <v-row dense>
            <v-col cols="12" md="4">
              <v-select
                v-model="draft.transitionKey"
                :items="transitionSelectItems"
                item-title="title"
                item-value="value"
                :label="t('operationCore.workspaceDefinitions.mail.fieldTransitionKey')"
                density="comfortable"
                clearable
              />
            </v-col>
            <v-col cols="12" md="4">
              <v-select
                v-model="draft.fromStateId"
                :items="stateSelectItems"
                item-title="title"
                item-value="value"
                :label="t('operationCore.workspaceDefinitions.mail.fieldFromState')"
                density="comfortable"
                clearable
              />
            </v-col>
            <v-col cols="12" md="4">
              <v-select
                v-model="draft.toStateId"
                :items="stateSelectItems"
                item-title="title"
                item-value="value"
                :label="t('operationCore.workspaceDefinitions.mail.fieldToState')"
                density="comfortable"
                clearable
              />
            </v-col>
          </v-row>
        </template>

        <v-divider class="my-4" />
        <div class="text-subtitle-2 font-weight-bold mb-2">
          {{ t('operationCore.workspaceDefinitions.mail.sectionDelivery') }}
        </div>

        <v-combobox
          v-model="draft.recipients"
          :items="recipientItems"
          item-title="title"
          item-value="value"
          :label="t('operationCore.workspaceDefinitions.mail.fieldRecipients')"
          :hint="t('operationCore.workspaceDefinitions.mail.fieldRecipientsHint')"
          persistent-hint
          multiple
          chips
          closable-chips
          density="comfortable"
          class="mb-3"
        />

        <template v-if="wantsEmail">
          <v-combobox
            v-model="draft.emailTemplateKey"
            :items="emailTemplateItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.workspaceDefinitions.mail.fieldEmailTemplate')"
            :hint="t('operationCore.workspaceDefinitions.mail.fieldEmailTemplateHint')"
            :placeholder="t('operationCore.workspaceDefinitions.mail.fieldEmailTemplateEmpty')"
            persistent-hint
            density="comfortable"
            class="mb-3"
          />
          <v-text-field
            v-model="draft.emailSubject"
            :label="t('operationCore.workspaceDefinitions.mail.fieldEmailSubject')"
            :hint="t('operationCore.workspaceDefinitions.mail.fieldEmailSubjectHint')"
            persistent-hint
            density="comfortable"
            class="mb-3"
          />
        </template>

        <template v-if="wantsInApp">
          <v-combobox
            v-model="draft.notificationTemplateKey"
            :items="inAppTemplateItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.workspaceDefinitions.mail.fieldInAppTemplate')"
            :hint="t('operationCore.workspaceDefinitions.mail.fieldInAppTemplateHint')"
            :placeholder="t('operationCore.workspaceDefinitions.mail.fieldInAppTemplateEmpty')"
            persistent-hint
            density="comfortable"
            class="mb-3"
          />
          <v-switch
            v-model="draft.pushToast"
            :label="t('operationCore.workspaceDefinitions.mail.pushToast')"
            :hint="t('operationCore.workspaceDefinitions.mail.pushToastHint')"
            persistent-hint
            color="primary"
            hide-details="auto"
            density="comfortable"
            class="mb-3"
          />
          <v-select
            v-if="draft.pushToast"
            v-model="draft.toastSeverity"
            :items="toastSeverityItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.workspaceDefinitions.mail.toastSeverity')"
            :hint="t('operationCore.workspaceDefinitions.mail.toastSeverityHint')"
            persistent-hint
            density="comfortable"
            class="mb-3"
          />
        </template>

        <v-row dense>
          <v-col cols="12" md="6">
            <v-text-field
              v-model.number="draft.policyPriority"
              type="number"
              :label="t('operationCore.workspaceDefinitions.mail.policyPriority')"
              :hint="t('operationCore.workspaceDefinitions.mail.policyPriorityHint')"
              persistent-hint
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="6" class="d-flex align-center">
            <v-switch
              v-model="draft.excludeActor"
              :label="t('operationCore.workspaceDefinitions.mail.excludeActor')"
              color="primary"
              hide-details
              density="comfortable"
            />
          </v-col>
        </v-row>

        <v-switch
          v-model="draft.isActive"
          :label="t('operationCore.workspaceDefinitions.mail.isActive')"
          color="primary"
          hide-details
          density="comfortable"
        />
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="close">{{ t('operationCore.workspaceDefinitions.mail.cancel') }}</v-btn>
        <v-btn color="primary" variant="flat" :disabled="!canSave" :loading="saving" @click="submit">
          {{ t('operationCore.workspaceDefinitions.mail.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
