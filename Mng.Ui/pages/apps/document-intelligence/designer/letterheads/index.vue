<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DiLetterheadCatalogForm from '@/components/apps/document-intelligence/DiLetterheadCatalogForm.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  diCreateLetterhead,
  diCreateDefaultLetterheadSettings,
  diDeleteLetterhead,
  diListLetterheads,
  diUpdateLetterhead,
} from '@/services/documentIntelligenceService';
import type { DiLetterhead, DiLetterheadSettings, DiTemplateLetterhead } from '@/types/apps/documentIntelligence';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const breadcrumbs = computed(() => [
  { title: t('documentIntelligence.menuTitle'), to: '/apps/document-intelligence' },
  { title: t('documentIntelligence.designer.title'), to: '/apps/document-intelligence/designer' },
  { title: t('documentIntelligence.letterheads.title'), disabled: true },
]);

const tableHeaders = computed(() => [
  { title: t('documentIntelligence.letterheads.name'), key: 'name', sortable: true },
  { title: t('documentIntelligence.letterheads.code'), key: 'code', sortable: true },
  { title: t('documentIntelligence.letterheads.isDefault'), key: 'isDefault', sortable: true, width: '120px' },
  { title: t('documentIntelligence.letterheads.isActive'), key: 'isActive', sortable: true, width: '120px' },
  { title: t('documentIntelligence.letterheads.designAction'), key: 'hasDesign', sortable: true, width: '120px' },
  { title: t('documentIntelligence.designer.columnActions'), key: 'actions', sortable: false, align: 'end' as const, width: '160px' },
]);

function createDefaultLetterheadConfig(): DiTemplateLetterhead {
  return {
    enabled: true,
    showLogo: true,
    showDocumentName: true,
    showDocumentNumber: true,
    showGeneratedAt: true,
  };
}

function createDefaultSettings(): DiLetterheadSettings {
  return diCreateDefaultLetterheadSettings();
}

const loading = ref(false);
const letterheads = ref<DiLetterhead[]>([]);
const error = ref<string | null>(null);
const notify = ref<string | null>(null);

const editDialog = ref(false);
const createDialog = ref(false);
const deleteDialog = ref(false);
const saving = ref(false);
const deleting = ref(false);
const selected = ref<DiLetterhead | null>(null);

const formName = ref('');
const formCode = ref('');
const formDescription = ref('');
const formIsDefault = ref(false);
const formIsActive = ref(true);
const formLetterhead = ref<DiTemplateLetterhead>(createDefaultLetterheadConfig());
const formSettings = ref<DiLetterheadSettings>(createDefaultSettings());

async function loadLetterheads() {
  loading.value = true;
  error.value = null;
  try {
    const res = await diListLetterheads();
    letterheads.value = res.items;
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.letterheads.errors.list');
    letterheads.value = [];
  } finally {
    loading.value = false;
  }
}

function resetForm() {
  formName.value = '';
  formCode.value = '';
  formDescription.value = '';
  formIsDefault.value = false;
  formIsActive.value = true;
  formLetterhead.value = createDefaultLetterheadConfig();
  formSettings.value = createDefaultSettings();
}

function openCreateDialog() {
  selected.value = null;
  resetForm();
  createDialog.value = true;
}

function openEditDialog(item: DiLetterhead) {
  selected.value = item;
  formName.value = item.name;
  formCode.value = item.code;
  formDescription.value = item.description ?? '';
  formIsDefault.value = item.isDefault;
  formIsActive.value = item.isActive;
  formLetterhead.value = { ...item.letterhead };
  formSettings.value = JSON.parse(JSON.stringify(item.settings)) as DiLetterheadSettings;
  editDialog.value = true;
}

function openDeleteDialog(item: DiLetterhead) {
  selected.value = item;
  deleteDialog.value = true;
}

function buildRequestBody() {
  return {
    name: formName.value.trim(),
    code: formCode.value.trim(),
    description: formDescription.value.trim() || null,
    isDefault: formIsDefault.value,
    isActive: formIsActive.value,
    letterhead: {
      ...formLetterhead.value,
      showDocumentName: true,
      showDocumentNumber: true,
      showGeneratedAt: true,
    },
    settings: formSettings.value,
  };
}

async function submitCreate() {
  const body = buildRequestBody();
  if (!body.name || !body.code) return;

  saving.value = true;
  error.value = null;
  notify.value = null;
  try {
    await diCreateLetterhead(body);
    createDialog.value = false;
    await loadLetterheads();
    notify.value = t('documentIntelligence.letterheads.created');
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.letterheads.errors.create');
  } finally {
    saving.value = false;
  }
}

async function submitEdit() {
  const item = selected.value;
  const body = buildRequestBody();
  if (!item || !body.name || !body.code) return;

  saving.value = true;
  error.value = null;
  notify.value = null;
  try {
    await diUpdateLetterhead(item.id, body);
    editDialog.value = false;
    selected.value = null;
    await loadLetterheads();
    notify.value = t('documentIntelligence.letterheads.updated');
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.letterheads.errors.update');
  } finally {
    saving.value = false;
  }
}

