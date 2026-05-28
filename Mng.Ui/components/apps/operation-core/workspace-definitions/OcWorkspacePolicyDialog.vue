<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcFormPolicyDefaultValueInput from '@/components/apps/operation-core/workspace-definitions/OcFormPolicyDefaultValueInput.vue';
import OcWorkspacePolicyConditionList from '@/components/apps/operation-core/workspace-definitions/OcWorkspacePolicyConditionList.vue';
import {
  areAllClausesComplete,
  formatWorkspaceFieldPolicySummary,
  isClauseValueFilled,
  isWorkspacePolicyComplete,
  newWorkspacePolicyClauseId,
  newWorkspacePolicyId,
  workspacePolicyKindLabel,
  type OcPolicyConditionFieldOption,
  type OcPolicyTargetFieldMeta,
  type OcPolicyValueResolveContext,
  type OcWorkspaceFieldPolicy,
  type OcWorkspaceFieldPolicyKind,
  type OcWorkspacePolicyConditionClause,
  type OcWorkspacePolicyScope,
} from '@/utils/ocWorkspaceFieldPolicies';

const props = defineProps<{
  modelValue: boolean;
  kind: OcWorkspaceFieldPolicyKind;
  targetField: OcPolicyTargetFieldMeta;
  policy: OcWorkspaceFieldPolicy | null;
  conditionFields: OcPolicyConditionFieldOption[];
  workspaceId: string;
  valueResolveContext: OcPolicyValueResolveContext;
  typeItems?: { value: string; title: string }[];
  priorityItems?: { value: string; title: string }[];
  stateItems?: { value: string; title: string }[];
  boardItems?: { value: string; title: string }[];
  saving?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  save: [OcWorkspaceFieldPolicy];
}>();

const { t } = useAppI18n();

const scope = ref<OcWorkspacePolicyScope>('always');
const visible = ref(true);
const readonly = ref(true);
const defaultValue = ref<unknown>(null);
const clauses = ref<OcWorkspacePolicyConditionClause[]>([]);

const kindMeta = computed(() => {
  if (props.kind === 'visibility') {
    return {
      icon: 'mdi-eye-settings-outline',
      color: 'primary',
      stepEffectHint: t('operationCore.workspaceDefinitions.policies.stepEffectHint_visibility'),
    };
  }
  if (props.kind === 'readonly') {
    return {
      icon: 'mdi-lock-outline',
      color: 'warning',
      stepEffectHint: t('operationCore.workspaceDefinitions.policies.stepEffectHint_readonly'),
    };
  }
  return {
    icon: 'mdi-form-dropdown',
    color: 'secondary',
    stepEffectHint: t('operationCore.workspaceDefinitions.policies.stepEffectHint_defaultValue'),
  };
});

const scopeItems = computed(() => [
  {
    value: 'always' as const,
    title: t('operationCore.workspaceDefinitions.policies.policyScopeAlways'),
    subtitle: t('operationCore.workspaceDefinitions.policies.policyScopeAlwaysHint'),
  },
  {
    value: 'conditional' as const,
    title: t('operationCore.workspaceDefinitions.policies.policyScopeConditional'),
    subtitle: t('operationCore.workspaceDefinitions.policies.policyScopeConditionalHint'),
  },
]);

const dialogOpen = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const isEdit = computed(() => props.policy != null);

const dialogTitle = computed(() => {
  const field = props.targetField.label;
  if (props.kind === 'visibility') {
    return isEdit.value
      ? t('operationCore.workspaceDefinitions.policies.editPolicyTitle_visibility', { field })
      : t('operationCore.workspaceDefinitions.policies.addPolicyTitle_visibility', { field });
  }
  if (props.kind === 'readonly') {
    return isEdit.value
      ? t('operationCore.workspaceDefinitions.policies.editPolicyTitle_readonly', { field })
      : t('operationCore.workspaceDefinitions.policies.addPolicyTitle_readonly', { field });
  }
  return isEdit.value
    ? t('operationCore.workspaceDefinitions.policies.editPolicyTitle_defaultValue', { field })
    : t('operationCore.workspaceDefinitions.policies.addPolicyTitle_defaultValue', { field });
});

const dialogIntro = computed(() =>
  t(`operationCore.workspaceDefinitions.policies.policyDialogIntro_${props.kind}`)
);

const effectSwitchLabel = computed(() => {
  if (props.kind === 'visibility') {
    return visible.value
      ? t('operationCore.workspaceDefinitions.policies.visibleOn')
      : t('operationCore.workspaceDefinitions.policies.visibleOff');
  }
  return readonly.value
    ? t('operationCore.workspaceDefinitions.policies.readonlyOn')
    : t('operationCore.workspaceDefinitions.policies.readonlyOff');
});

