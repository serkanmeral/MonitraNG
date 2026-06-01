<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcWorkspaceBoardColumnEditor from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceBoardColumnEditor.vue';
import OcWorkspaceBoardListScopeEditor from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceBoardListScopeEditor.vue';
import type {
  OpBoard,
  OpBoardColumnConfig,
  OpBoardListColumnConfig,
  OpBoardSortConfig,
  OpField,
  OpPriority,
  OpState,
  OpStateFlow,
  OpWorkItemType,
} from '@/types/apps/operationCore';
import { OC_BOARD_VIEW_TYPE_VALUES } from '@/types/apps/operationCore';
import { suggestBoardColumnsFromFlow } from '@/utils/ocBoardColumns';
import {
  boardListColumnKeys,
  buildListScopeColumns,
  deriveBoardListColumns,
  suggestListScopeStateIdsFromFlow,
} from '@/utils/ocBoardListColumns';

export type OcBoardFormModel = {
  name: string;
  viewType: string;
  defaultFormId: string;
  defaultDashboardId: string;
  defaultStateFlowId: string;
  defaultProfileId: string;
  defaultTypeId: string;
  defaultPriorityId: string;
  defaultStateId: string;
  listColumns: OpBoardListColumnConfig[];
  defaultSort: OpBoardSortConfig | null;
  viewGroups: string[];
  editGroups: string[];
  columns: OpBoardColumnConfig[];
};

const props = defineProps<{
  modelValue: boolean;
  editId: string | null;
  workspaceId: string;
  stateFlows: OpStateFlow[];
  formItems: { value: string; title: string }[];
  dashboardItems: { value: string; title: string }[];
  flowItems: { value: string; title: string }[];
  stateItems: { value: string; title: string }[];
  stateCatalog?: OpState[];
  priorityCatalog?: OpPriority[];
  typeCatalog?: OpWorkItemType[];
  fieldCatalog?: OpField[];
  profileItems: { value: string; title: string }[];
  typeItems: { value: string; title: string }[];
  priorityItems: { value: string; title: string }[];
  groupItems: { value: string; title: string }[];
  enabledStateIds: string[];
  saving?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  save: [Record<string, unknown>];
}>();

const { t } = useAppI18n();

const poolFieldKeys = computed(() =>
  (props.fieldCatalog ?? []).map((f) => f.key).filter((k): k is string => !!k?.trim())
);

const form = ref<OcBoardFormModel>(emptyForm());
const validationError = ref<string | null>(null);
const validationAlertRef = ref<HTMLElement | null>(null);
const advancedOpen = ref<number | undefined>(undefined);

const dialogOpen = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const viewTypeItems = computed(() =>
  OC_BOARD_VIEW_TYPE_VALUES.map((value) => ({
    value,
    title: t(`operationCore.workspaceDefinitions.boards.viewType.${value}`),
    icon: value === 'kanban' ? 'mdi-view-column' : 'mdi-format-list-bulleted',
    hint: t(`operationCore.workspaceDefinitions.boards.viewTypeHint.${value}`),
  }))
);

const isListView = computed(() => form.value.viewType !== 'kanban');

const stateTitleById = computed(
  () => new Map(props.stateItems.map((s) => [s.value, s.title]))
);

const previewLines = computed(() => {
  const cols = form.value.columns.filter((c) => c.stateId);
  const colNames = cols
    .map((c) => c.title?.trim() || stateTitleById.value.get(c.stateId) || '?')
    .join(' → ');
  return [
    {
      icon: 'mdi-view-dashboard-outline',
      text: form.value.name.trim() || t('operationCore.workspaceDefinitions.boards.previewNameEmpty'),
    },
    {
      icon: 'mdi-eye-outline',
      text: t(`operationCore.workspaceDefinitions.boards.viewType.${form.value.viewType || 'list'}`),
    },
    {
      icon: 'mdi-transit-connection-variant',
      text:
        props.flowItems.find((f) => f.value === form.value.defaultStateFlowId)?.title ??
        t('operationCore.workspaceDefinitions.boards.previewFlowEmpty'),
    },
    {
      icon: isListView.value ? 'mdi-table-column' : 'mdi-view-column-outline',
      text: isListView.value
        ? t('operationCore.workspaceDefinitions.boards.previewListTable', {
            cols: boardListColumnKeys(form.value.listColumns).join(', '),
          })
        : colNames || t('operationCore.workspaceDefinitions.boards.previewColumnsEmpty'),
    },
  ];
});

