<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import OdakSiparisNotificationPolicyDialog from '@/components/apps/odak-siparis/OdakSiparisNotificationPolicyDialog.vue';
import { listActiveMailTemplateOptions } from '@/services/notifier/mailTemplates';
import { odakNotificationEventLabelTr } from '@/utils/odakSiparisSettingsLabels';
import {
  createOdakNotificationPolicy,
  deleteOdakNotificationPolicy,
  invalidateOdakNotificationPoliciesCache,
  listOdakNotificationPoliciesForEvents,
  ODAK_GLOBAL_SHIPMENT_DEFAULT_MAIL_TEMPLATE,
  ODAK_GLOBAL_SHIPMENT_NOTIFICATION_EVENT,
  ODAK_GLOBAL_SHIPMENT_NOTIFICATION_EVENT_TYPES,
  ODAK_PACKAGE_NOTIFICATION_EVENT_TYPES,
  updateOdakNotificationPolicy,
  type OdakNotificationPolicyDraft,
  type OdakSiparisNotificationPolicy,
} from '@/utils/odakSiparisNotificationPolicies';

const props = withDefaults(
  defineProps<{
    eventTypes?: readonly string[];
    defaultEventType?: string;
    defaultTemplateKey?: string;
    hintKey?: string;
    i18nPrefix?: string;
  }>(),
  {
    eventTypes: () => [...ODAK_PACKAGE_NOTIFICATION_EVENT_TYPES],
    defaultEventType: 'PackageCreated',
    defaultTemplateKey: '',
    hintKey: 'odakSiparis.packages.settings.notifications.hint',
    i18nPrefix: 'odakSiparis.packages.settings.notifications',
  }
);

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorMessage = ref('');
const policies = ref<OdakSiparisNotificationPolicy[]>([]);
const emailTemplateItems = ref<{ value: string; title: string }[]>([]);
const dialogOpen = ref(false);
const editingPolicy = ref<OdakSiparisNotificationPolicy | null>(null);
const deleteDialog = ref(false);
const deleteTarget = ref<OdakSiparisNotificationPolicy | null>(null);

function label(key: string): string {
  return t(`${props.i18nPrefix}.${key}`);
}

function eventLabel(eventType: string): string {
  return odakNotificationEventLabelTr(eventType);
}

const headers = computed(() => [
  { title: label('colName'), key: 'name', sortable: true },
  { title: label('colEvent'), key: 'eventType', sortable: true },
  { title: label('colRecipients'), key: 'recipients', sortable: false },
  { title: label('colTemplate'), key: 'template', sortable: false },
  { title: label('colStatus'), key: 'isActive', sortable: true, width: 96 },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
]);

async function load() {
  loading.value = true;
  errorMessage.value = '';
  try {
    policies.value = await listOdakNotificationPoliciesForEvents(props.eventTypes);
    emailTemplateItems.value = await listActiveMailTemplateOptions();
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editingPolicy.value = null;
  dialogOpen.value = true;
}

function openEdit(policy: OdakSiparisNotificationPolicy) {
  editingPolicy.value = policy;
  dialogOpen.value = true;
}

function confirmDelete(policy: OdakSiparisNotificationPolicy) {
  deleteTarget.value = policy;
  deleteDialog.value = true;
}

async function onSave(draft: OdakNotificationPolicyDraft) {
  saving.value = true;
  errorMessage.value = '';
  try {
    if (draft.id) {
      await updateOdakNotificationPolicy(draft.id, draft);
    } else {
      await createOdakNotificationPolicy(draft);
    }
    invalidateOdakNotificationPoliciesCache();
    dialogOpen.value = false;
    await load();
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    saving.value = false;
  }
}

async function doDelete() {
  const target = deleteTarget.value;
  if (!target?.__dataId) return;
  deleting.value = true;
  try {
    await deleteOdakNotificationPolicy(target.__dataId);
    invalidateOdakNotificationPoliciesCache();
    deleteDialog.value = false;
    deleteTarget.value = null;
    await load();
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    deleting.value = false;
  }
}

const dialogDefaultTemplateKey = computed(() => {
  if (props.defaultTemplateKey) return props.defaultTemplateKey;
  if (props.defaultEventType === ODAK_GLOBAL_SHIPMENT_NOTIFICATION_EVENT) {
    return ODAK_GLOBAL_SHIPMENT_DEFAULT_MAIL_TEMPLATE;
  }
  return '';
});

onMounted(() => void load());
</script>

<template>
  <div>
    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      {{ t(hintKey) }}
    </v-alert>
    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-4">{{ errorMessage }}</v-alert>

    <div class="d-flex mb-3">
      <v-btn color="primary" variant="flat" prepend-icon="mdi-plus" @click="openCreate">
        {{ label('addPolicy') }}
      </v-btn>
    </div>

    <v-data-table :headers="headers" :items="policies" :loading="loading" density="compact" class="border rounded-md">
      <template #item.eventType="{ item }">
        {{ eventLabel(item.eventType) }}
      </template>
      <template #item.recipients="{ item }">
        {{ item.recipientPersonIds.length }}
      </template>
      <template #item.template="{ item }">
        {{ item.emailTemplateKey || '—' }}
      </template>
      <template #item.isActive="{ item }">
        <v-chip :color="item.isActive ? 'success' : 'default'" size="small" variant="tonal">
          {{ item.isActive ? label('active') : label('inactive') }}
        </v-chip>
      </template>
      <template #item.actions="{ item }">
        <v-btn icon="mdi-pencil" size="x-small" variant="text" @click="openEdit(item)" />
        <v-btn icon="mdi-delete" size="x-small" variant="text" color="error" @click="confirmDelete(item)" />
      </template>
    </v-data-table>

    <OdakSiparisNotificationPolicyDialog
      v-model="dialogOpen"
      :policy="editingPolicy"
      :email-template-items="emailTemplateItems"
      :allowed-event-types="[...eventTypes]"
      :default-event-type="defaultEventType"
      :default-template-key="dialogDefaultTemplateKey"
      :i18n-prefix="i18nPrefix"
      :saving="saving"
      @save="onSave"
    />

    <v-dialog v-model="deleteDialog" max-width="420">
      <v-card>
        <v-card-title>{{ label('deleteTitle') }}</v-card-title>
        <v-card-text>{{ label('deleteConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">{{ t('odakSiparis.packages.cancel') }}</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="doDelete">{{ t('odakSiparis.packages.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
