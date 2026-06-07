<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useOcPersonPicker } from '@/composables/useOcPersonPicker';
import { useUserStore } from '@/stores/apps/user';
import AcAlarmNotificationPolicyDialog from '@/components/apps/alarm-center/AcAlarmNotificationPolicyDialog.vue';
import { listActiveMailTemplateOptions } from '@/services/notifier/mailTemplates';
import {
  alarmNotificationPolicyCreate,
  alarmNotificationPolicyDelete,
  alarmNotificationPolicyList,
  alarmNotificationPolicyUpdate,
  alarmRuleList,
} from '@/services/alarmService';
import type { AlarmNotificationPolicy } from '@/types/apps/alarmNotificationPolicy';
import type { AlarmRule } from '@/types/apps/alarm';
import {
  alarmNotificationPolicySpecificityScore,
  formatAlarmNotificationChannelsSummary,
  formatAlarmNotificationSeverityRange,
} from '@/utils/acAlarmNotificationPolicies';
import { buildOcPersonPickerTitle } from '@/utils/ocPersonPicker';

const { t } = useAppI18n();
const userStore = useUserStore();
const personPicker = useOcPersonPicker();

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);
const activeOnly = ref(false);
const inAppOnly = ref(false);

const policies = ref<AlarmNotificationPolicy[]>([]);
const rules = ref<AlarmRule[]>([]);
const emailTemplateItems = ref<{ value: string; title: string }[]>([]);
const personTitleById = ref<Map<string, string>>(new Map());

const dialog = ref(false);
const editingPolicy = ref<AlarmNotificationPolicy | null>(null);
const deleteDialog = ref(false);
const deleteTarget = ref<AlarmNotificationPolicy | null>(null);

const ruleItems = computed(() =>
  rules.value.map((r) => ({ value: r.id, title: r.name !== r.matchKey ? `${r.name} (${r.matchKey})` : r.name }))
);

const ruleNameById = computed(() => new Map(rules.value.map((r) => [r.id, r.name])));

const filteredPolicies = computed(() => {
  let list = policies.value;
  if (activeOnly.value) list = list.filter((p) => p.isActive !== false);
  if (inAppOnly.value) list = list.filter((p) => p.channels.includes('inApp'));
  return list;
});

const activeCount = computed(() => policies.value.filter((p) => p.isActive !== false).length);
const inAppCount = computed(() => policies.value.filter((p) => p.channels.includes('inApp')).length);
const emailCount = computed(() => policies.value.filter((p) => p.channels.includes('email')).length);

