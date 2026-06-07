<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useOcWorkspaceCatalogInject } from '@/composables/useOcWorkspaceCatalog';
import OcWorkspaceMailPolicyDialog from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceMailPolicyDialog.vue';
import { ocListStateFlowsForWorkspace } from '@/services/operationCore/flows';
import { listActiveInAppTemplateOptions } from '@/services/notifier/inAppTemplates';
import { listActiveMailTemplateOptions } from '@/services/notifier/mailTemplates';
import {
  ocCreateNotificationPolicy,
  ocDeleteNotificationPolicy,
  ocExtractDgErrorMessage,
  ocListNotificationPoliciesForWorkspace,
  ocListPoolFieldsForWorkspace,
  ocUpdateNotificationPolicy,
} from '@/services/operationCoreService';
import type { OpField, OpNotificationPolicy } from '@/types/apps/operationCore';
import {
  collectTransitionOptions,
  formatNotificationChannelsSummary,
  formatNotificationRecipientsSummary,
  formatNotificationTransitionSummary,
  notificationPolicySpecificityScore,
  recipientDisplayKey,
} from '@/utils/ocNotificationPolicies';

const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();
const catalog = useOcWorkspaceCatalogInject();

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);
const activeOnly = ref(false);
const emailOnly = ref(false);

const policies = ref<OpNotificationPolicy[]>([]);
const poolFields = ref<OpField[]>([]);
const transitionOptions = ref<ReturnType<typeof collectTransitionOptions>>([]);
const emailTemplateItems = ref<{ value: string; title: string }[]>([]);
const inAppTemplateItems = ref<{ value: string; title: string }[]>([]);

const dialog = ref(false);
const editingPolicy = ref<OpNotificationPolicy | null>(null);
const deleteDialog = ref(false);
const deleteTarget = ref<OpNotificationPolicy | null>(null);

const typeItems = computed(() => catalog.types.value.map((ty) => ({ value: ty.__dataId, title: ty.name })));
const boardItems = computed(() => catalog.boards.value.map((b) => ({ value: b.__dataId, title: b.name })));
const stateItems = computed(() => catalog.states.value.map((s) => ({ value: s.__dataId, title: s.name })));
const typeNameById = computed(() => new Map(catalog.types.value.map((ty) => [ty.__dataId, ty.name])));
const boardNameById = computed(() => new Map(catalog.boards.value.map((b) => [b.__dataId, b.name])));
const stateNameById = computed(() => new Map(catalog.states.value.map((s) => [s.__dataId, s.name])));

const personFieldItems = computed(() =>
  poolFields.value
    .filter((f) => f.fieldType === 'person' || f.fieldType === 'persons')
    .map((f) => ({ value: f.key, title: f.label?.trim() || f.key }))
);

const filteredPolicies = computed(() => {
  let list = policies.value;
  if (activeOnly.value) list = list.filter((p) => p.isActive !== false);
  if (emailOnly.value) list = list.filter((p) => p.channels.includes('email'));
  return list;
});

const activeCount = computed(() => policies.value.filter((p) => p.isActive !== false).length);
const emailCount = computed(() => policies.value.filter((p) => p.channels.includes('email')).length);

