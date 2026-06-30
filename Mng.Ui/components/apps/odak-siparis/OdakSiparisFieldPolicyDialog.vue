<script setup lang="ts">
import { ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import {
  newOdakFieldPolicyId,
  type OdakFieldPolicy,
  type OdakFieldPolicyKind,
  type OdakFieldPolicyScope,
} from '@/utils/odakSiparisFieldPolicies';
import { newConditionClauseId } from '@/utils/ocConditionClauses';

const props = defineProps({
  modelValue: { type: Boolean, required: true },
  kind: { type: String, required: true },
  fieldLabel: { type: String, required: true },
  policy: { type: Object as () => OdakFieldPolicy | null, default: null },
  conditionFieldItems: { type: Array as () => { value: string; title: string }[], required: true },
  defaultConditionField: { type: String, default: 'status' },
  enumFieldOptions: {
    type: Object as () => Record<string, { value: string; title: string }[]>,
    default: () => ({}),
  },
});

const emit = defineEmits<{
  'update:modelValue': [boolean];
  save: [OdakFieldPolicy];
}>();

const { t } = useAppI18n();

const scope = ref<OdakFieldPolicyScope>('always');
const groups = ref<string[]>([]);
const visible = ref(true);
const readonly = ref(true);
const clauses = ref<{ id: string; fieldKey: string; operator: 'eq' | 'ne'; value: unknown }[]>([]);

watch(
  () => [props.modelValue, props.policy?.id] as const,
  ([open]) => {
    if (!open) return;
    if (props.policy) {
      scope.value = props.policy.scope;
      groups.value = [...props.policy.groups];
      if (props.policy.kind === 'visibility') visible.value = props.policy.visible;
      else readonly.value = props.policy.readonly;
      clauses.value = (props.policy.conditions?.clauses ?? []).map((c) => ({ ...c }));
    } else {
      scope.value = 'always';
      groups.value = [];
      visible.value = props.kind === 'visibility';
      readonly.value = true;
      clauses.value = [];
    }
  },
  { immediate: true }
);

function addClause() {
  const fieldKey = props.defaultConditionField || props.conditionFieldItems[0]?.value || 'status';
  const enumOpts = props.enumFieldOptions[fieldKey];
  clauses.value.push({
    id: newConditionClauseId(),
    fieldKey,
    operator: 'eq',
    value: enumOpts?.[0]?.value ?? '',
  });
}

function enumOptionsForField(fieldKey: string) {
  return props.enumFieldOptions[fieldKey] ?? [];
}

function removeClause(id: string) {
  clauses.value = clauses.value.filter((c) => c.id !== id);
}

function close() {
  emit('update:modelValue', false);
}

function save() {
  const base = {
    id: props.policy?.id ?? newOdakFieldPolicyId(),
    groups: [...groups.value],
    scope: scope.value,
    conditions:
      scope.value === 'conditional' && clauses.value.length
        ? { clauses: clauses.value.map((c) => ({ ...c })) }
        : undefined,
  };
  const policy: OdakFieldPolicy =
    props.kind === 'visibility'
      ? { ...base, kind: 'visibility' as const, visible: visible.value }
      : { ...base, kind: 'readonly' as const, readonly: readonly.value };
  emit('save', policy);
  close();
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="720" persistent @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="py-4">
        {{
          policy
            ? t('odakSiparis.packages.settings.fieldPolicies.editTitle', { field: fieldLabel })
            : t('odakSiparis.packages.settings.fieldPolicies.addTitle', { field: fieldLabel })
        }}
      </v-card-title>
      <v-divider />
      <v-card-text class="pt-4">
        <MngDirectoryPickerField
          v-model="groups"
          entity="group"
          group-value-key="name"
          multiple
          :label="t('odakSiparis.packages.settings.fieldPolicies.groups')"
          density="comfortable"
          class="mb-4"
        />

        <v-btn-toggle v-model="scope" mandatory divided color="primary" class="mb-4">
          <v-btn value="always">{{ t('odakSiparis.packages.settings.fieldPolicies.scopeAlways') }}</v-btn>
          <v-btn value="conditional">{{ t('odakSiparis.packages.settings.fieldPolicies.scopeConditional') }}</v-btn>
        </v-btn-toggle>

        <v-switch
          v-if="kind === 'visibility'"
          v-model="visible"
          :label="t('odakSiparis.packages.settings.fieldPolicies.visible')"
          color="primary"
          hide-details
          class="mb-4"
        />
        <v-switch
          v-else
          v-model="readonly"
          :label="t('odakSiparis.packages.settings.fieldPolicies.readonly')"
          color="warning"
          hide-details
          class="mb-4"
        />

        <div v-if="scope === 'conditional'">
          <div class="text-subtitle-2 mb-2">{{ t('odakSiparis.packages.settings.fieldPolicies.conditions') }}</div>
          <div v-for="clause in clauses" :key="clause.id" class="d-flex flex-wrap ga-2 mb-2 align-center">
            <v-select
              v-model="clause.fieldKey"
              :items="conditionFieldItems"
              item-title="title"
              item-value="value"
              density="compact"
              hide-details
              style="min-width: 160px"
            />
            <v-select
              v-model="clause.operator"
              :items="[
                { value: 'eq', title: t('odakSiparis.packages.settings.fieldPolicies.operatorEq') },
                { value: 'ne', title: t('odakSiparis.packages.settings.fieldPolicies.operatorNe') },
              ]"
              item-title="title"
              item-value="value"
              density="compact"
              hide-details
              style="max-width: 120px"
            />
            <v-select
              v-if="enumOptionsForField(clause.fieldKey).length"
              v-model="clause.value"
              :items="enumOptionsForField(clause.fieldKey)"
              item-title="title"
              item-value="value"
              density="compact"
              hide-details
              style="min-width: 140px"
            />
            <v-text-field v-else v-model="clause.value" density="compact" hide-details style="min-width: 140px" />
            <v-btn icon="mdi-close" size="small" variant="text" @click="removeClause(clause.id)" />
          </div>
          <v-btn size="small" variant="tonal" prepend-icon="mdi-plus" @click="addClause">
            {{ t('odakSiparis.packages.settings.fieldPolicies.addCondition') }}
          </v-btn>
        </div>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="close">{{ t('odakSiparis.packages.cancel') }}</v-btn>
        <v-btn color="primary" variant="flat" @click="save">{{ t('odakSiparis.packages.settings.save') }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
