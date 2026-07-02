<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcMailTemplateDialog from '@/components/apps/notifier/OcMailTemplateDialog.vue';
import {
  createMailTemplate,
  deleteMailTemplate,
  listMailTemplatesPage,
  listMailTemplateCategoryOptions,
  MAIL_TEMPLATE_DEFAULT_PAGE_SIZE,
  MAIL_TEMPLATE_PAGE_SIZE_OPTIONS,
  updateMailTemplate,
} from '@/services/notifier/mailTemplates';
import type { MailTemplate } from '@/types/apps/mailTemplates';
import { isSystemMailTemplate } from '@/utils/ocMailTemplates';

const { t } = useAppI18n();

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);
const activeOnly = ref(false);
const categoryFilter = ref<string | null>(null);
const searchQuery = ref('');
const page = ref(1);
const itemsPerPage = ref(MAIL_TEMPLATE_DEFAULT_PAGE_SIZE);
const total = ref(0);
const suppressPageWatch = ref(false);

const templates = ref<MailTemplate[]>([]);
const dialog = ref(false);
const editing = ref<MailTemplate | null>(null);
const deleteDialog = ref(false);
const deleteTarget = ref<MailTemplate | null>(null);
const categoryOptions = ref<string[]>([]);

const categoryItems = computed(() => [
  { value: null, title: t('notifier.mailTemplates.filterCategoryAll') },
  ...categoryOptions.value.map((value) => ({
    value,
    title: categoryFilterLabel(value),
  })),
]);

function categoryFilterLabel(value: string): string {
  if (value === 'system') return t('notifier.mailTemplates.categorySystem');
  if (value === 'custom') return t('notifier.mailTemplates.categoryCustom');
  return value;
}

const skip = computed(() => (page.value - 1) * itemsPerPage.value);

const pageCount = computed(() => Math.max(1, Math.ceil(total.value / itemsPerPage.value)));

