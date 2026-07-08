<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';

export interface DiLetterheadSelectOption {
  value: string;
  title: string;
  subtitle?: string;
}

export interface DiCoverPageSelectOption {
  value: string;
  title: string;
  subtitle?: string;
}

const props = withDefaults(
  defineProps<{
    draftHint?: boolean;
    loading?: boolean;
    letterheadOptions?: DiLetterheadSelectOption[];
    coverPageOptions?: DiCoverPageSelectOption[];
  }>(),
  {
    draftHint: false,
    loading: false,
    letterheadOptions: () => [],
    coverPageOptions: () => [],
  }
);

const defaultLetterheadId = defineModel<string | null>('defaultLetterheadId', { default: null });
const defaultCoverPageId = defineModel<string | null>('defaultCoverPageId', { default: null });

const { t } = useAppI18n();

const letterheadSelectItems = computed(() => [
  { value: null, title: t('documentIntelligence.designer.defaultLetterhead.none') },
  ...props.letterheadOptions.map((item) => ({
    value: item.value,
    title: item.title,
    subtitle: item.subtitle,
  })),
]);

const coverPageSelectItems = computed(() => [
  { value: null, title: t('documentIntelligence.designer.defaultCoverPage.none') },
  ...props.coverPageOptions.map((item) => ({
    value: item.value,
    title: item.title,
    subtitle: item.subtitle,
  })),
]);
</script>

<template>
  <div>
    <div class="text-subtitle-2 font-weight-bold mb-2">
      {{ t('documentIntelligence.designer.pageStructure.title') }}
    </div>
    <p class="text-body-2 text-medium-emphasis mb-3">
      {{
        draftHint
          ? t('documentIntelligence.designer.pageStructure.letterheadOnlyDraftHint')
          : t('documentIntelligence.designer.pageStructure.letterheadOnlyHint')
      }}
    </p>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />

    <template v-else>
      <div class="text-caption font-weight-bold text-medium-emphasis mb-2">
        {{ t('documentIntelligence.designer.defaultLetterhead.title') }}
      </div>
      <p class="text-body-2 text-medium-emphasis mb-2">
        {{ t('documentIntelligence.designer.defaultLetterhead.hint') }}
      </p>
      <v-select
        v-model="defaultLetterheadId"
        :items="letterheadSelectItems"
        item-title="title"
        item-value="value"
        :label="t('documentIntelligence.designer.defaultLetterhead.label')"
        density="compact"
        variant="outlined"
        hide-details
        clearable
        class="mb-4"
      >
        <template #item="{ item, props: itemProps }">
          <v-list-item v-bind="itemProps" :subtitle="item.raw.subtitle" />
        </template>
      </v-select>

      <div class="text-caption font-weight-bold text-medium-emphasis mb-2">
        {{ t('documentIntelligence.designer.defaultCoverPage.title') }}
      </div>
      <p class="text-body-2 text-medium-emphasis mb-2">
        {{ t('documentIntelligence.designer.defaultCoverPage.hint') }}
      </p>
      <v-select
        v-model="defaultCoverPageId"
        :items="coverPageSelectItems"
        item-title="title"
        item-value="value"
        :label="t('documentIntelligence.designer.defaultCoverPage.label')"
        density="compact"
        variant="outlined"
        hide-details
        clearable
        class="mb-2"
      >
        <template #item="{ item, props: itemProps }">
          <v-list-item v-bind="itemProps" :subtitle="item.raw.subtitle" />
        </template>
      </v-select>

      <v-alert type="info" variant="tonal" density="compact" class="rounded-lg">
        {{ t('documentIntelligence.designer.pageStructure.managedOnLetterhead') }}
      </v-alert>
    </template>
  </div>
</template>
