<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import TmStatusIconPicker from '@/components/apps/task-manager/TmStatusIconPicker.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  ocCreateWorkItemType,
  ocDeleteWorkItemType,
  ocListGlobalWorkItemTypes,
  ocUpdateWorkItemType,
} from '@/services/operationCoreService';
import type { OpWorkItemType } from '@/types/apps/operationCore';
import { OC_WORK_ITEM_TYPE_CATEGORIES } from '@/types/apps/operationCore';
import {
  TM_STATUS_THEME_COLORS,
  isLegacyHexStatusColor,
  isTmStatusThemeColor,
} from '@/utils/taskManagerStatusColor';
import { getTmStatusTablerIconComponent } from '@/utils/tmStatusTablerIcons';

const { t } = useAppI18n();

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);
const types = ref<OpWorkItemType[]>([]);

const dialog = ref(false);
const editId = ref<string | null>(null);
const legacyColorWarning = ref(false);

const deleteDialog = ref(false);
const deleteTarget = ref<OpWorkItemType | null>(null);

const groupBy = ref([{ key: 'category', order: 'asc' as const }]);
const sortBy = ref([{ key: 'sortOrder', order: 'asc' as const }]);

const defaultForm = () => ({
  name: '',
  category: 'task' as string,
  description: '',
  icon: '',
  color: 'secondary' as string,
  sortOrder: '' as string,
});

const form = ref(defaultForm());

const themeColorItems = computed(() =>
  [...TM_STATUS_THEME_COLORS].map((v) => ({
    value: v,
    title: t(`operationCore.definitions.types.themeColor.${v}`),
  }))
);

const categoryItems = computed(() =>
  OC_WORK_ITEM_TYPE_CATEGORIES.map((value) => ({
    value,
    title: t(`operationCore.definitions.types.category.${value}`),
  }))
);

const tableHeaders = computed(() => [
  { title: t('operationCore.definitions.types.colName'), key: 'name', sortable: true, groupable: false },
  { title: t('operationCore.definitions.types.colCategory'), key: 'category', sortable: true },
  { title: t('operationCore.definitions.types.colDescription'), key: 'description', sortable: false, groupable: false },
  { title: t('operationCore.definitions.types.colIcon'), key: 'icon', sortable: false, groupable: false },
  { title: t('operationCore.definitions.types.colColor'), key: 'color', sortable: false, groupable: false },
  { title: t('operationCore.definitions.types.colSortOrder'), key: 'sortOrder', sortable: true, groupable: false },
  { title: t('operationCore.definitions.types.colActions'), key: 'actions', sortable: false, align: 'end' as const, groupable: false },
]);

function categoryLabel(value: string) {
  const key = `operationCore.definitions.types.category.${value}`;
  const translated = t(key);
  return translated !== key ? translated : value;
}

function statusIconEl(name: string | null | undefined) {
  return getTmStatusTablerIconComponent(name);
}

function buildPayload(): Record<string, unknown> {
  const sortRaw = form.value.sortOrder.trim();
  const sortOrder = sortRaw === '' ? null : Number(sortRaw);
  return {
    name: form.value.name.trim(),
    category: form.value.category.trim(),
    description: form.value.description.trim() || null,
    icon: form.value.icon.trim() || null,
    color: form.value.color.trim() ? form.value.color.trim() : null,
    sortOrder: Number.isFinite(sortOrder) ? sortOrder : null,
  };
}

