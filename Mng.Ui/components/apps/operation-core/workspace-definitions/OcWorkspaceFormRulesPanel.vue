<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  buildOcDefaultSetAssigneeRulePayload,
  buildOcDefaultSetFieldRulePayload,
  buildOcValidationRulePayload,
  formatOcRuleActionsSummary,
  formatOcRuleConditionSummary,
} from '@/utils/ocFormRuleSummary';
import {
  ocCreateRule,
  ocDeleteRule,

  ocListRulesForWorkspace,
} from '@/services/operationCoreService';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import type { OpRule } from '@/types/apps/operationCore';

const props = withDefaults(
  defineProps<{
    workspaceId: string;
    layoutFieldKeys: string[];
    defaultTypeId?: string;
    /** form = Forms altında bilgi + Kurallar sekmesine link */
    embedded?: 'form';
    /** Kurallar sekmesinde üst başlık dışarıda */
    hideHeader?: boolean;
  }>(),
  { hideHeader: false }
);

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(false);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);
const rules = ref<OpRule[]>([]);

const createDialog = ref(false);
const deleteDialog = ref(false);
const deleteTarget = ref<OpRule | null>(null);

const createForm = ref({
  ruleType: 'validation' as 'validation' | 'default',
  defaultAction: 'setField' as 'setField' | 'setAssignee',
  name: '',
  trigger: 'WorkItemTransition',
  transitionKey: '',
  conditionField: '',
  errorMessage: '',
  defaultField: '',
  defaultValue: '',
});

const defaultActionItems = computed(() => [
  { value: 'setField', title: t('operationCore.workspaceDefinitions.forms.rulesActionSetField') },
  { value: 'setAssignee', title: t('operationCore.workspaceDefinitions.forms.rulesActionSetAssignee') },
]);

const ruleTypeItems = computed(() => [
  { value: 'validation', title: t('operationCore.workspaceDefinitions.forms.rulesTypeValidation') },
  { value: 'default', title: t('operationCore.workspaceDefinitions.forms.rulesTypeDefault') },
]);

const triggerItems = computed(() => [
  { value: 'WorkItemCreated', title: 'WorkItemCreated' },
  { value: 'WorkItemTransition', title: 'WorkItemTransition' },
  { value: 'WorkItemUpdated', title: 'WorkItemUpdated' },
]);

const conditionFieldItems = computed(() =>
  props.layoutFieldKeys.map((key) => ({ value: key, title: key }))
);

const tableHeaders = computed(() => [
  { title: t('operationCore.workspaceDefinitions.forms.rulesColName'), key: 'name' },
  { title: t('operationCore.workspaceDefinitions.forms.rulesColType'), key: 'ruleType' },
  { title: t('operationCore.workspaceDefinitions.forms.rulesColTrigger'), key: 'trigger' },
  { title: t('operationCore.workspaceDefinitions.forms.rulesColSummary'), key: 'summary' },
  { title: t('operationCore.workspaceDefinitions.forms.colActions'), key: 'actions', align: 'end' as const },
]);

const showTransitionKey = computed(() => createForm.value.trigger === 'WorkItemTransition');

watch(
  () => createForm.value.defaultAction,
  () => {
    createForm.value.defaultValue = '';
  }
);

