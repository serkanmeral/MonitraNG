<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import type { OdakCustomerRow, OdakQualityReqTemplateRow } from '@/utils/odakSiparisConfig';
import { fetchOdakCustomersPage } from '@/utils/odakSiparisCustomerService';
import {
  copyQualityReqsFromCustomer,
  copyQualityReqTemplatesToCustomer,
  listQualityReqTemplates,
} from '@/utils/odakSiparisCustomerQualityReqService';
import { packageDataId } from '@/utils/odakSiparisService';

const props = defineProps<{
  modelValue: boolean;
  mode: 'template' | 'customer';
  customerRow: OdakCustomerRow;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  copied: [count: number];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const successMessage = ref('');

const templates = ref<OdakQualityReqTemplateRow[]>([]);
const selectedTemplateIds = ref<string[]>([]);

const customerItems = ref<{ value: string; title: string }[]>([]);
const sourceCustomerId = ref<string | null>(null);

const customerId = computed(() => packageDataId(props.customerRow));

const title = computed(() =>
  props.mode === 'template'
    ? t('odakSiparis.customers.qualityReqs.copyDialog.templateTitle')
    : t('odakSiparis.customers.qualityReqs.copyDialog.customerTitle')
);

const hint = computed(() =>
  props.mode === 'template'
    ? t('odakSiparis.customers.qualityReqs.copyDialog.templateHint')
    : t('odakSiparis.customers.qualityReqs.copyDialog.customerHint')
);

const templateItems = computed(() =>
  templates.value
    .map((row) => {
      const id = packageDataId(row);
      if (!id) return null;
      const kod = row.kod ?? '';
      const ad = row.ad ?? '';
      return { value: id, title: ad ? `${kod} — ${ad}` : kod };
    })
    .filter((x): x is { value: string; title: string } => !!x)
);

async function loadDialog() {
  if (!props.modelValue) return;
  loading.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  selectedTemplateIds.value = [];
  sourceCustomerId.value = null;
  try {
    if (props.mode === 'template') {
      templates.value = await listQualityReqTemplates({
        sektor: props.customerRow.sektor ?? null,
        activeOnly: true,
      });
    } else {
      const resp = await fetchOdakCustomersPage({ limit: 500, skip: 0, aktifTab: 'all' });
      customerItems.value = (resp.items ?? [])
        .filter((c) => packageDataId(c) && packageDataId(c) !== customerId.value)
        .map((c) => ({
          value: packageDataId(c),
          title: `${c.kod ?? ''} — ${c.unvan ?? ''}`.trim(),
        }));
    }
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    loading.value = false;
  }
}

function closeDialog() {
  emit('update:modelValue', false);
}

async function doCopy() {
  saving.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  try {
    let count = 0;
    if (props.mode === 'template') {
      const selected = templates.value.filter((row) =>
        selectedTemplateIds.value.includes(packageDataId(row))
      );
      count = await copyQualityReqTemplatesToCustomer(customerId.value, selected);
    } else if (sourceCustomerId.value) {
      count = await copyQualityReqsFromCustomer(customerId.value, sourceCustomerId.value);
    }
    if (count > 0) {
      successMessage.value = t('odakSiparis.customers.qualityReqs.copyDone', { count });
      emit('copied', count);
      setTimeout(() => closeDialog(), 600);
    } else {
      errorMessage.value = t('odakSiparis.customers.qualityReqs.copyNone');
    }
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    saving.value = false;
  }
}

watch(
  () => [props.modelValue, props.mode] as const,
  ([open]) => {
    if (open) void loadDialog();
  }
);
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="560" persistent @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="py-4">{{ title }}</v-card-title>
      <v-divider />
      <v-card-text class="pt-4">
        <p class="text-body-2 text-medium-emphasis mb-4">{{ hint }}</p>
        <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
          {{ errorMessage }}
        </v-alert>
        <v-alert v-if="successMessage" type="success" variant="tonal" density="compact" class="mb-3">
          {{ successMessage }}
        </v-alert>
        <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />

        <v-autocomplete
          v-if="mode === 'template'"
          v-model="selectedTemplateIds"
          :items="templateItems"
          :label="t('odakSiparis.customers.qualityReqs.copyDialog.selectTemplates')"
          multiple
          chips
          closable-chips
          variant="outlined"
          density="comfortable"
          :disabled="loading || saving"
        />

        <v-autocomplete
          v-else
          v-model="sourceCustomerId"
          :items="customerItems"
          :label="t('odakSiparis.customers.qualityReqs.copyDialog.selectCustomer')"
          variant="outlined"
          density="comfortable"
          :disabled="loading || saving"
        />
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="closeDialog">
          {{ t('odakSiparis.packages.cancel') }}
        </v-btn>
        <v-btn color="primary" variant="flat" :loading="saving" :disabled="loading" @click="doCopy">
          {{ t('odakSiparis.customers.qualityReqs.copyDialog.import') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
