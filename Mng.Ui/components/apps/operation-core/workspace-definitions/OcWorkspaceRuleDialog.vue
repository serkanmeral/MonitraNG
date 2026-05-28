<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcConditionClauseList from '@/components/apps/operation-core/workspace-definitions/OcConditionClauseList.vue';
import OcRuleScopePanel from '@/components/apps/operation-core/workspace-definitions/OcRuleScopePanel.vue';
import OcRuleEffectPanel from '@/components/apps/operation-core/workspace-definitions/OcRuleEffectPanel.vue';
import type { OpRule } from '@/types/apps/operationCore';
import type { OcConditionFieldOption } from '@/utils/ocConditionClauses';
import {
  buildOpRulePayloadFromDraft,
  formatRuleDraftScopeSummary,
  formatRuleDraftThenSummary,
  formatRuleDraftWhenSummary,
  isRuleDraftComplete,
  newWorkspaceRuleDraft,
  parseOpRuleToDraft,
  seedEmptyRuleClause,
  type OcWorkspaceRuleCatalogContext,
  type OcWorkspaceRuleDraft,
  type OcWorkspaceRuleTrigger,
  type OcWorkspaceRuleType,
} from '@/utils/ocWorkspaceRules';

const props = defineProps<{
  modelValue: boolean;
  rule: OpRule | null;
  workspaceId: string;
  conditionFields: OcConditionFieldOption[];
  typeItems: { value: string; title: string }[];
  boardItems: { value: string; title: string }[];
  stateItems: { value: string; title: string }[];
  transitionItems: { value: string; title: string }[];
  priorityItems?: { value: string; title: string }[];
  catalogContext: OcWorkspaceRuleCatalogContext;
  saving?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  save: [Record<string, unknown>];
}>();

const { t } = useAppI18n();

const draft = ref<OcWorkspaceRuleDraft>(newWorkspaceRuleDraft(''));

const ruleTypeItems = computed(() => [
  { value: 'validation', title: t('operationCore.workspaceDefinitions.rules.ruleTypeValidation') },
  { value: 'default', title: t('operationCore.workspaceDefinitions.rules.ruleTypeDefault') },
]);

const triggerItems = computed(() => [
  {
    value: 'WorkItemCreated',
    title: t('operationCore.workspaceDefinitions.rules.triggerCreated'),
    subtitle: t('operationCore.workspaceDefinitions.rules.triggerCreatedHint'),
  },
  {
    value: 'WorkItemTransition',
    title: t('operationCore.workspaceDefinitions.rules.triggerTransition'),
    subtitle: t('operationCore.workspaceDefinitions.rules.triggerTransitionHint'),
  },
  {
    value: 'WorkItemUpdated',
    title: t('operationCore.workspaceDefinitions.rules.triggerUpdated'),
    subtitle: t('operationCore.workspaceDefinitions.rules.triggerUpdatedHint'),
  },
]);

const whenModeItems = computed(() => [
  { value: 'always', title: t('operationCore.workspaceDefinitions.rules.whenAlways') },
  { value: 'conditional', title: t('operationCore.workspaceDefinitions.rules.whenConditional') },
]);

const isEdit = computed(() => !!props.rule?.__dataId);

const dialogOpen = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const showTransitionScope = computed(() => draft.value.trigger === 'WorkItemTransition');

const canSave = computed(() => isRuleDraftComplete(draft.value));

const triggerPreviewLabel = computed(() => {
  const item = triggerItems.value.find((x) => x.value === draft.value.trigger);
  return item?.title ?? draft.value.trigger;
});

const ruleTypePreviewLabel = computed(() => {
  const item = ruleTypeItems.value.find((x) => x.value === draft.value.ruleType);
  return item?.title ?? draft.value.ruleType;
});

const livePreviewLines = computed(() => [
  {
    icon: 'mdi-lightning-bolt-outline',
    text: t('operationCore.workspaceDefinitions.rules.livePreviewTrigger', {
      trigger: triggerPreviewLabel.value,
    }),
  },
  {
    icon: 'mdi-target',
    text: t('operationCore.workspaceDefinitions.rules.livePreviewScope', {
      scope: formatRuleDraftScopeSummary(
        draft.value,
        props.catalogContext,
        t('operationCore.workspaceDefinitions.rules.scopeAny')
      ),
    }),
  },
  {
    icon: 'mdi-filter-outline',
    text: t('operationCore.workspaceDefinitions.rules.livePreviewWhen', {
      when: formatRuleDraftWhenSummary(
        draft.value,
        props.catalogContext,
        t('operationCore.workspaceDefinitions.rules.whenAlwaysShort')
      ),
    }),
  },
  {
    icon: draft.value.ruleType === 'validation' ? 'mdi-alert-circle-outline' : 'mdi-cog-outline',
    text: t('operationCore.workspaceDefinitions.rules.livePreviewThen', {
      type: ruleTypePreviewLabel.value,
      effect: formatRuleDraftThenSummary(
        draft.value,
        props.catalogContext,
        t('operationCore.workspaceDefinitions.rules.effectUnset')
      ),
    }),
  },
]);