function emptyForm(): OcBoardFormModel {
  return {
    name: '',
    viewType: 'list',
    defaultFormId: '',
    defaultDashboardId: '',
    defaultStateFlowId: '',
    defaultProfileId: '',
    defaultTypeId: '',
    defaultPriorityId: '',
    defaultStateId: '',
    listColumns: deriveBoardListColumns(null, null),
    defaultSort: null,
    viewGroups: [],
    editGroups: [],
    columns: [],
  };
}

function buildPayload(): Record<string, unknown> {
  const columns = form.value.columns
    .filter((c) => c.stateId)
    .map((c) => ({
      stateId: c.stateId,
      title: c.title?.trim() || null,
      queryKey: c.queryKey?.trim() || 'wi_board_column',
      defaultTransitionKey: c.defaultTransitionKey?.trim() || null,
    }));

  const listColumns = deriveBoardListColumns(form.value.listColumns, null, poolFieldKeys.value);
  // visibleFields = DG'den çekilecek gerçek alanlar; computed sütunlar hariç tutulur.
  const listColumnKeys = boardListColumnKeys(listColumns.filter((c) => !c.computed));
  const defaultSort =
    form.value.defaultSort?.field && listColumns.some((c) => c.key === form.value.defaultSort?.field && c.sortable)
      ? { field: form.value.defaultSort.field, direction: form.value.defaultSort.direction }
      : null;

  return {
    name: form.value.name.trim(),
    workspaceId: props.workspaceId,
    viewType: form.value.viewType || 'list',
    defaultFormId: form.value.defaultFormId || null,
    defaultDashboardId: form.value.defaultDashboardId || null,
    defaultStateFlowId: form.value.defaultStateFlowId || null,
    defaultProfileId: form.value.defaultProfileId || null,
    defaultTypeId: form.value.defaultTypeId || null,
    defaultPriorityId: form.value.defaultPriorityId || null,
    defaultStateId: form.value.defaultStateId || null,
    visibleFields: listColumnKeys,
    viewGroups: form.value.viewGroups.length ? form.value.viewGroups : null,
    editGroups: form.value.editGroups.length ? form.value.editGroups : null,
    config: { columns, listColumns, defaultSort },
  };
}

function setFormFromBoard(row: OpBoard | null, defaults?: Partial<OcBoardFormModel>) {
  if (row) {
    form.value = {
      name: row.name,
      viewType: row.viewType ?? 'list',
      defaultFormId: row.defaultFormId ?? '',
      defaultDashboardId: row.defaultDashboardId ?? '',
      defaultStateFlowId: row.defaultStateFlowId ?? '',
      defaultProfileId: row.defaultProfileId ?? '',
      defaultTypeId: row.defaultTypeId ?? '',
      defaultPriorityId: row.defaultPriorityId ?? '',
      defaultStateId: row.defaultStateId ?? '',
      listColumns: deriveBoardListColumns(row.listColumns, row.visibleFields, poolFieldKeys.value),
      defaultSort: row.defaultSort ? { ...row.defaultSort } : null,
      viewGroups: [...row.viewGroups],
      editGroups: [...row.editGroups],
      columns: row.columns.length ? row.columns.map((c) => ({ ...c })) : [],
    };
  } else {
    const next = {
      ...emptyForm(),
      defaultFormId: props.formItems[0]?.value ?? '',
      defaultStateFlowId: props.flowItems[0]?.value ?? '',
      defaultTypeId: props.typeItems[0]?.value ?? '',
      defaultPriorityId: props.priorityItems[0]?.value ?? '',
      defaultStateId: props.stateItems[0]?.value ?? '',
      defaultProfileId: props.profileItems[0]?.value ?? '',
      ...defaults,
    };
    const titleMap = new Map(props.stateItems.map((s) => [s.value, s.title]));
    const flow = props.stateFlows.find((f) => f.__dataId === next.defaultStateFlowId);
    if (flow) {
      if (next.viewType === 'kanban') {
        next.columns = suggestBoardColumnsFromFlow(flow, titleMap);
      } else {
        next.columns = buildListScopeColumns(
          suggestListScopeStateIdsFromFlow(flow),
          titleMap
        );
      }
    }
    form.value = next;
  }
  validationError.value = null;
  advancedOpen.value = undefined;
}