async function loadTypes() {
  loading.value = true;
  errorLocal.value = null;
  try {
    types.value = await ocListGlobalWorkItemTypes();
  } catch (e: unknown) {
    errorLocal.value =
      e instanceof Error ? e.message : t('operationCore.definitions.types.loadError');
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editId.value = null;
  legacyColorWarning.value = false;
  form.value = defaultForm();
  errorLocal.value = null;
  dialog.value = true;
}

function openEdit(row: OpWorkItemType) {
  editId.value = row.__dataId;
  const c = row.color?.trim() ?? '';
  legacyColorWarning.value = isLegacyHexStatusColor(c) || (!!c && !isTmStatusThemeColor(c));
  const themePick = isTmStatusThemeColor(c) ? c : 'secondary';
  form.value = {
    name: row.name,
    category: row.category || 'task',
    description: row.description ?? '',
    icon: row.icon ?? '',
    color: themePick,
    sortOrder: row.sortOrder != null ? String(row.sortOrder) : '',
  };
  errorLocal.value = null;
  dialog.value = true;
}

function openDelete(row: OpWorkItemType) {
  deleteTarget.value = row;
  deleteDialog.value = true;
}

onMounted(() => {
  void loadTypes();
});

async function submitForm() {
  if (!form.value.name.trim() || !form.value.category.trim()) return;
  saving.value = true;
  errorLocal.value = null;
  try {
    const body = buildPayload();
    if (editId.value) {
      await ocUpdateWorkItemType(editId.value, body);
    } else {
      await ocCreateWorkItemType(body);
    }
    dialog.value = false;
    await loadTypes();
  } catch (e: unknown) {
    errorLocal.value =
      e instanceof Error ? e.message : t('operationCore.definitions.types.saveError');
  } finally {
    saving.value = false;
  }
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await ocDeleteWorkItemType(deleteTarget.value.__dataId);
    deleteDialog.value = false;
    deleteTarget.value = null;
    await loadTypes();
  } catch (e: unknown) {
    errorLocal.value =
      e instanceof Error ? e.message : t('operationCore.definitions.types.deleteError');
    deleteDialog.value = false;
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="oc-types-tab pa-4 pa-md-6">
    <v-alert
      v-if="errorLocal"
      type="error"
      variant="tonal"
      class="mb-4"
      closable
      @click:close="errorLocal = null"
    >
      {{ errorLocal }}
    </v-alert>

    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('operationCore.definitions.types.subtitle') }}
      </p>
      <v-btn color="primary" rounded="lg" class="text-none" @click="openCreate">
        <v-icon icon="mdi-plus" start />
        {{ t('operationCore.definitions.types.newType') }}
      </v-btn>
    </div>

    <v-card variant="outlined" rounded="lg">
      <v-data-table
        :headers="tableHeaders"
        :items="types"
        :loading="loading"
        :group-by="groupBy"
        :sort-by="sortBy"
        item-value="__dataId"
        class="oc-types-table"
      >
        <template #group-header="{ item, columns, toggleGroup, isGroupOpen }">
          <tr>
            <td :colspan="columns.length">
              <v-btn
                :icon="isGroupOpen(item) ? 'mdi-chevron-down' : 'mdi-chevron-right'"
                size="small"
                variant="text"
                @click="toggleGroup(item)"
              />
              <span class="font-weight-medium ms-1">
                {{ categoryLabel(String(item.value)) }}
              </span>
              <v-chip size="x-small" variant="tonal" class="ms-2">
                {{ item.items?.length ?? 0 }}
              </v-chip>
            </td>
          </tr>
        </template>
        <template #[`item.category`]="{ item }">
          <v-chip size="small" variant="tonal" rounded="lg" class="text-none">
            {{ categoryLabel(item.category) }}
          </v-chip>
        </template>
        <template #[`item.description`]="{ item }">
          <span
            class="text-body-2 text-medium-emphasis text-truncate d-inline-block"
            style="max-width: 220px"
            :title="item.description || ''"
          >
            {{ item.description || '—' }}
          </span>
        </template>
        <template #[`item.icon`]="{ item }">
          <div v-if="item.icon" class="d-flex align-center gap-2">
            <component
              v-if="statusIconEl(item.icon)"
              :is="statusIconEl(item.icon)"
              :size="20"
              class="flex-shrink-0 oc-type-icon-cell"
            />
            <v-icon v-else icon="mdi-help-circle-outline" size="20" class="text-medium-emphasis flex-shrink-0" />
            <code class="text-caption text-medium-emphasis text-truncate" style="max-width: 120px">{{ item.icon }}</code>
          </div>
          <span v-else class="text-medium-emphasis">—</span>
        </template>
        <template #[`item.color`]="{ item }">
          <v-chip
            v-if="item.color && isTmStatusThemeColor(item.color)"
            :color="item.color"
            size="small"
            variant="tonal"
            rounded="lg"
            class="text-none"
          >
            {{ item.color }}
          </v-chip>
          <div v-else-if="item.color && isLegacyHexStatusColor(item.color)" class="d-flex align-center gap-2 flex-wrap">
            <div
              class="oc-swatch rounded-lg flex-shrink-0"
              :style="{ backgroundColor: item.color, boxShadow: 'inset 0 0 0 1px rgba(0,0,0,.12)' }"
            />
            <span class="text-caption text-medium-emphasis">{{ item.color }}</span>
          </div>
          <span v-else-if="item.color" class="text-caption text-medium-emphasis">{{ item.color }}</span>
          <span v-else class="text-medium-emphasis">—</span>
        </template>
        <template #[`item.sortOrder`]="{ item }">
          <span>{{ item.sortOrder ?? '—' }}</span>
        </template>
        <template #[`item.actions`]="{ item }">
          <v-btn icon variant="text" size="small" @click="openEdit(item)">
            <v-icon icon="mdi-pencil-outline" />
          </v-btn>
          <v-btn
            icon
            variant="text"
            size="small"
            color="error"
            :disabled="item.isSystem"
            @click="openDelete(item)"
          >
            <v-icon icon="mdi-delete-outline" />
          </v-btn>
        </template>
      </v-data-table>
    </v-card>

    <v-dialog v-model="dialog" max-width="560">
      <v-card rounded="xl">
        <v-card-title class="text-h6">
          {{
            editId
              ? t('operationCore.definitions.types.editType')
              : t('operationCore.definitions.types.newType')
          }}
        </v-card-title>
        <v-card-text>
          <v-alert v-if="legacyColorWarning" type="info" variant="tonal" density="compact" class="mb-4">
            {{ t('operationCore.definitions.types.legacyColorHint') }}
          </v-alert>
          <v-text-field
            v-model="form.name"
            :label="t('operationCore.definitions.types.fieldName')"
            density="comfortable"
            required
          />
          <v-select
            v-model="form.category"
            class="mt-3"
            :items="categoryItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.definitions.types.fieldCategory')"
            :hint="t('operationCore.definitions.types.categoryHint')"
            persistent-hint
            density="comfortable"
          />
          <v-textarea
            v-model="form.description"
            class="mt-3"
            :label="t('operationCore.definitions.types.fieldDescription')"
            rows="2"
            auto-grow
            density="comfortable"
            variant="outlined"
          />
          <TmStatusIconPicker
            v-model="form.icon"
            class="mt-3"
            :label="t('operationCore.definitions.types.fieldIcon')"
            :hint="t('operationCore.definitions.types.iconHint')"
            :search-placeholder="t('operationCore.definitions.types.iconSearch')"
            :menu-title="t('operationCore.definitions.types.iconMenuTitle')"
            :clear-label="t('operationCore.definitions.types.iconClear')"
            :no-results="t('operationCore.definitions.types.iconNoMatch')"
          />
          <v-select
            v-model="form.color"
            class="mt-3"
            :items="themeColorItems"
            item-title="title"
            item-value="value"
            clearable
            :label="t('operationCore.definitions.types.fieldColor')"
            :hint="t('operationCore.definitions.types.colorHint')"
            persistent-hint
            density="comfortable"
          />
          <v-text-field
            v-model="form.sortOrder"
            class="mt-3"
            type="number"
            :label="t('operationCore.definitions.types.fieldSortOrder')"
            density="comfortable"
          />
        </v-card-text>
        <v-card-actions class="px-6 pb-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="dialog = false">
            {{ t('operationCore.definitions.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            rounded="lg"
            class="text-none"
            :loading="saving"
            :disabled="!form.name.trim() || !form.category.trim()"
            @click="submitForm"
          >
            {{ t('operationCore.definitions.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="xl">
        <v-card-title>{{ t('operationCore.definitions.types.deleteTitle') }}</v-card-title>
        <v-card-text>
          {{ t('operationCore.definitions.types.deleteBody') }}
          <div v-if="deleteTarget" class="mt-2 font-weight-medium">{{ deleteTarget.name }}</div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteDialog = false">
            {{ t('operationCore.definitions.cancel') }}
          </v-btn>
          <v-btn color="error" variant="flat" class="text-none" :loading="deleting" @click="confirmDelete">
            {{ t('operationCore.definitions.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.oc-swatch {
  width: 22px;
  height: 22px;
}
.oc-type-icon-cell {
  color: rgba(var(--v-theme-on-surface), 0.75);
}
</style>
