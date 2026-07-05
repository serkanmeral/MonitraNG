<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import OdakEgitimSubNav from '@/components/apps/odak-egitim/OdakEgitimSubNav.vue';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useKeeperUserPicker } from '@/composables/useKeeperUserPicker';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  fetchPersonTrainingHistory,
  formatOdakTrainingDate,
  formatOdakTrainingDuration,
  formatOdakTrainingHours,
  sumPersonTrainingHours,
  trainingDisplayNo,
  trainingStatusLabel,
  trainingStatusFromRow,
  type PersonTrainingHistoryRow,
} from '@/utils/odakEgitimService';
import { fetchPersonLabelMap } from '@/utils/odakSiparisPackagePersonnel';
import { EyeIcon, RefreshIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const route = useRoute();
const router = useRouter();
const personPicker = useKeeperUserPicker();

const selectedPersonId = ref<string | null>(null);
const personLabel = ref('');

const loading = ref(false);
const errorMessage = ref('');
const history = ref<PersonTrainingHistoryRow[]>([]);

const page = computed(() => ({ title: t('odakEgitim.personTrainings.title') }));
const breadcrumbs = computed(() => [
  { text: t('breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('odakSiparis.module'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('menu.odakSiparis.egitim.menuTitle'), disabled: false, href: '/apps/odak-egitim/trainings' },
  { text: t('odakEgitim.personTrainings.title'), disabled: true, href: '#' },
]);

const headers = computed(() => [
  { title: t('odakEgitim.trainings.columns.egitimNo'), key: 'egitimNo', width: 130 },
  { title: t('odakEgitim.trainings.columns.baslik'), key: 'baslik', minWidth: 200 },
  { title: t('odakEgitim.trainings.columns.egitimVeren'), key: 'egitimVeren', width: 140 },
  { title: t('odakEgitim.trainings.columns.gerceklesenTarih'), key: 'gerceklesenTarih', width: 150 },
  { title: t('odakEgitim.trainings.columns.durum'), key: 'durumLabel', width: 110 },
  { title: t('odakEgitim.stats.columns.durationHours'), key: 'sureSaatLabel', width: 100, align: 'end' as const },
  { title: t('odakEgitim.participants.columns.katildi'), key: 'katildi', width: 90 },
  { title: t('odakEgitim.participants.columns.etkin'), key: 'etkin', width: 90 },
  {
    title: t('odakEgitim.common.actions'),
    key: 'actions',
    width: 80,
    sortable: false,
    align: 'end' as const,
  },
]);

const tableItems = computed(() =>
  history.value.map((row) => {
    const training = row.training;
    return {
      raw: row,
      trainingId: row.trainingId,
      egitimNo: training ? trainingDisplayNo(training) : '—',
      baslik: training?.baslik || training?.konu || '—',
      egitimVeren: training?.egitimVeren || '—',
      gerceklesenTarih: formatOdakTrainingDate(training?.gerceklesenTarih ?? training?.planlananTarih),
      durumLabel: training ? trainingStatusLabel(trainingStatusFromRow(training)) : '—',
      katildi: row.participation.katildi !== false,
      etkin: row.participation.etkin === true,
      sureSaatLabel: formatOdakTrainingDuration(
        row.participation.katildi !== false ? training : null
      ),
    };
  })
);

const totalTrainingHours = computed(() => sumPersonTrainingHours(history.value));

const displayPersonName = computed(() => {
  const id = selectedPersonId.value?.trim();
  if (!id) return '';
  return personPicker.labelFor(id) || personLabel.value || id;
});

const summaryText = computed(() => {
  if (!selectedPersonId.value) return t('odakEgitim.personTrainings.selectHint');
  const hoursLabel = formatOdakTrainingHours(totalTrainingHours.value);
  return t('odakEgitim.personTrainings.summaryWithHours', {
    name: displayPersonName.value,
    count: history.value.length,
    hours: hoursLabel,
  });
});

async function loadHistory() {
  const personId = selectedPersonId.value?.trim();
  if (!personId) {
    history.value = [];
    return;
  }
  loading.value = true;
  errorMessage.value = '';
  try {
    await personPicker.ensureSelectedLabels([personId]);
    const labels = await fetchPersonLabelMap([personId]);
    personLabel.value = labels[personId] || personLabel.value || personId;
    history.value = await fetchPersonTrainingHistory(personId);
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    history.value = [];
  } finally {
    loading.value = false;
  }
}

function goTrainingDetail(trainingId: string) {
  if (!trainingId) return;
  void router.push(`/apps/odak-egitim/trainings/${encodeURIComponent(trainingId)}`);
}

function applyRoutePersonId() {
  const q = String(route.query.personId ?? route.query.userId ?? '').trim();
  const nameFromQuery = String(route.query.personName ?? '').trim();
  if (nameFromQuery) personLabel.value = nameFromQuery;
  if (q) void personPicker.ensureSelectedLabels([q]);
  if (q && q !== selectedPersonId.value) {
    selectedPersonId.value = q;
  } else if (q) {
    void loadHistory();
  }
}

watch(selectedPersonId, (id) => {
  const nextQuery = { ...route.query };
  if (id) nextQuery.personId = id;
  else delete nextQuery.personId;
  delete nextQuery.userId;
  delete nextQuery.personName;
  void router.replace({ query: nextQuery });
  void loadHistory();
});

onMounted(() => {
  applyRoutePersonId();
});

watch(
  () => route.query.personId,
  () => applyRoutePersonId()
);
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
    <OdakEgitimSubNav />

    <v-card rounded="lg">
      <v-card-title class="d-flex flex-wrap align-center justify-space-between gap-3 py-4 px-6">
        <div>
          <div class="text-h6">{{ t('odakEgitim.personTrainings.panelTitle') }}</div>
          <div class="text-body-2 text-medium-emphasis">{{ t('odakEgitim.personTrainings.panelSubtitle') }}</div>
        </div>
        <v-btn variant="tonal" :loading="loading" :disabled="!selectedPersonId" @click="loadHistory">
          <RefreshIcon size="18" />
        </v-btn>
      </v-card-title>

      <v-card-text class="px-6 pb-2">
        <MngDirectoryPickerField
          v-model="selectedPersonId"
          entity="user"
          :external-picker="personPicker"
          :label="t('odakEgitim.personTrainings.selectPerson')"
          class="mb-2"
        />
        <div class="text-body-2 text-medium-emphasis">{{ summaryText }}</div>
      </v-card-text>

      <v-card-text class="px-6 pt-0">
        <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-4" closable @click:close="errorMessage = ''">
          {{ errorMessage }}
        </v-alert>

        <v-data-table
          :headers="headers"
          :items="tableItems"
          :loading="loading"
          item-value="trainingId"
          density="comfortable"
          class="rounded-lg border"
          hide-default-footer
          :items-per-page="-1"
        >
          <template #item.egitimNo="{ item }">
            <a
              v-if="item.trainingId"
              href="#"
              class="text-primary text-decoration-none"
              @click.prevent="goTrainingDetail(item.trainingId)"
            >
              {{ item.egitimNo }}
            </a>
            <span v-else>{{ item.egitimNo }}</span>
          </template>
          <template #item.katildi="{ item }">
            <v-chip size="small" :color="item.katildi ? 'success' : 'default'" variant="tonal">
              {{ item.katildi ? t('odakEgitim.common.yes') : t('odakEgitim.common.no') }}
            </v-chip>
          </template>
          <template #item.etkin="{ item }">
            <v-chip
              v-if="item.etkin !== undefined && item.raw.participation.etkin != null"
              size="small"
              :color="item.etkin ? 'success' : 'default'"
              variant="tonal"
            >
              {{ item.etkin ? t('odakEgitim.common.yes') : t('odakEgitim.common.no') }}
            </v-chip>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.actions="{ item }">
            <v-btn
              v-if="item.trainingId"
              icon
              size="small"
              variant="text"
              @click="goTrainingDetail(item.trainingId)"
            >
              <EyeIcon size="18" />
            </v-btn>
          </template>
          <template #no-data>
            <div class="text-center py-8 text-medium-emphasis">
              {{
                selectedPersonId
                  ? t('odakEgitim.personTrainings.empty')
                  : t('odakEgitim.personTrainings.selectHint')
              }}
            </div>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>
  </div>
</template>

<style scoped>
.border {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
