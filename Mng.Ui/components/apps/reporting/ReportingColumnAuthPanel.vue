<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import OdakSiparisFieldPolicyDialog from '@/components/apps/odak-siparis/OdakSiparisFieldPolicyDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { FieldDefinition } from '@/stores/apps/dataset';
import {
  policiesForOdakField,
  setPoliciesForOdakField,
  type OdakFieldPoliciesBlob,
  type OdakFieldPolicy,
} from '@/utils/odakSiparisFieldPolicies';
import { reportingEnumFieldOptions, reportingFieldLabel } from '@/utils/reportingListConfig';

const props = defineProps<{
  fieldPolicies: OdakFieldPoliciesBlob;
  fields: FieldDefinition[];
  disabled?: boolean;
}>();

const emit = defineEmits<{ reset: [] }>();

const { t } = useAppI18n();

const fieldMap = computed(() => new Map(props.fields.map((f) => [f.name, f])));

const fieldKeys = computed(() =>
  props.fields.map((f) => f.name).filter((name) => Boolean(name?.trim()))
);

const selectedField = ref('');
const dialogOpen = ref(false);
const editingPolicy = ref<OdakFieldPolicy | null>(null);

watch(
  fieldKeys,
  (keys) => {
    if (!keys.length) {
      selectedField.value = '';
      return;
    }
    if (!keys.includes(selectedField.value)) {
      selectedField.value = keys[0] ?? '';
    }
  },
  { immediate: true }
);

function columnLabel(fieldName: string): string {
  return reportingFieldLabel(fieldMap.value.get(fieldName), fieldName);
}

const fieldItems = computed(() =>
  fieldKeys.value.map((key) => ({
    value: key,
    title: columnLabel(key),
  }))
);

const conditionFieldItems = computed(() => fieldItems.value);

const enumFieldOptions = computed(() => reportingEnumFieldOptions(props.fields));

const defaultConditionField = computed(() => fieldKeys.value[0] ?? '');

const currentPolicies = computed(() =>
  selectedField.value ? policiesForOdakField(props.fieldPolicies, selectedField.value) : []
);

const visibilityPolicies = computed(() =>
  currentPolicies.value.filter((p) => p.kind === 'visibility')
);

const headers = computed(() => [
  { title: t('reporting.columnAuth.colGroups'), key: 'groups' },
  { title: t('reporting.columnAuth.colScope'), key: 'scope' },
  { title: t('reporting.columnAuth.colEffect'), key: 'effect' },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
]);

function policyCountForField(fieldName: string): number {
  return policiesForOdakField(props.fieldPolicies, fieldName).filter((p) => p.kind === 'visibility')
    .length;
}

function openAdd() {
  editingPolicy.value = null;
  dialogOpen.value = true;
}

function openEdit(policy: OdakFieldPolicy) {
  editingPolicy.value = policy;
  dialogOpen.value = true;
}

function updateBlob(next: OdakFieldPoliciesBlob) {
  props.fieldPolicies.policiesByField = next.policiesByField;
}

function removePolicy(policy: OdakFieldPolicy) {
  if (!selectedField.value) return;
  const list = policiesForOdakField(props.fieldPolicies, selectedField.value).filter(
    (p) => p.id !== policy.id
  );
  updateBlob(setPoliciesForOdakField(props.fieldPolicies, selectedField.value, list));
}

function onPolicySave(policy: OdakFieldPolicy) {
  if (!selectedField.value) return;
  const list = [...policiesForOdakField(props.fieldPolicies, selectedField.value)];
  const idx = list.findIndex((p) => p.id === policy.id);
  if (idx >= 0) list[idx] = policy;
  else list.push(policy);
  updateBlob(setPoliciesForOdakField(props.fieldPolicies, selectedField.value, list));
}

function resetPolicies() {
  emit('reset');
}
</script>

<template>
  <div>
    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      {{ t('reporting.columnAuth.hint') }}
    </v-alert>

    <div class="d-flex justify-end mb-3">
      <v-btn size="small" variant="text" :disabled="disabled" @click="resetPolicies">
        {{ t('reporting.columnAuth.reset') }}
      </v-btn>
    </div>

    <v-row v-if="fieldKeys.length">
      <v-col cols="12" md="4">
        <v-list density="compact" nav color="primary">
          <v-list-item
            v-for="item in fieldItems"
            :key="item.value"
            :value="item.value"
            :title="item.title"
            :active="selectedField === item.value"
            :disabled="disabled"
            @click="selectedField = item.value"
          >
            <template v-if="policyCountForField(item.value)" #append>
              <v-chip size="x-small" variant="tonal" color="primary">
                {{ policyCountForField(item.value) }}
              </v-chip>
            </template>
          </v-list-item>
        </v-list>
      </v-col>
      <v-col cols="12" md="8">
        <div class="d-flex flex-wrap ga-2 mb-3">
          <v-btn size="small" variant="tonal" :disabled="disabled || !selectedField" @click="openAdd">
            {{ t('reporting.columnAuth.addVisibility') }}
          </v-btn>
        </div>
        <v-data-table
          :headers="headers"
          :items="visibilityPolicies"
          density="compact"
          class="border rounded-md"
        >
          <template #item.groups="{ item }">
            {{
              item.groups.length
                ? item.groups.join(', ')
                : t('reporting.columnAuth.allGroups')
            }}
          </template>
          <template #item.scope="{ item }">
            {{
              item.scope === 'conditional'
                ? t('reporting.columnAuth.scopeConditional')
                : t('reporting.columnAuth.scopeAlways')
            }}
          </template>
          <template #item.effect="{ item }">
            {{
              item.visible
                ? t('reporting.columnAuth.visible')
                : t('reporting.columnAuth.hidden')
            }}
          </template>
          <template #item.actions="{ item }">
            <v-btn
              icon="mdi-pencil"
              size="x-small"
              variant="text"
              :disabled="disabled"
              @click="openEdit(item)"
            />
            <v-btn
              icon="mdi-delete"
              size="x-small"
              variant="text"
              color="error"
              :disabled="disabled"
              @click="removePolicy(item)"
            />
          </template>
          <template #no-data>
            <div class="text-body-2 text-medium-emphasis py-4 text-center">
              {{ t('reporting.columnAuth.noPolicies') }}
            </div>
          </template>
        </v-data-table>
      </v-col>
    </v-row>

    <v-alert v-else type="warning" variant="tonal" density="compact">
      {{ t('reporting.columnAuth.noSchema') }}
    </v-alert>

    <OdakSiparisFieldPolicyDialog
      v-model="dialogOpen"
      kind="visibility"
      :field-label="selectedField ? columnLabel(selectedField) : ''"
      :policy="editingPolicy"
      :condition-field-items="conditionFieldItems"
      :default-condition-field="defaultConditionField"
      :enum-field-options="enumFieldOptions"
      @save="onPolicySave"
    />
  </div>
</template>
