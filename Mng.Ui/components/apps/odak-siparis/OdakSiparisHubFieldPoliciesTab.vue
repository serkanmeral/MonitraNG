<script setup lang="ts">
import { ref, computed } from 'vue';
import { storeToRefs } from 'pinia';
import OdakSiparisFieldPolicyDialog from '@/components/apps/odak-siparis/OdakSiparisFieldPolicyDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useOdakSiparisHubSettingsStore } from '@/stores/apps/odakSiparisHubSettings';
import {
  policiesForOdakField,
  setPoliciesForOdakField,
  type OdakFieldPolicy,
  type OdakFieldPolicyKind,
  type OdakFieldPoliciesBlob,
} from '@/utils/odakSiparisFieldPolicies';
import type { OdakHubFieldPoliciesScope } from '@/utils/odakSiparisHubSettingsService';

const props = defineProps({
  scope: { type: String, required: true },
  hintKey: { type: String, required: true },
  fieldKeys: { type: Array as () => string[], required: true },
  fieldLabel: { type: Function, required: true },
  conditionFieldKeys: { type: Array as () => string[], required: true },
  defaultConditionField: { type: String, default: 'status' },
  enumFieldOptions: { type: Object as () => Record<string, { value: string; title: string }[]>, default: () => ({}) },
});

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const hubStore = useOdakSiparisHubSettingsStore();
const { bootstrapStatus } = storeToRefs(hubStore);

const hubScope = computed(() => props.scope as OdakHubFieldPoliciesScope);
const errorMessage = ref('');
const successMessage = ref('');

const blob = computed(() => hubStore.scopes[hubScope.value].config as OdakFieldPoliciesBlob);

const loading = computed(
  () => bootstrapStatus.value === 'loading' || !hubStore.scopeReady(hubScope.value)
);
const saving = computed(() => hubStore.scopeSaving(hubScope.value));
const canSave = computed(() => hubStore.canSaveScope(hubScope.value));
const canEdit = computed(() => hubStore.canEditScope(hubScope.value));

const selectedField = ref<string>(props.fieldKeys[0] ?? '');
const dialogOpen = ref(false);
const dialogKind = ref<OdakFieldPolicyKind>('visibility');
const editingPolicy = ref<OdakFieldPolicy | null>(null);

const fieldItems = computed(() =>
  props.fieldKeys.map((key) => ({
    value: key,
    title: (props.fieldLabel as (fieldName: string) => string)(key),
  }))
);

const conditionFieldItems = computed(() =>
  props.conditionFieldKeys.map((key) => ({
    value: key,
    title: (props.fieldLabel as (fieldName: string) => string)(key),
  }))
);

const currentPolicies = computed(() => policiesForOdakField(blob.value, selectedField.value));

const headers = computed(() => [
  { title: t('odakSiparis.packages.settings.fieldPolicies.colKind'), key: 'kind' },
  { title: t('odakSiparis.packages.settings.fieldPolicies.colGroups'), key: 'groups' },
  { title: t('odakSiparis.packages.settings.fieldPolicies.colScope'), key: 'scope' },
  { title: t('odakSiparis.packages.settings.fieldPolicies.colEffect'), key: 'effect' },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
]);

function columnLabel(fieldName: string): string {
  return (props.fieldLabel as (name: string) => string)(fieldName);
}

function openAdd(kind: OdakFieldPolicyKind) {
  dialogKind.value = kind;
  editingPolicy.value = null;
  dialogOpen.value = true;
}

function openEdit(policy: OdakFieldPolicy) {
  dialogKind.value = policy.kind;
  editingPolicy.value = policy;
  dialogOpen.value = true;
}

function removePolicy(policy: OdakFieldPolicy) {
  const list = policiesForOdakField(blob.value, selectedField.value).filter((p) => p.id !== policy.id);
  hubStore.setFieldPoliciesBlob(hubScope.value, setPoliciesForOdakField(blob.value, selectedField.value, list));
}

