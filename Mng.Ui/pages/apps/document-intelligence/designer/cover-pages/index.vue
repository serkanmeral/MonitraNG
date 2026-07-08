<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  diCreateCoverPage,
  diCreateDefaultCoverPageDefinition,
  diCreateDefaultCoverPageSettings,
  diDeleteCoverPage,
  diListCoverPages,
  diUpdateCoverPage,
} from '@/services/documentIntelligenceService';
import type { DiCoverPage, DiCoverPageDefinition } from '@/types/apps/documentIntelligence';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const breadcrumbs = computed(() => [
  { title: t('documentIntelligence.designer.title'), to: '/apps/document-intelligence/designer' },
  { title: t('documentIntelligence.coverPages.title'), disabled: true },
]);

const tableHeaders = computed(() => [
  { title: t('documentIntelligence.coverPages.name'), key: 'name', sortable: true },
  { title: t('documentIntelligence.coverPages.code'), key: 'code', sortable: true },
  { title: t('documentIntelligence.coverPages.isDefault'), key: 'isDefault', sortable: true, width: '120px' },
  { title: t('documentIntelligence.coverPages.isActive'), key: 'isActive', sortable: true, width: '120px' },
  { title: t('documentIntelligence.coverPages.designAction'), key: 'hasDesign', sortable: true, width: '120px' },
  { title: t('documentIntelligence.designer.columnActions'), key: 'actions', sortable: false, align: 'end' as const, width: '160px' },
]);

function createDefaultDefinition(): DiCoverPageDefinition {
  return diCreateDefaultCoverPageDefinition();
}

const loading = ref(false);
const coverPages = ref<DiCoverPage[]>([]);
const error = ref<string | null>(null);
const notify = ref<string | null>(null);

const editDialog = ref(false);
const createDialog = ref(false);
const deleteDialog = ref(false);
const saving = ref(false);
const deleting = ref(false);
const selected = ref<DiCoverPage | null>(null);

const formName = ref('');
const formCode = ref('');
const formDescription = ref('');
const formIsDefault = ref(false);
const formIsActive = ref(true);
const formDefinition = ref<DiCoverPageDefinition>(createDefaultDefinition());

async function loadCoverPages() {
  loading.value = true;
  error.value = null;
  try {
    const res = await diListCoverPages();
    coverPages.value = res.items;
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.coverPages.errors.list');
    coverPages.value = [];
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
  formDefinition.value = createDefaultDefinition();
}

function openCreateDialog() {
  selected.value = null;
  resetForm();
  createDialog.value = true;
}

function openEditDialog(item: DiCoverPage) {
  selected.value = item;
  formName.value = item.name;
  formCode.value = item.code;
  formDescription.value = item.description ?? '';
  formIsDefault.value = item.isDefault;
  formIsActive.value = item.isActive;
  formDefinition.value = { ...item.definition };
  editDialog.value = true;
}

function openDeleteDialog(item: DiCoverPage) {
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
    definition: formDefinition.value,
    settings: diCreateDefaultCoverPageSettings(),
  };
}

async function submitCreate() {
  const body = buildRequestBody();
  if (!body.name || !body.code) return;

  saving.value = true;
  error.value = null;
  notify.value = null;
  try {
    await diCreateCoverPage(body);
    createDialog.value = false;
    await loadCoverPages();
    notify.value = t('documentIntelligence.coverPages.created');
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.coverPages.errors.create');
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
    await diUpdateCoverPage(item.id, body);
    editDialog.value = false;
    selected.value = null;
    await loadCoverPages();
    notify.value = t('documentIntelligence.coverPages.updated');
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.coverPages.errors.update');
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
    await diDeleteCoverPage(item.id);
    deleteDialog.value = false;
    selected.value = null;
    await loadCoverPages();
    notify.value = t('documentIntelligence.coverPages.deleted');
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.coverPages.errors.delete');
  } finally {
    deleting.value = false;
  }
}

