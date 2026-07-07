<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { diCreateTag, diDeleteTag, diListTags, diUpdateTag } from '@/services/documentIntelligenceService';
import type { DiTag } from '@/types/apps/documentIntelligence';
import { TM_STATUS_THEME_COLORS, isTmStatusThemeColor } from '@/utils/taskManagerStatusColor';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const breadcrumbs = computed(() => [
  { title: t('documentIntelligence.menuTitle'), to: '/apps/document-intelligence' },
  { title: t('documentIntelligence.designer.title'), to: '/apps/document-intelligence/designer' },
  { title: t('documentIntelligence.tagsCatalog.title'), disabled: true },
]);

const tableHeaders = computed(() => [
  { title: t('documentIntelligence.tagsCatalog.name'), key: 'name', sortable: true },
  { title: t('documentIntelligence.tagsCatalog.color'), key: 'color', sortable: false, width: '120px' },
  { title: t('documentIntelligence.tagsCatalog.isActive'), key: 'isActive', sortable: true, width: '120px' },
  { title: t('documentIntelligence.designer.columnActions'), key: 'actions', sortable: false, align: 'end' as const, width: '140px' },
]);

const colorItems = computed(() => [
  { title: t('documentIntelligence.tagsCatalog.noColor'), value: null as string | null },
  ...TM_STATUS_THEME_COLORS.map((c) => ({ title: c, value: c as string | null })),
]);

const loading = ref(false);
const tags = ref<DiTag[]>([]);
const error = ref<string | null>(null);
const notify = ref<string | null>(null);

const dialogOpen = ref(false);
const deleteDialogOpen = ref(false);
const saving = ref(false);
const deleting = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<DiTag | null>(null);

const form = ref<{ name: string; color: string | null; description: string; isActive: boolean }>({
  name: '',
  color: null,
  description: '',
  isActive: true,
});

