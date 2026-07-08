<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { DiDocumentProducerDetail } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  profileCode: string | null | undefined;
  producer: DiDocumentProducerDetail | null;
  loading?: boolean;
}>();

const { t } = useAppI18n();

const folderLabel = computed(() =>
  (props.producer?.outputFolderPath ?? []).length
    ? props.producer!.outputFolderPath.join(' / ')
    : '—'
);

const writebackLabel = computed(() =>
  (props.producer?.writebackFields ?? []).length
    ? props.producer!.writebackFields.join(', ')
    : '—'
);
</script>

<template>
  <v-card v-if="profileCode" variant="tonal" color="primary" rounded="lg" class="mb-4">
    <v-card-title class="text-subtitle-2 font-weight-bold d-flex align-center ga-2">
      {{ t('documentIntelligence.designer.parameterStudio.producerTitle') }}
      <v-progress-circular v-if="loading" indeterminate size="16" width="2" />
    </v-card-title>
    <v-card-text class="pt-0">
      <v-row dense>
        <v-col cols="12" md="4">
          <div class="text-caption text-medium-emphasis">
            {{ t('documentIntelligence.designer.parameterStudio.producerCode') }}
          </div>
          <code>{{ profileCode }}</code>
        </v-col>
        <v-col cols="12" md="4">
          <div class="text-caption text-medium-emphasis">
            {{ t('documentIntelligence.designer.parameterStudio.producerContext') }}
          </div>
          <div>{{ producer?.contextType ?? '—' }}</div>
        </v-col>
        <v-col cols="12" md="4">
          <div class="text-caption text-medium-emphasis">
            {{ t('documentIntelligence.designer.parameterStudio.producerOutputFormat') }}
          </div>
          <div>{{ producer?.outputFormat ?? '—' }}</div>
        </v-col>
        <v-col cols="12" md="6">
          <div class="text-caption text-medium-emphasis">
            {{ t('documentIntelligence.designer.parameterStudio.producerFolder') }}
          </div>
          <div class="text-body-2">{{ folderLabel }}</div>
        </v-col>
        <v-col cols="12" md="6">
          <div class="text-caption text-medium-emphasis">
            {{ t('documentIntelligence.designer.parameterStudio.producerFilePattern') }}
          </div>
          <div class="text-body-2">{{ producer?.fileNamePattern ?? '—' }}</div>
        </v-col>
        <v-col v-if="producer?.idempotencyDataset" cols="12" md="6">
          <div class="text-caption text-medium-emphasis">
            {{ t('documentIntelligence.designer.parameterStudio.producerIdempotency') }}
          </div>
          <div class="text-body-2">
            {{ producer.idempotencyDataset }} · {{ producer.idempotencyGuardField ?? '—' }}
          </div>
        </v-col>
        <v-col v-if="producer?.writebackFields?.length" cols="12" md="6">
          <div class="text-caption text-medium-emphasis">
            {{ t('documentIntelligence.designer.parameterStudio.producerWriteback') }}
          </div>
          <div class="text-body-2">{{ writebackLabel }}</div>
        </v-col>
      </v-row>
      <v-alert
        v-if="!loading && !producer"
        type="warning"
        variant="tonal"
        density="compact"
        class="mt-2 rounded-lg"
      >
        {{ t('documentIntelligence.designer.parameterStudio.producerMissing') }}
      </v-alert>
    </v-card-text>
  </v-card>
</template>
