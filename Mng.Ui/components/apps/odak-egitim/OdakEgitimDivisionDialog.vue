<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakDivisionRow } from '@/utils/odakEgitimConfig';
import {
  divisionRowToFormModel,
  emptyDivisionFormModel,
  suggestNextDivisionKod,
  type OdakDivisionDialogMode,
  type OdakDivisionFormModel,
} from '@/utils/odakEgitimService';

const props = defineProps<{
  modelValue: boolean;
  mode: OdakDivisionDialogMode;
  seed?: OdakDivisionRow | null;
  saving?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  save: [form: OdakDivisionFormModel];
}>();

const { t } = useAppI18n();
const formRef = ref<{ validate: () => Promise<{ valid: boolean }> } | null>(null);
const form = ref<OdakDivisionFormModel>(emptyDivisionFormModel());

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const title = computed(() =>
  props.mode === 'create'
    ? t('odakEgitim.divisions.dialog.createTitle')
    : t('odakEgitim.divisions.dialog.editTitle', { kod: props.seed?.kod ?? '' })
);

const kodRules = [
  (v: string) => !!v?.trim() || t('odakEgitim.divisions.validation.kodRequired'),
  (v: string) => (v?.trim().length ?? 0) <= 32 || t('odakEgitim.divisions.validation.kodMax'),
];
const adRules = [(v: string) => !!v?.trim() || t('odakEgitim.divisions.validation.adRequired')];

watch(
  () => [props.modelValue, props.mode, props.seed] as const,
  async ([visible, mode, seed]) => {
    if (!visible) return;
    if (mode === 'edit' && seed) {
      form.value = divisionRowToFormModel(seed);
      return;
    }
    const suggestedKod = await suggestNextDivisionKod().catch(() => '');
    form.value = emptyDivisionFormModel({ kod: suggestedKod });
  },
  { immediate: true }
);

async function onSave() {
  const result = await formRef.value?.validate();
  if (result && !result.valid) return;
  emit('save', { ...form.value });
}
</script>

<template>
  <v-dialog v-model="open" max-width="520" persistent>
    <v-card rounded="lg">
      <v-card-title>{{ title }}</v-card-title>
      <v-card-text>
        <v-form ref="formRef" @submit.prevent="onSave">
          <v-text-field
            v-model="form.kod"
            :label="t('odakEgitim.divisions.fields.kod')"
            :rules="kodRules"
            :disabled="mode === 'edit'"
            maxlength="32"
            class="mb-2"
          />
          <v-text-field
            v-model="form.ad"
            :label="t('odakEgitim.divisions.fields.ad')"
            :rules="adRules"
            maxlength="100"
            class="mb-2"
          />
          <v-switch
            v-model="form.aktif"
            :label="t('odakEgitim.divisions.fields.aktif')"
            color="primary"
            hide-details
          />
          <div
            v-if="mode === 'edit' && seed?.legacyDivisionId"
            class="text-caption text-medium-emphasis mt-3"
          >
            Legacy ID: {{ seed.legacyDivisionId }}
          </div>
        </v-form>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="open = false">{{ t('odakEgitim.common.cancel') }}</v-btn>
        <v-btn color="primary" :loading="saving" @click="onSave">{{ t('odakEgitim.common.save') }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
