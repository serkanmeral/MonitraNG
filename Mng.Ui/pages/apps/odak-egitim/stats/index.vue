<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import OdakEgitimSubNav from '@/components/apps/odak-egitim/OdakEgitimSubNav.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  buildOdakEgitimStatsYearOptions,
  fetchOdakEgitimMonthlyHoursStats,
  fetchOdakEgitimStats,
  formatOdakTrainingHours,
  type OdakEgitimMonthlyHoursStats,
  type OdakEgitimStatsSummary,
} from '@/utils/odakEgitimService';
import { RefreshIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();

const loading = ref(false);
const errorMessage = ref('');
const stats = ref<OdakEgitimStatsSummary | null>(null);
const monthlyStats = ref<OdakEgitimMonthlyHoursStats | null>(null);
const statsYear = ref(new Date().getFullYear());

const page = computed(() => ({ title: t('odakEgitim.stats.title') }));
const breadcrumbs = computed(() => [
  { text: t('breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('odakSiparis.module'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('menu.odakSiparis.egitim.menuTitle'), disabled: false, href: '/apps/odak-egitim/trainings' },
  { text: t('odakEgitim.stats.title'), disabled: true, href: '#' },
]);

const yearOptions = computed(() => buildOdakEgitimStatsYearOptions());

const summaryCards = computed(() => {
  const s = stats.value;
  if (!s) return [];
  const cards = [
    { label: t('odakEgitim.stats.cards.trainings'), value: s.trainingCount, color: 'primary' },
    { label: t('odakEgitim.stats.cards.planned'), value: s.plannedCount, color: 'info' },
    { label: t('odakEgitim.stats.cards.completed'), value: s.completedCount, color: 'success' },
    { label: t('odakEgitim.stats.cards.participations'), value: s.participationCount, color: 'secondary' },
    { label: t('odakEgitim.stats.cards.participants'), value: s.distinctParticipantCount, color: 'warning' },
    { label: t('odakEgitim.stats.cards.divisions'), value: s.activeDivisionCount, color: 'default' },
  ];
  if (monthlyStats.value) {
    cards.push({
      label: t('odakEgitim.stats.cards.yearTrainingHours', { year: statsYear.value }),
      value: formatOdakTrainingHours(monthlyStats.value.yearTotalHours),
      color: 'teal',
    });
  }
  return cards;
});

const divisionHeaders = computed(() => [
  { title: t('odakEgitim.divisions.fields.ad'), key: 'divisionLabel' },
  { title: t('odakEgitim.stats.columns.trainingCount'), key: 'trainingCount', width: 140, align: 'end' as const },
]);

const yearHeaders = computed(() => [
  { title: t('odakEgitim.stats.columns.year'), key: 'year', width: 100 },
  { title: t('odakEgitim.stats.columns.planned'), key: 'planned', width: 120, align: 'end' as const },
  { title: t('odakEgitim.stats.columns.completed'), key: 'completed', width: 120, align: 'end' as const },
]);

const monthlyHeaders = computed(() => [
  { title: t('odakEgitim.stats.columns.month'), key: 'monthLabel', width: 120 },
  {
    title: t('odakEgitim.stats.columns.monthlyHours'),
    key: 'monthlyHoursLabel',
    width: 160,
    align: 'end' as const,
  },
  {
    title: t('odakEgitim.stats.columns.cumulativeHours'),
    key: 'cumulativeHoursLabel',
    width: 160,
    align: 'end' as const,
  },
]);

const monthlyTableItems = computed(() =>
  (monthlyStats.value?.rows ?? []).map((row) => ({
    ...row,
    monthLabel: t(`odakEgitim.stats.monthNames.${row.month}`),
    monthlyHoursLabel: formatOdakTrainingHours(row.monthlyHours),
    cumulativeHoursLabel: formatOdakTrainingHours(row.cumulativeHours),
  }))
);

async function loadStats() {
  loading.value = true;
  errorMessage.value = '';
  try {
    const [summary, monthly] = await Promise.all([
      fetchOdakEgitimStats(),
      fetchOdakEgitimMonthlyHoursStats(statsYear.value),
    ]);
    stats.value = summary;
    monthlyStats.value = monthly;
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    stats.value = null;
    monthlyStats.value = null;
  } finally {
    loading.value = false;
  }
}

watch(statsYear, () => void loadStats());

onMounted(() => void loadStats());
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
    <OdakEgitimSubNav />

    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <v-select
        v-model="statsYear"
        :items="yearOptions"
        :label="t('odakEgitim.stats.yearFilter')"
        density="compact"
        hide-details
        style="min-width: 140px; max-width: 180px"
      />
      <v-btn variant="tonal" :loading="loading" @click="loadStats">
        <RefreshIcon size="18" />
      </v-btn>
    </div>

    <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-4" closable @click:close="errorMessage = ''">
      {{ errorMessage }}
    </v-alert>

    <v-row v-if="stats" class="mb-4">
      <v-col v-for="card in summaryCards" :key="card.label" cols="12" sm="6" md="4" lg="2">
        <v-card rounded="lg" variant="tonal" :color="card.color">
          <v-card-text class="text-center py-4">
            <div class="text-h4 font-weight-bold">{{ card.value }}</div>
            <div class="text-body-2 mt-1">{{ card.label }}</div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-row v-if="monthlyStats">
      <v-col cols="12">
        <v-card rounded="lg" class="mb-4">
          <v-card-title>
            {{ t('odakEgitim.stats.monthlyHoursTitle', { year: statsYear }) }}
          </v-card-title>
          <v-card-subtitle class="pb-2">
            {{ t('odakEgitim.stats.monthlyHoursHint') }}
          </v-card-subtitle>
          <v-divider />
          <v-data-table
            :headers="monthlyHeaders"
            :items="monthlyTableItems"
            density="comfortable"
            hide-default-footer
            :items-per-page="-1"
          />
        </v-card>
      </v-col>
    </v-row>

    <v-row v-if="stats">
      <v-col cols="12" md="6">
        <v-card rounded="lg">
          <v-card-title>{{ t('odakEgitim.stats.byDivisionTitle') }}</v-card-title>
          <v-divider />
          <v-data-table
            :headers="divisionHeaders"
            :items="stats.byDivision"
            density="comfortable"
            hide-default-footer
            :items-per-page="-1"
          />
        </v-card>
      </v-col>
      <v-col cols="12" md="6">
        <v-card rounded="lg">
          <v-card-title>{{ t('odakEgitim.stats.byYearTitle') }}</v-card-title>
          <v-divider />
          <v-data-table
            :headers="yearHeaders"
            :items="stats.byYear"
            density="comfortable"
            hide-default-footer
            :items-per-page="-1"
          />
        </v-card>
      </v-col>
    </v-row>

    <v-skeleton-loader v-else-if="loading" type="article" />
  </div>
</template>
