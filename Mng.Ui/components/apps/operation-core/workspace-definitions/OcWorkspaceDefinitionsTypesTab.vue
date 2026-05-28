<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import TmStatusIconPicker from '@/components/apps/task-manager/TmStatusIconPicker.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  ocCreateWorkItemType,
  ocDeleteWorkItemType,
  ocExtractDgErrorMessage,
  ocGetWorkspace,
  ocListGlobalWorkItemTypes,
  ocListWorkspaceScopedWorkItemTypes,
  ocSaveWorkspaceEnabledTypeIds,
  ocUpdateWorkItemType,
} from '@/services/operationCoreService';
import type { OpWorkItemType } from '@/types/apps/operationCore';
import { OC_WORK_ITEM_TYPE_CATEGORIES } from '@/types/apps/operationCore';
import {
  TM_STATUS_THEME_COLORS,
  isLegacyHexStatusColor,
  isTmStatusThemeColor,
} from '@/utils/taskManagerStatusColor';

const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();

const loading = ref(true);
const savingSelection = ref(false);
const savingType = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);

const globalTypes = ref<OpWorkItemType[]>([]);
const scopedTypes = ref<OpWorkItemType[]>([]);
const selectedTypeIds = ref<string[]>([]);

const dialog = ref(false);
const editId = ref<string | null>(null);
const legacyColorWarning = ref(false);
const deleteDialog = ref(false);
const deleteTarget = ref<OpWorkItemType | null>(null);

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

const globalTypesByCategory = computed(() => {
  const map = new Map<string, OpWorkItemType[]>();
  for (const type of globalTypes.value) {
    const cat = type.category || 'task';
    if (!map.has(cat)) map.set(cat, []);
    map.get(cat)!.push(type);
  }
  return [...map.entries()].sort(([a], [b]) => a.localeCompare(b));
});

