<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import OdakEgitimDialog from '@/components/apps/odak-egitim/OdakEgitimDialog.vue';
import OdakEgitimSubNav from '@/components/apps/odak-egitim/OdakEgitimSubNav.vue';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useKeeperUserPicker } from '@/composables/useKeeperUserPicker';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakDivisionRow, OdakTrainingParticipationRow, OdakTrainingRow } from '@/utils/odakEgitimConfig';
import {
  addParticipation,
  fetchDivisionLabelMap,
  fetchOdakDivisions,
  fetchOdakTrainingById,
  fetchParticipationsForTraining,
  formatOdakTrainingDate,
  formatOdakTrainingDuration,
  formatOdakTrainingHours,
  participationDataId,
  participationPersonId,
  relationIdFromRow,
  removeParticipation,
  trainingDataId,
  trainingDisplayNo,
  trainingStatusLabel,
  trainingStatusFromRow,
  trainingTotalAttendedPersonHours,
  updateOdakTraining,
  updateParticipation,
  type OdakTrainingFormModel,
} from '@/utils/odakEgitimService';
import { fetchPersonLabelMap, personLabelFromRow } from '@/utils/odakSiparisPackagePersonnel';
import { EditIcon, PlusIcon, RefreshIcon, TrashIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const route = useRoute();
const router = useRouter();
const personPicker = useKeeperUserPicker();

const trainingId = computed(() => String(route.params.id ?? '').trim());
const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const training = ref<OdakTrainingRow | null>(null);
const participations = ref<OdakTrainingParticipationRow[]>([]);
const personLabels = ref<Record<string, string>>({});
const divisionLabel = ref('—');
const divisions = ref<OdakDivisionRow[]>([]);

const dialogOpen = ref(false);
const addPersonOpen = ref(false);
const selectedPersonId = ref<string | null>(null);
const addingPerson = ref(false);

const page = computed(() => ({ title: trainingDisplayNo(training.value ?? {}) }));
const breadcrumbs = computed(() => [
  { text: t('breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('odakSiparis.module'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('menu.odakSiparis.egitim.menuTitle'), disabled: false, href: '/apps/odak-egitim/trainings' },
  { text: t('odakEgitim.trainings.title'), disabled: false, href: '/apps/odak-egitim/trainings' },
  { text: trainingDisplayNo(training.value ?? {}), disabled: true, href: '#' },
]);

const summaryRows = computed(() => {
  const row = training.value;
  if (!row) return [];
  const attendedCount = participations.value.filter((p) => p.katildi !== false).length;
  const totalPersonHours = trainingTotalAttendedPersonHours(row, attendedCount);
  return [
    { label: t('odakEgitim.trainings.fields.konu'), value: row.konu || row.baslik || '—' },
    { label: t('odakEgitim.trainings.fields.birim'), value: divisionLabel.value },
    { label: t('odakEgitim.trainings.fields.egitimVeren'), value: row.egitimVeren || '—' },
    { label: t('odakEgitim.trainings.fields.konum'), value: row.konum || '—' },
    { label: t('odakEgitim.trainings.fields.planlananTarih'), value: formatOdakTrainingDate(row.planlananTarih) },
    { label: t('odakEgitim.trainings.fields.gerceklesenTarih'), value: formatOdakTrainingDate(row.gerceklesenTarih) },
    { label: t('odakEgitim.trainings.fields.egitimAmaci'), value: row.egitimAmaci || '—' },
    { label: t('odakEgitim.trainings.fields.degerlendirmeYontemi'), value: row.degerlendirmeYontemi || '—' },
    {
      label: t('odakEgitim.trainings.fields.sureDakika'),
      value: row.sureDakika != null ? `${row.sureDakika} (${formatOdakTrainingDuration(row)} saat)` : '—',
    },
    { label: t('odakEgitim.trainings.fields.toplamCalisanSayisi'), value: row.toplamCalisanSayisi ?? '—' },
    {
      label: t('odakEgitim.trainings.fields.toplamHarcananSure'),
      value: t('odakEgitim.trainings.totalPersonHoursValue', {
        hours: formatOdakTrainingHours(totalPersonHours),
        count: attendedCount,
      }),
    },
    { label: t('odakEgitim.trainings.fields.durum'), value: trainingStatusLabel(trainingStatusFromRow(row)) },
  ];
});

const participantHeaders = computed(() => [
  { title: t('odakEgitim.participants.columns.name'), key: 'name' },
  { title: t('odakEgitim.participants.columns.katildi'), key: 'katildi', width: 100 },
  { title: t('odakEgitim.participants.columns.etkin'), key: 'etkin', width: 100 },
  { title: t('odakEgitim.common.actions'), key: 'actions', width: 120, align: 'end' as const },
]);

const participantItems = computed(() =>
  participations.value.map((p) => {
    const pid = participationPersonId(p);
    const name =
      personLabels.value[pid] ||
      personLabelFromRow(p.personelId, personLabels.value) ||
      pid ||
      '—';
    return {
      raw: p,
      id: participationDataId(p),
      personId: pid,
      name,
      katildi: p.katildi !== false,
      etkin: p.etkin,
    };
  })
);

const participantCountLabel = computed(() =>
  t('odakEgitim.participants.titleWithCount', { count: participations.value.length })
);

const showLegacyEmptyHint = computed(() => {
  if (participations.value.length > 0 || !training.value) return false;
  const no = training.value.egitimNo ?? '';
  const m = no.match(/^EGTM(\d{4})\//);
  if (!m) return false;
  return parseInt(m[1], 10) >= 2023;
});

async function loadTraining() {
  const id = trainingId.value;
  if (!id) return;
  loading.value = true;
  errorMessage.value = '';
  try {
    training.value = await fetchOdakTrainingById(id);
    if (!training.value) {
      errorMessage.value = t('odakEgitim.trainings.notFound');
      return;
    }
    const birimId = relationIdFromRow(training.value.birimId);
    if (birimId) {
      const map = await fetchDivisionLabelMap([birimId]);
      divisionLabel.value = map[birimId] ?? birimId;
    } else {
      divisionLabel.value = '—';
    }
    await loadParticipations();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

async function loadParticipations() {
  const id = trainingId.value;
  if (!id) return;
  participations.value = await fetchParticipationsForTraining(id);
  const ids = participations.value.map((p) => participationPersonId(p)).filter(Boolean);
  personLabels.value = await fetchPersonLabelMap(ids);
}

async function onDialogSave(form: OdakTrainingFormModel) {
  const id = trainingId.value;
  if (!id) return;
  saving.value = true;
  errorMessage.value = '';
  try {
    await updateOdakTraining(id, form);
    dialogOpen.value = false;
    await loadTraining();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

async function toggleKatildi(row: OdakTrainingParticipationRow) {
  const id = participationDataId(row);
  if (!id) return;
  await updateParticipation(id, { katildi: row.katildi === false });
  await loadParticipations();
}

async function toggleEtkin(row: OdakTrainingParticipationRow) {
  const id = participationDataId(row);
  if (!id) return;
  await updateParticipation(id, { etkin: row.etkin !== true });
  await loadParticipations();
}

async function removeParticipant(row: OdakTrainingParticipationRow) {
  const id = participationDataId(row);
  if (!id) return;
  await removeParticipation(id);
  await loadParticipations();
}

async function submitAddPerson() {
  const pid = selectedPersonId.value?.trim();
  const tid = trainingId.value;
  if (!pid || !tid) return;
  if (participations.value.some((p) => participationPersonId(p) === pid)) {
    errorMessage.value = t('odakEgitim.participants.alreadyAdded');
    return;
  }
  addingPerson.value = true;
  errorMessage.value = '';
  try {
    await addParticipation(tid, pid, true);
    addPersonOpen.value = false;
    selectedPersonId.value = null;
    await loadParticipations();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    addingPerson.value = false;
  }
}

function goPersonTrainings(personId: string, personName?: string) {
  if (!personId) return;
  const label = personName?.trim() || personLabels.value[personId]?.trim();
  void router.push({
    path: '/apps/odak-egitim/person-trainings',
    query: {
      personId,
      ...(label && label !== personId ? { personName: label } : {}),
    },
  });
}

function openAddPerson() {
  selectedPersonId.value = null;
  addPersonOpen.value = true;
}

onMounted(async () => {
  divisions.value = await fetchOdakDivisions(false);
  await loadTraining();
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
    <OdakEgitimSubNav />

    <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-4" closable @click:close="errorMessage = ''">
      {{ errorMessage }}
    </v-alert>

    <v-card v-if="training" rounded="lg" class="mb-4">
      <v-card-title class="d-flex flex-wrap align-center justify-space-between gap-3 py-4 px-6">
        <div>
          <div class="text-overline text-medium-emphasis">F39 — {{ t('odakEgitim.trainings.detailTitle') }}</div>
          <div class="text-h6">{{ trainingDisplayNo(training) }} · {{ training.baslik }}</div>
        </div>
        <div class="d-flex gap-2">
          <v-btn variant="tonal" :loading="loading" @click="loadTraining">
            <RefreshIcon size="18" />
          </v-btn>
          <v-btn color="primary" variant="tonal" @click="dialogOpen = true">
            <EditIcon size="18" class="mr-1" />
            {{ t('odakEgitim.common.edit') }}
          </v-btn>
        </div>
      </v-card-title>
      <v-divider />
      <v-card-text class="pa-0">
        <v-table density="comfortable">
          <tbody>
            <tr v-for="row in summaryRows" :key="row.label">
              <th class="text-right font-weight-medium" style="width: 28%; white-space: nowrap">{{ row.label }}</th>
              <td>{{ row.value }}</td>
            </tr>
          </tbody>
        </v-table>
      </v-card-text>
    </v-card>

    <v-card v-if="training" rounded="lg">
      <v-card-title class="d-flex flex-wrap align-center justify-space-between gap-3 py-4 px-6">
        <span>{{ participantCountLabel }}</span>
        <v-btn color="primary" size="small" @click="openAddPerson">
          <PlusIcon size="16" class="mr-1" />
          {{ t('odakEgitim.participants.add') }}
        </v-btn>
      </v-card-title>
      <v-divider />
      <v-alert
        v-if="showLegacyEmptyHint"
        type="info"
        variant="tonal"
        density="compact"
        class="ma-4 mb-0"
      >
        {{ t('odakEgitim.participants.legacyEmptyHint') }}
      </v-alert>
      <v-data-table
        :headers="participantHeaders"
        :items="participantItems"
        item-value="id"
        density="comfortable"
        hide-default-footer
        :items-per-page="-1"
      >
        <template #item.name="{ item }">
          <a
            v-if="item.personId"
            href="#"
            class="text-primary text-decoration-none"
            @click.prevent="goPersonTrainings(item.personId, item.name)"
          >
            {{ item.name }}
          </a>
          <span v-else>{{ item.name }}</span>
        </template>
        <template #item.katildi="{ item }">
          <v-switch
            :model-value="item.katildi"
            density="compact"
            hide-details
            color="primary"
            @update:model-value="toggleKatildi(item.raw)"
          />
        </template>
        <template #item.etkin="{ item }">
          <v-switch
            :model-value="item.etkin === true"
            density="compact"
            hide-details
            color="success"
            @update:model-value="toggleEtkin(item.raw)"
          />
        </template>
        <template #item.actions="{ item }">
          <v-btn icon size="small" variant="text" color="error" @click="removeParticipant(item.raw)">
            <TrashIcon size="18" />
          </v-btn>
        </template>
        <template #no-data>
          <div class="text-center py-8 text-medium-emphasis">{{ t('odakEgitim.participants.empty') }}</div>
        </template>
      </v-data-table>
    </v-card>

    <OdakEgitimDialog
      v-model="dialogOpen"
      mode="edit"
      :seed="training"
      :divisions="divisions"
      :saving="saving"
      @save="onDialogSave"
    />

    <v-dialog v-model="addPersonOpen" max-width="560">
      <v-card rounded="lg">
        <v-card-title>{{ t('odakEgitim.participants.addTitle') }}</v-card-title>
        <v-card-text>
          <MngDirectoryPickerField
            v-model="selectedPersonId"
            entity="user"
            :external-picker="personPicker"
            :label="t('odakEgitim.participants.selectPerson')"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="addPersonOpen = false">{{ t('odakEgitim.common.cancel') }}</v-btn>
          <v-btn color="primary" :loading="addingPerson" :disabled="!selectedPersonId" @click="submitAddPerson">
            {{ t('odakEgitim.common.add') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
