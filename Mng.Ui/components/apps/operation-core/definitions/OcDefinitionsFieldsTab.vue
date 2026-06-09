<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  ocCreateField,
  ocDeleteField,
  ocListGlobalPoolFields,
  ocUpdateField,
} from '@/services/operationCoreService';
import type { OpField } from '@/types/apps/operationCore';
import {
  OC_CORE_WORK_ITEM_FIELDS,
  OC_FIELD_CATEGORIES,
  OC_FIELD_KEY_PATTERN,
  OC_POOL_FIELD_TYPE_VALUES,
  parseOcFieldOptions,
  resolveOcFieldOptionsHint,
  stringifyOcFieldOptions,
} from '@/utils/ocFieldDefinitions';

const { t, locale } = useAppI18n();

const fieldOptionsHintText = computed(() => resolveOcFieldOptionsHint(locale()));

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);
const poolFields = ref<OpField[]>([]);

const dialog = ref(false);
const editId = ref<string | null>(null);
const optionsError = ref<string | null>(null);

const deleteDialog = ref(false);
const deleteTarget = ref<OpField | null>(null);

const poolGroupBy = ref([{ key: 'category', order: 'asc' as const }]);
const poolSortBy = ref([{ key: 'sortOrder', order: 'asc' as const }]);

const coreGroupBy = ref([{ key: 'group', order: 'asc' as const }]);

const defaultForm = () => ({
  key: '',
  label: '',
  fieldType: 'text' as string,
  category: 'classification' as string,
  cardinality: 'single' as string,
  description: '',
  sortOrder: '' as string,
  relationDatasetName: '',
  optionsJson: '',
  isSensitive: false,
});

const form = ref(defaultForm());

const coreRows = computed(() =>
  OC_CORE_WORK_ITEM_FIELDS.map((entry) => ({
    ...entry,
    scope: 'core',
  }))
);

const fieldTypeItems = computed(() =>
  OC_POOL_FIELD_TYPE_VALUES.map((value) => ({
    value,
    title: t(`operationCore.definitions.fields.fieldType.${value}`),
  }))
);

const categoryItems = computed(() =>
  OC_FIELD_CATEGORIES.map((value) => ({
    value,
    title: t(`operationCore.definitions.fields.category.${value}`),
  }))
);

const cardinalityItems = computed(() => [
  { value: 'single', title: t('operationCore.definitions.fields.cardinality.single') },
  { value: 'multi', title: t('operationCore.definitions.fields.cardinality.multi') },
]);

const coreHeaders = computed(() => [
  { title: t('operationCore.definitions.fields.colKey'), key: 'key', sortable: true, groupable: false },
  { title: t('operationCore.definitions.fields.colFieldType'), key: 'fieldType', sortable: true, groupable: false },
  { title: t('operationCore.definitions.fields.colGroup'), key: 'group', sortable: true },
  { title: t('operationCore.definitions.fields.colScope'), key: 'scope', sortable: false, groupable: false },
]);

const poolHeaders = computed(() => [
  { title: t('operationCore.definitions.fields.colKey'), key: 'key', sortable: true, groupable: false },
  { title: t('operationCore.definitions.fields.colLabel'), key: 'label', sortable: true, groupable: false },
  { title: t('operationCore.definitions.fields.colFieldType'), key: 'fieldType', sortable: true },
  { title: t('operationCore.definitions.fields.colCategory'), key: 'category', sortable: true },
  { title: t('operationCore.definitions.fields.colCardinality'), key: 'cardinality', sortable: false },
  { title: t('operationCore.definitions.fields.colDescription'), key: 'description', sortable: false, groupable: false },
  { title: t('operationCore.definitions.fields.colSortOrder'), key: 'sortOrder', sortable: true, groupable: false },
  { title: t('operationCore.definitions.fields.colSensitive'), key: 'isSensitive', sortable: false, groupable: false },
  { title: t('operationCore.definitions.fields.colActions'), key: 'actions', sortable: false, align: 'end' as const, groupable: false },
]);

