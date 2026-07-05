<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import OdakEgitimDivisionDialog from '@/components/apps/odak-egitim/OdakEgitimDivisionDialog.vue';
import OdakEgitimSubNav from '@/components/apps/odak-egitim/OdakEgitimSubNav.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakDivisionRow } from '@/utils/odakEgitimConfig';
import {
  createOdakDivision,
  deleteOdakDivision,
  divisionDataId,
  fetchOdakDivisionsPage,
  updateOdakDivision,
  type OdakDivisionAktifTab,
  type OdakDivisionDialogMode,
  type OdakDivisionFormModel,
} from '@/utils/odakEgitimService';
import { EditIcon, PlusIcon, RefreshIcon, TrashIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();

const aktifTab = ref<OdakDivisionAktifTab>('active');
const searchQuery = ref('');
const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const items = ref<OdakDivisionRow[]>([]);

const dialogOpen = ref(false);
const dialogMode = ref<OdakDivisionDialogMode>('create');
const dialogSeed = ref<OdakDivisionRow | null>(null);

const deleteDialog = ref(false);
const rowToDelete = ref<OdakDivisionRow | null>(null);
const deleting = ref(false);

const page = computed(() => ({ title: t('odakEgitim.divisions.title') }));
const breadcrumbs = computed(() => [
  { text: t('breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('odakSiparis.module'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('menu.odakSiparis.egitim.menuTitle'), disabled: false, href: '/apps/odak-egitim/trainings' },
  { text: t('odakEgitim.divisions.title'), disabled: true, href: '#' },
]);

const aktifTabs = computed(() => [
  { value: 'active' as const, label: t('odakEgitim.divisions.tabs.active') },
  { value: 'inactive' as const, label: t('odakEgitim.divisions.tabs.inactive') },
  { value: 'all' as const, label: t('odakEgitim.divisions.tabs.all') },
]);

const headers = computed(() => [
  { title: t('odakEgitim.divisions.fields.kod'), key: 'kod', width: 120 },
  { title: t('odakEgitim.divisions.fields.ad'), key: 'ad', minWidth: 200 },
  { title: t('odakEgitim.divisions.fields.aktif'), key: 'aktifLabel', width: 100 },
  { title: t('odakEgitim.divisions.fields.legacyDivisionId'), key: 'legacyDivisionId', width: 120 },
  {
    title: t('odakEgitim.common.actions'),
    key: 'actions',
    width: 120,
    sortable: false,
    align: 'end' as const,
  },
]);

const tableItems = computed(() =>
  items.value.map((row) => ({
    raw: row,
    __dataId: divisionDataId(row),
    kod: row.kod || '—',
    ad: row.ad || '—',
    aktifLabel: row.aktif !== false ? t('odakEgitim.common.yes') : t('odakEgitim.common.no'),
    legacyDivisionId: row.legacyDivisionId || '—',
  }))
);

async function loadItems() {
  loading.value = true;
  errorMessage.value = '';
  try {
    const resp = await fetchOdakDivisionsPage({
      aktifTab: aktifTab.value,
      search: searchQuery.value.trim(),
    });
    items.value = resp.items;
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    items.value = [];
  } finally {
    loading.value = false;
  }
}

function openDialog(mode: OdakDivisionDialogMode, row?: OdakDivisionRow) {
  dialogMode.value = mode;
  dialogSeed.value = row ?? null;
  dialogOpen.value = true;
}

async function onDialogSave(form: OdakDivisionFormModel) {
  saving.value = true;
  errorMessage.value = '';
  try {
    if (dialogMode.value === 'create') {
      await createOdakDivision(form);
    } else {
      const id = divisionDataId(dialogSeed.value);
      if (!id) throw new Error(t('odakEgitim.divisions.dialog.missingId'));
      await updateOdakDivision(id, form);
    }
    dialogOpen.value = false;
    await loadItems();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

function confirmDelete(row: OdakDivisionRow) {
  rowToDelete.value = row;
  deleteDialog.value = true;
}

async function executeDelete() {
  const row = rowToDelete.value;
  if (!row) return;
  const id = divisionDataId(row);
  if (!id) return;
  deleting.value = true;
  errorMessage.value = '';
  try {
    await deleteOdakDivision(id);
    deleteDialog.value = false;
    rowToDelete.value = null;
    await loadItems();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    deleting.value = false;
  }
}

watch(aktifTab, () => void loadItems());

onMounted(() => void loadItems());
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
    <OdakEgitimSubNav />

    <v-card rounded="lg">
      <v-card-title class="d-flex flex-wrap align-center justify-space-between gap-3 py-4 px-6">
        <div>
          <div class="text-h6">{{ t('odakEgitim.divisions.panelTitle') }}</div>
          <div class="text-body-2 text-medium-emphasis">{{ t('odakEgitim.divisions.panelSubtitle') }}</div>
        </div>
        <div class="d-flex gap-2">
          <v-btn color="primary" @click="openDialog('create')">
            <PlusIcon size="18" class="mr-1" />
            {{ t('odakEgitim.divisions.add') }}
          </v-btn>
          <v-btn variant="tonal" :loading="loading" @click="loadItems">
            <RefreshIcon size="18" />
          </v-btn>
        </div>
      </v-card-title>

      <v-card-text class="px-6 pb-2">
        <v-text-field
          v-model="searchQuery"
          :label="t('odakEgitim.divisions.search')"
          density="comfortable"
          hide-details
          clearable
          @keyup.enter="loadItems"
          @click:clear="loadItems"
        />
      </v-card-text>

      <v-card-text class="px-6 pt-0">
        <v-tabs v-model="aktifTab" density="comfortable" color="primary" class="mb-4">
          <v-tab v-for="tab in aktifTabs" :key="tab.value" :value="tab.value">{{ tab.label }}</v-tab>
        </v-tabs>

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
          :items-per-page="-1"
        >
          <template #item.actions="{ item }">
            <div class="d-flex justify-end ga-1">
              <v-btn icon size="small" variant="text" @click="openDialog('edit', item.raw)">
                <EditIcon size="18" />
              </v-btn>
              <v-btn icon size="small" variant="text" color="error" @click="confirmDelete(item.raw)">
                <TrashIcon size="18" />
              </v-btn>
            </div>
          </template>
          <template #no-data>
            <div class="text-center py-8 text-medium-emphasis">{{ t('odakEgitim.divisions.empty') }}</div>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <OdakEgitimDivisionDialog
      v-model="dialogOpen"
      :mode="dialogMode"
      :seed="dialogSeed"
      :saving="saving"
      @save="onDialogSave"
    />

    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="lg">
        <v-card-title>{{ t('odakEgitim.divisions.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('odakEgitim.divisions.deleteConfirm') }}</v-card-text>
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
