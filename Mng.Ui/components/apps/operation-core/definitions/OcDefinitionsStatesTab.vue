<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import TmStatusIconPicker from '@/components/apps/task-manager/TmStatusIconPicker.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  ocCreateState,
  ocDeleteState,
  ocListStates,
  ocUpdateState,
} from '@/services/operationCoreService';
import type { OpState } from '@/types/apps/operationCore';
import { OC_STATE_CATEGORIES } from '@/types/apps/operationCore';
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
const states = ref<OpState[]>([]);

const dialog = ref(false);
const editId = ref<string | null>(null);
const legacyColorWarning = ref(false);

const deleteDialog = ref(false);
const deleteTarget = ref<OpState | null>(null);

const defaultForm = () => ({
  name: '',
  category: 'open' as string,
  description: '',
  icon: '',
  color: 'secondary' as string,
  isInitial: false,
  isStart: false,
  isClosed: false,
  isTerminal: false,
  allowReopen: false,
  sortOrder: '' as string,
});

const form = ref(defaultForm());

const themeColorItems = computed(() =>
  [...TM_STATUS_THEME_COLORS].map((v) => ({
    value: v,
    title: t(`operationCore.definitions.states.themeColor.${v}`),
  }))
);

const categoryItems = computed(() =>
  OC_STATE_CATEGORIES.map((value) => ({
    value,
    title: t(`operationCore.definitions.states.category.${value}`),
  }))
);