function validate(): string | null {
  if (!form.value.name.trim()) {
    return t('operationCore.workspaceDefinitions.boards.validationName');
  }
  if (!form.value.defaultStateFlowId) {
    return t('operationCore.workspaceDefinitions.boards.validationFlow');
  }
  if (form.value.columns.filter((c) => c.stateId).length === 0) {
    return t('operationCore.workspaceDefinitions.boards.columnsRequired');
  }
  return null;
}

function scrollValidationIntoView() {
  requestAnimationFrame(() => {
    validationAlertRef.value?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  });
}

function submit() {
  validationError.value = validate();
  if (validationError.value) {
    scrollValidationIntoView();
    return;
  }
  emit('save', buildPayload());
}

watch(
  () => form.value.defaultStateFlowId,
  () => {
    const flow = props.stateFlows.find((f) => f.__dataId === form.value.defaultStateFlowId);
    if (!flow) return;
    for (const col of form.value.columns) {
      if (!col.defaultTransitionKey) continue;
      const valid = flow.transitions.some(
        (tr) => tr.fromStateId === col.stateId && tr.transitionKey === col.defaultTransitionKey
      );
      if (!valid) col.defaultTransitionKey = null;
    }
  }
);

defineExpose({ setFormFromBoard, emptyForm });
</script>

