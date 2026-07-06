<script setup lang="ts">
import { useAppI18n } from '@/composables/useAppI18n';
import type { DiResource } from '@/types/apps/documentIntelligence';

defineProps<{
  indexPage: DiResource;
  areaName: string;
}>();

const emit = defineEmits<{ open: [DiResource] }>();

const { t } = useAppI18n();
</script>

<template>
  <v-card variant="tonal" color="primary" rounded="lg" class="di-area-index mb-4">
    <v-card-text class="d-flex align-center flex-wrap ga-3 py-3">
      <v-avatar color="primary" variant="flat" size="40" rounded="lg">
        <v-icon icon="mdi-book-open-page-variant-outline" size="22" />
      </v-avatar>
      <div class="flex-grow-1 min-w-0">
        <div class="text-subtitle-2 font-weight-bold">{{ t('documentIntelligence.areaIndex.title', { area: areaName }) }}</div>
        <div class="text-body-2 text-medium-emphasis">{{ t('documentIntelligence.areaIndex.hint') }}</div>
      </div>
      <v-btn
        color="primary"
        variant="flat"
        size="small"
        class="text-none"
        prepend-icon="mdi-arrow-right"
        @click="emit('open', indexPage)"
      >
        {{ indexPage.title || indexPage.name }}
      </v-btn>
    </v-card-text>
  </v-card>
</template>
