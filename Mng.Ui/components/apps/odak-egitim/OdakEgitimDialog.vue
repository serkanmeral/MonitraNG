<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakDivisionRow, OdakTrainingFormModel, OdakTrainingRow } from '@/utils/odakEgitimConfig';
import {
  emptyTrainingFormModel,
  resolveTrainingDurum,
  trainingRowToFormModel,
  type OdakTrainingDialogMode,
} from '@/utils/odakEgitimService';
import { ODAK_TRAINING_STATUS_OPTIONS } from '@/utils/odakEgitimConfig';

const props = defineProps<{
  modelValue: boolean;
  mode: OdakTrainingDialogMode;
  trainingId?: string;
  seed?: OdakTrainingRow | null;
  divisions: OdakDivisionRow[];
  saving?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  save: [form: OdakTrainingFormModel];
}>();

const { t } = useAppI18n();
const form = ref<OdakTrainingFormModel>(emptyTrainingFormModel());
const formRef = ref<{ validate: () => Promise<{ valid: boolean }> } | null>(null);

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const title = computed(() =>
  props.mode === 'create'
    ? t('odakEgitim.trainings.dialog.createTitle')
    : t('odakEgitim.trainings.dialog.editTitle', { no: props.seed?.egitimNo ?? '' })
);

const divisionItems = computed(() =>
  props.divisions.map((d) => ({
    title: d.ad ?? d.kod ?? '',
    value: d.__dataId ?? d.dataId ?? '',
  }))
);

const statusItems = computed(() =>
  ODAK_TRAINING_STATUS_OPTIONS.map((o) => ({ title: o.title, value: o.value }))
);

const baslikRules = [(v: string) => !!v?.trim() || t('odakEgitim.trainings.validation.baslikRequired')];

watch(
  () => [props.modelValue, props.mode, props.seed] as const,
  ([visible]) => {
    if (!visible) return;
    form.value = props.seed ? trainingRowToFormModel(props.seed) : emptyTrainingFormModel();
  },
  { immediate: true }
);

watch(
  () => form.value.gerceklesenTarih,
  (v) => {
    if (v?.trim() && form.value.durum !== 'Iptal') {
      form.value.durum = 'Tamamlandi';
    }
  }
);

async function onSave() {
  const result = await formRef.value?.validate();
  if (result && !result.valid) return;
  if (!form.value.baslik.trim()) return;
  emit('save', { ...form.value, durum: resolveTrainingDurum(form.value) });
}
</script>

<template>
  <v-dialog v-model="open" max-width="720" persistent scrollable>
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center justify-space-between py-4 px-6">
        <span>{{ title }}</span>
        <v-btn icon="mdi-close" variant="text" @click="open = false" />
      </v-card-title>
      <v-divider />
      <v-card-text class="pa-6">
        <v-form ref="formRef" @submit.prevent="onSave">
          <v-row dense>
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.baslik"
                :label="t('odakEgitim.trainings.fields.baslik')"
                :rules="baslikRules"
                required
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-select
                v-model="form.birimId"
                :items="divisionItems"
                :label="t('odakEgitim.trainings.fields.birim')"
                clearable
              />
            </v-col>
            <v-col cols="12">
              <v-text-field v-model="form.konu" :label="t('odakEgitim.trainings.fields.konu')" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="form.egitimVeren" :label="t('odakEgitim.trainings.fields.egitimVeren')" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="form.konum" :label="t('odakEgitim.trainings.fields.konum')" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.planlananTarih"
                type="datetime-local"
                :label="t('odakEgitim.trainings.fields.planlananTarih')"
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.gerceklesenTarih"
                type="datetime-local"
                :label="t('odakEgitim.trainings.fields.gerceklesenTarih')"
              />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field
                v-model.number="form.sureDakika"
                type="number"
                min="0"
                :label="t('odakEgitim.trainings.fields.sureDakika')"
              />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field
                v-model.number="form.toplamCalisanSayisi"
                type="number"
                min="0"
                :label="t('odakEgitim.trainings.fields.toplamCalisanSayisi')"
              />
            </v-col>
            <v-col cols="12" md="4">
              <v-select
                v-model="form.durum"
                :items="statusItems"
                :label="t('odakEgitim.trainings.fields.durum')"
              />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="form.egitimAmaci" rows="2" :label="t('odakEgitim.trainings.fields.egitimAmaci')" />
            </v-col>
            <v-col cols="12">
              <v-textarea
                v-model="form.degerlendirmeYontemi"
                rows="2"
                :label="t('odakEgitim.trainings.fields.degerlendirmeYontemi')"
              />
            </v-col>
          </v-row>
        </v-form>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="open = false">{{ t('odakEgitim.common.cancel') }}</v-btn>
        <v-btn color="primary" :loading="saving" @click="onSave">{{ t('odakEgitim.common.save') }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