async function loadRules() {
  if (!props.workspaceId) {
    rules.value = [];
    return;
  }
  loading.value = true;
  errorLocal.value = null;
  try {
    rules.value = await ocListRulesForWorkspace(props.workspaceId);
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.workspaceDefinitions.forms.rulesLoadError');
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.workspaceId,
  () => {
    void loadRules();
  },
  { immediate: true }
);

function openCreate() {
  createForm.value = {
    ruleType: 'validation',
    defaultAction: 'setField',
    name: '',
    trigger: 'WorkItemTransition',
    transitionKey: '',
    conditionField: props.layoutFieldKeys.includes('description')
      ? 'description'
      : (props.layoutFieldKeys[0] ?? ''),
    errorMessage: '',
    defaultField: props.layoutFieldKeys[0] ?? 'priorityId',
    defaultValue: '',
  };
  createDialog.value = true;
}

function ruleSummary(rule: OpRule): string {
  if (rule.ruleType === 'validation') {
    const cond = formatOcRuleConditionSummary(rule);
    const scope = rule.transitionKey ? ` · ${rule.transitionKey}` : '';
    return `${cond}${scope}`;
  }
  return formatOcRuleActionsSummary(rule);
}

async function submitCreate() {
  if (!props.workspaceId || !createForm.value.name.trim()) return;
  saving.value = true;
  errorLocal.value = null;
  try {
    const typeId = props.defaultTypeId?.trim() || undefined;
    if (createForm.value.ruleType === 'validation') {
      if (!createForm.value.conditionField.trim() || !createForm.value.errorMessage.trim()) {
        errorLocal.value = t('operationCore.workspaceDefinitions.forms.rulesCreateValidation');
        return;
      }
      if (showTransitionKey.value && !createForm.value.transitionKey.trim()) {
        errorLocal.value = t('operationCore.workspaceDefinitions.forms.rulesTransitionKeyRequired');
        return;
      }
      await ocCreateRule(
        buildOcValidationRulePayload({
          name: createForm.value.name,
          workspaceId: props.workspaceId,
          trigger: createForm.value.trigger,
          transitionKey: createForm.value.transitionKey,
          conditionField: createForm.value.conditionField,
          errorMessage: createForm.value.errorMessage,
          typeId,
        })
      );
    } else if (createForm.value.defaultAction === 'setAssignee') {
      if (!createForm.value.defaultValue.trim()) {
        errorLocal.value = t('operationCore.workspaceDefinitions.forms.rulesAssigneeRequired');
        return;
      }
      await ocCreateRule(
        buildOcDefaultSetAssigneeRulePayload({
          name: createForm.value.name,
          workspaceId: props.workspaceId,
          trigger: createForm.value.trigger,
          assignee: createForm.value.defaultValue,
          typeId,
        })
      );
    } else {
      if (!createForm.value.defaultField.trim()) {
        errorLocal.value = t('operationCore.workspaceDefinitions.forms.rulesCreateDefault');
        return;
      }
      await ocCreateRule(
        buildOcDefaultSetFieldRulePayload({
          name: createForm.value.name,
          workspaceId: props.workspaceId,
          trigger: createForm.value.trigger,
          field: createForm.value.defaultField,
          value: createForm.value.defaultValue,
          typeId,
        })
      );
    }
    createDialog.value = false;
    await loadRules();
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.workspaceDefinitions.forms.rulesCreateError');
  } finally {
    saving.value = false;
  }
}

function openDelete(rule: OpRule) {
  deleteTarget.value = rule;
  deleteDialog.value = true;
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await ocDeleteRule(deleteTarget.value.__dataId);
    deleteDialog.value = false;
    deleteTarget.value = null;
    await loadRules();
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.workspaceDefinitions.forms.rulesDeleteError');
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="oc-form-rules-panel" :class="embedded === 'form' ? 'mt-8' : ''">
    <v-divider v-if="embedded === 'form'" class="mb-6" />

    <div class="d-flex flex-wrap align-center gap-2 mb-3">
      <h4 v-if="!hideHeader" class="text-subtitle-2 font-weight-medium mb-0">
        {{ t('operationCore.workspaceDefinitions.forms.rulesSectionTitle') }}
      </h4>
      <v-spacer />
      <v-btn
        size="small"
        variant="tonal"
        color="primary"
        class="text-none"
        prepend-icon="mdi-plus"
        :disabled="!workspaceId"
        @click="openCreate"
      >
        {{ t('operationCore.workspaceDefinitions.forms.rulesAdd') }}
      </v-btn>
      <v-btn
        icon
        variant="text"
        size="small"
        :loading="loading"
        :disabled="!workspaceId"
        @click="void loadRules()"
      >
        <v-icon icon="mdi-refresh" />
      </v-btn>
    </div>

    <p v-if="!hideHeader" class="text-caption text-medium-emphasis mb-2">
      {{ t('operationCore.workspaceDefinitions.forms.rulesSectionHint') }}
    </p>
    <v-alert v-if="errorLocal" type="error" variant="tonal" density="compact" class="mb-3 rounded-lg" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />

    <v-data-table
      v-if="rules.length"
      :headers="tableHeaders"
      :items="rules"
      density="compact"
      class="oc-form-rules-table rounded-lg border"
      hide-default-footer
    >
      <template #[`item.ruleType`]="{ item }">
        <v-chip size="x-small" variant="tonal" class="text-none">
          {{ item.ruleType }}
        </v-chip>
      </template>
      <template #[`item.trigger`]="{ item }">
        <span class="text-caption">{{ item.trigger }}</span>
        <span v-if="item.transitionKey" class="text-caption text-medium-emphasis d-block">
          {{ item.transitionKey }}
        </span>
      </template>
      <template #[`item.summary`]="{ item }">
        <span class="text-body-2">{{ ruleSummary(item) }}</span>
      </template>
      <template #[`item.actions`]="{ item }">
        <v-btn icon variant="text" size="small" color="error" @click="openDelete(item)">
          <v-icon icon="mdi-delete-outline" />
        </v-btn>
      </template>
    </v-data-table>

    <v-alert v-else-if="!loading" type="info" variant="tonal" density="compact" class="rounded-lg">
      {{ t('operationCore.workspaceDefinitions.forms.rulesEmpty') }}
    </v-alert>

    <v-dialog v-model="createDialog" max-width="520" persistent>
      <v-card rounded="xl">
        <v-card-title class="text-h6">
          {{ t('operationCore.workspaceDefinitions.forms.rulesAdd') }}
        </v-card-title>
        <v-card-text>
          <v-select
            v-model="createForm.ruleType"
            :items="ruleTypeItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.workspaceDefinitions.forms.rulesColType')"
            density="comfortable"
            class="mb-2"
          />
          <v-text-field
            v-model="createForm.name"
            :label="t('operationCore.workspaceDefinitions.forms.rulesColName')"
            density="comfortable"
            required
            class="mb-2"
          />
          <v-select
            v-model="createForm.trigger"
            :items="triggerItems"
            item-title="title"
            item-value="value"
            label="trigger"
            density="comfortable"
            class="mb-2"
          />
          <v-text-field
            v-if="showTransitionKey"
            v-model="createForm.transitionKey"
            :label="t('operationCore.workspaceDefinitions.forms.rulesTransitionKey')"
            density="comfortable"
            class="mb-2"
          />
          <template v-if="createForm.ruleType === 'validation'">
            <v-select
              v-model="createForm.conditionField"
              :items="conditionFieldItems"
              item-title="title"
              item-value="value"
              :label="t('operationCore.workspaceDefinitions.forms.rulesConditionField')"
              density="comfortable"
              class="mb-2"
            />
            <v-textarea
              v-model="createForm.errorMessage"
              :label="t('operationCore.workspaceDefinitions.forms.rulesErrorMessage')"
              rows="2"
              auto-grow
              density="comfortable"
            />
          </template>
          <template v-else>
            <v-select
              v-model="createForm.defaultAction"
              :items="defaultActionItems"
              item-title="title"
              item-value="value"
              :label="t('operationCore.workspaceDefinitions.forms.rulesDefaultAction')"
              density="comfortable"
              class="mb-2"
            />
            <v-select
              v-if="createForm.defaultAction === 'setField'"
              v-model="createForm.defaultField"
              :items="conditionFieldItems"
              item-title="title"
              item-value="value"
              :label="t('operationCore.workspaceDefinitions.forms.rulesDefaultField')"
              density="comfortable"
              class="mb-2"
            />
            <MngDirectoryPickerField
              v-if="createForm.defaultAction === 'setAssignee'"
              v-model="createForm.defaultValue"
              entity="user"
              density="comfortable"
              hide-details
            />
            <v-text-field
              v-else
              v-model="createForm.defaultValue"
              :label="t('operationCore.workspaceDefinitions.forms.rulesDefaultValue')"
              density="comfortable"
            />
          </template>
        </v-card-text>
        <v-card-actions class="px-4 pb-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="createDialog = false">
            {{ t('operationCore.definitions.cancel') }}
          </v-btn>
          <v-btn color="primary" variant="flat" class="text-none" :loading="saving" @click="submitCreate">
            {{ t('operationCore.definitions.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteDialog" max-width="400">
      <v-card rounded="xl">
        <v-card-title>{{ t('operationCore.workspaceDefinitions.forms.rulesDeleteTitle') }}</v-card-title>
        <v-card-text>{{ t('operationCore.workspaceDefinitions.forms.rulesDeleteBody') }}</v-card-text>
        <v-card-actions class="px-4 pb-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteDialog = false">
            {{ t('operationCore.definitions.cancel') }}
          </v-btn>
          <v-btn color="error" variant="flat" class="text-none" :loading="deleting" @click="confirmDelete">
            {{ t('operationCore.definitions.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
