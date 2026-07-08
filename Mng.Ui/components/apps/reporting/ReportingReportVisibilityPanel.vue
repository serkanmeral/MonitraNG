<script setup lang="ts">
import { computed, ref } from 'vue';
import OdakSiparisFieldPolicyDialog from '@/components/apps/odak-siparis/OdakSiparisFieldPolicyDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakFieldPolicy, OdakFieldVisibilityPolicy } from '@/utils/odakSiparisFieldPolicies';

const props = defineProps<{
  visibilityPolicies: OdakFieldVisibilityPolicy[];
  disabled?: boolean;
}>();

const emit = defineEmits<{ reset: [] }>();

const { t } = useAppI18n();

const dialogOpen = ref(false);
const editingPolicy = ref<OdakFieldVisibilityPolicy | null>(null);

const headers = computed(() => [
  { title: t('reporting.reportAuth.colGroups'), key: 'groups' },
  { title: t('reporting.reportAuth.colScope'), key: 'scope' },
  { title: t('reporting.reportAuth.colEffect'), key: 'effect' },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
]);

function openAdd() {
  editingPolicy.value = null;
  dialogOpen.value = true;
}

function openEdit(policy: OdakFieldVisibilityPolicy) {
  editingPolicy.value = policy;
  dialogOpen.value = true;
}

function removePolicy(policy: OdakFieldVisibilityPolicy) {
  const idx = props.visibilityPolicies.findIndex((p) => p.id === policy.id);
  if (idx >= 0) props.visibilityPolicies.splice(idx, 1);
}

function onPolicySave(policy: OdakFieldPolicy) {
  if (policy.kind !== 'visibility') return;
  const list = props.visibilityPolicies;
  const idx = list.findIndex((p) => p.id === policy.id);
  if (idx >= 0) list[idx] = policy;
  else list.push(policy);
}

function resetPolicies() {
  emit('reset');
}
</script>

<template>
  <div>
    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      {{ t('reporting.reportAuth.hint') }}
    </v-alert>

    <div class="d-flex flex-wrap ga-2 mb-3">
      <v-btn size="small" variant="tonal" :disabled="disabled" @click="openAdd">
        {{ t('reporting.reportAuth.addVisibility') }}
      </v-btn>
      <v-btn size="small" variant="text" :disabled="disabled" @click="resetPolicies">
        {{ t('reporting.reportAuth.reset') }}
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
          item.groups.length ? item.groups.join(', ') : t('reporting.reportAuth.allGroups')
        }}
      </template>
      <template #item.scope="{ item }">
        {{
          item.scope === 'conditional'
            ? t('reporting.reportAuth.scopeConditional')
            : t('reporting.reportAuth.scopeAlways')
        }}
      </template>
      <template #item.effect="{ item }">
        {{ item.visible ? t('reporting.reportAuth.visible') : t('reporting.reportAuth.hidden') }}
      </template>
      <template #item.actions="{ item }">
        <v-btn icon="mdi-pencil" size="x-small" variant="text" :disabled="disabled" @click="openEdit(item)" />
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
          {{ t('reporting.reportAuth.noPolicies') }}
        </div>
      </template>
    </v-data-table>

    <OdakSiparisFieldPolicyDialog
      v-model="dialogOpen"
      kind="visibility"
      :field-label="t('reporting.reportAuth.reportLabel')"
      :policy="editingPolicy"
      :condition-field-items="[]"
      default-condition-field=""
      @save="onPolicySave"
    />
  </div>
</template>