const headers = computed(() => [
  { title: t('alarmCenter.notificationPolicies.colName'), key: 'name', sortable: true },
  { title: t('alarmCenter.notificationPolicies.colEvent'), key: 'eventType', sortable: true },
  { title: t('alarmCenter.notificationPolicies.colRule'), key: 'ruleId', sortable: false },
  { title: t('alarmCenter.notificationPolicies.colSeverity'), key: 'severity', sortable: false },
  { title: t('alarmCenter.notificationPolicies.colRecipients'), key: 'recipients', sortable: false },
  { title: t('alarmCenter.notificationPolicies.colChannels'), key: 'channels', sortable: false, width: 140 },
  { title: t('alarmCenter.notificationPolicies.colTemplate'), key: 'template', sortable: false },
  { title: t('alarmCenter.notificationPolicies.colPriority'), key: 'priority', sortable: true, width: 88 },
  { title: t('alarmCenter.notificationPolicies.colStatus'), key: 'isActive', sortable: true, width: 96 },
  { title: t('alarmCenter.notificationPolicies.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

const howItWorksSteps = computed(() => [
  {
    icon: 'mdi-lightning-bolt-outline',
    color: 'primary',
    title: t('alarmCenter.notificationPolicies.howStep1Title'),
    body: t('alarmCenter.notificationPolicies.howStep1Body'),
  },
  {
    icon: 'mdi-account-multiple-outline',
    color: 'secondary',
    title: t('alarmCenter.notificationPolicies.howStep2Title'),
    body: t('alarmCenter.notificationPolicies.howStep2Body'),
  },
  {
    icon: 'mdi-bell-ring-outline',
    color: 'success',
    title: t('alarmCenter.notificationPolicies.howStep3Title'),
    body: t('alarmCenter.notificationPolicies.howStep3Body'),
  },
]);

function eventLabel(eventType: string): string {
  const key = `alarmCenter.notificationPolicies.eventTypes.${eventType}`;
  const translated = t(key);
  return translated !== key ? translated : eventType;
}

function ruleLabel(ruleId: string | null | undefined): string {
  if (!ruleId) return t('alarmCenter.notificationPolicies.ruleAny');
  return ruleNameById.value.get(ruleId) ?? ruleId;
}

function recipientsSummary(policy: AlarmNotificationPolicy): string {
  const ids = policy.recipientPersonIds;
  if (!ids.length) return '—';
  const labels = ids.map((id) => personTitleById.value.get(id) ?? id);
  if (labels.length <= 2) return labels.join(', ');
  return t('alarmCenter.notificationPolicies.recipientsCount', {
    count: labels.length,
    first: labels[0],
  });
}

function specificityLabel(policy: AlarmNotificationPolicy): string {
  const score = alarmNotificationPolicySpecificityScore(policy);
  if (score >= 4) return t('alarmCenter.notificationPolicies.specificityRule');
  if (score >= 2) return t('alarmCenter.notificationPolicies.specificitySeverity');
  return t('alarmCenter.notificationPolicies.specificityDefault');
}

async function resolvePersonTitles(ids: string[]) {
  const unique = [...new Set(ids.filter(Boolean))];
  if (!unique.length) {
    personTitleById.value = new Map();
    return;
  }
  await personPicker.ensureSelectedIds(unique);
  const map = new Map<string, string>();
  await Promise.all(
    unique.map(async (id) => {
      const fromPicker = personPicker.items.value.find((i) => i.value === id);
      if (fromPicker?.title && fromPicker.title !== id) {
        map.set(id, fromPicker.title);
        return;
      }
      const user = userStore.getUserById(id);
      if (user) {
        map.set(id, buildOcPersonPickerTitle(user));
        return;
      }
      try {
        await userStore.fetchUserById(id);
        const fetched = userStore.getUserById(id);
        map.set(id, fetched ? buildOcPersonPickerTitle(fetched) : id);
      } catch {
        map.set(id, id);
      }
    })
  );
  personTitleById.value = map;
}

async function loadAll() {
  loading.value = true;
  errorLocal.value = null;
  try {
    const [policyRows, ruleRows, mailTemplateOpts] = await Promise.all([
      alarmNotificationPolicyList(),
      alarmRuleList(),
      listActiveMailTemplateOptions().catch(() => [] as { value: string; title: string }[]),
    ]);
    policies.value = policyRows;
    rules.value = ruleRows;
    emailTemplateItems.value = mailTemplateOpts;
    const allRecipientIds = policyRows.flatMap((p) => p.recipientPersonIds);
    await resolvePersonTitles(allRecipientIds);
  } catch (e) {
    errorLocal.value =
      e instanceof Error && e.message.trim()
        ? e.message
        : t('alarmCenter.notificationPolicies.loadError');
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void loadAll();
});

function openCreate() {
  editingPolicy.value = null;
  dialog.value = true;
}

function openEdit(policy: AlarmNotificationPolicy) {
  editingPolicy.value = policy;
  dialog.value = true;
}

function confirmDelete(policy: AlarmNotificationPolicy) {
  deleteTarget.value = policy;
  deleteDialog.value = true;
}

async function savePolicy(payload: Record<string, unknown>, isEdit: boolean) {
  saving.value = true;
  errorLocal.value = null;
  successLocal.value = null;
  try {
    if (isEdit && editingPolicy.value?.id) {
      await alarmNotificationPolicyUpdate(editingPolicy.value.id, payload);
    } else {
      await alarmNotificationPolicyCreate(payload);
    }
    dialog.value = false;
    successLocal.value = t('alarmCenter.notificationPolicies.saveSuccess');
    await loadAll();
  } catch (e) {
    errorLocal.value =
      e instanceof Error && e.message.trim()
        ? e.message
        : t('alarmCenter.notificationPolicies.saveError');
  } finally {
    saving.value = false;
  }
}

async function deletePolicy() {
  if (!deleteTarget.value?.id) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await alarmNotificationPolicyDelete(deleteTarget.value.id);
    deleteDialog.value = false;
    successLocal.value = t('alarmCenter.notificationPolicies.deleteSuccess');
    await loadAll();
  } catch (e) {
    errorLocal.value =
      e instanceof Error && e.message.trim()
        ? e.message
        : t('alarmCenter.notificationPolicies.deleteError');
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="ac-notification-policies">
    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>
    <v-alert v-if="successLocal" type="success" variant="tonal" class="mb-4" closable @click:close="successLocal = null">
      {{ successLocal }}
    </v-alert>

    <v-row class="mb-6">
      <v-col v-for="(step, idx) in howItWorksSteps" :key="idx" cols="12" md="4">
        <v-card variant="outlined" rounded="lg" class="h-100 pa-4">
          <v-avatar :color="step.color" variant="tonal" size="40" rounded="lg" class="mb-3">
            <v-icon :icon="step.icon" />
          </v-avatar>
          <div class="text-subtitle-2 font-weight-bold mb-1">{{ step.title }}</div>
          <p class="text-body-2 text-medium-emphasis mb-0">{{ step.body }}</p>
        </v-card>
      </v-col>
    </v-row>

    <div class="d-flex flex-wrap align-center ga-3 mb-4">
      <v-chip size="small" variant="outlined">
        {{ t('alarmCenter.notificationPolicies.statsTotal', { count: policies.length }) }}
      </v-chip>
      <v-chip size="small" variant="outlined" color="success">
        {{ t('alarmCenter.notificationPolicies.statsActive', { count: activeCount }) }}
      </v-chip>
      <v-chip size="small" variant="outlined" color="primary">
        {{ t('alarmCenter.notificationPolicies.statsInApp', { count: inAppCount }) }}
      </v-chip>
      <v-chip size="small" variant="outlined" color="info">
        {{ t('alarmCenter.notificationPolicies.statsEmail', { count: emailCount }) }}
      </v-chip>
      <v-spacer />
      <v-switch
        v-model="inAppOnly"
        :label="t('alarmCenter.notificationPolicies.filterInAppOnly')"
        density="compact"
        hide-details
        color="primary"
      />
      <v-switch
        v-model="activeOnly"
        :label="t('alarmCenter.notificationPolicies.filterActiveOnly')"
        density="compact"
        hide-details
        color="primary"
      />
      <v-btn icon="mdi-refresh" variant="text" :loading="loading" @click="loadAll" />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">
        {{ t('alarmCenter.notificationPolicies.addPolicy') }}
      </v-btn>
    </div>

    <v-card v-if="loading" variant="outlined" rounded="lg" class="pa-8 text-center">
      <v-progress-circular indeterminate color="primary" />
    </v-card>

    <v-card
      v-else-if="!policies.length"
      variant="outlined"
      rounded="lg"
      class="text-center pa-8 pa-md-12"
    >
      <v-icon icon="mdi-bell-ring-outline" size="48" color="primary" class="mb-3" />
      <h4 class="text-h6 font-weight-bold mb-2">{{ t('alarmCenter.notificationPolicies.emptyTitle') }}</h4>
      <p class="text-body-2 text-medium-emphasis mx-auto mb-4" style="max-width: 520px">
        {{ t('alarmCenter.notificationPolicies.emptyBody') }}
      </p>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">
        {{ t('alarmCenter.notificationPolicies.addPolicy') }}
      </v-btn>
    </v-card>

    <v-data-table
      v-else
      :headers="headers"
      :items="filteredPolicies"
      item-value="id"
      density="comfortable"
      class="rounded-lg"
    >
      <template #item.name="{ item }">
        <div class="font-weight-medium">{{ item.name }}</div>
        <div v-if="item.description" class="text-caption text-medium-emphasis text-truncate" style="max-width: 220px">
          {{ item.description }}
        </div>
      </template>

      <template #item.eventType="{ item }">
        <v-chip size="small" variant="tonal" color="primary">
          {{ eventLabel(item.eventType) }}
        </v-chip>
      </template>

      <template #item.ruleId="{ item }">
        <span class="text-body-2">{{ ruleLabel(item.ruleId) }}</span>
        <div class="text-caption text-medium-emphasis">{{ specificityLabel(item) }}</div>
      </template>

      <template #item.severity="{ item }">
        {{ formatAlarmNotificationSeverityRange(item.minSeverity, item.maxSeverity, t) }}
      </template>

      <template #item.recipients="{ item }">
        <span class="text-body-2">{{ recipientsSummary(item) }}</span>
      </template>

      <template #item.channels="{ item }">
        {{ formatAlarmNotificationChannelsSummary(item.channels, t) }}
        <div v-if="item.channels.includes('inApp') && item.settings?.pushToast" class="text-caption text-medium-emphasis">
          {{ t('alarmCenter.notificationPolicies.toastOn') }}
        </div>
      </template>

      <template #item.template="{ item }">
        <span v-if="item.channels.includes('email')" class="text-body-2">{{ item.emailTemplateKey || '—' }}</span>
        <span v-else class="text-medium-emphasis">—</span>
      </template>

      <template #item.priority="{ item }">
        {{ item.priority ?? 50 }}
      </template>

      <template #item.isActive="{ item }">
        <v-chip :color="item.isActive ? 'success' : 'default'" size="small" variant="tonal">
          {{
            item.isActive
              ? t('alarmCenter.notificationPolicies.statusActive')
              : t('alarmCenter.notificationPolicies.statusInactive')
          }}
        </v-chip>
      </template>

      <template #item.actions="{ item }">
        <v-btn icon="mdi-pencil-outline" variant="text" size="small" @click="openEdit(item)" />
        <v-btn icon="mdi-delete-outline" variant="text" size="small" color="error" @click="confirmDelete(item)" />
      </template>
    </v-data-table>

    <AcAlarmNotificationPolicyDialog
      v-model="dialog"
      :policy="editingPolicy"
      :rule-items="ruleItems"
      :email-template-items="emailTemplateItems"
      :saving="saving"
      @save="savePolicy"
    />

    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="lg">
        <v-card-title>{{ t('alarmCenter.notificationPolicies.deleteTitle') }}</v-card-title>
        <v-card-text>
          {{ t('alarmCenter.notificationPolicies.deleteConfirm', { name: deleteTarget?.name ?? '' }) }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">
            {{ t('alarmCenter.notificationPolicies.cancel') }}
          </v-btn>
          <v-btn color="error" :loading="deleting" @click="deletePolicy">
            {{ t('alarmCenter.notificationPolicies.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