const headers = computed(() => [
  { title: t('operationCore.workspaceDefinitions.mail.colName'), key: 'name', sortable: true },
  { title: t('operationCore.workspaceDefinitions.mail.colEvent'), key: 'eventType', sortable: true },
  { title: t('operationCore.workspaceDefinitions.mail.colTransition'), key: 'transition', sortable: false },
  { title: t('operationCore.workspaceDefinitions.mail.colRecipients'), key: 'recipients', sortable: false },
  { title: t('operationCore.workspaceDefinitions.mail.colTemplate'), key: 'template', sortable: false },
  { title: t('operationCore.workspaceDefinitions.mail.colChannels'), key: 'channels', sortable: false, width: 120 },
  { title: t('operationCore.workspaceDefinitions.mail.colPolicyPriority'), key: 'priority', sortable: true, width: 88 },
  { title: t('operationCore.workspaceDefinitions.mail.colStatus'), key: 'isActive', sortable: true, width: 96 },
  { title: t('operationCore.workspaceDefinitions.mail.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

const howItWorksSteps = computed(() => [
  {
    icon: 'mdi-lightning-bolt-outline',
    color: 'primary',
    title: t('operationCore.workspaceDefinitions.mail.howStep1Title'),
    body: t('operationCore.workspaceDefinitions.mail.howStep1Body'),
  },
  {
    icon: 'mdi-account-multiple-outline',
    color: 'secondary',
    title: t('operationCore.workspaceDefinitions.mail.howStep2Title'),
    body: t('operationCore.workspaceDefinitions.mail.howStep2Body'),
  },
  {
    icon: 'mdi-email-fast-outline',
    color: 'success',
    title: t('operationCore.workspaceDefinitions.mail.howStep3Title'),
    body: t('operationCore.workspaceDefinitions.mail.howStep3Body'),
  },
]);

function recipientLabel(key: string): string {
  const normalized = recipientDisplayKey(key);
  if (normalized.startsWith('field:')) {
    const fieldKey = normalized.slice(6);
    const field = poolFields.value.find((f) => f.key === fieldKey);
    return t('operationCore.workspaceDefinitions.mail.recipients.field', {
      field: field?.label?.trim() || fieldKey,
    });
  }
  const i18nKey = `operationCore.workspaceDefinitions.mail.recipients.${normalized}`;
  const translated = t(i18nKey);
  return translated !== i18nKey ? translated : normalized;
}

function channelLabel(key: string): string {
  const i18nKey = `operationCore.workspaceDefinitions.mail.channels.${key}`;
  const translated = t(i18nKey);
  return translated !== i18nKey ? translated : key;
}

function eventLabel(eventType: string): string {
  const i18nKey = `operationCore.workspaceDefinitions.mail.eventTypes.${eventType}`;
  const translated = t(i18nKey);
  return translated !== i18nKey ? translated : eventType;
}

function scopeSummary(policy: OpNotificationPolicy): string {
  const parts: string[] = [];
  if (policy.typeId) parts.push(typeNameById.value.get(policy.typeId) ?? policy.typeId);
  if (policy.boardId) parts.push(boardNameById.value.get(policy.boardId) ?? policy.boardId);
  return parts.length ? parts.join(' · ') : t('operationCore.workspaceDefinitions.mail.scopeAny');
}

function transitionSummary(policy: OpNotificationPolicy): string {
  return formatNotificationTransitionSummary(
    policy,
    stateNameById.value,
    t('operationCore.workspaceDefinitions.mail.anyTransition')
  );
}

function specificityLabel(policy: OpNotificationPolicy): string {
  const score = notificationPolicySpecificityScore(policy);
  if (score >= 7) return t('operationCore.workspaceDefinitions.mail.specificityFull');
  if (score >= 4) return t('operationCore.workspaceDefinitions.mail.specificityTransition');
  if (score >= 2) return t('operationCore.workspaceDefinitions.mail.specificityType');
  if (score >= 1) return t('operationCore.workspaceDefinitions.mail.specificityBoard');
  return t('operationCore.workspaceDefinitions.mail.specificityDefault');
}

async function loadAll() {
  if (!props.workspaceId) {
    policies.value = [];
    return;
  }
  loading.value = true;
  errorLocal.value = null;
  try {
    const [policyRows, flows, fields, mailTemplateOpts, inAppTemplateOpts] = await Promise.all([
      ocListNotificationPoliciesForWorkspace(props.workspaceId),
      ocListStateFlowsForWorkspace(props.workspaceId),
      ocListPoolFieldsForWorkspace(props.workspaceId),
      listActiveMailTemplateOptions().catch(() => [] as { value: string; title: string }[]),
      listActiveInAppTemplateOptions().catch(() => [] as { value: string; title: string }[]),
      catalog.whenReady(),
    ]);
    emailTemplateItems.value = mailTemplateOpts;
    inAppTemplateItems.value = inAppTemplateOpts;
    policies.value = policyRows;
    poolFields.value = fields;
    const transitions = flows.flatMap((f) => f.transitions);
    transitionOptions.value = collectTransitionOptions(transitions);
  } catch (e) {
    errorLocal.value = ocExtractDgErrorMessage(e, t('operationCore.workspaceDefinitions.mail.loadError'));
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.workspaceId,
  () => {
    void loadAll();
  },
  { immediate: true }
);

function openCreate() {
  editingPolicy.value = null;
  dialog.value = true;
}

function openEdit(policy: OpNotificationPolicy) {
  editingPolicy.value = policy;
  dialog.value = true;
}

function confirmDelete(policy: OpNotificationPolicy) {
  deleteTarget.value = policy;
  deleteDialog.value = true;
}

async function savePolicy(payload: Record<string, unknown>) {
  saving.value = true;
  errorLocal.value = null;
  successLocal.value = null;
  try {
    if (editingPolicy.value?.__dataId) {
      await ocUpdateNotificationPolicy(editingPolicy.value.__dataId, payload);
    } else {
      await ocCreateNotificationPolicy(payload);
    }
    dialog.value = false;
    successLocal.value = t('operationCore.workspaceDefinitions.mail.saveSuccess');
    await loadAll();
  } catch (e) {
    errorLocal.value = ocExtractDgErrorMessage(e, t('operationCore.workspaceDefinitions.mail.saveError'));
  } finally {
    saving.value = false;
  }
}

async function deletePolicy() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await ocDeleteNotificationPolicy(deleteTarget.value.__dataId);
    deleteDialog.value = false;
    successLocal.value = t('operationCore.workspaceDefinitions.mail.deleteSuccess');
    await loadAll();
  } catch (e) {
    errorLocal.value = ocExtractDgErrorMessage(e, t('operationCore.workspaceDefinitions.mail.deleteError'));
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="oc-ws-mail-tab pa-4 pa-md-6">
    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>
    <v-alert v-if="successLocal" type="success" variant="tonal" class="mb-4" closable @click:close="successLocal = null">
      {{ successLocal }}
    </v-alert>

    <div class="mb-6">
      <h3 class="text-h6 font-weight-bold mb-1">{{ t('operationCore.workspaceDefinitions.mail.pageTitle') }}</h3>
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('operationCore.workspaceDefinitions.mail.pageSubtitle') }}
      </p>
    </div>

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
        {{ t('operationCore.workspaceDefinitions.mail.statsTotal', { count: policies.length }) }}
      </v-chip>
      <v-chip size="small" variant="outlined" color="success">
        {{ t('operationCore.workspaceDefinitions.mail.statsActive', { count: activeCount }) }}
      </v-chip>
      <v-chip size="small" variant="outlined" color="primary">
        {{ t('operationCore.workspaceDefinitions.mail.statsEmail', { count: emailCount }) }}
      </v-chip>
      <v-spacer />
      <v-switch
        v-model="emailOnly"
        :label="t('operationCore.workspaceDefinitions.mail.filterEmailOnly')"
        density="compact"
        hide-details
        color="primary"
      />
      <v-switch
        v-model="activeOnly"
        :label="t('operationCore.workspaceDefinitions.mail.filterActiveOnly')"
        density="compact"
        hide-details
        color="primary"
      />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">
        {{ t('operationCore.workspaceDefinitions.mail.addPolicy') }}
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
      <v-icon icon="mdi-bell-outline" size="48" color="primary" class="mb-3" />
      <h4 class="text-h6 font-weight-bold mb-2">{{ t('operationCore.workspaceDefinitions.mail.emptyTitle') }}</h4>
      <p class="text-body-2 text-medium-emphasis mx-auto mb-4" style="max-width: 520px">
        {{ t('operationCore.workspaceDefinitions.mail.emptyBody') }}
      </p>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">
        {{ t('operationCore.workspaceDefinitions.mail.emptyCta') }}
      </v-btn>
    </v-card>

    <v-card v-else variant="outlined" rounded="lg">
      <v-data-table
        :headers="headers"
        :items="filteredPolicies"
        item-value="__dataId"
        density="comfortable"
        hide-default-footer
      >
        <template #[`item.name`]="{ item }">
          <div>
            <div class="font-weight-medium">{{ item.name }}</div>
            <div class="text-caption text-medium-emphasis">{{ scopeSummary(item) }}</div>
          </div>
        </template>
        <template #[`item.eventType`]="{ item }">
          <span class="text-body-2">{{ eventLabel(item.eventType) }}</span>
        </template>
        <template #[`item.transition`]="{ item }">
          <div>
            <span class="text-body-2">{{ transitionSummary(item) }}</span>
            <v-chip size="x-small" variant="tonal" class="ms-2">{{ specificityLabel(item) }}</v-chip>
          </div>
        </template>
        <template #[`item.recipients`]="{ item }">
          <span class="text-body-2">{{
            formatNotificationRecipientsSummary(item.recipients, recipientLabel)
          }}</span>
        </template>
        <template #[`item.template`]="{ item }">
          <div class="text-body-2">
            <div v-if="item.channels.includes('email')">
              {{ item.emailTemplateKey || '—' }}
              <span v-if="item.emailSubject" class="text-caption text-medium-emphasis d-block text-truncate" style="max-width: 200px">
                {{ item.emailSubject }}
              </span>
            </div>
            <span v-else class="text-medium-emphasis">—</span>
          </div>
        </template>
        <template #[`item.channels`]="{ item }">
          <span class="text-body-2">{{
            formatNotificationChannelsSummary(item.channels, channelLabel)
          }}</span>
        </template>
        <template #[`item.priority`]="{ item }">
          <span class="tabular-nums">{{ item.priority ?? 100 }}</span>
        </template>
        <template #[`item.isActive`]="{ item }">
          <v-chip size="small" :color="item.isActive !== false ? 'success' : 'default'" variant="tonal">
            {{
              item.isActive !== false
                ? t('operationCore.workspaceDefinitions.mail.activeYes')
                : t('operationCore.workspaceDefinitions.mail.activeNo')
            }}
          </v-chip>
        </template>
        <template #[`item.actions`]="{ item }">
          <div class="d-flex justify-end ga-1">
            <v-btn icon="mdi-pencil-outline" size="small" variant="text" @click="openEdit(item)" />
            <v-btn icon="mdi-delete-outline" size="small" variant="text" color="error" @click="confirmDelete(item)" />
          </div>
        </template>
      </v-data-table>
    </v-card>

    <p class="text-caption text-medium-emphasis mt-4 mb-0">
      {{ t('operationCore.workspaceDefinitions.mail.technicalFootnote') }}
    </p>

    <OcWorkspaceMailPolicyDialog
      v-model="dialog"
      :policy="editingPolicy"
      :workspace-id="workspaceId"
      :type-items="typeItems"
      :board-items="boardItems"
      :state-items="stateItems"
      :transition-items="transitionOptions"
      :person-field-items="personFieldItems"
      :email-template-items="emailTemplateItems"
      :in-app-template-items="inAppTemplateItems"
      :saving="saving"
      @save="savePolicy"
    />

    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="lg">
        <v-card-title>{{ t('operationCore.workspaceDefinitions.mail.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('operationCore.workspaceDefinitions.mail.deleteBody') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">{{ t('operationCore.workspaceDefinitions.mail.cancel') }}</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="deletePolicy">
            {{ t('operationCore.workspaceDefinitions.mail.deleteConfirm') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.tabular-nums {
  font-variant-numeric: tabular-nums;
}
</style>