const scopedTableHeaders = computed(() => [
  { title: t('operationCore.definitions.types.colName'), key: 'name', sortable: true },
  { title: t('operationCore.definitions.types.colCategory'), key: 'category', sortable: true },
  { title: t('operationCore.definitions.types.colSortOrder'), key: 'sortOrder', sortable: true },
  { title: t('operationCore.definitions.types.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

function categoryLabel(value: string) {
  const key = `operationCore.definitions.types.category.${value}`;
  const translated = t(key);
  return translated !== key ? translated : value;
}

function toggleTypeId(id: string, enabled: boolean) {
  if (enabled) {
    if (!selectedTypeIds.value.includes(id)) {
      selectedTypeIds.value = [...selectedTypeIds.value, id];
    }
  } else {
    selectedTypeIds.value = selectedTypeIds.value.filter((x) => x !== id);
  }
}

function buildTypePayload(): Record<string, unknown> {
  const sortRaw = form.value.sortOrder.trim();
  const sortOrder = sortRaw === '' ? null : Number(sortRaw);
  return {
    name: form.value.name.trim(),
    category: form.value.category,
    description: form.value.description.trim() || null,
    icon: form.value.icon.trim() || null,
    color: form.value.color.trim() ? form.value.color.trim() : null,
    sortOrder: Number.isFinite(sortOrder) ? sortOrder : null,
    workspaceId: props.workspaceId,
  };
}

async function loadAll() {
  if (!props.workspaceId) return;
  loading.value = true;
  errorLocal.value = null;
  try {
    const [ws, global, scoped] = await Promise.all([
      ocGetWorkspace(props.workspaceId),
      ocListGlobalWorkItemTypes(),
      ocListWorkspaceScopedWorkItemTypes(props.workspaceId),
    ]);
    globalTypes.value = global;
    scopedTypes.value = scoped;
    selectedTypeIds.value = ws?.enabledTypeIds ? [...ws.enabledTypeIds] : [];
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.types.loadError')
    );
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.workspaceId,
  () => {
    void loadAll();
  },
  { immediate: true }
);

function openCreateScoped() {
  editId.value = null;
  legacyColorWarning.value = false;
  form.value = defaultForm();
  dialog.value = true;
}

function openEditScoped(row: OpWorkItemType) {
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
  dialog.value = true;
}

function openDelete(row: OpWorkItemType) {
  deleteTarget.value = row;
  deleteDialog.value = true;
}

async function saveSelection() {
  if (!props.workspaceId) return;
  savingSelection.value = true;
  errorLocal.value = null;
  successLocal.value = null;
  try {
    await ocSaveWorkspaceEnabledTypeIds(props.workspaceId, selectedTypeIds.value);
    await loadAll();
    successLocal.value = t('operationCore.workspaceDefinitions.saveSuccess');
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.types.saveSelectionError')
    );
  } finally {
    savingSelection.value = false;
  }
}

async function submitScopedType() {
  if (!form.value.name.trim()) return;
  savingType.value = true;
  errorLocal.value = null;
  try {
    const body = buildTypePayload();
    if (editId.value) {
      await ocUpdateWorkItemType(editId.value, body);
    } else {
      await ocCreateWorkItemType(body);
    }
    dialog.value = false;
    await loadAll();
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.definitions.types.saveError')
    );
  } finally {
    savingType.value = false;
  }
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    const id = deleteTarget.value.__dataId;
    await ocDeleteWorkItemType(id);
    selectedTypeIds.value = selectedTypeIds.value.filter((x) => x !== id);
    deleteDialog.value = false;
    deleteTarget.value = null;
    await loadAll();
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.definitions.types.deleteError')
    );
    deleteDialog.value = false;
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="oc-ws-types-tab pa-4 pa-md-6">
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

    <v-alert
      v-if="successLocal"
      type="success"
      variant="tonal"
      class="mb-4"
      closable
      @click:close="successLocal = null"
    >
      {{ successLocal }}
    </v-alert>

    <div v-if="loading" class="d-flex justify-center py-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <template v-else>
      <section class="mb-8">
        <h3 class="text-subtitle-1 font-weight-medium mb-1">
          {{ t('operationCore.workspaceDefinitions.types.catalogTitle') }}
        </h3>
        <p class="text-body-2 text-medium-emphasis mb-4">
          {{ t('operationCore.workspaceDefinitions.types.catalogSubtitle') }}
        </p>

        <v-alert
          v-if="!selectedTypeIds.length"
          type="info"
          variant="tonal"
          density="compact"
          class="mb-4"
        >
          {{ t('operationCore.workspaceDefinitions.types.noneSelectedHint') }}
        </v-alert>

        <v-card
          v-for="[category, types] in globalTypesByCategory"
          :key="category"
          variant="outlined"
          rounded="lg"
          class="mb-3"
        >
          <v-card-title class="text-subtitle-2 py-3">
            {{ categoryLabel(category) }}
          </v-card-title>
          <v-divider />
          <v-card-text class="pt-3">
            <div class="d-flex flex-column ga-1">
              <v-checkbox
                v-for="type in types"
                :key="type.__dataId"
                :model-value="selectedTypeIds.includes(type.__dataId)"
                hide-details
                density="compact"
                @update:model-value="(v) => toggleTypeId(type.__dataId, !!v)"
              >
                <template #label>
                  <span>{{ type.name }}</span>
                  <v-chip
                    v-if="type.color && isTmStatusThemeColor(type.color)"
                    :color="type.color"
                    size="x-small"
                    variant="tonal"
                    class="ml-2 text-none"
                  >
                    {{ type.color }}
                  </v-chip>
                </template>
              </v-checkbox>
            </div>
          </v-card-text>
        </v-card>

        <div class="d-flex justify-end mt-4">
          <v-btn
            color="primary"
            rounded="lg"
            class="text-none"
            :loading="savingSelection"
            @click="saveSelection"
          >
            {{ t('operationCore.workspaceDefinitions.types.saveSelection') }}
          </v-btn>
        </div>
      </section>

      <section>
        <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
          <div>
            <h3 class="text-subtitle-1 font-weight-medium mb-1">
              {{ t('operationCore.workspaceDefinitions.types.scopedTitle') }}
            </h3>
            <p class="text-body-2 text-medium-emphasis mb-0">
              {{ t('operationCore.workspaceDefinitions.types.scopedSubtitle') }}
            </p>
          </div>
          <v-btn color="primary" variant="tonal" rounded="lg" class="text-none" @click="openCreateScoped">
            <v-icon icon="mdi-plus" start />
            {{ t('operationCore.workspaceDefinitions.types.newScopedType') }}
          </v-btn>
        </div>

        <v-card variant="outlined" rounded="lg">
          <v-data-table
            :headers="scopedTableHeaders"
            :items="scopedTypes"
            class="oc-ws-scoped-types-table"
          >
            <template #[`item.category`]="{ item }">
              {{ categoryLabel(item.category) }}
            </template>
            <template #[`item.sortOrder`]="{ item }">
              {{ item.sortOrder ?? '—' }}
            </template>
            <template #[`item.actions`]="{ item }">
              <v-btn icon variant="text" size="small" @click="openEditScoped(item)">
                <v-icon icon="mdi-pencil-outline" />
              </v-btn>
              <v-btn icon variant="text" size="small" color="error" @click="openDelete(item)">
                <v-icon icon="mdi-delete-outline" />
              </v-btn>
            </template>
          </v-data-table>
        </v-card>
      </section>
    </template>

    <v-dialog v-model="dialog" max-width="560">
      <v-card rounded="xl">
        <v-card-title class="text-h6">
          {{
            editId
              ? t('operationCore.workspaceDefinitions.types.editScopedType')
              : t('operationCore.workspaceDefinitions.types.newScopedType')
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
        <v-card-actions class="px-4 pb-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="dialog = false">
            {{ t('operationCore.definitions.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            rounded="lg"
            class="text-none"
            :loading="savingType"
            :disabled="!form.name.trim()"
            @click="submitScopedType"
          >
            {{ t('operationCore.definitions.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="xl">
        <v-card-title>{{ t('operationCore.definitions.types.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('operationCore.definitions.types.deleteBody') }}</v-card-text>
        <v-card-actions class="px-4 pb-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteDialog = false">
            {{ t('operationCore.definitions.cancel') }}
          </v-btn>
          <v-btn
            color="error"
            variant="flat"
            rounded="lg"
            class="text-none"
            :loading="deleting"
            @click="confirmDelete"
          >
            {{ t('operationCore.definitions.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
