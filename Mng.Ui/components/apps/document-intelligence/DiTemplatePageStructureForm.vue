<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type {
  DiTemplateFooter,
  DiTemplateLetterhead,
  DiTemplatePageLayout,
} from '@/types/apps/documentIntelligence';
import { diCmToTwips, diTwipsToCm } from '@/utils/diPageLayout';

withDefaults(
  defineProps<{
    draftHint?: boolean;
    loading?: boolean;
  }>(),
  {
    draftHint: false,
    loading: false,
  }
);

const letterhead = defineModel<DiTemplateLetterhead>('letterhead', { required: true });
const footer = defineModel<DiTemplateFooter>('footer', { required: true });
const pageLayout = defineModel<DiTemplatePageLayout>('pageLayout', { required: true });

const { t } = useAppI18n();

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
    <div class="text-subtitle-2 font-weight-bold mb-2">
      {{ t('documentIntelligence.designer.pageStructure.title') }}
    </div>
    <p class="text-body-2 text-medium-emphasis mb-3">
      {{
        draftHint
          ? t('documentIntelligence.designer.pageStructure.draftHint')
          : t('documentIntelligence.designer.pageStructure.hint')
      }}
    </p>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />

    <template v-else>
      <div class="text-caption font-weight-bold text-medium-emphasis mb-2">
        {{ t('documentIntelligence.designer.pageStructure.marginsTitle') }}
      </div>
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
        {{ t('documentIntelligence.designer.letterhead.title') }}
      </div>
      <p v-if="draftHint" class="text-body-2 text-medium-emphasis mb-2">
        {{ t('documentIntelligence.designer.letterhead.draftHint') }}
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
        <v-checkbox
          v-model="letterhead.showDocumentName"
          :label="t('documentIntelligence.designer.letterhead.showDocumentName')"
          hide-details
          density="compact"
        />
        <v-checkbox
          v-model="letterhead.showDocumentNumber"
          :label="t('documentIntelligence.designer.letterhead.showDocumentNumber')"
          hide-details
          density="compact"
        />
        <v-checkbox
          v-model="letterhead.showGeneratedAt"
          :label="t('documentIntelligence.designer.letterhead.showGeneratedAt')"
          hide-details
          density="compact"
        />
      </template>

      <v-divider class="my-4" />

      <div class="text-caption font-weight-bold text-medium-emphasis mb-2">
        {{ t('documentIntelligence.designer.footer.title') }}
      </div>
      <p v-if="draftHint" class="text-body-2 text-medium-emphasis mb-2">
        {{ t('documentIntelligence.designer.footer.draftHint') }}
      </p>
      <v-checkbox
        v-model="footer.enabled"
        :label="t('documentIntelligence.designer.footer.enabled')"
        hide-details
        density="compact"
      />
      <template v-if="footer.enabled">
        <v-checkbox
          v-model="footer.showFormRevision"
          :label="t('documentIntelligence.designer.footer.showFormRevision')"
          hide-details
          density="compact"
        />
        <v-checkbox
          v-model="footer.showOfficeColumns"
          :label="t('documentIntelligence.designer.footer.showOfficeColumns')"
          hide-details
          density="compact"
        />
        <v-checkbox
          v-model="footer.showAddresses"
          :label="t('documentIntelligence.designer.footer.showAddresses')"
          hide-details
          density="compact"
        />
        <v-checkbox
          v-model="footer.showContacts"
          :label="t('documentIntelligence.designer.footer.showContacts')"
          hide-details
          density="compact"
        />
        <v-checkbox
          v-model="footer.showDividerLine"
          :label="t('documentIntelligence.designer.footer.showDividerLine')"
          hide-details
          density="compact"
        />
      </template>
    </template>
  </div>
</template>
