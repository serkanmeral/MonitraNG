<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import OdakEgitimDialog from '@/components/apps/odak-egitim/OdakEgitimDialog.vue';
import OdakEgitimSubNav from '@/components/apps/odak-egitim/OdakEgitimSubNav.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocDelete } from '@/services/operationCoreService';
import { ODAK_EGITIM_CONFIG, type OdakDivisionRow, type OdakTrainingRow, type OdakTrainingTab } from '@/utils/odakEgitimConfig';
import { exportOdakTrainingsToCsv } from '@/utils/odakEgitimExport';
import {
  buildOdakEgitimYearOptions,
  createOdakTraining,
  fetchDivisionLabelMap,
  fetchOdakDivisions,
  fetchOdakTrainingsPage,
  fetchParticipationCountByTrainingId,
  formatOdakTrainingDate,
  relationIdFromRow,
  trainingDataId,
  trainingDisplayNo,
  trainingStatusLabel,
  trainingStatusFromRow,
  updateOdakTraining,
  type OdakTrainingDialogMode,
  type OdakTrainingFormModel,
} from '@/utils/odakEgitimService';
import { DownloadIcon, EditIcon, EyeIcon, PlusIcon, RefreshIcon, TrashIcon, UsersIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const router = useRouter();

const tab = ref<OdakTrainingTab>('complete');
/** null = tüm yıllar (legacy katılımcı verisi 2017–2022) */
const year = ref<number | null>(null);
const searchQuery = ref('');
const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const items = ref<OdakTrainingRow[]>([]);
const totalCount = ref(0);
const tablePage = ref(1);
const tableItemsPerPage = ref(20);
const divisionLabels = ref<Record<string, string>>({});
const participationCounts = ref<Record<string, number>>({});
const divisions = ref<OdakDivisionRow[]>([]);

const dialogOpen = ref(false);
const dialogMode = ref<OdakTrainingDialogMode>('create');
const dialogSeed = ref<OdakTrainingRow | null>(null);

const deleteDialog = ref(false);
const rowToDelete = ref<OdakTrainingRow | null>(null);
const deleting = ref(false);
const exporting = ref(false);

const page = computed(() => ({ title: t('odakEgitim.trainings.title') }));
const breadcrumbs = computed(() => [
  { text: t('breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('odakSiparis.module'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('menu.odakSiparis.egitim.menuTitle'), disabled: false, href: '/apps/odak-egitim/trainings' },
  { text: t('odakEgitim.trainings.title'), disabled: true, href: '#' },
]);

const tabs = computed(() => [
  { value: 'plan' as const, label: t('odakEgitim.trainings.tabs.plan') },
  { value: 'complete' as const, label: t('odakEgitim.trainings.tabs.complete') },
  { value: 'all' as const, label: t('odakEgitim.trainings.tabs.all') },
]);

const yearOptions = computed(() => buildOdakEgitimYearOptions());

const yearSelectItems = computed(() => [
  { title: t('odakEgitim.trainings.allYears'), value: null as number | null },
  ...yearOptions.value.map((y) => ({ title: String(y), value: y as number | null })),
]);

const headers = computed(() => [
  { title: t('odakEgitim.trainings.columns.egitimNo'), key: 'egitimNo', width: 130 },
  { title: t('odakEgitim.trainings.columns.baslik'), key: 'baslik', minWidth: 200 },
  { title: t('odakEgitim.trainings.columns.birim'), key: 'birimLabel', width: 140 },
  { title: t('odakEgitim.trainings.columns.egitimVeren'), key: 'egitimVeren', width: 140 },
  { title: t('odakEgitim.trainings.columns.planlananTarih'), key: 'planlananTarih', width: 150 },
  { title: t('odakEgitim.trainings.columns.gerceklesenTarih'), key: 'gerceklesenTarih', width: 150 },
  { title: t('odakEgitim.trainings.columns.durum'), key: 'durumLabel', width: 110 },
  { title: t('odakEgitim.trainings.columns.katilimciSayisi'), key: 'katilimciSayisi', width: 110, align: 'center' as const },
  {
    title: t('odakEgitim.common.actions'),
    key: 'actions',
    width: 132,
    sortable: false,
    align: 'end' as const,
  },
]);

const tableItems = computed(() =>
  items.value.map((row) => {
    const birimId = relationIdFromRow(row.birimId);
    return {
      raw: row,
      __dataId: trainingDataId(row),
      egitimNo: trainingDisplayNo(row),
      baslik: row.baslik || '—',
      birimLabel: birimId ? divisionLabels.value[birimId] ?? birimId : '—',
      egitimVeren: row.egitimVeren || '—',
      planlananTarih: formatOdakTrainingDate(row.planlananTarih),
      gerceklesenTarih: formatOdakTrainingDate(row.gerceklesenTarih),
      durumLabel: trainingStatusLabel(trainingStatusFromRow(row)),
      katilimciSayisi: participationCounts.value[trainingDataId(row)] ?? 0,
    };
  })
);

const paginationLabel = computed(() =>
  t('odakEgitim.trainings.paginationSummary', {
    from: totalCount.value === 0 ? 0 : (tablePage.value - 1) * tableItemsPerPage.value + 1,
    to: Math.min(tablePage.value * tableItemsPerPage.value, totalCount.value),
    total: totalCount.value,
  })
);

async function loadDivisions() {
  divisions.value = await fetchOdakDivisions(false);
}

async function loadParticipationCounts() {
  try {
    participationCounts.value = await fetchParticipationCountByTrainingId();
  } catch {
    participationCounts.value = {};
  }
}

async function loadItems() {
  loading.value = true;
  errorMessage.value = '';
  try {
    const resp = await fetchOdakTrainingsPage({
      tab: tab.value,
      year: year.value,
      search: searchQuery.value.trim(),
      page: tablePage.value,
      limit: tableItemsPerPage.value,
    });
    items.value = resp.items;
    totalCount.value = resp.total;
    const ids = resp.items.map((r) => relationIdFromRow(r.birimId)).filter(Boolean);
    divisionLabels.value = await fetchDivisionLabelMap(ids);
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    items.value = [];
    totalCount.value = 0;
  } finally {
    loading.value = false;
  }
}

function openDialog(mode: OdakTrainingDialogMode, row?: OdakTrainingRow) {
  dialogMode.value = mode;
  dialogSeed.value = row ?? null;
  dialogOpen.value = true;
}

async function onDialogSave(form: OdakTrainingFormModel) {
  saving.value = true;
  errorMessage.value = '';
  try {
    if (dialogMode.value === 'create') {
      const id = await createOdakTraining(form);
      dialogOpen.value = false;
      await router.push(`/apps/odak-egitim/trainings/${encodeURIComponent(id)}`);
      return;
    }
    const id = trainingDataId(dialogSeed.value);
    if (!id) throw new Error(t('odakEgitim.trainings.dialog.missingId'));
    await updateOdakTraining(id, form);
    dialogOpen.value = false;
    await loadItems();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

function goDetail(row: OdakTrainingRow) {
  const id = trainingDataId(row);
  if (id) void router.push(`/apps/odak-egitim/trainings/${encodeURIComponent(id)}`);
}

function confirmDelete(row: OdakTrainingRow) {
  rowToDelete.value = row;
  deleteDialog.value = true;
}

async function executeDelete() {
  const row = rowToDelete.value;
  if (!row) return;
  const id = trainingDataId(row);
  if (!id) return;
  deleting.value = true;
  errorMessage.value = '';
  try {
    await ocDelete(ODAK_EGITIM_CONFIG.trainingsDataset, id);
    deleteDialog.value = false;
    rowToDelete.value = null;
    await loadItems();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    deleting.value = false;
  }
}

async function exportTrainings() {
  exporting.value = true;
  errorMessage.value = '';
  try {
    await exportOdakTrainingsToCsv(
      {
        tab: tab.value,
        year: year.value,
        search: searchQuery.value.trim(),
      },
      {
        baslik: t('odakEgitim.trainings.fields.baslik'),
        konu: t('odakEgitim.trainings.fields.konu'),
        konum: t('odakEgitim.trainings.fields.konum'),
        egitimVeren: t('odakEgitim.trainings.fields.egitimVeren'),
        planlananTarih: t('odakEgitim.trainings.fields.planlananTarih'),
        gerceklesenTarih: t('odakEgitim.trainings.fields.gerceklesenTarih'),
        degerlendirmeYontemi: t('odakEgitim.trainings.fields.degerlendirmeYontemi'),
        egitimAmaci: t('odakEgitim.trainings.fields.egitimAmaci'),
        toplamCalisanSayisi: t('odakEgitim.trainings.fields.toplamCalisanSayisi'),
        sureDakika: t('odakEgitim.trainings.fields.sureDakika'),
        egitimNo: t('odakEgitim.trainings.columns.egitimNo'),
        birim: t('odakEgitim.trainings.columns.birim'),
        durum: t('odakEgitim.trainings.columns.durum'),
        katilimciSayisi: t('odakEgitim.trainings.columns.katilimciSayisi'),
      }
    );
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    exporting.value = false;
  }
}

watch([tab, year], () => {
  if (tablePage.value !== 1) tablePage.value = 1;
  else void loadItems();
});

watch([tablePage, tableItemsPerPage], () => void loadItems());

onMounted(async () => {
  await loadDivisions();
  await loadParticipationCounts();
  await loadItems();
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
    <OdakEgitimSubNav />

    <v-card rounded="lg" class="mb-4">
      <v-card-title class="d-flex flex-wrap align-center justify-space-between gap-3 py-4 px-6">
        <div>
          <div class="text-h6">{{ t('odakEgitim.trainings.panelTitle') }}</div>
          <div class="text-body-2 text-medium-emphasis">{{ t('odakEgitim.trainings.panelSubtitle') }}</div>
        </div>
        <div class="d-flex flex-wrap gap-2">
          <v-btn color="primary" prepend-icon="" @click="openDialog('create')">
            <PlusIcon size="18" class="mr-1" />
            {{ t('odakEgitim.trainings.add') }}
          </v-btn>
          <v-btn variant="tonal" color="success" :loading="exporting" @click="exportTrainings">
            <DownloadIcon size="18" class="mr-1" />
            {{ t('odakEgitim.trainings.export') }}
          </v-btn>
          <v-btn variant="tonal" :loading="loading" @click="loadItems">
            <RefreshIcon size="18" />
          </v-btn>
        </div>
      </v-card-title>

      <v-card-text class="px-6 pb-2">
        <v-text-field
          v-model="searchQuery"
          :label="t('odakEgitim.trainings.search')"
          density="comfortable"
          hide-details
          clearable
          @keyup.enter="loadItems"
          @click:clear="loadItems"
        />
      </v-card-text>

      <v-card-text class="px-6 pt-0">
        <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
          <v-tabs v-model="tab" density="comfortable" color="primary">
            <v-tab v-for="item in tabs" :key="item.value" :value="item.value">{{ item.label }}</v-tab>
          </v-tabs>
          <v-select
            v-model="year"
            :items="yearSelectItems"
            item-title="title"
            item-value="value"
            :label="t('odakEgitim.trainings.yearFilter')"
            density="compact"
            hide-details
            style="min-width: 140px; max-width: 180px"
          />
        </div>

        <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-4" closable @click:close="errorMessage = ''">
          {{ errorMessage }}
        </v-alert>

        <v-data-table
          :headers="headers"
          :items="tableItems"
          :loading="loading"
          item-value="__dataId"
          density="comfortable"
          class="rounded-lg border"
          hide-default-footer
        >
          <template #item.egitimNo="{ item }">
            <a href="#" class="text-primary text-decoration-none" @click.prevent="goDetail(item.raw)">
              {{ item.egitimNo }}
            </a>
          </template>
          <template #item.baslik="{ item }">
            <a href="#" class="text-primary text-decoration-none" @click.prevent="goDetail(item.raw)">
              {{ item.baslik }}
            </a>
          </template>
          <template #item.katilimciSayisi="{ item }">
            <v-chip size="small" variant="tonal" color="primary" @click="goDetail(item.raw)">
              <UsersIcon size="14" class="mr-1" />
              {{ item.katilimciSayisi }}
            </v-chip>
          </template>
          <template #item.actions="{ item }">
            <div class="d-flex justify-end ga-1">
              <v-btn icon size="small" variant="text" @click="goDetail(item.raw)">
                <EyeIcon size="18" />
              </v-btn>
              <v-btn icon size="small" variant="text" @click="openDialog('edit', item.raw)">
                <EditIcon size="18" />
              </v-btn>
              <v-btn icon size="small" variant="text" color="error" @click="confirmDelete(item.raw)">
                <TrashIcon size="18" />
              </v-btn>
            </div>
          </template>
          <template #bottom>
            <div class="d-flex flex-wrap align-center justify-space-between px-4 py-3 gap-3">
              <span class="text-body-2 text-medium-emphasis">{{ paginationLabel }}</span>
              <div class="d-flex align-center gap-3">
                <v-select
                  v-model="tableItemsPerPage"
                  :items="[10, 20, 50, 100]"
                  density="compact"
                  hide-details
                  style="max-width: 90px"
                />
                <v-pagination v-model="tablePage" :length="Math.max(1, Math.ceil(totalCount / tableItemsPerPage))" density="comfortable" />
              </div>
            </div>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <OdakEgitimDialog
      v-model="dialogOpen"
      :mode="dialogMode"
      :seed="dialogSeed"
      :divisions="divisions"
      :saving="saving"
      @save="onDialogSave"
    />

    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="lg">
        <v-card-title>{{ t('odakEgitim.trainings.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('odakEgitim.trainings.deleteConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">{{ t('odakEgitim.common.cancel') }}</v-btn>
          <v-btn color="error" :loading="deleting" @click="executeDelete">{{ t('odakEgitim.common.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.border {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
