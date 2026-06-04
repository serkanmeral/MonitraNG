<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useAppI18n } from '@/composables/useAppI18n';
import type { WorkflowDefinitionSummary } from '@/types/apps/workflowDefinition';
import { workflowDefinitionCreate, workflowDefinitionList } from '@/services/workflowService';

const { t } = useAppI18n();
const router = useRouter();

const loading = ref(true);
const saving = ref(false);
const errorLocal = ref<string | null>(null);
const rows = ref<WorkflowDefinitionSummary[]>([]);
const dialogOpen = ref(false);

const form = ref({
  key: '',
  name: '',
  category: '',
});

const headers = computed(() => [
  { title: t('automationCenter.workflows.colName'), key: 'name', sortable: true },
  { title: t('automationCenter.workflows.colKey'), key: 'key', sortable: true },
  { title: t('automationCenter.workflows.colCategory'), key: 'category', sortable: true },
  { title: t('automationCenter.workflows.colVersion'), key: 'currentVersion', sortable: true },
  { title: t('automationCenter.workflows.colUpdated'), key: 'updatedAt', sortable: true },
  { title: t('automationCenter.workflows.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

function resetForm() {
  form.value = { key: '', name: '', category: '' };
}

function openCreate() {
  resetForm();
  dialogOpen.value = true;
}

async function load() {
  loading.value = true;
  errorLocal.value = null;
  try {
    rows.value = await workflowDefinitionList();
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('automationCenter.workflows.loadError');
  } finally {
    loading.value = false;
  }
}

async function submitCreate() {
  if (!form.value.key.trim() || !form.value.name.trim()) return;
  saving.value = true;
  errorLocal.value = null;
  try {
    const created = await workflowDefinitionCreate({
      key: form.value.key.trim(),
      name: form.value.name.trim(),
      category: form.value.category.trim() || undefined,
    });
    dialogOpen.value = false;
    await router.push(`/apps/automation-center/workflows/${created.id}`);
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('automationCenter.workflows.createError');
  } finally {
    saving.value = false;
  }
}

function openEditor(row: WorkflowDefinitionSummary) {
  void router.push(`/apps/automation-center/workflows/${row.id}`);
}

function formatDate(value?: string) {
  if (!value) return '—';
  try {
    return new Date(value).toLocaleString();
  } catch {
    return value;
  }
}

onMounted(() => {
  void load();
});
</script>

<template>
  <div>
    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>

    <div class="d-flex flex-wrap align-center ga-2 mb-4">
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">
        {{ t('automationCenter.workflows.create') }}
      </v-btn>
      <v-spacer />
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="load">
        {{ t('automationCenter.workflows.refresh') }}
      </v-btn>
    </div>

    <v-data-table
      :headers="headers"
      :items="rows"
      :loading="loading"
      item-value="id"
      class="rounded-lg"
      density="comfortable"
    >
      <template #item.updatedAt="{ item }">
        {{ formatDate(item.updatedAt) }}
      </template>
      <template #item.category="{ item }">
        {{ item.category || '—' }}
      </template>
      <template #item.actions="{ item }">
        <v-btn size="small" variant="text" color="primary" @click="openEditor(item)">
          {{ t('automationCenter.workflows.edit') }}
        </v-btn>
      </template>
      <template #no-data>
        <div class="text-center py-8 text-medium-emphasis">
          {{ t('automationCenter.workflows.empty') }}
        </div>
      </template>
    </v-data-table>

    <v-dialog v-model="dialogOpen" max-width="520" persistent>
      <v-card>
        <v-card-title>{{ t('automationCenter.workflows.createTitle') }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="form.key"
            :label="t('automationCenter.workflows.fieldKey')"
            hint="alarm-cpu-response"
            persistent-hint
            class="mb-3"
          />
          <v-text-field v-model="form.name" :label="t('automationCenter.workflows.fieldName')" class="mb-3" />
          <v-text-field v-model="form.category" :label="t('automationCenter.workflows.fieldCategory')" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="dialogOpen = false">{{ t('automationCenter.workflows.cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" @click="submitCreate">{{ t('automationCenter.workflows.save') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