const showRelationDataset = computed(() => form.value.fieldType === 'relation');

function groupLabel(value: string) {
  const key = `operationCore.definitions.fields.coreGroup.${value}`;
  const translated = t(key);
  return translated !== key ? translated : value;
}

function categoryLabel(value: string | null | undefined) {
  if (!value) return '—';
  const key = `operationCore.definitions.fields.category.${value}`;
  const translated = t(key);
  return translated !== key ? translated : value;
}

function cardinalityLabel(value: string | null | undefined) {
  if (!value) return '—';
  const key = `operationCore.definitions.fields.cardinality.${value}`;
  const translated = t(key);
  return translated !== key ? translated : value;
}

function validateKey(key: string): boolean {
  return OC_FIELD_KEY_PATTERN.test(key.trim());
}

function buildPayload(): Record<string, unknown> | null {
  optionsError.value = null;
  const key = form.value.key.trim();
  const label = form.value.label.trim();
  if (!label) return null;
  if (!editId.value && !validateKey(key)) {
    optionsError.value = t('operationCore.definitions.fields.keyInvalid');
    return null;
  }

  const optionsRaw = form.value.optionsJson.trim();
  let options: Record<string, unknown> | null = null;
  if (optionsRaw) {
    const parsed = parseOcFieldOptions(optionsRaw);
    if (!parsed) {
      optionsError.value = t('operationCore.definitions.fields.optionsInvalid');
      return null;
    }
    options = parsed;
  }

  const sortRaw = form.value.sortOrder.trim();
  const sortOrder = sortRaw === '' ? null : Number(sortRaw);

  const payload: Record<string, unknown> = {
    label,
    fieldType: form.value.fieldType,
    scope: 'pool',
    category: form.value.category || null,
    cardinality: form.value.cardinality || null,
    description: form.value.description.trim() || null,
    sortOrder: Number.isFinite(sortOrder) ? sortOrder : null,
    relationDatasetName:
      form.value.fieldType === 'relation' && form.value.relationDatasetName.trim()
        ? form.value.relationDatasetName.trim()
        : null,
    options,
    isSensitive: form.value.isSensitive,
    isSystem: false,
    workspaceId: null,
  };

  if (!editId.value) {
    payload.key = key;
  }

  return payload;
}

