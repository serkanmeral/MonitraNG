<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { DiLetterhead, DiLetterheadDesignSession } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  letterhead: DiLetterhead | null;
  session: DiLetterheadDesignSession | null;
}>();

const { t } = useAppI18n();

const footer = computed(() => props.letterhead?.settings.footer);
const source = computed(() => props.session?.designFooterSource ?? 'pending');

const sourceLabelKey = computed(() => {
  if (source.value === 'disabled') return 'documentIntelligence.letterheads.designFooterSourceDisabled';
  if (source.value === 'design') return 'documentIntelligence.letterheads.designFooterSourceDesign';
  if (source.value === 'pending') return 'documentIntelligence.letterheads.designFooterSourcePending';
  return 'documentIntelligence.letterheads.designFooterSourceDesign';
});

const sourceColor = computed(() => {
  if (source.value === 'disabled') return 'grey';
  if (source.value === 'pending') return 'warning';
  return 'primary';
});

const tableSizeLabel = computed(() => {
  const f = footer.value;
  if (!f?.enabled) return '';
  return t('documentIntelligence.letterheads.footerTableSizeSummary', {
    rows: f.tableRows,
    cols: f.tableColumns,
  });
});

const previewLines = computed(() => props.session?.footerPreviewLines ?? []);
</script>

<template>
  <v-card v-if="letterhead" variant="outlined" rounded="lg" class="mb-3">
    <v-card-text class="pa-4">
      <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-2">
        <div class="text-subtitle-2 font-weight-bold">
          {{ t('documentIntelligence.letterheads.designFooterSummaryTitle') }}
        </div>
        <v-chip size="small" :color="sourceColor" variant="tonal">
          {{ t(sourceLabelKey) }}
        </v-chip>
      </div>

      <p class="text-body-2 text-medium-emphasis mb-3">
        {{ t('documentIntelligence.letterheads.designFooterSummaryHint') }}
      </p>

      <template v-if="footer?.enabled">
        <div class="text-caption font-weight-bold text-medium-emphasis mb-1">
          {{ t('documentIntelligence.letterheads.designFooterTableConfigTitle') }}
        </div>
        <div class="text-body-2 mb-3">{{ tableSizeLabel }}</div>

        <div v-if="previewLines.length > 1" class="text-caption font-weight-bold text-medium-emphasis mb-1">
          {{ t('documentIntelligence.letterheads.designFooterPreviewTitle') }}
        </div>
        <div v-if="previewLines.length > 1" class="footer-preview-lines rounded-lg pa-3 mb-2">
          <div
            v-for="(line, index) in previewLines.slice(1)"
            :key="index"
            class="text-body-2"
          >
            {{ line }}
          </div>
        </div>

        <v-alert
          v-if="source === 'design' || source === 'pending'"
          type="info"
          variant="tonal"
          density="compact"
          class="rounded-lg mb-0"
        >
          {{ t('documentIntelligence.letterheads.designFooterCollaboraNote') }}
        </v-alert>
      </template>

      <v-alert v-else type="info" variant="tonal" density="compact" class="rounded-lg mb-0">
        {{ t('documentIntelligence.letterheads.designFooterDisabledNote') }}
      </v-alert>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.footer-preview-lines {
  background: rgba(var(--v-theme-on-surface), 0.04);
  border: 1px dashed rgba(var(--v-theme-on-surface), 0.12);
  font-size: 0.8125rem;
  line-height: 1.45;
}
</style>