const effectSwitchHint = computed(() => {
  if (props.kind === 'visibility') {
    return visible.value
      ? t('operationCore.workspaceDefinitions.policies.visibleOnHint')
      : t('operationCore.workspaceDefinitions.policies.visibleOffHint');
  }
  return readonly.value
    ? t('operationCore.workspaceDefinitions.policies.readonlyOnHint')
    : t('operationCore.workspaceDefinitions.policies.readonlyOffHint');
});

const effectModel = computed({
  get: () => (props.kind === 'visibility' ? visible.value : readonly.value),
  set: (v: boolean) => {
    if (props.kind === 'visibility') visible.value = v;
    else readonly.value = v;
  },
});

const summaryLabels = computed(() => ({
  kindVisibility: t('operationCore.workspaceDefinitions.policies.kindVisibility'),
  kindReadonly: t('operationCore.workspaceDefinitions.policies.kindReadonly'),
  kindDefaultValue: t('operationCore.workspaceDefinitions.policies.kindDefaultValue'),
  scopeAlways: t('operationCore.workspaceDefinitions.policies.policyScopeAlways'),
  scopeConditional: t('operationCore.workspaceDefinitions.policies.policyScopeConditional'),
  alwaysVisible: t('operationCore.workspaceDefinitions.policies.summaryAlwaysVisible'),
  alwaysHidden: t('operationCore.workspaceDefinitions.policies.summaryAlwaysHidden'),
  conditionalVisible: t('operationCore.workspaceDefinitions.policies.summaryConditionalVisible'),
  conditionalHidden: t('operationCore.workspaceDefinitions.policies.summaryConditionalHidden'),
  alwaysReadonly: t('operationCore.workspaceDefinitions.policies.summaryAlwaysReadonly'),
  alwaysEditable: t('operationCore.workspaceDefinitions.policies.summaryAlwaysEditable'),
  conditionalReadonly: t('operationCore.workspaceDefinitions.policies.summaryConditionalReadonly'),
  conditionalEditable: t('operationCore.workspaceDefinitions.policies.summaryConditionalEditable'),
  defaultValueAlways: t('operationCore.workspaceDefinitions.policies.summaryDefaultAlways'),
  defaultValueConditional: t('operationCore.workspaceDefinitions.policies.summaryDefaultConditional'),
  operatorEq: t('operationCore.workspaceDefinitions.policies.operatorEq'),
  operatorNe: t('operationCore.workspaceDefinitions.policies.operatorNe'),
  andJoin: t('operationCore.workspaceDefinitions.policies.andJoin'),
  emptyConditions: t('operationCore.workspaceDefinitions.policies.summaryEmptyConditions'),
}));

const previewSummary = computed(() => {
  const policy = buildPolicy();
  if (!policy || !isWorkspacePolicyComplete(policy)) {
    return t('operationCore.workspaceDefinitions.policies.livePreviewIncomplete');
  }
  return formatWorkspaceFieldPolicySummary(
    policy,
    props.targetField.key,
    props.valueResolveContext,
    summaryLabels.value
  );
});

const livePreviewLines = computed(() => [
  {
    icon: 'mdi-form-textbox',
    text: t('operationCore.workspaceDefinitions.policies.livePreviewField', {
      field: props.targetField.label,
    }),
  },
  {
    icon: kindMeta.value.icon,
    text: t('operationCore.workspaceDefinitions.policies.livePreviewKind', {
      kind: workspacePolicyKindLabel(props.kind, summaryLabels.value),
    }),
  },
  {
    icon: scope.value === 'always' ? 'mdi-infinity' : 'mdi-filter-outline',
    text: t('operationCore.workspaceDefinitions.policies.livePreviewScope', {
      scope:
        scope.value === 'always'
          ? t('operationCore.workspaceDefinitions.policies.policyScopeAlways')
          : t('operationCore.workspaceDefinitions.policies.policyScopeConditional'),
    }),
  },
  {
    icon: 'mdi-eye-outline',
    text: previewSummary.value,
  },
]);

function seedEmptyClause() {
  const first = props.conditionFields[0];
  if (!first) return;
  clauses.value = [
    {
      id: newWorkspacePolicyClauseId(),
      fieldKey: first.key,
      operator: 'eq',
      value: null,
    },
  ];
}