async function loadFields() {
  loading.value = true;
  errorLocal.value = null;
  try {
    poolFields.value = await ocListGlobalPoolFields();
  } catch (e: unknown) {
    errorLocal.value =
      e instanceof Error ? e.message : t('operationCore.definitions.fields.loadError');
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editId.value = null;
  optionsError.value = null;
  form.value = defaultForm();
  errorLocal.value = null;
  dialog.value = true;
}

function openEdit(row: OpField) {
  editId.value = row.__dataId;
  optionsError.value = null;
  form.value = {
    key: row.key,
    label: row.label,
    fieldType: row.fieldType || 'text',
    category: row.category || 'classification',
    cardinality: row.cardinality || 'single',
    description: row.description ?? '',
    sortOrder: row.sortOrder != null ? String(row.sortOrder) : '',
    relationDatasetName: row.relationDatasetName ?? '',
    optionsJson: stringifyOcFieldOptions(row.options),
    isSensitive: Boolean(row.isSensitive),
  };
  errorLocal.value = null;
  dialog.value = true;
}

function openDelete(row: OpField) {
  deleteTarget.value = row;
  deleteDialog.value = true;
}

onMounted(() => {
  void loadFields();
});

async function submitForm() {
  if (!form.value.label.trim()) return;
  if (!editId.value && !form.value.key.trim()) return;

  const body = buildPayload();
  if (!body) return;

  saving.value = true;
  errorLocal.value = null;
  try {
    if (editId.value) {
      await ocUpdateField(editId.value, body);
    } else {
      await ocCreateField(body);
    }
    dialog.value = false;
    await loadFields();
  } catch (e: unknown) {
    errorLocal.value =
      e instanceof Error ? e.message : t('operationCore.definitions.fields.saveError');
  } finally {
    saving.value = false;
  }
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await ocDeleteField(deleteTarget.value.__dataId);
    deleteDialog.value = false;
    deleteTarget.value = null;
    await loadFields();
  } catch (e: unknown) {
    errorLocal.value =
      e instanceof Error ? e.message : t('operationCore.definitions.fields.deleteError');
    deleteDialog.value = false;
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="oc-fields-tab pa-4 pa-md-6">
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

    <section class="mb-8">
      <h3 class="text-subtitle-1 font-weight-medium mb-1">
        {{ t('operationCore.definitions.fields.coreTitle') }}
      </h3>
      <p class="text-body-2 text-medium-emphasis mb-4">
        {{ t('operationCore.definitions.fields.coreSubtitle') }}
      </p>
      <v-card variant="outlined" rounded="lg">
        <v-data-table
          :headers="coreHeaders"
          :items="coreRows"
          :group-by="coreGroupBy"
          class="oc-core-fields-table"
          density="comfortable"
        >
          <template #[`group-header`]="{ item, columns, toggleGroup, isGroupOpen }">
            <tr>
              <td :colspan="columns.length">
                <v-btn
                  variant="text"
                  size="small"
                  class="text-none font-weight-medium"
                  @click="toggleGroup(item)"
                >
                  <v-icon
                    :icon="isGroupOpen(item) ? 'mdi-chevron-down' : 'mdi-chevron-right'"
                    start
                  />
                  {{ groupLabel(String(item.value)) }}
                </v-btn>
              </td>
            </tr>
          </template>
          <template #[`item.fieldType`]="{ item }">
            <code class="text-caption">{{ item.fieldType }}</code>
          </template>
          <template #[`item.group`]="{ item }">
            {{ groupLabel(item.group) }}
          </template>
          <template #[`item.scope`]="{ item }">
            <v-chip size="small" variant="tonal" color="secondary" rounded="lg" class="text-none">
              {{ item.scope }}
            </v-chip>
          </template>
        </v-data-table>
      </v-card>
    </section>

    <section>
      <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
        <div>
          <h3 class="text-subtitle-1 font-weight-medium mb-1">
            {{ t('operationCore.definitions.fields.poolTitle') }}
          </h3>
          <p class="text-body-2 text-medium-emphasis mb-0">
            {{ t('operationCore.definitions.fields.poolSubtitle') }}
          </p>
        </div>
        <v-btn color="primary" rounded="lg" class="text-none" @click="openCreate">
          <v-icon icon="mdi-plus" start />
          {{ t('operationCore.definitions.fields.newField') }}
        </v-btn>
      </div>

      <v-card variant="outlined" rounded="lg">
        <v-data-table
          :headers="poolHeaders"
          :items="poolFields"
          :loading="loading"
          :group-by="poolGroupBy"
          :sort-by="poolSortBy"
          class="oc-pool-fields-table"
        >
          <template #[`group-header`]="{ item, columns, toggleGroup, isGroupOpen }">
            <tr>
              <td :colspan="columns.length">
                <v-btn
                  variant="text"
                  size="small"
                  class="text-none font-weight-medium"
                  @click="toggleGroup(item)"
                >
                  <v-icon
                    :icon="isGroupOpen(item) ? 'mdi-chevron-down' : 'mdi-chevron-right'"
                    start
                  />
                  {{ categoryLabel(String(item.value)) }}
                </v-btn>
              </td>
            </tr>
          </template>
          <template #[`item.key`]="{ item }">
            <code class="text-caption">{{ item.key }}</code>
          </template>
          <template #[`item.fieldType`]="{ item }">
            <code class="text-caption">{{ item.fieldType }}</code>
          </template>
          <template #[`item.category`]="{ item }">
            {{ categoryLabel(item.category) }}
          </template>
          <template #[`item.cardinality`]="{ item }">
            {{ cardinalityLabel(item.cardinality) }}
          </template>
          <template #[`item.description`]="{ item }">
            <span class="text-truncate d-inline-block" style="max-width: 220px">
              {{ item.description || '—' }}
            </span>
          </template>
          <template #[`item.sortOrder`]="{ item }">
            <span>{{ item.sortOrder ?? '—' }}</span>
          </template>
          <template #[`item.isSensitive`]="{ item }">
            <v-icon
              v-if="item.isSensitive"
              icon="mdi-shield-lock-outline"
              size="18"
              color="warning"
            />
            <span v-else class="text-medium-emphasis">—</span>
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
    </section>

    <v-dialog v-model="dialog" max-width="640">
      <v-card rounded="xl">
        <v-card-title class="text-h6">
          {{
            editId
              ? t('operationCore.definitions.fields.editField')
              : t('operationCore.definitions.fields.newField')
          }}
        </v-card-title>
        <v-card-text>
          <v-alert v-if="optionsError" type="warning" variant="tonal" density="compact" class="mb-4">
            {{ optionsError }}
          </v-alert>
          <v-text-field
            v-model="form.key"
            :label="t('operationCore.definitions.fields.fieldKey')"
            :hint="t('operationCore.definitions.fields.keyHint')"
            persistent-hint
            density="comfortable"
            :disabled="!!editId"
            required
          />
          <v-text-field
            v-model="form.label"
            class="mt-3"
            :label="t('operationCore.definitions.fields.fieldLabel')"
            density="comfortable"
            required
          />
          <v-select
            v-model="form.fieldType"
            class="mt-3"
            :items="fieldTypeItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.definitions.fields.fieldFieldType')"
            density="comfortable"
          />
          <v-select
            v-model="form.category"
            class="mt-3"
            :items="categoryItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.definitions.fields.fieldCategory')"
            density="comfortable"
          />
          <v-select
            v-model="form.cardinality"
            class="mt-3"
            :items="cardinalityItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.definitions.fields.fieldCardinality')"
            density="comfortable"
          />
          <v-text-field
            v-if="showRelationDataset"
            v-model="form.relationDatasetName"
            class="mt-3"
            :label="t('operationCore.definitions.fields.fieldRelationDataset')"
            :hint="t('operationCore.definitions.fields.relationDatasetHint')"
            persistent-hint
            density="comfortable"
          />
          <v-textarea
            v-model="form.description"
            class="mt-3"
            :label="t('operationCore.definitions.fields.fieldDescription')"
            rows="2"
            auto-grow
            density="comfortable"
            variant="outlined"
          />
          <v-textarea
            v-model="form.optionsJson"
            class="mt-3"
            :label="t('operationCore.definitions.fields.fieldOptions')"
            :hint="fieldOptionsHintText"
            persistent-hint
            rows="4"
            auto-grow
            density="comfortable"
            variant="outlined"
          />
          <v-text-field
            v-model="form.sortOrder"
            class="mt-3"
            type="number"
            :label="t('operationCore.definitions.fields.fieldSortOrder')"
            density="comfortable"
          />
          <v-checkbox
            v-model="form.isSensitive"
            class="mt-1"
            :label="t('operationCore.definitions.fields.fieldSensitive')"
            density="comfortable"
            hide-details
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
            :loading="saving"
            :disabled="!form.label.trim() || (!editId && !form.key.trim())"
            @click="submitForm"
          >
            {{ t('operationCore.definitions.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="xl">
        <v-card-title>{{ t('operationCore.definitions.fields.deleteTitle') }}</v-card-title>
        <v-card-text>
          {{ t('operationCore.definitions.fields.deleteBody') }}
          <strong v-if="deleteTarget">{{ deleteTarget.label }} ({{ deleteTarget.key }})</strong>
        </v-card-text>
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