const headers = computed(() => [
  { title: t('notifier.mailTemplates.colKey'), key: 'templateKey', sortable: false },
  { title: t('notifier.mailTemplates.colName'), key: 'name', sortable: false },
  { title: t('notifier.mailTemplates.colSubject'), key: 'subject', sortable: false },
  { title: t('notifier.mailTemplates.colCategory'), key: 'category', sortable: false, width: 100 },
  { title: t('notifier.mailTemplates.colStatus'), key: 'isActive', sortable: false, width: 96 },
  { title: t('notifier.mailTemplates.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

async function loadPage() {
  loading.value = true;
  errorLocal.value = null;
  try {
    const result = await listMailTemplatesPage({
      skip: skip.value,
      limit: itemsPerPage.value,
      activeOnly: activeOnly.value,
      category: categoryFilter.value,
      search: searchQuery.value.trim() || undefined,
    });
    templates.value = result.items;
    total.value = result.total;

    if (total.value > 0 && page.value > pageCount.value) {
      suppressPageWatch.value = true;
      page.value = pageCount.value;
      suppressPageWatch.value = false;
      await loadPage();
    }
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('notifier.mailTemplates.loadError');
    templates.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function resetPageAndLoad() {
  page.value = 1;
  void loadPage();
}

let searchTimer: ReturnType<typeof setTimeout> | null = null;
watch(searchQuery, () => {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => resetPageAndLoad(), 350);
});

watch([activeOnly, categoryFilter], () => resetPageAndLoad());

watch([page, itemsPerPage], () => {
  if (suppressPageWatch.value) return;
  void loadPage();
});

onMounted(() => {
  void loadCategoryOptions();
  void loadPage();
});

async function loadCategoryOptions() {
  try {
    categoryOptions.value = await listMailTemplateCategoryOptions();
  } catch {
    categoryOptions.value = ['custom', 'system'];
  }
}

function openCreate() {
  editing.value = null;
  dialog.value = true;
}

function openEdit(row: MailTemplate) {
  editing.value = row;
  dialog.value = true;
}

function confirmDelete(row: MailTemplate) {
  deleteTarget.value = row;
  deleteDialog.value = true;
}

async function saveTemplate(payload: Record<string, unknown>) {
  saving.value = true;
  errorLocal.value = null;
  successLocal.value = null;
  try {
    if (editing.value?.__dataId) {
      await updateMailTemplate(editing.value.__dataId, payload);
    } else {
      await createMailTemplate(payload);
      page.value = 1;
    }
    dialog.value = false;
    successLocal.value = t('notifier.mailTemplates.saveSuccess');
    await loadCategoryOptions();
    await loadPage();
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('notifier.mailTemplates.saveError');
  } finally {
    saving.value = false;
  }
}

async function removeTemplate() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await deleteMailTemplate(deleteTarget.value.__dataId);
    deleteDialog.value = false;
    successLocal.value = t('notifier.mailTemplates.deleteSuccess');
    await loadPage();
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('notifier.mailTemplates.deleteError');
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="oc-mail-templates pa-4 pa-md-6">
    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>
    <v-alert v-if="successLocal" type="success" variant="tonal" class="mb-4" closable @click:close="successLocal = null">
      {{ successLocal }}
    </v-alert>

    <div class="mb-6">
      <h3 class="text-h6 font-weight-bold mb-1">{{ t('notifier.mailTemplates.pageTitle') }}</h3>
      <p class="text-body-2 text-medium-emphasis mb-0">{{ t('notifier.mailTemplates.pageSubtitle') }}</p>
    </div>

    <div class="d-flex flex-wrap align-center ga-3 mb-4">
      <v-text-field
        v-model="searchQuery"
        :label="t('notifier.mailTemplates.search')"
        prepend-inner-icon="mdi-magnify"
        density="compact"
        hide-details
        clearable
        style="min-width: 220px; max-width: 320px"
      />
      <v-select
        v-model="categoryFilter"
        :items="categoryItems"
        item-title="title"
        item-value="value"
        :label="t('notifier.mailTemplates.filterCategory')"
        density="compact"
        hide-details
        style="max-width: 200px"
      />
      <v-switch
        v-model="activeOnly"
        :label="t('notifier.mailTemplates.filterActiveOnly')"
        density="compact"
        hide-details
        color="primary"
      />
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="loadPage">
        {{ t('notifier.mailTemplates.refresh') }}
      </v-btn>
      <v-spacer />
      <v-chip v-if="total > 0" variant="tonal" size="small">
        {{ t('notifier.mailTemplates.statTotal', { count: total }) }}
      </v-chip>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">
        {{ t('notifier.mailTemplates.addTemplate') }}
      </v-btn>
    </div>

    <v-card v-if="!loading && total === 0" variant="outlined" rounded="lg" class="pa-8 text-center">
      <v-icon icon="mdi-email-outline" size="48" color="primary" class="mb-3 opacity-60" />
      <div class="text-h6 font-weight-bold mb-2">{{ t('notifier.mailTemplates.empty') }}</div>
      <p class="text-body-2 text-medium-emphasis mb-0">{{ t('notifier.mailTemplates.emptyHint') }}</p>
    </v-card>

    <v-card v-else variant="outlined" rounded="lg">
      <v-data-table-server
        v-model:page="page"
        v-model:items-per-page="itemsPerPage"
        :headers="headers"
        :items="templates"
        :items-length="total"
        :items-per-page-options="[...MAIL_TEMPLATE_PAGE_SIZE_OPTIONS]"
        :loading="loading"
        item-value="__dataId"
        density="comfortable"
        class="oc-mail-templates-table"
      >
        <template #[`item.templateKey`]="{ item }">
          <code class="text-body-2">{{ item.templateKey }}</code>
        </template>
        <template #[`item.subject`]="{ item }">
          <span class="text-body-2 text-truncate d-inline-block" style="max-width: 280px">{{ item.subject }}</span>
        </template>
        <template #[`item.category`]="{ item }">
          <v-chip size="small" variant="tonal">{{ item.category }}</v-chip>
        </template>
        <template #[`item.isActive`]="{ item }">
          <v-chip size="small" :color="item.isActive !== false ? 'success' : 'default'" variant="tonal">
            {{
              item.isActive !== false
                ? t('notifier.mailTemplates.activeYes')
                : t('notifier.mailTemplates.activeNo')
            }}
          </v-chip>
        </template>
        <template #[`item.actions`]="{ item }">
          <div class="d-flex justify-end ga-1">
            <v-btn icon="mdi-pencil-outline" size="small" variant="text" @click="openEdit(item)" />
            <v-btn
              icon="mdi-delete-outline"
              size="small"
              variant="text"
              color="error"
              :disabled="isSystemMailTemplate(item)"
              @click="confirmDelete(item)"
            />
          </div>
        </template>
      </v-data-table-server>
    </v-card>

    <p class="text-caption text-medium-emphasis mt-4 mb-0">
      {{ t('notifier.mailTemplates.technicalFootnote') }}
    </p>

    <OcMailTemplateDialog
      v-model="dialog"
      :template="editing"
      :category-options="categoryOptions"
      :saving="saving"
      @save="saveTemplate"
    />

    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="lg">
        <v-card-title>{{ t('notifier.mailTemplates.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('notifier.mailTemplates.deleteBody') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">{{ t('notifier.mailTemplates.cancel') }}</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="removeTemplate">
            {{ t('notifier.mailTemplates.deleteConfirm') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