const tableHeaders = computed(() => [
  { title: t('operationCore.definitions.states.colName'), key: 'name', sortable: true },
  { title: t('operationCore.definitions.states.colCategory'), key: 'category', sortable: true },
  { title: t('operationCore.definitions.states.colFlags'), key: 'flags', sortable: false },
  { title: t('operationCore.definitions.states.colDescription'), key: 'description', sortable: false },
  { title: t('operationCore.definitions.states.colIcon'), key: 'icon', sortable: false },
  { title: t('operationCore.definitions.states.colColor'), key: 'color', sortable: false },
  { title: t('operationCore.definitions.states.colSortOrder'), key: 'sortOrder', sortable: true },
  { title: t('operationCore.definitions.states.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

function categoryLabel(value: string) {
  const key = `operationCore.definitions.states.category.${value}`;
  const translated = t(key);
  return translated !== key ? translated : value;
}

function statusIconEl(name: string | null | undefined) {
  return getTmStatusTablerIconComponent(name);
}

function flagLabels(state: OpState): string[] {
  const flags: string[] = [];
  if (state.isInitial) flags.push(t('operationCore.definitions.states.flagInitial'));
  if (state.isStart) flags.push(t('operationCore.definitions.states.flagStart'));
  if (state.isClosed) flags.push(t('operationCore.definitions.states.flagClosed'));
  if (state.isTerminal) flags.push(t('operationCore.definitions.states.flagTerminal'));
  if (state.allowReopen) flags.push(t('operationCore.definitions.states.flagReopen'));
  return flags;
}

function buildPayload(): Record<string, unknown> {
  const sortRaw = form.value.sortOrder.trim();
  const sortOrder = sortRaw === '' ? null : Number(sortRaw);
  const body: Record<string, unknown> = {
    name: form.value.name.trim(),
    category: form.value.category.trim(),
    description: form.value.description.trim() || null,
    icon: form.value.icon.trim() || null,
    color: form.value.color.trim() ? form.value.color.trim() : null,
    isInitial: form.value.isInitial,
    isStart: form.value.isStart,
    isClosed: form.value.isClosed,
    isTerminal: form.value.isTerminal,
    allowReopen: form.value.allowReopen,
    sortOrder: Number.isFinite(sortOrder) ? sortOrder : null,
  };
  return body;
}

async function loadStates() {
  loading.value = true;
  errorLocal.value = null;
  try {
    states.value = await ocListStates();
  } catch (e: unknown) {
    errorLocal.value =
      e instanceof Error ? e.message : t('operationCore.definitions.states.loadError');
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

function openEdit(state: OpState) {
  editId.value = state.__dataId;
  const c = state.color?.trim() ?? '';
  legacyColorWarning.value = isLegacyHexStatusColor(c) || (!!c && !isTmStatusThemeColor(c));
  const themePick = isTmStatusThemeColor(c) ? c : 'secondary';
  form.value = {
    name: state.name,
    category: state.category || 'open',
    description: state.description ?? '',
    icon: state.icon ?? '',
    color: themePick,
    isInitial: Boolean(state.isInitial),
    isStart: Boolean(state.isStart),
    isClosed: Boolean(state.isClosed),
    isTerminal: Boolean(state.isTerminal),
    allowReopen: Boolean(state.allowReopen),
    sortOrder: state.sortOrder != null ? String(state.sortOrder) : '',
  };
  errorLocal.value = null;
  dialog.value = true;
}

function openDelete(state: OpState) {
  deleteTarget.value = state;
  deleteDialog.value = true;
}

onMounted(() => {
  void loadStates();
});

async function submitForm() {
  if (!form.value.name.trim() || !form.value.category.trim()) return;
  saving.value = true;
  errorLocal.value = null;
  try {
    const body = buildPayload();
    if (editId.value) {
      await ocUpdateState(editId.value, body);
    } else {
      await ocCreateState(body);
    }
    dialog.value = false;
    await loadStates();
  } catch (e: unknown) {
    errorLocal.value =
      e instanceof Error ? e.message : t('operationCore.definitions.states.saveError');
  } finally {
    saving.value = false;
  }
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await ocDeleteState(deleteTarget.value.__dataId);
    deleteDialog.value = false;
    deleteTarget.value = null;
    await loadStates();
  } catch (e: unknown) {
    errorLocal.value =
      e instanceof Error ? e.message : t('operationCore.definitions.states.deleteError');
    deleteDialog.value = false;
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="oc-states-tab pa-4 pa-md-6">
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
        {{ t('operationCore.definitions.states.subtitle') }}
      </p>
      <v-btn color="primary" rounded="lg" class="text-none" @click="openCreate">
        <v-icon icon="mdi-plus" start />
        {{ t('operationCore.definitions.states.newState') }}
      </v-btn>
    </div>

    <v-card variant="outlined" rounded="lg">
      <v-data-table
        :headers="tableHeaders"
        :items="states"
        :loading="loading"
        class="oc-states-table"
      >
        <template #[`item.category`]="{ item }">
          <v-chip size="small" variant="tonal" rounded="lg" class="text-none">
            {{ categoryLabel(item.category) }}
          </v-chip>
        </template>
        <template #[`item.flags`]="{ item }">
          <div v-if="flagLabels(item).length" class="d-flex flex-wrap gap-1">
            <v-chip
              v-for="label in flagLabels(item)"
              :key="label"
              size="x-small"
              variant="outlined"
              density="compact"
              class="text-none"
            >
              {{ label }}
            </v-chip>
          </div>
          <span v-else class="text-medium-emphasis">—</span>
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
              class="flex-shrink-0 oc-state-icon-cell"
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
          <v-btn icon variant="text" size="small" color="error" @click="openDelete(item)">
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
              ? t('operationCore.definitions.states.editState')
              : t('operationCore.definitions.states.newState')
          }}
        </v-card-title>
        <v-card-text>
          <v-alert v-if="legacyColorWarning" type="info" variant="tonal" density="compact" class="mb-4">
            {{ t('operationCore.definitions.states.legacyColorHint') }}
          </v-alert>
          <v-text-field
            v-model="form.name"
            :label="t('operationCore.definitions.states.fieldName')"
            density="comfortable"
            required
          />
          <v-select
            v-model="form.category"
            class="mt-3"
            :items="categoryItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.definitions.states.fieldCategory')"
            density="comfortable"
          />
          <v-textarea
            v-model="form.description"
            class="mt-3"
            :label="t('operationCore.definitions.states.fieldDescription')"
            rows="2"
            auto-grow
            density="comfortable"
            variant="outlined"
          />
          <TmStatusIconPicker
            v-model="form.icon"
            class="mt-3"
            :label="t('operationCore.definitions.states.fieldIcon')"
            :hint="t('operationCore.definitions.states.iconHint')"
            :search-placeholder="t('operationCore.definitions.states.iconSearch')"
            :menu-title="t('operationCore.definitions.states.iconMenuTitle')"
            :clear-label="t('operationCore.definitions.states.iconClear')"
            :no-results="t('operationCore.definitions.states.iconNoMatch')"
          />
          <v-select
            v-model="form.color"
            class="mt-3"
            :items="themeColorItems"
            item-title="title"
            item-value="value"
            clearable
            :label="t('operationCore.definitions.states.fieldColor')"
            :hint="t('operationCore.definitions.states.colorHint')"
            persistent-hint
            density="comfortable"
          />
          <v-text-field
            v-model="form.sortOrder"
            class="mt-3"
            type="number"
            :label="t('operationCore.definitions.states.fieldSortOrder')"
            density="comfortable"
          />
          <v-divider class="my-4" />
          <div class="d-flex flex-column gap-1">
            <v-switch
              v-model="form.isInitial"
              :label="t('operationCore.definitions.states.fieldInitial')"
              density="compact"
              hide-details
              color="primary"
            />
            <v-switch
              v-model="form.isStart"
              :label="t('operationCore.definitions.states.fieldStart')"
              density="compact"
              hide-details
              color="primary"
            />
            <v-switch
              v-model="form.isClosed"
              :label="t('operationCore.definitions.states.fieldClosed')"
              density="compact"
              hide-details
              color="primary"
            />
            <v-switch
              v-model="form.isTerminal"
              :label="t('operationCore.definitions.states.fieldTerminal')"
              density="compact"
              hide-details
              color="primary"
            />
            <v-switch
              v-model="form.allowReopen"
              :label="t('operationCore.definitions.states.fieldReopen')"
              density="compact"
              hide-details
              color="primary"
            />
          </div>
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
        <v-card-title>{{ t('operationCore.definitions.states.deleteTitle') }}</v-card-title>
        <v-card-text>
          {{ t('operationCore.definitions.states.deleteBody') }}
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
.oc-state-icon-cell {
  color: rgba(var(--v-theme-on-surface), 0.75);
}
</style>