<template>
  <v-dialog v-model="dialogOpen" max-width="1040" scrollable persistent>
    <v-card rounded="lg" class="oc-board-dialog">
      <v-card-title class="d-flex align-start gap-3 pt-5 px-5 pb-2">
        <v-avatar color="primary" variant="tonal" size="44" rounded="lg">
          <v-icon icon="mdi-view-dashboard-outline" size="24" />
        </v-avatar>
        <div class="flex-grow-1 min-width-0">
          <div class="text-h6 font-weight-bold">
            {{
              editId
                ? t('operationCore.workspaceDefinitions.boards.editBoard')
                : t('operationCore.workspaceDefinitions.boards.newBoard')
            }}
          </div>
          <p class="text-body-2 text-medium-emphasis mb-0 mt-1">
            {{ t('operationCore.workspaceDefinitions.boards.dialogIntro') }}
          </p>
        </div>
      </v-card-title>

      <v-divider />

      <v-card-text class="px-5 py-4">
        <div v-if="validationError" ref="validationAlertRef">
          <v-alert
            type="warning"
            variant="tonal"
            density="compact"
            class="mb-4 rounded-lg"
            closable
            @click:close="validationError = null"
          >
            {{ validationError }}
          </v-alert>
        </div>

        <v-row dense>
          <v-col cols="12" lg="7">
            <section class="oc-board-dialog__section mb-5">
              <div class="oc-board-dialog__section-head mb-3">
                <span class="oc-board-dialog__step">1</span>
                <div>
                  <div class="text-subtitle-1 font-weight-bold">
                    {{ t('operationCore.workspaceDefinitions.boards.sectionBasics') }}
                  </div>
                  <p class="text-body-2 text-medium-emphasis mb-0">
                    {{ t('operationCore.workspaceDefinitions.boards.sectionBasicsHint') }}
                  </p>
                </div>
              </div>

              <v-text-field
                v-model="form.name"
                :label="t('operationCore.workspaceDefinitions.boards.fieldName')"
                :placeholder="t('operationCore.workspaceDefinitions.boards.fieldNamePlaceholder')"
                variant="outlined"
                density="comfortable"
                class="mb-3"
              />

              <div class="text-caption text-medium-emphasis mb-2">
                {{ t('operationCore.workspaceDefinitions.boards.fieldViewType') }}
              </div>
              <v-item-group v-model="form.viewType" mandatory class="d-flex flex-wrap gap-2 mb-3">
                <v-item v-for="vt in viewTypeItems" :key="vt.value" v-slot="{ isSelected, toggle }" :value="vt.value">
                  <v-card
                    :variant="isSelected ? 'flat' : 'outlined'"
                    :color="isSelected ? 'primary' : undefined"
                    rounded="lg"
                    class="oc-board-dialog__view-card pa-3 cursor-pointer flex-grow-1"
                    min-width="140"
                    @click="toggle"
                  >
                    <div class="d-flex align-center gap-2">
                      <v-icon :icon="vt.icon" size="22" />
                      <div>
                        <div class="text-body-2 font-weight-bold">{{ vt.title }}</div>
                        <div
                          class="text-caption"
                          :class="isSelected ? 'text-white text-opacity-80' : 'text-medium-emphasis'"
                        >
                          {{ vt.hint }}
                        </div>
                      </div>
                    </div>
                  </v-card>
                </v-item>
              </v-item-group>

              <v-select
                v-model="form.defaultStateFlowId"
                :items="flowItems"
                item-title="title"
                item-value="value"
                :label="t('operationCore.workspaceDefinitions.boards.fieldDefaultFlow')"
                :hint="t('operationCore.workspaceDefinitions.boards.fieldDefaultFlowHint')"
                persistent-hint
                variant="outlined"
                density="comfortable"
              />
            </section>

            <section class="oc-board-dialog__section mb-4">
              <div class="oc-board-dialog__section-head mb-3">
                <span class="oc-board-dialog__step">2</span>
                <div>
                  <div class="text-subtitle-1 font-weight-bold">
                    {{
                      isListView
                        ? t('operationCore.workspaceDefinitions.boards.sectionListScope')
                        : t('operationCore.workspaceDefinitions.boards.sectionColumns')
                    }}
                  </div>
                  <p class="text-body-2 text-medium-emphasis mb-0">
                    {{
                      isListView
                        ? t('operationCore.workspaceDefinitions.boards.sectionListScopeHint')
                        : t('operationCore.workspaceDefinitions.boards.sectionColumnsHint')
                    }}
                  </p>
                </div>
              </div>

              <OcWorkspaceBoardListScopeEditor
                v-if="isListView"
                v-model:columns="form.columns"
                v-model:list-columns="form.listColumns"
                v-model:default-sort="form.defaultSort"
                :state-flow-id="form.defaultStateFlowId"
                :state-flows="stateFlows"
                :state-items="stateItems"
                :enabled-state-ids="enabledStateIds"
                :state-catalog="stateCatalog"
                :priority-catalog="priorityCatalog"
                :type-catalog="typeCatalog"
                :field-catalog="fieldCatalog"
              />

              <template v-else>
                <v-alert type="info" variant="tonal" density="compact" class="rounded-lg mb-4">
                  {{ t('operationCore.workspaceDefinitions.boards.kanbanConfigNotice') }}
                </v-alert>
                <OcWorkspaceBoardColumnEditor
                  v-model:columns="form.columns"
                  :state-flow-id="form.defaultStateFlowId"
                  :state-flows="stateFlows"
                  :state-items="stateItems"
                  :enabled-state-ids="enabledStateIds"
                  :view-type="form.viewType"
                />
              </template>
            </section>

            <v-expansion-panels v-model="advancedOpen" variant="accordion" class="oc-board-dialog__advanced">
              <v-expansion-panel>
                <v-expansion-panel-title>
                  <div class="d-flex align-center gap-2">
                    <v-icon icon="mdi-tune-variant" size="20" />
                    <span>{{ t('operationCore.workspaceDefinitions.boards.sectionAdvanced') }}</span>
                  </div>
                </v-expansion-panel-title>
                <v-expansion-panel-text>
                  <p class="text-body-2 text-medium-emphasis mb-4">
                    {{ t('operationCore.workspaceDefinitions.boards.sectionAdvancedHint') }}
                  </p>

                  <div class="text-subtitle-2 font-weight-medium mb-2">
                    {{ t('operationCore.workspaceDefinitions.boards.defaultsSection') }}
                  </div>
                  <v-row dense class="mb-4">
                    <v-col cols="12" md="6">
                      <v-select
                        v-model="form.defaultFormId"
                        :items="formItems"
                        item-title="title"
                        item-value="value"
                        :label="t('operationCore.workspaceDefinitions.boards.fieldDefaultForm')"
                        variant="outlined"
                        density="compact"
                        clearable
                      />
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-select
                        v-model="form.defaultDashboardId"
                        :items="dashboardItems"
                        item-title="title"
                        item-value="value"
                        :label="t('operationCore.workspaceDefinitions.boards.fieldDefaultDashboard')"
                        :no-data-text="t('operationCore.workspaceDefinitions.boards.dashboardNoData')"
                        prepend-inner-icon="mdi-view-dashboard-outline"
                        variant="outlined"
                        density="compact"
                        clearable
                      />
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-select
                        v-model="form.defaultProfileId"
                        :items="profileItems"
                        item-title="title"
                        item-value="value"
                        :label="t('operationCore.workspaceDefinitions.boards.fieldDefaultProfile')"
                        variant="outlined"
                        density="compact"
                        clearable
                      />
                    </v-col>
                    <v-col cols="12" md="4">
                      <v-select
                        v-model="form.defaultTypeId"
                        :items="typeItems"
                        item-title="title"
                        item-value="value"
                        :label="t('operationCore.workspaceDefinitions.boards.fieldDefaultType')"
                        variant="outlined"
                        density="compact"
                        clearable
                      />
                    </v-col>
                    <v-col cols="12" md="4">
                      <v-select
                        v-model="form.defaultPriorityId"
                        :items="priorityItems"
                        item-title="title"
                        item-value="value"
                        :label="t('operationCore.workspaceDefinitions.boards.fieldDefaultPriority')"
                        variant="outlined"
                        density="compact"
                        clearable
                      />
                    </v-col>
                    <v-col cols="12" md="4">
                      <v-select
                        v-model="form.defaultStateId"
                        :items="stateItems"
                        item-title="title"
                        item-value="value"
                        :label="t('operationCore.workspaceDefinitions.boards.fieldDefaultState')"
                        variant="outlined"
                        density="compact"
                        clearable
                      />
                    </v-col>
                  </v-row>

                  <div class="text-subtitle-2 font-weight-medium mb-2">
                    {{ t('operationCore.workspaceDefinitions.boards.accessSection') }}
                  </div>
                  <v-row dense>
                    <v-col cols="12" md="6">
                      <v-select
                        v-model="form.viewGroups"
                        :items="groupItems"
                        item-title="title"
                        item-value="value"
                        :label="t('operationCore.workspaceDefinitions.boards.fieldViewGroups')"
                        variant="outlined"
                        density="compact"
                        multiple
                        chips
                        closable-chips
                        clearable
                      />
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-select
                        v-model="form.editGroups"
                        :items="groupItems"
                        item-title="title"
                        item-value="value"
                        :label="t('operationCore.workspaceDefinitions.boards.fieldEditGroups')"
                        variant="outlined"
                        density="compact"
                        multiple
                        chips
                        closable-chips
                        clearable
                      />
                    </v-col>
                  </v-row>
                </v-expansion-panel-text>
              </v-expansion-panel>
            </v-expansion-panels>
          </v-col>

          <v-col cols="12" lg="5">
            <v-card variant="tonal" color="primary" rounded="lg" class="oc-board-dialog__preview pa-4 sticky-preview">
              <div class="text-subtitle-1 font-weight-bold mb-3">
                {{ t('operationCore.workspaceDefinitions.boards.livePreview') }}
              </div>
              <div
                v-for="(line, idx) in previewLines"
                :key="idx"
                class="d-flex align-start gap-2 mb-3"
              >
                <v-icon :icon="line.icon" size="20" class="mt-1 flex-shrink-0" />
                <span class="text-body-2">{{ line.text }}</span>
              </div>
            </v-card>
          </v-col>
        </v-row>
      </v-card-text>

      <v-divider />

      <v-card-actions class="px-5 py-4">
        <v-spacer />
        <v-btn variant="text" class="text-none" @click="dialogOpen = false">
          {{ t('operationCore.definitions.cancel') }}
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          rounded="lg"
          class="text-none"
          :loading="saving"
          @click="submit"
        >
          {{ t('operationCore.definitions.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.oc-board-dialog__section-head {
  display: flex;
  gap: 0.75rem;
  align-items: flex-start;
}

.oc-board-dialog__step {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 999px;
  font-size: 0.8125rem;
  font-weight: 700;
  flex-shrink: 0;
  background: rgba(var(--v-theme-primary), 0.14);
  color: rgb(var(--v-theme-primary));
}

.oc-board-dialog__view-card {
  max-width: 220px;
}

.sticky-preview {
  position: sticky;
  top: 0.5rem;
}

.cursor-pointer {
  cursor: pointer;
}

@media (max-width: 1279px) {
  .sticky-preview {
    position: static;
  }
}
</style>