async function submitDelete() {
  const item = selected.value;
  if (!item) return;

  deleting.value = true;
  error.value = null;
  notify.value = null;
  try {
    await diDeleteLetterhead(item.id);
    deleteDialog.value = false;
    selected.value = null;
    await loadLetterheads();
    notify.value = t('documentIntelligence.letterheads.deleted');
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.letterheads.errors.delete');
  } finally {
    deleting.value = false;
  }
}

onMounted(() => {
  void loadLetterheads();
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="t('documentIntelligence.letterheads.title')" :breadcrumbs="breadcrumbs" />

    <div class="d-flex align-center justify-space-between mb-4 ga-2 flex-wrap">
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('documentIntelligence.letterheads.subtitle') }}
      </p>
      <div class="d-flex ga-2">
        <v-btn variant="text" class="text-none" to="/apps/document-intelligence/designer">
          {{ t('documentIntelligence.letterheads.backToDesigner') }}
        </v-btn>
        <v-btn color="primary" variant="flat" class="text-none" prepend-icon="mdi-plus" @click="openCreateDialog">
          {{ t('documentIntelligence.letterheads.new') }}
        </v-btn>
      </div>
    </div>

    <v-alert v-if="error" type="error" variant="tonal" closable class="mb-3 rounded-lg" @click:close="error = null">
      {{ error }}
    </v-alert>
    <v-alert v-if="notify" type="success" variant="tonal" closable class="mb-3 rounded-lg" @click:close="notify = null">
      {{ notify }}
    </v-alert>

    <v-card elevation="10" rounded="lg" class="pa-4">
      <v-data-table
        :headers="tableHeaders"
        :items="letterheads"
        :loading="loading"
        item-value="id"
        density="comfortable"
        class="rounded-lg border"
        :no-data-text="t('documentIntelligence.letterheads.empty')"
      >
        <template #item.isDefault="{ item }">
          <v-chip v-if="item.isDefault" size="small" color="primary" variant="tonal" label>
            {{ t('documentIntelligence.letterheads.defaultBadge') }}
          </v-chip>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <template #item.isActive="{ item }">
          <v-chip
            size="small"
            variant="tonal"
            label
            :color="item.isActive ? 'success' : 'warning'"
          >
            {{
              item.isActive
                ? t('documentIntelligence.letterheads.activeBadge')
                : t('documentIntelligence.letterheads.inactiveBadge')
            }}
          </v-chip>
        </template>

        <template #item.hasDesign="{ item }">
          <v-chip
            v-if="item.hasDesign"
            size="small"
            color="info"
            variant="tonal"
            label
          >
            {{ t('documentIntelligence.letterheads.designBadge') }}
          </v-chip>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <template #item.actions="{ item }">
          <v-btn
            icon
            size="small"
            variant="text"
            color="primary"
            :title="t('documentIntelligence.letterheads.designAction')"
            @click="navigateTo(`/apps/document-intelligence/designer/letterheads/${item.id}/edit`)"
          >
            <v-icon icon="mdi-draw-pen" />
          </v-btn>
          <v-btn icon size="small" variant="text" @click="openEditDialog(item)">
            <v-icon icon="mdi-pencil-outline" />
          </v-btn>
          <v-btn icon size="small" variant="text" color="error" @click="openDeleteDialog(item)">
            <v-icon icon="mdi-delete-outline" />
          </v-btn>
        </template>
      </v-data-table>
    </v-card>

    <v-dialog v-model="createDialog" max-width="640">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.letterheads.new') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <DiLetterheadCatalogForm
            v-model:name="formName"
            v-model:code="formCode"
            v-model:description="formDescription"
            v-model:is-default="formIsDefault"
            v-model:is-active="formIsActive"
            v-model:letterhead="formLetterhead"
            v-model:settings="formSettings"
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="createDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            class="text-none"
            :loading="saving"
            :disabled="!formName.trim() || !formCode.trim()"
            @click="submitCreate"
          >
            {{ t('documentIntelligence.create') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="editDialog" max-width="640">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.letterheads.edit') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <DiLetterheadCatalogForm
            v-model:name="formName"
            v-model:code="formCode"
            v-model:description="formDescription"
            v-model:is-default="formIsDefault"
            v-model:is-active="formIsActive"
            v-model:letterhead="formLetterhead"
            v-model:settings="formSettings"
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="editDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            class="text-none"
            :loading="saving"
            :disabled="!formName.trim() || !formCode.trim()"
            @click="submitEdit"
          >
            {{ t('documentIntelligence.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.delete') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4 text-body-2">
          {{
            t('documentIntelligence.letterheads.deleteConfirm', {
              name: selected?.name ?? '',
            })
          }}
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn color="error" variant="flat" class="text-none" :loading="deleting" @click="submitDelete">
            {{ t('documentIntelligence.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.border {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}
</style>