function resetDraft() {
  if (props.rule) {
    draft.value = parseOpRuleToDraft(props.rule);
  } else {
    const firstField = props.conditionFields[0]?.key ?? 'description';
    draft.value = newWorkspaceRuleDraft(props.workspaceId, {
      whenClauses: [seedEmptyRuleClause(firstField)],
    });
  }
}

watch(
  () => [props.modelValue, props.rule] as const,
  ([open]) => {
    if (open) resetDraft();
  }
);

watch(
  () => draft.value.whenMode,
  (mode) => {
    if (mode === 'conditional' && !draft.value.whenClauses.length) {
      const firstField = props.conditionFields[0]?.key ?? 'description';
      draft.value.whenClauses = [seedEmptyRuleClause(firstField)];
    }
  }
);

function save() {
  if (!canSave.value) return;
  emit('save', buildOpRulePayloadFromDraft(draft.value, props.workspaceId));
}

function onRuleTypeChange(v: OcWorkspaceRuleType) {
  draft.value.ruleType = v;
}

function onTriggerChange(v: OcWorkspaceRuleTrigger) {
  draft.value.trigger = v;
  if (v !== 'WorkItemTransition') {
    draft.value.scope = { ...draft.value.scope, transitionKey: undefined };
  }
}
</script>

<template>
  <v-dialog v-model="dialogOpen" max-width="960" scrollable>
    <v-card rounded="lg" class="oc-rule-dialog">
      <v-card-title class="d-flex align-start gap-3 pt-5 px-5 pb-2">
        <v-avatar color="primary" variant="tonal" size="44" rounded="lg">
          <v-icon icon="mdi-format-list-checks" size="24" />
        </v-avatar>
        <div class="flex-grow-1 min-width-0">
          <div class="text-h6 font-weight-bold">
            {{
              isEdit
                ? t('operationCore.workspaceDefinitions.rules.editRule')
                : t('operationCore.workspaceDefinitions.rules.addRule')
            }}
          </div>
          <p class="text-body-2 text-medium-emphasis mb-0 mt-1">
            {{ t('operationCore.workspaceDefinitions.rules.dialogIntro') }}
          </p>
        </div>
      </v-card-title>

      <v-divider />

      <v-card-text class="px-5 py-4">
        <v-row dense>
          <v-col cols="12" lg="7">
            <!-- Step 1 -->
            <section class="oc-rule-dialog__section mb-4">
              <div class="oc-rule-dialog__section-head mb-3">
                <span class="oc-rule-dialog__step">1</span>
                <div>
                  <div class="text-subtitle-1 font-weight-bold">
                    {{ t('operationCore.workspaceDefinitions.rules.sectionGeneral') }}
                  </div>
                  <p class="text-body-2 text-medium-emphasis mb-0">
                    {{ t('operationCore.workspaceDefinitions.rules.sectionGeneralHint') }}
                  </p>
                </div>
              </div>
              <v-text-field
                v-model="draft.name"
                :label="t('operationCore.workspaceDefinitions.rules.fieldName')"
                :placeholder="t('operationCore.workspaceDefinitions.rules.fieldNamePlaceholder')"
                variant="outlined"
                density="comfortable"
                class="mb-3"
              />
              <v-textarea
                v-model="draft.description"
                :label="t('operationCore.workspaceDefinitions.rules.description')"
                :placeholder="t('operationCore.workspaceDefinitions.rules.fieldDescriptionPlaceholder')"
                rows="2"
                auto-grow
                variant="outlined"
                density="comfortable"
                class="mb-3"
              />
              <v-row dense>
                <v-col cols="12" sm="6">
                  <v-select
                    :model-value="draft.ruleType"
                    :items="ruleTypeItems"
                    item-title="title"
                    item-value="value"
                    :label="t('operationCore.workspaceDefinitions.rules.fieldRuleType')"
                    variant="outlined"
                    density="comfortable"
                    @update:model-value="onRuleTypeChange"
                  />
                </v-col>
                <v-col cols="12" sm="6">
                  <v-select
                    :model-value="draft.trigger"
                    :items="triggerItems"
                    item-title="title"
                    item-value="value"
                    :label="t('operationCore.workspaceDefinitions.rules.fieldTrigger')"
                    variant="outlined"
                    density="comfortable"
                    @update:model-value="onTriggerChange"
                  >
                    <template #item="{ props: itemProps, item }">
                      <v-list-item v-bind="itemProps" :subtitle="item.raw.subtitle" />
                    </template>
                  </v-select>
                </v-col>
                <v-col cols="12" sm="4">
                  <v-text-field
                    v-model.number="draft.priority"
                    type="number"
                    :label="t('operationCore.workspaceDefinitions.rules.priority')"
                    :hint="t('operationCore.workspaceDefinitions.rules.priorityHint')"
                    persistent-hint
                    variant="outlined"
                    density="comfortable"
                  />
                </v-col>
                <v-col cols="12" sm="8" class="d-flex align-center">
                  <v-switch v-model="draft.isActive" color="primary" hide-details>
                    <template #label>
                      <div>
                        <div class="text-body-2 font-weight-medium">
                          {{ t('operationCore.workspaceDefinitions.rules.isActive') }}
                        </div>
                        <div class="text-caption text-medium-emphasis">
                          {{ t('operationCore.workspaceDefinitions.rules.fieldActiveHint') }}
                        </div>
                      </div>
                    </template>
                  </v-switch>
                </v-col>
              </v-row>
            </section>

            <!-- Step 2 -->
            <section class="oc-rule-dialog__section mb-4">
              <div class="oc-rule-dialog__section-head mb-3">
                <span class="oc-rule-dialog__step">2</span>
                <div>
                  <div class="text-subtitle-1 font-weight-bold">
                    {{ t('operationCore.workspaceDefinitions.rules.sectionScope') }}
                  </div>
                  <p class="text-body-2 text-medium-emphasis mb-0">
                    {{ t('operationCore.workspaceDefinitions.rules.sectionScopeHint') }}
                  </p>
                </div>
              </div>
              <OcRuleScopePanel
                :scope="draft.scope"
                :type-items="typeItems"
                :board-items="boardItems"
                :state-items="stateItems"
                :transition-items="transitionItems"
                :show-transition-key="showTransitionScope"
                @update:scope="draft.scope = $event"
              />
            </section>

            <!-- Step 3 -->
            <section class="oc-rule-dialog__section mb-4">
              <div class="oc-rule-dialog__section-head mb-3">
                <span class="oc-rule-dialog__step">3</span>
                <div>
                  <div class="text-subtitle-1 font-weight-bold">
                    {{ t('operationCore.workspaceDefinitions.rules.sectionWhen') }}
                  </div>
                  <p class="text-body-2 text-medium-emphasis mb-0">
                    {{ t('operationCore.workspaceDefinitions.rules.sectionWhenHint') }}
                  </p>
                </div>
              </div>
              <v-radio-group v-model="draft.whenMode" density="comfortable" hide-details class="mb-3">
                <v-radio
                  v-for="item in whenModeItems"
                  :key="item.value"
                  :label="item.title"
                  :value="item.value"
                />
              </v-radio-group>
              <v-alert
                v-if="draft.whenMode === 'conditional'"
                type="info"
                variant="tonal"
                density="compact"
                class="mb-3 rounded-lg"
              >
                {{ t('operationCore.workspaceDefinitions.rules.conditionsAndHint') }}
              </v-alert>
              <OcConditionClauseList
                v-if="draft.whenMode === 'conditional'"
                v-model="draft.whenClauses"
                label-mode="rules"
                :workspace-id="workspaceId"
                :condition-fields="conditionFields"
                :type-items="typeItems"
                :priority-items="priorityItems"
                :state-items="stateItems"
                :board-items="boardItems"
              />
            </section>

            <!-- Step 4 -->
            <section class="oc-rule-dialog__section">
              <div class="oc-rule-dialog__section-head mb-3">
                <span class="oc-rule-dialog__step">4</span>
                <div>
                  <div class="text-subtitle-1 font-weight-bold">
                    {{ t('operationCore.workspaceDefinitions.rules.sectionThen') }}
                  </div>
                  <p class="text-body-2 text-medium-emphasis mb-0">
                    {{ t('operationCore.workspaceDefinitions.rules.sectionThenHint') }}
                  </p>
                </div>
              </div>
              <OcRuleEffectPanel
                :rule-type="draft.ruleType"
                :apply-mode="draft.applyMode"
                :error-message="draft.errorMessage ?? ''"
                :default-action="draft.defaultAction"
                :default-field="draft.defaultField ?? ''"
                :default-value="draft.defaultValue"
                :assignee="draft.assignee ?? ''"
                :condition-field-items="conditionFields"
                :workspace-id="workspaceId"
                :type-items="typeItems"
                :priority-items="priorityItems"
                :state-items="stateItems"
                :board-items="boardItems"
                @update:apply-mode="draft.applyMode = $event"
                @update:error-message="draft.errorMessage = $event"
                @update:default-action="draft.defaultAction = $event"
                @update:default-field="draft.defaultField = $event"
                @update:default-value="draft.defaultValue = $event"
                @update:assignee="draft.assignee = $event"
              />
            </section>
          </v-col>

          <v-col cols="12" lg="5">
            <v-card variant="tonal" color="primary" rounded="lg" class="oc-rule-dialog__preview sticky-preview">
              <v-card-title class="text-subtitle-1 font-weight-bold d-flex align-center gap-2 py-4">
                <v-icon icon="mdi-eye-outline" size="20" />
                {{ t('operationCore.workspaceDefinitions.rules.livePreviewCardTitle') }}
              </v-card-title>
              <v-divider />
              <v-card-text class="pt-4">
                <p class="text-body-2 mb-4">
                  {{ t('operationCore.workspaceDefinitions.rules.livePreviewCardIntro') }}
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
                  {{ t('operationCore.workspaceDefinitions.rules.livePreviewFootnote') }}
                </p>
              </v-card-text>
            </v-card>
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
          {{ t('operationCore.workspaceDefinitions.rules.saveRule') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.oc-rule-dialog__section-head {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}

.oc-rule-dialog__step {
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
