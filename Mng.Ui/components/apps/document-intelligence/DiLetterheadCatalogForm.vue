<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type {
  DiLetterheadFooterSettings,
  DiLetterheadGeneralDocNo,
  DiLetterheadHeaderFields,
  DiLetterheadSettings,
  DiTemplateLetterhead,
  DiTemplatePageLayout,
} from '@/types/apps/documentIntelligence';
import { diCmToTwips, diTwipsToCm } from '@/utils/diPageLayout';

defineProps<{
  loading?: boolean;
}>();

const name = defineModel<string>('name', { required: true });
const code = defineModel<string>('code', { required: true });
const description = defineModel<string>('description', { default: '' });
const isDefault = defineModel<boolean>('isDefault', { default: false });
const isActive = defineModel<boolean>('isActive', { default: true });
const letterhead = defineModel<DiTemplateLetterhead>('letterhead', { required: true });
const settings = defineModel<DiLetterheadSettings>('settings', { required: true });

const { t } = useAppI18n();

const headerFields = computed<DiLetterheadHeaderFields>({
  get: () => settings.value.headerFields,
  set: (value) => {
    settings.value = { ...settings.value, headerFields: value };
  },
});

const generalDocNo = computed<DiLetterheadGeneralDocNo>({
  get: () => settings.value.generalDocNo,
  set: (value) => {
    settings.value = { ...settings.value, generalDocNo: value };
  },
});

const footer = computed<DiLetterheadFooterSettings>({
  get: () => settings.value.footer,
  set: (value) => {
    settings.value = { ...settings.value, footer: value };
  },
});

const pageLayout = computed<DiTemplatePageLayout>({
  get: () => settings.value.pageLayout,
  set: (value) => {
    settings.value = { ...settings.value, pageLayout: value };
  },
});

type MarginField =
  | 'marginTopTwips'
  | 'marginRightTwips'
  | 'marginBottomTwips'
  | 'marginLeftTwips'
  | 'headerDistanceTwips'
  | 'footerDistanceTwips';

const marginFields: { key: MarginField; labelKey: string }[] = [
  { key: 'marginTopTwips', labelKey: 'documentIntelligence.designer.pageStructure.marginTop' },
  { key: 'marginRightTwips', labelKey: 'documentIntelligence.designer.pageStructure.marginRight' },
  { key: 'marginBottomTwips', labelKey: 'documentIntelligence.designer.pageStructure.marginBottom' },
  { key: 'marginLeftTwips', labelKey: 'documentIntelligence.designer.pageStructure.marginLeft' },
  { key: 'headerDistanceTwips', labelKey: 'documentIntelligence.designer.pageStructure.headerDistance' },
  { key: 'footerDistanceTwips', labelKey: 'documentIntelligence.designer.pageStructure.footerDistance' },
];

const marginCmValues = computed(() =>
  marginFields.map((field) => ({
    ...field,
    cm: diTwipsToCm(pageLayout.value[field.key]),
  }))
);

const scopeModeItems = computed(() => [
  { title: t('documentIntelligence.letterheads.scopeLetterhead'), value: 'letterhead' },
  { title: t('documentIntelligence.letterheads.scopeGlobal'), value: 'global' },
  { title: t('documentIntelligence.letterheads.scopeCustom'), value: 'custom' },
]);

const resetPolicyItems = computed(() => [
  { title: t('documentIntelligence.letterheads.resetYearly'), value: 'yearly' },
  { title: t('documentIntelligence.letterheads.resetNone'), value: 'none' },
]);

const docNoPreview = computed(() => {
  const fmt = generalDocNo.value.format || '{yyyy}-{0:D4}';
  const year = new Date().getFullYear();
  const sample = fmt
    .replace(/\{yyyy\}/gi, String(year))
    .replace(/\{yy\}/gi, String(year).slice(-2))
    .replace(/\{0(?::[^}]*)?\}/gi, '0001');
  return sample;
});

