<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  ODAK_LINE_CURRENCY_OPTIONS,
  ODAK_LINE_UNIT_OPTIONS,
  type OdakLineRow,
} from '@/utils/odakSiparisConfig';
import {
  createOdakLine,
  emptyLineFormModel,
  fetchNextLineNo,
  fetchOdakLineById,
  lineRowToFormModel,
  productIdFromRow,
  productLabelFromRow,
  searchOdakProducts,
  syncLineTotalCost,
  updateOdakLine,
  type OdakLineFormModel,
  type OdakLineDialogMode,
} from '@/utils/odakSiparisLineService';

const props = defineProps<{
  modelValue: boolean;
  mode: OdakLineDialogMode;
  packageId: string;
  packageNo?: string;
  lineId?: string;
  /** Liste satirindan hizli acilis — tam kayit icin yine fetch edilir. */
  seedRow?: OdakLineRow | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  saved: [];
}>();

const { t } = useAppI18n();

const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const internalMode = ref<OdakLineDialogMode>('view');
const loadedLine = ref<OdakLineRow | null>(null);
const form = reactive<OdakLineFormModel>(emptyLineFormModel());

const productItems = ref<{ value: string; title: string }[]>([]);
const productSearchLoading = ref(false);
let productSearchTimer: ReturnType<typeof setTimeout> | null = null;

const readonly = computed(() => internalMode.value === 'view');

const dialogTitle = computed(() => {
  const pkg = props.packageNo ? ` · ${props.packageNo}` : '';
  if (internalMode.value === 'create') return t('odakSiparis.lines.dialog.createTitle') + pkg;
  if (internalMode.value === 'edit') return t('odakSiparis.lines.dialog.editTitle') + pkg;
  const no = form.lineNo ?? loadedLine.value?.lineNo ?? '';
  return t('odakSiparis.lines.dialog.viewTitle', { lineNo: no }) + pkg;
});

const unitItems = computed(() =>
  ODAK_LINE_UNIT_OPTIONS.map((o) => ({ value: o.value, title: o.title }))
);
const currencyItems = computed(() =>
  ODAK_LINE_CURRENCY_OPTIONS.map((o) => ({ value: o.value, title: o.title }))
);

const selectedProductTitle = computed(() => {
  if (!form.productId) return '';
  const hit = productItems.value.find((p) => p.value === form.productId);
  if (hit) return hit.title;
  return productLabelFromRow(loadedLine.value?.productId);
});

function resetFormFromRow(row: OdakLineRow) {
  Object.assign(form, lineRowToFormModel(row));
  const pid = productIdFromRow(row.productId);
  if (pid) {
    productItems.value = [{ value: pid, title: productLabelFromRow(row.productId) }];
  }
}