async function loadTags() {
  loading.value = true;
  error.value = null;
  try {
    const res = await diListTags(false);
    tags.value = res.items;
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.tagsCatalog.errors.list');
    tags.value = [];
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editingId.value = null;
  form.value = { name: '', color: null, description: '', isActive: true };
  dialogOpen.value = true;
}

function openEdit(tag: DiTag) {
  editingId.value = tag.id;
  form.value = {
    name: tag.name,
    color: tag.color && isTmStatusThemeColor(tag.color) ? tag.color : null,
    description: tag.description ?? '',
    isActive: tag.isActive,
  };
  dialogOpen.value = true;
}

function openDelete(tag: DiTag) {
  deleteTarget.value = tag;
  deleteDialogOpen.value = true;
}

async function save() {
  const name = form.value.name.trim();
  if (!name) return;
  saving.value = true;
  error.value = null;
  notify.value = null;
  try {
    const payload = {
      name,
      color: form.value.color,
      description: form.value.description.trim() || null,
      isActive: form.value.isActive,
    };
    if (editingId.value) {
      await diUpdateTag(editingId.value, payload);
      notify.value = t('documentIntelligence.tagsCatalog.updated');
    } else {
      await diCreateTag(payload);
      notify.value = t('documentIntelligence.tagsCatalog.created');
    }
    dialogOpen.value = false;
    await loadTags();
  } catch (e: unknown) {
    error.value = panelError(e, editingId.value
      ? 'documentIntelligence.tagsCatalog.errors.update'
      : 'documentIntelligence.tagsCatalog.errors.create');
  } finally {
    saving.value = false;
  }
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  error.value = null;
  notify.value = null;
  try {
    await diDeleteTag(deleteTarget.value.id);
    deleteDialogOpen.value = false;
    notify.value = t('documentIntelligence.tagsCatalog.deleted');
    await loadTags();
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.tagsCatalog.errors.delete');
  } finally {
    deleting.value = false;
  }
}

onMounted(() => void loadTags());
</script>

<template>
  <div>
    <BaseBreadcrumb :title="t('documentIntelligence.tagsCatalog.title')" :breadcrumbs="breadcrumbs" />

    <v-card elevation="10" rounded="lg" class="pa-4">
      <div class="d-flex align-center flex-wrap ga-2 mb-4">
        <div>
          <h3 class="text-h6 font-weight-bold">{{ t('documentIntelligence.tagsCatalog.title') }}</h3>
          <p class="text-body-2 text-medium-emphasis mb-0">
            {{ t('documentIntelligence.tagsCatalog.subtitle') }}
          </p>
        </div>
        <v-spacer />
        <v-btn variant="text" class="text-none" to="/apps/document-intelligence/designer">
          {{ t('documentIntelligence.tagsCatalog.backToDesigner') }}
        </v-btn>
        <v-btn color="primary" variant="flat" class="text-none" prepend-icon="mdi-plus" @click="openCreate">
          {{ t('documentIntelligence.tagsCatalog.new') }}
        </v-btn>
      </div>

      <v-alert v-if="error" type="error" variant="tonal" class="mb-3">{{ error }}</v-alert>
      <v-alert v-if="notify" type="success" variant="tonal" class="mb-3">{{ notify }}</v-alert>

      <v-data-table
        :headers="tableHeaders"
        :items="tags"
        :loading="loading"
        item-key="id"
        density="comfortable"
        :no-data-text="t('documentIntelligence.tagsCatalog.empty')"
      >
        <template #item.color="{ item }">
          <v-chip v-if="item.color" size="small" variant="flat" :color="item.color">{{ item.color }}</v-chip>
          <span v-else class="text-medium-emphasis">—</span>
        </template>
        <template #item.isActive="{ item }">
          <v-chip size="small" variant="tonal" :color="item.isActive ? 'success' : 'warning'">
            {{ item.isActive ? t('documentIntelligence.tagsCatalog.active') : t('documentIntelligence.tagsCatalog.inactive') }}
          </v-chip>
        </template>
        <template #item.actions="{ item }">
          <v-btn icon size="small" variant="text" @click="openEdit(item)">
            <v-icon size="18">mdi-pencil-outline</v-icon>
          </v-btn>
          <v-btn icon size="small" variant="text" color="error" @click="openDelete(item)">
            <v-icon size="18">mdi-delete-outline</v-icon>
          </v-btn>
        </template>
      </v-data-table>
    </v-card>

    <v-dialog v-model="dialogOpen" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">
          {{ editingId ? t('documentIntelligence.tagsCatalog.edit') : t('documentIntelligence.tagsCatalog.new') }}
        </v-card-title>
        <v-card-text>
          <v-text-field v-model="form.name" :label="t('documentIntelligence.tagsCatalog.name')" variant="outlined" density="comfortable" class="mb-3" />
          <v-select
            v-model="form.color"
            :items="colorItems"
            item-title="title"
            item-value="value"
            :label="t('documentIntelligence.tagsCatalog.color')"
            variant="outlined"
            density="comfortable"
            clearable
            class="mb-3"
          />
          <v-textarea v-model="form.description" :label="t('documentIntelligence.tagsCatalog.description')" variant="outlined" density="comfortable" rows="2" class="mb-3" />
          <v-switch v-model="form.isActive" :label="t('documentIntelligence.tagsCatalog.isActive')" color="primary" hide-details />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="dialogOpen = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn color="primary" variant="flat" class="text-none" :loading="saving" :disabled="!form.name.trim()" @click="save">
            {{ t('documentIntelligence.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteDialogOpen" max-width="420">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ t('documentIntelligence.delete') }}</v-card-title>
        <v-card-text>
          {{ t('documentIntelligence.tagsCatalog.deleteConfirm', { name: deleteTarget?.name ?? '' }) }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteDialogOpen = false">{{ t('documentIntelligence.cancel') }}</v-btn>
          <v-btn color="error" variant="flat" class="text-none" :loading="deleting" @click="confirmDelete">
            {{ t('documentIntelligence.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