const tableRowItems = [1, 2, 3, 4, 5, 6];
const tableColumnItems = [1, 2, 3, 4, 5, 6];

function updateMarginCm(key: MarginField, rawValue: string | number) {
  const parsed = typeof rawValue === 'number' ? rawValue : Number.parseFloat(String(rawValue));
  if (!Number.isFinite(parsed)) return;
  pageLayout.value = {
    ...pageLayout.value,
    [key]: diCmToTwips(parsed),
  };
}
</script>

<template>
  <div>
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />

    <template v-else>
      <v-text-field
        v-model="name"
        :label="t('documentIntelligence.letterheads.name')"
        density="comfortable"
        variant="outlined"
        hide-details
        class="mb-3"
      />
      <v-text-field
        v-model="code"
        :label="t('documentIntelligence.letterheads.code')"
        density="comfortable"
        variant="outlined"
        hide-details
        class="mb-3"
      />
      <v-text-field
        v-model="description"
        :label="t('documentIntelligence.letterheads.description')"
        density="comfortable"
        variant="outlined"
        hide-details
        class="mb-3"
      />
      <v-checkbox
        v-model="isDefault"
        :label="t('documentIntelligence.letterheads.isDefault')"
        hide-details
        density="compact"
      />
      <v-checkbox
        v-model="isActive"
        :label="t('documentIntelligence.letterheads.isActive')"
        hide-details
        density="compact"
        class="mb-2"
      />

      <v-divider class="my-4" />

      <div class="text-caption font-weight-bold text-medium-emphasis mb-2">
        {{ t('documentIntelligence.designer.letterhead.title') }}
      </div>
      <p class="text-body-2 text-medium-emphasis mb-2">
        {{ t('documentIntelligence.letterheads.configHint') }}
      </p>
      <v-checkbox
        v-model="letterhead.enabled"
        :label="t('documentIntelligence.designer.letterhead.enabled')"
        hide-details
        density="compact"
      />
      <template v-if="letterhead.enabled">
        <v-checkbox
          v-model="letterhead.showLogo"
          :label="t('documentIntelligence.designer.letterhead.showLogo')"
          hide-details
          density="compact"
        />
      </template>

      <v-divider class="my-4" />

      <div class="text-caption font-weight-bold text-medium-emphasis mb-2">
        {{ t('documentIntelligence.letterheads.headerFieldsTitle') }}
      </div>
      <p class="text-body-2 text-medium-emphasis mb-2">
        {{ t('documentIntelligence.letterheads.headerFieldsHint') }}
      </p>
      <v-checkbox
        v-model="headerFields.documentName"
        :label="t('documentIntelligence.letterheads.fieldDocumentName')"
        hide-details
        density="compact"
      />
      <v-checkbox
        v-model="headerFields.docNo"
        :label="t('documentIntelligence.letterheads.fieldDocNo')"
        hide-details
        density="compact"
      />
      <v-checkbox
        v-model="headerFields.generatedAt"
        :label="t('documentIntelligence.letterheads.fieldGeneratedAt')"
        hide-details
        density="compact"
      />
      <v-checkbox
        v-model="headerFields.createPerson"
        :label="t('documentIntelligence.letterheads.fieldCreatePerson')"
        hide-details
        density="compact"
        class="mb-2"
      />

      <v-divider class="my-4" />

      <div class="text-caption font-weight-bold text-medium-emphasis mb-2">
        {{ t('documentIntelligence.letterheads.generalDocNoTitle') }}
      </div>
      <p class="text-body-2 text-medium-emphasis mb-2">
        {{ t('documentIntelligence.letterheads.generalDocNoHint') }}
      </p>
      <v-checkbox
        v-model="generalDocNo.enabled"
        :label="t('documentIntelligence.letterheads.generalDocNoEnabled')"
        hide-details
        density="compact"
        class="mb-2"
      />

      <template v-if="generalDocNo.enabled">
        <v-text-field
          v-model="generalDocNo.format"
          :label="t('documentIntelligence.letterheads.docNoFormat')"
          density="comfortable"
          variant="outlined"
          hide-details
          class="mb-3"
        />
        <v-select
          v-model="generalDocNo.scopeMode"
          :items="scopeModeItems"
          item-title="title"
          item-value="value"
          :label="t('documentIntelligence.letterheads.scopeMode')"
          density="comfortable"
          variant="outlined"
          hide-details
          class="mb-3"
        />
        <v-text-field
          v-if="generalDocNo.scopeMode === 'custom'"
          v-model="generalDocNo.scopeKey"
          :label="t('documentIntelligence.letterheads.scopeKey')"
          density="comfortable"
          variant="outlined"
          hide-details
          class="mb-3"
        />
        <v-select
          v-model="generalDocNo.resetPolicy"
          :items="resetPolicyItems"
          item-title="title"
          item-value="value"
          :label="t('documentIntelligence.letterheads.resetPolicy')"
          density="comfortable"
          variant="outlined"
          hide-details
          class="mb-3"
        />
        <div class="d-flex ga-3 mb-3">
          <v-text-field
            v-model.number="generalDocNo.startValue"
            type="number"
            min="1"
            :label="t('documentIntelligence.letterheads.startValue')"
            density="comfortable"
            variant="outlined"
            hide-details
          />
          <v-text-field
            v-model.number="generalDocNo.incrementStep"
            type="number"
            min="1"
            :label="t('documentIntelligence.letterheads.incrementStep')"
            density="comfortable"
            variant="outlined"
            hide-details
          />
        </div>
        <v-alert type="info" variant="tonal" density="compact" class="rounded-lg mb-2">
          {{ t('documentIntelligence.letterheads.docNoPreview', { sample: docNoPreview }) }}
        </v-alert>
      </template>

      <v-divider class="my-4" />

      <div class="text-caption font-weight-bold text-medium-emphasis mb-2">
        {{ t('documentIntelligence.designer.pageStructure.marginsTitle') }}
      </div>
      <p class="text-body-2 text-medium-emphasis mb-2">
        {{ t('documentIntelligence.letterheads.pageLayoutHint') }}
      </p>
      <v-row dense class="mb-2">
        <v-col
          v-for="field in marginCmValues"
          :key="field.key"
          cols="12"
          sm="6"
        >
          <v-text-field
            :model-value="field.cm"
            :label="t(field.labelKey)"
            type="number"
            min="0"
            step="0.01"
            suffix="cm"
            density="compact"
            variant="outlined"
            hide-details
            @update:model-value="updateMarginCm(field.key, $event)"
          />
        </v-col>
      </v-row>

      <v-divider class="my-4" />

      <div class="text-caption font-weight-bold text-medium-emphasis mb-2">
        {{ t('documentIntelligence.designer.footer.title') }}
      </div>
      <p class="text-body-2 text-medium-emphasis mb-2">
        {{ t('documentIntelligence.letterheads.footerTableHint') }}
      </p>
      <v-checkbox
        v-model="footer.enabled"
        :label="t('documentIntelligence.designer.footer.enabled')"
        hide-details
        density="compact"
      />
      <template v-if="footer.enabled">
        <div class="d-flex ga-3 mt-2">
          <v-select
            v-model.number="footer.tableRows"
            :items="tableRowItems"
            :label="t('documentIntelligence.letterheads.footerTableRows')"
            density="comfortable"
            variant="outlined"
            hide-details
          />
          <v-select
            v-model.number="footer.tableColumns"
            :items="tableColumnItems"
            :label="t('documentIntelligence.letterheads.footerTableColumns')"
            density="comfortable"
            variant="outlined"
            hide-details
          />
        </div>
        <v-alert type="info" variant="tonal" density="compact" class="rounded-lg mt-3 mb-0">
          {{ t('documentIntelligence.letterheads.footerTableEditorNote') }}
        </v-alert>
      </template>
    </template>
  </div>
</template>