function onPolicySave(policy: OdakFieldPolicy) {
  const list = [...policiesForOdakField(blob.value, selectedField.value)];
  const idx = list.findIndex((p) => p.id === policy.id);
  if (idx >= 0) list[idx] = policy;
  else list.push(policy);
  hubStore.setFieldPoliciesBlob(hubScope.value, setPoliciesForOdakField(blob.value, selectedField.value, list));
}

async function save() {
  if (!canSave.value) return;
  errorMessage.value = '';
  successMessage.value = '';
  try {
    await hubStore.saveScope(hubScope.value);
    successMessage.value = t('odakSiparis.packages.settings.saved');
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  }
}
</script>

<template>
  <div>
    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      {{ t(hintKey) }}
    </v-alert>
    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-4">{{ errorMessage }}</v-alert>
    <v-alert v-if="successMessage" type="success" variant="tonal" density="compact" class="mb-4">{{ successMessage }}</v-alert>
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <v-row>
      <v-col cols="12" md="4">
        <v-list density="compact" nav color="primary">
          <v-list-item
            v-for="item in fieldItems"
            :key="item.value"
            :value="item.value"
            :title="item.title"
            :active="selectedField === item.value"
            :disabled="!canEdit"
            @click="selectedField = item.value"
          />
        </v-list>
      </v-col>
      <v-col cols="12" md="8">
        <div class="d-flex flex-wrap ga-2 mb-3">
          <v-btn size="small" variant="tonal" :disabled="!canEdit" @click="openAdd('visibility')">
            {{ t('odakSiparis.packages.settings.fieldPolicies.addVisibility') }}
          </v-btn>
          <v-btn size="small" variant="tonal" :disabled="!canEdit" @click="openAdd('readonly')">
            {{ t('odakSiparis.packages.settings.fieldPolicies.addReadonly') }}
          </v-btn>
        </div>
        <v-data-table :headers="headers" :items="currentPolicies" :loading="loading" density="compact" class="border rounded-md">
          <template #item.kind="{ item }">
            {{ item.kind === 'visibility' ? t('odakSiparis.packages.settings.fieldPolicies.kindVisibility') : t('odakSiparis.packages.settings.fieldPolicies.kindReadonly') }}
          </template>
          <template #item.groups="{ item }">
            {{ item.groups.length ? item.groups.join(', ') : t('odakSiparis.packages.settings.fieldPolicies.allGroups') }}
          </template>
          <template #item.scope="{ item }">
            {{ item.scope === 'conditional' ? t('odakSiparis.packages.settings.fieldPolicies.scopeConditional') : t('odakSiparis.packages.settings.fieldPolicies.scopeAlways') }}
          </template>
          <template #item.effect="{ item }">
            <span v-if="item.kind === 'visibility'">{{ item.visible ? t('odakSiparis.packages.settings.fieldPolicies.visible') : t('odakSiparis.packages.settings.fieldPolicies.hidden') }}</span>
            <span v-else>{{ item.readonly ? t('odakSiparis.packages.settings.fieldPolicies.readonly') : t('odakSiparis.packages.settings.fieldPolicies.editable') }}</span>
          </template>
          <template #item.actions="{ item }">
            <v-btn icon="mdi-pencil" size="x-small" variant="text" :disabled="!canEdit" @click="openEdit(item)" />
            <v-btn icon="mdi-delete" size="x-small" variant="text" color="error" :disabled="!canEdit" @click="removePolicy(item)" />
          </template>
        </v-data-table>
      </v-col>
    </v-row>

    <div class="d-flex mt-4">
      <v-spacer />
      <v-btn color="primary" variant="flat" :loading="saving" :disabled="!canSave" @click="save">{{ t('odakSiparis.packages.settings.save') }}</v-btn>
    </div>

    <OdakSiparisFieldPolicyDialog
      v-model="dialogOpen"
      :kind="dialogKind"
      :field-label="columnLabel(selectedField)"
      :policy="editingPolicy"
      :condition-field-items="conditionFieldItems"
      :default-condition-field="defaultConditionField"
      :enum-field-options="enumFieldOptions"
      @save="onPolicySave"
    />
  </div>
</template>