onMounted(() => {
  void loadCoverPages();
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="t('documentIntelligence.coverPages.title')" :breadcrumbs="breadcrumbs" />

    <div class="d-flex align-center justify-space-between mb-4 ga-2 flex-wrap">
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('documentIntelligence.coverPages.subtitle') }}
      </p>
      <div class="d-flex ga-2">
        <v-btn variant="text" class="text-none" to="/apps/document-intelligence/designer">
          {{ t('documentIntelligence.coverPages.backToDesigner') }}
        </v-btn>
        <v-btn color="primary" variant="flat" class="text-none" prepend-icon="mdi-plus" @click="openCreateDialog">
          {{ t('documentIntelligence.coverPages.new') }}
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
        :items="coverPages"
        :loading="loading"
        item-value="id"
        density="comfortable"
        class="rounded-lg border"
        :no-data-text="t('documentIntelligence.coverPages.empty')"
      >
        <template #item.isDefault="{ item }">
          <v-chip v-if="item.isDefault" size="small" color="primary" variant="tonal" label>
            {{ t('documentIntelligence.coverPages.defaultBadge') }}
          </v-chip>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <template #item.isActive="{ item }">
          <v-chip size="small" variant="tonal" label :color="item.isActive ? 'success' : 'warning'">
            {{
              item.isActive
                ? t('documentIntelligence.coverPages.activeBadge')
                : t('documentIntelligence.coverPages.inactiveBadge')
            }}
          </v-chip>
        </template>

        <template #item.hasDesign="{ item }">
          <v-chip v-if="item.hasDesign" size="small" color="info" variant="tonal" label>
            {{ t('documentIntelligence.coverPages.designBadge') }}
          </v-chip>
          <span v-else class="text-medium-emphasis">—</span>
        </template>

        <template #item.actions="{ item }">
          <v-btn
            icon
            size="small"
            variant="text"
            color="primary"
            :title="t('documentIntelligence.coverPages.designAction')"
            @click="navigateTo(`/apps/document-intelligence/designer/cover-pages/${item.id}/edit`)"
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
          {{ t('documentIntelligence.coverPages.new') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-text-field v-model="formName" :label="t('documentIntelligence.coverPages.name')" variant="outlined" density="comfortable" class="mb-3" />
          <v-text-field v-model="formCode" :label="t('documentIntelligence.coverPages.code')" variant="outlined" density="comfortable" class="mb-3" />
          <v-textarea v-model="formDescription" :label="t('documentIntelligence.coverPages.description')" variant="outlined" density="comfortable" rows="2" class="mb-3" />
          <v-switch v-model="formIsDefault" :label="t('documentIntelligence.coverPages.isDefault')" color="primary" hide-details class="mb-1" />
          <v-switch v-model="formIsActive" :label="t('documentIntelligence.coverPages.isActive')" color="primary" hide-details class="mb-3" />
          <div class="text-caption font-weight-bold text-medium-emphasis mb-2">
            {{ t('documentIntelligence.coverPages.definitionTitle') }}
          </div>
          <v-switch v-model="formDefinition.showLogo" :label="t('documentIntelligence.coverPages.fieldLogo')" color="primary" hide-details density="compact" />
          <v-switch v-model="formDefinition.showDocumentName" :label="t('documentIntelligence.coverPages.fieldDocumentName')" color="primary" hide-details density="compact" />
          <v-switch v-model="formDefinition.showDocNo" :label="t('documentIntelligence.coverPages.fieldDocNo')" color="primary" hide-details density="compact" />
          <v-switch v-model="formDefinition.showGeneratedAt" :label="t('documentIntelligence.coverPages.fieldGeneratedAt')" color="primary" hide-details density="compact" />
          <v-switch v-model="formDefinition.showCustomerName" :label="t('documentIntelligence.coverPages.fieldCustomerName')" color="primary" hide-details density="compact" />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="createDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn color="primary" variant="flat" class="text-none" :loading="saving" :disabled="!formName.trim() || !formCode.trim()" @click="submitCreate">
            {{ t('documentIntelligence.create') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="editDialog" max-width="640">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.coverPages.edit') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-text-field v-model="formName" :label="t('documentIntelligence.coverPages.name')" variant="outlined" density="comfortable" class="mb-3" />
          <v-text-field v-model="formCode" :label="t('documentIntelligence.coverPages.code')" variant="outlined" density="comfortable" class="mb-3" />
          <v-textarea v-model="formDescription" :label="t('documentIntelligence.coverPages.description')" variant="outlined" density="comfortable" rows="2" class="mb-3" />
          <v-switch v-model="formIsDefault" :label="t('documentIntelligence.coverPages.isDefault')" color="primary" hide-details class="mb-1" />
          <v-switch v-model="formIsActive" :label="t('documentIntelligence.coverPages.isActive')" color="primary" hide-details class="mb-3" />
          <div class="text-caption font-weight-bold text-medium-emphasis mb-2">
            {{ t('documentIntelligence.coverPages.definitionTitle') }}
          </div>
          <v-switch v-model="formDefinition.showLogo" :label="t('documentIntelligence.coverPages.fieldLogo')" color="primary" hide-details density="compact" />
          <v-switch v-model="formDefinition.showDocumentName" :label="t('documentIntelligence.coverPages.fieldDocumentName')" color="primary" hide-details density="compact" />
          <v-switch v-model="formDefinition.showDocNo" :label="t('documentIntelligence.coverPages.fieldDocNo')" color="primary" hide-details density="compact" />
          <v-switch v-model="formDefinition.showGeneratedAt" :label="t('documentIntelligence.coverPages.fieldGeneratedAt')" color="primary" hide-details density="compact" />
          <v-switch v-model="formDefinition.showCustomerName" :label="t('documentIntelligence.coverPages.fieldCustomerName')" color="primary" hide-details density="compact" />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="editDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn color="primary" variant="flat" class="text-none" :loading="saving" :disabled="!formName.trim() || !formCode.trim()" @click="submitEdit">
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
          {{ t('documentIntelligence.coverPages.deleteConfirm', { name: selected?.name ?? '' }) }}
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
