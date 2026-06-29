<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { ODAK_CAPA_STATUS_OPTIONS, type OdakCapaRow } from '@/utils/odakSiparisConfig';
import {
  capaDisplayNo,
  capaRowToFormModel,
  createOdakCapa,
  emptyCapaFormModel,
  fetchOdakCapaById,
  updateOdakCapa,
  type OdakCapaDialogMode,
  type OdakCapaFormModel,
} from '@/utils/odakSiparisCapaService';
import { listNcrsForPackage, ncrDataId, ncrDisplayNo } from '@/utils/odakSiparisNcrService';

const props = defineProps<{
  modelValue: boolean;
  mode: OdakCapaDialogMode;
  packageId: string;
  packageNo?: string;
  capaId?: string;
  seedRow?: OdakCapaRow | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  saved: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const internalMode = ref<OdakCapaDialogMode>('view');
const loadedRow = ref<OdakCapaRow | null>(null);
const form = reactive<OdakCapaFormModel>(emptyCapaFormModel());
const ncrItems = ref<{ value: string; title: string }[]>([]);

const readonly = computed(() => internalMode.value === 'view');
const statusItems = computed(() =>
  ODAK_CAPA_STATUS_OPTIONS.map((o) => ({ value: o.value, title: o.title }))
);

const dialogTitle = computed(() => {
  const pkg = props.packageNo ? ` · ${props.packageNo}` : '';
  if (internalMode.value === 'create') return t('odakSiparis.quality.capa.dialog.createTitle') + pkg;
  if (internalMode.value === 'edit') return t('odakSiparis.quality.capa.dialog.editTitle') + pkg;
  return t('odakSiparis.quality.capa.dialog.viewTitle', { capaNo: capaDisplayNo(loadedRow.value ?? {}) }) + pkg;
});

async function loadNcrOptions() {
  if (!props.packageId) {
    ncrItems.value = [];
    return;
  }
  try {
    const ncrs = await listNcrsForPackage(props.packageId);
    ncrItems.value = ncrs
      .map((ncr) => {
        const id = ncrDataId(ncr);
        if (!id) return null;
        return { value: id, title: ncrDisplayNo(ncr) };
      })
      .filter((x): x is { value: string; title: string } => !!x);
  } catch {
    ncrItems.value = [];
  }
}

async function loadDialogData() {
  if (!props.modelValue) return;
  internalMode.value = props.mode;
  errorMessage.value = '';
  loading.value = true;
  loadedRow.value = null;
  try {
    await loadNcrOptions();
    if (internalMode.value === 'create') {
      Object.assign(form, emptyCapaFormModel());
      return;
    }
    const id = props.capaId;
    if (!id) throw new Error(t('odakSiparis.quality.capa.dialog.missingId'));
    if (props.seedRow && props.mode === 'view') {
      Object.assign(form, capaRowToFormModel(props.seedRow));
      loadedRow.value = props.seedRow;
    }
    const full = await fetchOdakCapaById(id);
    if (!full) throw new Error(t('odakSiparis.quality.capa.dialog.notFound'));
    loadedRow.value = full;
    Object.assign(form, capaRowToFormModel(full));
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
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

async function saveCapa() {
  errorMessage.value = '';
  if (!form.description.trim()) {
    errorMessage.value = t('odakSiparis.quality.capa.validation.descriptionRequired');
    return;
  }
  saving.value = true;
  try {
    if (internalMode.value === 'create') {
      await createOdakCapa(props.packageId, form);
    } else if (props.capaId) {
      await updateOdakCapa(props.capaId, props.packageId, form);
    }
    emit('saved');
    closeDialog();
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    saving.value = false;
  }
}

watch(
  () => [props.modelValue, props.mode, props.capaId] as const,
  () => {
    if (props.modelValue) void loadDialogData();
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
    <v-card>
      <v-card-title class="d-flex align-center py-4 px-5">
        <div>
          <div class="text-h6">{{ dialogTitle }}</div>
          <div v-if="internalMode === 'view'" class="text-caption text-medium-emphasis">
            {{ t('odakSiparis.quality.capa.dialog.viewHint') }}
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
        <v-row dense>
          <v-col v-if="internalMode !== 'create'" cols="12" sm="6">
            <v-text-field
              :model-value="capaDisplayNo(loadedRow ?? {})"
              :label="t('odakSiparis.quality.capa.fields.capaNo')"
              readonly
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-select
              v-model="form.capaStatus"
              :items="statusItems"
              item-title="title"
              item-value="value"
              :label="t('odakSiparis.quality.capa.fields.capaStatus')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-select
              v-model="form.parentNcrId"
              :items="ncrItems"
              item-title="title"
              item-value="value"
              :label="t('odakSiparis.quality.capa.fields.parentNcrId')"
              :readonly="readonly"
              clearable
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="form.cpaDate"
              type="date"
              :label="t('odakSiparis.quality.capa.fields.cpaDate')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="form.source"
              :label="t('odakSiparis.quality.capa.fields.source')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="form.requestDivision"
              :label="t('odakSiparis.quality.capa.fields.requestDivision')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12">
            <v-text-field
              v-model="form.description"
              :label="t('odakSiparis.quality.capa.fields.description')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12">
            <v-textarea
              v-model="form.nonconformity"
              :label="t('odakSiparis.quality.capa.fields.nonconformity')"
              :readonly="readonly"
              rows="2"
              auto-grow
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12">
            <v-textarea
              v-model="form.rootCause"
              :label="t('odakSiparis.quality.capa.fields.rootCause')"
              :readonly="readonly"
              rows="2"
              auto-grow
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12">
            <v-textarea
              v-model="form.correctiveAction"
              :label="t('odakSiparis.quality.capa.fields.correctiveAction')"
              :readonly="readonly"
              rows="2"
              auto-grow
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12">
            <v-textarea
              v-model="form.preventiveAction"
              :label="t('odakSiparis.quality.capa.fields.preventiveAction')"
              :readonly="readonly"
              rows="2"
              auto-grow
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field
              v-model="form.closedDate"
              type="date"
              :label="t('odakSiparis.quality.capa.fields.closedDate')"
              :readonly="readonly"
              variant="outlined"
              density="compact"
            />
          </v-col>
          <v-col cols="12">
            <v-textarea
              v-model="form.notes"
              :label="t('odakSiparis.quality.capa.fields.notes')"
              :readonly="readonly"
              rows="2"
              auto-grow
              variant="outlined"
              density="compact"
            />
          </v-col>
        </v-row>
      </v-card-text>
      <v-divider />
      <v-card-actions class="px-5 py-3">
        <v-spacer />
        <template v-if="internalMode === 'view'">
          <v-btn variant="text" @click="closeDialog">{{ t('odakSiparis.lines.dialog.close') }}</v-btn>
          <v-btn color="primary" variant="flat" @click="switchToEdit">
            {{ t('odakSiparis.lines.dialog.edit') }}
          </v-btn>
        </template>
        <template v-else>
          <v-btn variant="text" @click="closeDialog">{{ t('odakSiparis.lines.dialog.cancel') }}</v-btn>
          <v-btn color="primary" variant="flat" :loading="saving" @click="saveCapa">
            {{ internalMode === 'create' ? t('odakSiparis.lines.dialog.create') : t('odakSiparis.lines.dialog.save') }}
          </v-btn>
        </template>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