function resetFromPolicy(p: OcWorkspaceFieldPolicy | null) {
  if (!p || p.kind !== props.kind) {
    scope.value = 'always';
    visible.value = true;
    readonly.value = true;
    defaultValue.value = null;
    clauses.value = [];
    return;
  }
  scope.value = p.scope;
  if (p.kind === 'visibility') visible.value = p.visible;
  if (p.kind === 'readonly') readonly.value = p.readonly;
  if (p.kind === 'defaultValue') defaultValue.value = p.value;
  const loaded = (p.conditions?.clauses ?? []).map((c) => ({ ...c }));
  clauses.value = loaded.length ? loaded : [];
  if (p.scope === 'conditional' && !clauses.value.length) seedEmptyClause();
}

watch(
  () => [props.modelValue, props.policy, props.kind] as const,
  ([open]) => {
    if (open) resetFromPolicy(props.policy);
  }
);

watch(scope, (next) => {
  if (next === 'conditional' && !clauses.value.length) seedEmptyClause();
});

function buildPolicy(): OcWorkspaceFieldPolicy | null {
  const base = {
    id: props.policy?.id ?? newWorkspacePolicyId(),
    scope: scope.value,
    conditions:
      scope.value === 'conditional' && areAllClausesComplete(clauses.value)
        ? {
            clauses: clauses.value.map((c) => ({
              id: c.id,
              fieldKey: c.fieldKey,
              operator: c.operator,
              value: c.value,
            })),
          }
        : undefined,
  };

  if (props.kind === 'visibility') {
    return { ...base, kind: 'visibility', visible: visible.value };
  }
  if (props.kind === 'readonly') {
    return { ...base, kind: 'readonly', readonly: readonly.value };
  }
  if (!isClauseValueFilled(defaultValue.value)) return null;
  return { ...base, kind: 'defaultValue', value: defaultValue.value };
}

const canSave = computed(() => {
  const policy = buildPolicy();
  return policy != null && isWorkspacePolicyComplete(policy);
});

function save() {
  const policy = buildPolicy();
  if (!policy || !isWorkspacePolicyComplete(policy)) return;
  emit('save', policy);
  dialogOpen.value = false;
}
</script>