async function loadDialogData() {
  if (!props.modelValue) return;
  internalMode.value = props.mode;
  errorMessage.value = '';
  loading.value = true;
  loadedLine.value = null;
  productItems.value = [];

  try {
    if (internalMode.value === 'create') {
      const nextNo = await fetchNextLineNo(props.packageId);
      Object.assign(form, emptyLineFormModel({ lineNo: nextNo }));
      return;
    }

    const id = props.lineId;
    if (!id) throw new Error(t('odakSiparis.lines.dialog.missingId'));

    if (props.seedRow && props.mode === 'view') {
      resetFormFromRow(props.seedRow);
      loadedLine.value = props.seedRow;
    }

    const full = await fetchOdakLineById(id);
    if (!full) throw new Error(t('odakSiparis.lines.dialog.notFound'));
    loadedLine.value = full;
    resetFormFromRow(full);
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

function closeDialog() {
  emit('update:modelValue', false);
}

function switchToEdit() {
  internalMode.value = 'edit';
}

async function saveLine() {
  errorMessage.value = '';
  if (!form.description.trim()) {
    errorMessage.value = t('odakSiparis.lines.validation.descriptionRequired');
    return;
  }
  if (form.quantity == null || form.quantity < 0) {
    errorMessage.value = t('odakSiparis.lines.validation.quantityRequired');
    return;
  }
  if (form.lineNo == null || form.lineNo < 1) {
    errorMessage.value = t('odakSiparis.lines.validation.lineNoRequired');
    return;
  }

  saving.value = true;
  try {
    if (internalMode.value === 'create') {
      await createOdakLine(props.packageId, form);
    } else if (props.lineId) {
      await updateOdakLine(props.lineId, props.packageId, form);
    }
    emit('saved');
    closeDialog();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

function onProductSearch(q: string) {
  if (productSearchTimer) clearTimeout(productSearchTimer);
  productSearchTimer = setTimeout(async () => {
    if (!q || q.length < 2) return;
    productSearchLoading.value = true;
    try {
      productItems.value = await searchOdakProducts(q);
    } catch {
      productItems.value = [];
    } finally {
      productSearchLoading.value = false;
    }
  }, 350);
}

watch(
  () => [props.modelValue, props.mode, props.lineId] as const,
  () => {
    if (props.modelValue) void loadDialogData();
  }
);

watch(
  () => [form.quantity, form.unitCost] as const,
  () => {
    if (!readonly.value) syncLineTotalCost(form);
  }
);
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    max-width="860"
    scrollable
    @update:model-value="emit('update:modelValue', $event)"
  >
    <v-card class="odak-line-dialog">
      <v-card-title class="d-flex align-center py-4 px-5">
        <div>
          <div class="text-h6">{{ dialogTitle }}</div>
          <div v-if="internalMode === 'view'" class="text-caption text-medium-emphasis">
            {{ t('odakSiparis.lines.dialog.viewHint') }}
          </div>
        </div>
        <v-spacer />
        <v-btn icon variant="text" size="small" @click="closeDialog">
          <v-icon>mdi-close</v-icon>
        </v-btn>
      </v-card-title>

      <v-divider />

      <v-card-text class="px-5 py-4">
        <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-4">
          {{ errorMessage }}
        </v-alert>
        <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

        <template v-if="!loading">
          <!-- Müşteri PO -->
          <v-card variant="outlined" class="mb-4 section-card">
            <v-card-subtitle class="font-weight-medium py-3">
              {{ t('odakSiparis.lines.groups.customerPo') }}
            </v-card-subtitle>
            <v-card-text class="pt-0">
              <v-row dense>
                <v-col cols="12" sm="4">
                  <v-text-field
                    v-model="form.lineNo"
                    :label="t('odakSiparis.lines.fields.lineNo')"
                    :readonly="readonly || internalMode === 'edit'"
                    variant="outlined"
                    density="comfortable"
                    type="number"
                    hide-details="auto"
                  />
                </v-col>
                <v-col cols="12" sm="4">
                  <v-text-field
                    v-model="form.customerProjectNo"
                    :label="t('odakSiparis.lines.fields.customerProjectNo')"
                    :readonly="readonly"
                    variant="outlined"
                    density="comfortable"
                    hide-details
                  />
                </v-col>
                <v-col cols="12" sm="4">
                  <v-text-field
                    v-model="form.customerPoNo"
                    :label="t('odakSiparis.lines.fields.customerPoNo')"
                    :readonly="readonly"
                    variant="outlined"
                    density="comfortable"
                    hide-details
                  />
                </v-col>
                <v-col cols="12" sm="4">
                  <v-text-field
                    v-model="form.customerPoItemNo"
                    :label="t('odakSiparis.lines.fields.customerPoItemNo')"
                    :readonly="readonly"
                    variant="outlined"
                    density="comfortable"
                    hide-details
                  />
                </v-col>
                <v-col cols="12" sm="4">
                  <v-text-field
                    v-model="form.customerJobNo"
                    :label="t('odakSiparis.lines.fields.customerJobNo')"
                    :readonly="readonly"
                    variant="outlined"
                    density="comfortable"
                    hide-details
                  />
                </v-col>
                <v-col cols="12" sm="4">
                  <v-text-field
                    v-model="form.poItemRevNo"
                    :label="t('odakSiparis.lines.fields.poItemRevNo')"
                    :readonly="readonly"
                    variant="outlined"
                    density="comfortable"
                    hide-details
                  />
                </v-col>
              </v-row>
            </v-card-text>
          </v-card>

          <!-- Ürün -->
          <v-card variant="outlined" class="mb-4 section-card">
            <v-card-subtitle class="font-weight-medium py-3">
              {{ t('odakSiparis.lines.groups.product') }}
            </v-card-subtitle>
            <v-card-text class="pt-0">
              <v-row dense>
                <v-col cols="12">
                  <v-textarea
                    v-model="form.description"
                    :label="t('odakSiparis.lines.fields.description')"
                    :readonly="readonly"
                    variant="outlined"
                    density="comfortable"
                    rows="2"
                    auto-grow
                    hide-details="auto"
                  />
                </v-col>
                <v-col cols="12">
                  <v-autocomplete
                    v-if="!readonly"
                    v-model="form.productId"
                    :items="productItems"
                    :label="t('odakSiparis.lines.fields.productId')"
                    item-title="title"
                    item-value="value"
                    variant="outlined"
                    density="comfortable"
                    clearable
                    hide-details
                    :loading="productSearchLoading"
                    :custom-filter="() => true"
                    @update:search="onProductSearch"
                  />
                  <v-text-field
                    v-else
                    :model-value="selectedProductTitle || '—'"
                    :label="t('odakSiparis.lines.fields.productId')"
                    readonly
                    variant="outlined"
                    density="comfortable"
                    hide-details
                  />
                </v-col>
              </v-row>
            </v-card-text>
          </v-card>

          <!-- Miktar -->
          <v-card variant="outlined" class="mb-4 section-card">
            <v-card-subtitle class="font-weight-medium py-3">
              {{ t('odakSiparis.lines.groups.quantity') }}
            </v-card-subtitle>
            <v-card-text class="pt-0">
              <v-row dense>
                <v-col cols="12" sm="4">
                  <v-text-field
                    v-model.number="form.quantity"
                    :label="t('odakSiparis.lines.fields.quantity')"
                    :readonly="readonly"
                    variant="outlined"
                    density="comfortable"
                    type="number"
                    hide-details
                  />
                </v-col>
                <v-col cols="12" sm="4">
                  <v-select
                    v-model="form.unit"
                    :items="unitItems"
                    item-title="title"
                    item-value="value"
                    :label="t('odakSiparis.lines.fields.unit')"
                    :readonly="readonly"
                    variant="outlined"
                    density="comfortable"
                    hide-details
                  />
                </v-col>
                <v-col cols="12" sm="4">
                  <v-text-field
                    :model-value="form.shippedQuantity ?? 0"
                    :label="t('odakSiparis.lines.fields.shippedQuantity')"
                    readonly
                    variant="outlined"
                    density="comfortable"
                    hide-details
                  />
                </v-col>
              </v-row>
            </v-card-text>
          </v-card>

          <!-- Kalite -->
          <v-card variant="outlined" class="mb-4 section-card">
            <v-card-subtitle class="font-weight-medium py-3">
              {{ t('odakSiparis.lines.groups.quality') }}
            </v-card-subtitle>
            <v-card-text class="pt-0">
              <v-row dense>
                <v-col cols="12">
                  <v-textarea
                    v-model="form.qualityReqs"
                    :label="t('odakSiparis.lines.fields.qualityReqs')"
                    :readonly="readonly"
                    variant="outlined"
                    density="comfortable"
                    rows="2"
                    auto-grow
                    hide-details
                  />
                </v-col>
                <v-col cols="12" sm="6">
                  <v-switch
                    v-model="form.isFai"
                    :label="t('odakSiparis.lines.fields.isFai')"
                    :readonly="readonly"
                    color="primary"
                    hide-details
                    density="compact"
                  />
                </v-col>
                <v-col cols="12" sm="6">
                  <v-switch
                    v-model="form.isFaiComplete"
                    :label="t('odakSiparis.lines.fields.isFaiComplete')"
                    :readonly="readonly"
                    color="primary"
                    hide-details
                    density="compact"
                  />
                </v-col>
              </v-row>
            </v-card-text>
          </v-card>

          <!-- Sevkiyat + Maliyet -->
          <v-row dense>
            <v-col cols="12" md="6">
              <v-card variant="outlined" class="section-card h-100">
                <v-card-subtitle class="font-weight-medium py-3">
                  {{ t('odakSiparis.lines.groups.shipment') }}
                </v-card-subtitle>
                <v-card-text class="pt-0">
                  <v-text-field
                    v-model="form.shipmentDate"
                    :label="t('odakSiparis.lines.fields.shipmentDate')"
                    :readonly="readonly"
                    variant="outlined"
                    density="comfortable"
                    type="date"
                    hide-details
                    class="mb-3"
                  />
                  <v-textarea
                    v-model="form.shipmentAddress"
                    :label="t('odakSiparis.lines.fields.shipmentAddress')"
                    :readonly="readonly"
                    variant="outlined"
                    density="comfortable"
                    rows="2"
                    hide-details
                  />
                </v-card-text>
              </v-card>
            </v-col>
            <v-col cols="12" md="6">
              <v-card variant="outlined" class="section-card h-100">
                <v-card-subtitle class="font-weight-medium py-3">
                  {{ t('odakSiparis.lines.groups.cost') }}
                </v-card-subtitle>
                <v-card-text class="pt-0">
                  <v-row dense>
                    <v-col cols="12" sm="6">
                      <v-text-field
                        v-model.number="form.unitCost"
                        :label="t('odakSiparis.lines.fields.unitCost')"
                        :readonly="readonly"
                        variant="outlined"
                        density="comfortable"
                        type="number"
                        hide-details
                      />
                    </v-col>
                    <v-col cols="12" sm="6">
                      <v-text-field
                        v-model.number="form.totalCost"
                        :label="t('odakSiparis.lines.fields.totalCost')"
                        readonly
                        variant="outlined"
                        density="comfortable"
                        type="number"
                        hide-details
                      />
                    </v-col>
                    <v-col cols="12">
                      <v-select
                        v-model="form.currency"
                        :items="currencyItems"
                        item-title="title"
                        item-value="value"
                        :label="t('odakSiparis.lines.fields.currency')"
                        :readonly="readonly"
                        variant="outlined"
                        density="comfortable"
                        hide-details
                      />
                    </v-col>
                  </v-row>
                </v-card-text>
              </v-card>
            </v-col>
          </v-row>
        </template>
      </v-card-text>

      <v-divider />

      <v-card-actions class="px-5 py-3">
        <v-spacer />
        <v-btn variant="text" @click="closeDialog">
          {{ readonly ? t('odakSiparis.lines.dialog.close') : t('odakSiparis.lines.dialog.cancel') }}
        </v-btn>
        <v-btn v-if="readonly" color="primary" variant="flat" @click="switchToEdit">
          {{ t('odakSiparis.lines.dialog.edit') }}
        </v-btn>
        <v-btn
          v-else
          color="primary"
          variant="flat"
          :loading="saving"
          @click="saveLine"
        >
          {{ internalMode === 'create' ? t('odakSiparis.lines.dialog.create') : t('odakSiparis.lines.dialog.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.section-card {
  border-color: rgba(var(--v-border-color), 0.55);
}

.section-card :deep(.v-card-subtitle) {
  opacity: 1;
  font-size: 0.8125rem;
  letter-spacing: 0.02em;
  color: rgb(var(--v-theme-primary));
}
</style>
