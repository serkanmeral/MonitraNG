<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakCustomerQualityReqRow } from '@/utils/odakSiparisConfig';
import {
  computeIsFaiFromQualityReqs,
  formatQualityReqSelectLabel,
  listQualityReqsForCustomer,
  qualityReqDataId,
} from '@/utils/odakSiparisCustomerQualityReqService';
import { TrashIcon } from 'vue-tabler-icons';

const props = withDefaults(
  defineProps<{
    customerId?: string | null;
    modelValue: string[];
    readonly?: boolean;
  }>(),
  {
    customerId: null,
    readonly: false,
  }
);

const emit = defineEmits<{
  'update:modelValue': [value: string[]];
  'fai-suggest': [value: boolean];
}>();

const { t } = useAppI18n();

const loading = ref(false);
const errorMessage = ref('');
const allReqs = ref<OdakCustomerQualityReqRow[]>([]);
const addSelection = ref<string[]>([]);

const activeReqs = computed(() => allReqs.value.filter((r) => r.aktif !== false));

const selectedRows = computed(() => {
  const byId = new Map(allReqs.value.map((r) => [qualityReqDataId(r), r]));
  return props.modelValue.map((id) => byId.get(id)).filter((r): r is OdakCustomerQualityReqRow => !!r);
});

const addableItems = computed(() => {
  const selected = new Set(props.modelValue);
  return activeReqs.value
    .filter((r) => {
      const id = qualityReqDataId(r);
      return id && !selected.has(id);
    })
    .map((r) => ({
      value: qualityReqDataId(r),
      title: formatQualityReqSelectLabel(r),
    }));
});

const tableHeaders = computed(() => [
  { title: t('odakSiparis.lines.qualityPicker.columns.kod'), key: 'kod', width: 100 },
  { title: t('odakSiparis.lines.qualityPicker.columns.ad'), key: 'ad', minWidth: 160 },
  { title: t('odakSiparis.lines.qualityPicker.columns.fai'), key: 'faiLabel', width: 72 },
  ...(props.readonly
    ? []
    : [
        {
          title: '',
          key: 'actions',
          sortable: false,
          align: 'end' as const,
          width: 48,
        },
      ]),
]);

async function loadReqs() {
  const id = props.customerId?.trim();
  if (!id) {
    allReqs.value = [];
    return;
  }
  loading.value = true;
  errorMessage.value = '';
  try {
    allReqs.value = await listQualityReqsForCustomer(id, { activeOnly: false });
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    allReqs.value = [];
  } finally {
    loading.value = false;
  }
}

function emitSelection(ids: string[]) {
  emit('update:modelValue', ids);
  const byId = new Map(allReqs.value.map((r) => [qualityReqDataId(r), r]));
  const rows = ids.map((id) => byId.get(id)).filter((r): r is OdakCustomerQualityReqRow => !!r);
  emit('fai-suggest', computeIsFaiFromQualityReqs(rows));
}

function onAddSelection(ids: string[]) {
  if (!ids.length) return;
  const merged = [...new Set([...props.modelValue, ...ids])];
  addSelection.value = [];
  emitSelection(merged);
}

function removeReq(id: string) {
  emitSelection(props.modelValue.filter((x) => x !== id));
}

watch(
  () => props.customerId,
  () => {
    void loadReqs();
  }
);

onMounted(() => {
  void loadReqs();
});
</script>

<template>
  <div class="odak-line-quality-req-picker">
    <v-alert v-if="!customerId" type="info" variant="tonal" density="compact" class="mb-3">
      {{ t('odakSiparis.lines.qualityPicker.noCustomer') }}
    </v-alert>

    <v-alert v-else-if="!loading && !activeReqs.length && !modelValue.length" type="info" variant="tonal" density="compact" class="mb-3">
      {{ t('odakSiparis.lines.validation.noCustomerQualityReqs') }}
    </v-alert>

    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
      {{ errorMessage }}
    </v-alert>

    <v-autocomplete
      v-if="!readonly && customerId && addableItems.length"
      v-model="addSelection"
      :items="addableItems"
      :label="t('odakSiparis.lines.qualityPicker.addLabel')"
      multiple
      chips
      closable-chips
      variant="outlined"
      density="comfortable"
      hide-details
      class="mb-3"
      :disabled="loading"
      @update:model-value="onAddSelection"
    />

    <v-data-table
      v-if="selectedRows.length"
      :headers="tableHeaders"
      :items="selectedRows"
      :loading="loading"
      density="compact"
      class="border rounded-md"
      hide-default-footer
      :items-per-page="-1"
    >
      <template #item.kod="{ item }">
        <span :class="{ 'text-medium-emphasis': item.aktif === false }">{{ item.kod ?? '—' }}</span>
        <v-chip v-if="item.aktif === false" size="x-small" variant="tonal" class="ml-1">
          {{ t('odakSiparis.customers.qualityReqs.inactive') }}
        </v-chip>
      </template>
      <template #item.ad="{ item }">
        <span :class="{ 'text-medium-emphasis': item.aktif === false }">{{ item.ad ?? '—' }}</span>
      </template>
      <template #item.faiLabel="{ item }">
        {{
          item.faiUygulanacak
            ? t('odakSiparis.customers.qualityReqs.faiYes')
            : t('odakSiparis.customers.qualityReqs.faiNo')
        }}
      </template>
      <template #item.actions="{ item }">
        <v-btn icon size="x-small" variant="text" color="error" @click="removeReq(qualityReqDataId(item))">
          <TrashIcon size="16" />
        </v-btn>
      </template>
    </v-data-table>

    <p v-else-if="customerId && !loading" class="text-caption text-medium-emphasis mb-0">
      {{ t('odakSiparis.lines.qualityPicker.noneSelected') }}
    </p>
  </div>
</template>