<template>
  <v-dialog v-model="dialogOpen" max-width="920" scrollable>
    <v-card rounded="lg" class="oc-policy-dialog">
      <v-card-title class="d-flex align-start gap-3 pt-5 px-5 pb-2">
        <v-avatar :color="kindMeta.color" variant="tonal" size="44" rounded="lg">
          <v-icon :icon="kindMeta.icon" size="24" />
        </v-avatar>
        <div class="flex-grow-1 min-width-0">
          <div class="text-h6 font-weight-bold">{{ dialogTitle }}</div>
          <p class="text-body-2 text-medium-emphasis mb-0 mt-1">{{ dialogIntro }}</p>
        </div>
      </v-card-title>

      <v-divider />

      <v-card-text class="px-5 py-4">
        <v-row dense>
          <v-col cols="12" lg="7">
            <!-- Step 1 -->
            <section class="oc-policy-dialog__section mb-4">
              <div class="oc-policy-dialog__section-head mb-3">
                <span class="oc-policy-dialog__step">1</span>
                <div>
                  <div class="text-subtitle-1 font-weight-bold">
                    {{ t('operationCore.workspaceDefinitions.policies.sectionWhen') }}
                  </div>
                  <p class="text-body-2 text-medium-emphasis mb-0">
                    {{ t('operationCore.workspaceDefinitions.policies.sectionWhenHint') }}
                  </p>
                </div>
              </div>
              <v-radio-group v-model="scope" density="comfortable" hide-details>
                <v-radio
                  v-for="item in scopeItems"
                  :key="item.value"
                  :value="item.value"
                  class="mb-2"
                >
                  <template #label>
                    <div>
                      <div class="text-body-2 font-weight-medium">{{ item.title }}</div>
                      <div class="text-caption text-medium-emphasis">{{ item.subtitle }}</div>
                    </div>
                  </template>
                </v-radio>
              </v-radio-group>
            </section>

            <!-- Step 2 -->
            <section class="oc-policy-dialog__section mb-4">
              <div class="oc-policy-dialog__section-head mb-3">
                <span class="oc-policy-dialog__step">2</span>
                <div>
                  <div class="text-subtitle-1 font-weight-bold">
                    {{ t('operationCore.workspaceDefinitions.policies.sectionEffect') }}
                  </div>
                  <p class="text-body-2 text-medium-emphasis mb-0">
                    {{ kindMeta.stepEffectHint }}
                  </p>
                </div>
              </div>

              <v-switch
                v-if="kind === 'visibility' || kind === 'readonly'"
                v-model="effectModel"
                color="primary"
                hide-details
                class="mt-1"
              >
                <template #label>
                  <div>
                    <div class="text-body-2 font-weight-medium">{{ effectSwitchLabel }}</div>
                    <div class="text-caption text-medium-emphasis">{{ effectSwitchHint }}</div>
                  </div>
                </template>
              </v-switch>

              <template v-else>
                <p class="text-body-2 text-medium-emphasis mb-3">
                  {{ t('operationCore.workspaceDefinitions.policies.defaultValueEffectHint') }}
                </p>
                <OcFormPolicyDefaultValueInput
                  v-model="defaultValue"
                  :field-key="targetField.key"
                  :field-type="targetField.fieldType"
                  :relation-dataset="targetField.relationDataset"
                  :cardinality="targetField.cardinality"
                  :workspace-id="workspaceId"
                  :type-items="typeItems"
                  :priority-items="priorityItems"
                  :state-items="stateItems"
                  :board-items="boardItems"
                  :field-label="t('operationCore.workspaceDefinitions.policies.defaultValueEffectLabel')"
                  density="comfortable"
                  control-variant="outlined"
                />
              </template>
            </section>

            <!-- Step 3 -->
            <section v-if="scope === 'conditional'" class="oc-policy-dialog__section">
              <div class="oc-policy-dialog__section-head mb-3">
                <span class="oc-policy-dialog__step">3</span>
                <div>
                  <div class="text-subtitle-1 font-weight-bold">
                    {{ t('operationCore.workspaceDefinitions.policies.sectionConditions') }}
                  </div>
                  <p class="text-body-2 text-medium-emphasis mb-0">
                    {{ t('operationCore.workspaceDefinitions.policies.conditionsAndHint') }}
                  </p>
                </div>
              </div>
              <OcWorkspacePolicyConditionList
                v-model="clauses"
                :workspace-id="workspaceId"
                :condition-fields="conditionFields"
                :type-items="typeItems"
                :priority-items="priorityItems"
                :state-items="stateItems"
                :board-items="boardItems"
              />
            </section>

            <v-alert
              v-if="!canSave"
              type="warning"
              variant="tonal"
              density="compact"
              class="mt-4 rounded-lg"
            >
              {{ t('operationCore.workspaceDefinitions.policies.conditionRequired') }}
            </v-alert>
          </v-col>

          <v-col cols="12" lg="5">
            <v-card variant="tonal" :color="kindMeta.color" rounded="lg" class="sticky-preview">
              <v-card-title class="text-subtitle-1 font-weight-bold d-flex align-center gap-2 py-4">
                <v-icon icon="mdi-eye-outline" size="20" />
                {{ t('operationCore.workspaceDefinitions.policies.livePreviewCardTitle') }}
              </v-card-title>
              <v-divider />
              <v-card-text class="pt-4">
                <p class="text-body-2 mb-4">
                  {{ t('operationCore.workspaceDefinitions.policies.livePreviewCardIntro') }}
                </p>
                <div class="d-flex flex-column ga-3">
                  <div
                    v-for="(line, idx) in livePreviewLines"
                    :key="idx"
                    class="d-flex align-start gap-3"
                  >
                    <v-icon :icon="line.icon" size="20" class="mt-1 flex-shrink-0" />
                    <span class="text-body-2">{{ line.text }}</span>
                  </div>
                </div>
                <v-divider class="my-4" />
                <p class="text-caption text-medium-emphasis mb-0">
                  {{ t('operationCore.workspaceDefinitions.policies.livePreviewFootnote') }}
                </p>
              </v-card-text>
            </v-card>

            <v-alert type="info" variant="tonal" density="compact" class="mt-4 rounded-lg">
              {{ t('operationCore.workspaceDefinitions.policies.vsRulesHint') }}
            </v-alert>
          </v-col>
        </v-row>
      </v-card-text>

      <v-divider />

      <v-card-actions class="pa-4 px-5">
        <v-spacer />
        <v-btn variant="text" class="text-none" @click="dialogOpen = false">
          {{ t('operationCore.definitions.cancel') }}
        </v-btn>
        <v-btn
          color="primary"
          rounded="lg"
          class="text-none px-5"
          :disabled="!canSave"
          :loading="saving"
          @click="save"
        >
          {{ t('operationCore.workspaceDefinitions.policies.savePolicy') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.oc-policy-dialog__section-head {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}

.oc-policy-dialog__step {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 999px;
  font-size: 0.8125rem;
  font-weight: 700;
  flex-shrink: 0;
  background: rgba(var(--v-theme-primary), 0.12);
  color: rgb(var(--v-theme-primary));
}

@media (min-width: 1280px) {
  .sticky-preview {
    position: sticky;
    top: 12px;
  }
}
</style>
