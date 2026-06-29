<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { DiGenerateDocumentResult, DiTemplateSummary } from '@/types/apps/documentIntelligence';
import type { OdakLineRow } from '@/utils/odakSiparisConfig';
import { lineDataId } from '@/utils/odakSiparisLineService';
import {
  generateOdakLineDocument,
  isLineDocumentAlreadyGeneratedError,
  lineDocumentErrorMessage,
  lineEligibleForProfile,
  profileCodeForTemplate,
  type OdakLineDocumentProfileCode,
} from '@/utils/odakSiparisLineDocumentService';
import { fetchOdakLineDocumentTemplates } from '@/utils/odakSiparisLineDocumentTemplates';

const props = defineProps<{
  modelValue: boolean;
  lines: OdakLineRow[];
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  created: [result: DiGenerateDocumentResult | null];
}>();

const { t } = useAppI18n();

const loadingTemplates = ref(false);
const templates = ref<DiTemplateSummary[]>([]);
const templateCode = ref<string | null>(null);
const lineId = ref<string | null>(null);
const submitting = ref(false);
const errorMessage = ref('');

const selectedTemplate = computed(() =>
  templates.value.find((tpl) => tpl.code === templateCode.value) ?? null
);

const selectedProfileCode = computed((): OdakLineDocumentProfileCode | null =>
  profileCodeForTemplate(selectedTemplate.value?.generationProfile)
);

const eligibleLines = computed(() => {
  const profile = selectedProfileCode.value;
  if (!profile) return [];
  return props.lines.filter((row) => lineEligibleForProfile(row, profile));
});

const lineItems = computed(() =>
  eligibleLines.value
    .map((row) => {
      const id = lineDataId(row);
      const no = row.lineNo ?? '?';
      const desc = row.description?.trim();
      const label = desc ? `K${no} — ${desc}` : `K${no}`;
      return { title: label, value: id ?? '' };
    })
    .filter((item) => item.value)
);

const templateItems = computed(() =>
  templates.value
    .map((tpl) => {
      const profile = profileCodeForTemplate(tpl.generationProfile);
      const profileLabel = profile
        ? t(`odakSiparis.lineDocuments.profileLabels.${profile}`)
        : tpl.generationProfile ?? '';
      return {
        title: tpl.name,
        value: tpl.code ?? '',
        subtitle: [tpl.code, profileLabel].filter(Boolean).join(' · '),
      };
    })
    .filter((item) => item.value)
);

const canSubmit = computed(
  () =>
    Boolean(
      templateCode.value?.trim() &&
        lineId.value?.trim() &&
        selectedProfileCode.value &&
        !submitting.value
    )
);

async function loadTemplates() {
  loadingTemplates.value = true;
  errorMessage.value = '';
  try {
    templates.value = await fetchOdakLineDocumentTemplates();
    if (!templateCode.value && templates.value.length === 1) {
      templateCode.value = templates.value[0]!.code ?? null;
    }
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    templates.value = [];
  } finally {
    loadingTemplates.value = false;
  }
}

async function submit() {
  const tpl = templateCode.value?.trim();
  const id = lineId.value?.trim();
  const profile = selectedProfileCode.value;
  if (!tpl || !id || !profile) return;
  submitting.value = true;
  errorMessage.value = '';
  try {
    const result = await generateOdakLineDocument(id, tpl, profile);
    emit('created', result);
    emit('update:modelValue', false);
  } catch (e: unknown) {
    if (isLineDocumentAlreadyGeneratedError(e)) {
      errorMessage.value = t('odakSiparis.lineDocuments.alreadyGenerated');
      emit('created', null);
    } else {
      errorMessage.value = lineDocumentErrorMessage(e, t('odakSiparis.lineDocuments.generateError'));
    }
  } finally {
    submitting.value = false;
  }
}

watch(templateCode, () => {
  if (!lineId.value) return;
  const stillEligible = eligibleLines.value.some((row) => lineDataId(row) === lineId.value);
  if (!stillEligible) lineId.value = null;
  if (!lineId.value && eligibleLines.value.length === 1) {
    lineId.value = lineDataId(eligibleLines.value[0]!) ?? null;
  }
});

watch(
  () => props.modelValue,
  (open) => {
    if (!open) {
      templateCode.value = null;
      lineId.value = null;
      errorMessage.value = '';
      return;
    }
    void loadTemplates();
  }
);

watch([eligibleLines, templateCode], () => {
  if (!props.modelValue) return;
  if (lineId.value) return;
  if (eligibleLines.value.length === 1) {
    lineId.value = lineDataId(eligibleLines.value[0]!) ?? null;
  }
});
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    max-width="520"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <v-card>
      <v-card-title>{{ t('odakSiparis.lineDocuments.createTitle') }}</v-card-title>
      <v-card-text>
        <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
          {{ errorMessage }}
        </v-alert>

        <v-alert
          v-if="!loadingTemplates && !templates.length"
          type="warning"
          variant="tonal"
          density="compact"
          class="mb-3"
        >
          {{ t('odakSiparis.lineDocuments.noTemplates') }}
        </v-alert>

        <v-alert
          v-if="templateCode && !eligibleLines.length"
          type="info"
          variant="tonal"
          density="compact"
          class="mb-3"
        >
          {{ t('odakSiparis.lineDocuments.noEligibleLines') }}
        </v-alert>

        <v-select
          v-model="templateCode"
          :items="templateItems"
          :label="t('odakSiparis.lineDocuments.templateLabel')"
          :loading="loadingTemplates"
          :disabled="!templateItems.length"
          item-title="title"
          item-value="value"
          density="compact"
          variant="outlined"
          class="mb-3"
        >
          <template #item="{ item, props: itemProps }">
            <v-list-item v-bind="itemProps" :subtitle="item.raw.subtitle" />
          </template>
        </v-select>

        <v-select
          v-model="lineId"
          :items="lineItems"
          :label="t('odakSiparis.lineDocuments.lineLabel')"
          :disabled="!lineItems.length"
          item-title="title"
          item-value="value"
          density="compact"
          variant="outlined"
        >
          <template #selection="{ item }">
            {{ item.title }}
          </template>
        </v-select>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="emit('update:modelValue', false)">
          {{ t('odakSiparis.packages.cancel') }}
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          :loading="submitting"
          :disabled="!canSubmit"
          @click="submit"
        >
          {{ t('odakSiparis.lineDocuments.createAction') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
