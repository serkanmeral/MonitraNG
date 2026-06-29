<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useOcWorkspaceCatalogInject } from '@/composables/useOcWorkspaceCatalog';
import OcDashboardWidgetForm from '@/components/apps/operation-core/dashboards/OcDashboardWidgetForm.vue';
import OcDashboardLayoutEditor from '@/components/apps/operation-core/dashboards/OcDashboardLayoutEditor.vue';
import {
  ocCreateDashboard,
  ocDeleteDashboard,

  ocGetBoard,
  ocGetDashboardRecord,
  ocListBoardsForWorkspace,
  ocListDashboardsForWorkspace,
  ocSetBoardDefaultDashboard,
  ocUpdateDashboard,
} from '@/services/operationCoreService';
import type { OpBoard } from '@/types/apps/operationCore';
import type {
  OcDashboardLayout,
  OcDashboardListItem,
  OcDashboardWidgetDef,
  OpPriority,
  OpState,
} from '@/types/apps/operationCore';
import { buildSummaryCardConfig } from '@/utils/ocDashboardWidgetStyle';

const props = defineProps<{ workspaceId: string }>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const catalog = useOcWorkspaceCatalogInject();

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);

const dashboards = ref<OcDashboardListItem[]>([]);
const workspaceBoards = ref<OpBoard[]>([]);
const states = ref<OpState[]>([]);
const priorities = ref<OpPriority[]>([]);

const linkBoardId = ref('');
const linkingBoard = ref(false);

// Editor state
const editorOpen = ref(false);
const editingId = ref<string | null>(null);
const editorForm = ref<{ name: string; description: string; isDefault: boolean; isActive: boolean }>({
  name: '',
  description: '',
  isDefault: false,
  isActive: true,
});
const editorWidgets = ref<OcDashboardWidgetDef[]>([]);
const editorLayout = ref<OcDashboardLayout>({ type: 'rows', rows: [] });

// Widget form state
const widgetFormOpen = ref(false);
const editingWidget = ref<OcDashboardWidgetDef | null>(null);

// Delete state
const deleteDialogOpen = ref(false);
const deleteTarget = ref<OcDashboardListItem | null>(null);

const widgetKeys = computed(() => editorWidgets.value.map((w) => w.key));

const linkedBoards = computed(() => {
  if (!editingId.value) return [];
  return workspaceBoards.value.filter((b) => b.defaultDashboardId === editingId.value);
});

const linkableBoardItems = computed(() =>
  workspaceBoards.value
    .filter((b) => b.defaultDashboardId !== editingId.value)
    .map((b) => ({ value: b.__dataId, title: b.name }))
);

async function linkBoardToDashboard() {
  const boardId = linkBoardId.value?.trim();
  const dashId = editingId.value;
  if (!boardId || !dashId) return;
  linkingBoard.value = true;
  errorLocal.value = null;
  try {
    const board = (await ocGetBoard(boardId)) ?? workspaceBoards.value.find((b) => b.__dataId === boardId);
    if (!board) {
      errorLocal.value = t('operationCore.dashboards.editor.linkBoardError');
      return;
    }
    await ocSetBoardDefaultDashboard(board, dashId);
    linkBoardId.value = '';
    workspaceBoards.value = await ocListBoardsForWorkspace(props.workspaceId);
    successLocal.value = t('operationCore.dashboards.editor.linkBoardSuccess');
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.dashboards.editor.linkBoardError');
  } finally {
    linkingBoard.value = false;
  }
}

async function unlinkBoardFromDashboard(board: OpBoard) {
  linkingBoard.value = true;
  errorLocal.value = null;
  try {
    await ocSetBoardDefaultDashboard(board, null);
    workspaceBoards.value = await ocListBoardsForWorkspace(props.workspaceId);
    successLocal.value = t('operationCore.dashboards.editor.unlinkBoardSuccess');
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.dashboards.editor.linkBoardError');
  } finally {
    linkingBoard.value = false;
  }
}

async function loadAll() {
  if (!props.workspaceId) return;
  loading.value = true;
  errorLocal.value = null;
  try {
    const [dash, boards] = await Promise.all([
      ocListDashboardsForWorkspace(props.workspaceId),
      ocListBoardsForWorkspace(props.workspaceId),
      catalog.whenReady(),
    ]);
    dashboards.value = dash;
    workspaceBoards.value = boards;
    states.value = catalog.states.value;
    priorities.value = catalog.priorities.value;
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.dashboards.editor.loadError');
  } finally {
    loading.value = false;
  }
}

watch(() => props.workspaceId, () => void loadAll(), { immediate: true });

function openCreate() {
  editingId.value = null;
  editorForm.value = { name: '', description: '', isDefault: false, isActive: true };
  editorWidgets.value = [];
  editorLayout.value = { type: 'rows', rows: [] };
  editorOpen.value = true;
}

async function openEdit(item: OcDashboardListItem) {
  errorLocal.value = null;
  try {
    const rec = await ocGetDashboardRecord(item.id);
    if (!rec) {
      errorLocal.value = t('operationCore.dashboards.editor.loadError');
      return;
    }
    editingId.value = rec.id;
    editorForm.value = {
      name: rec.name,
      description: rec.description ?? '',
      isDefault: rec.isDefault,
      isActive: rec.isActive,
    };
    editorWidgets.value = rec.widgets.map((w) => ({ ...w }));
    editorLayout.value = rec.layout?.rows
      ? { type: 'rows', rows: JSON.parse(JSON.stringify(rec.layout.rows)) }
      : { type: 'rows', rows: [] };
    editorOpen.value = true;
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.dashboards.editor.loadError');
  }
}

function openAddWidget() {
  editingWidget.value = null;
  widgetFormOpen.value = true;
}

function openEditWidget(w: OcDashboardWidgetDef) {
  editingWidget.value = w;
  widgetFormOpen.value = true;
}

function onWidgetSaved(widget: OcDashboardWidgetDef) {
  const prevKey = editingWidget.value?.key;
  if (prevKey) {
    const idx = editorWidgets.value.findIndex((w) => w.key === prevKey);
    if (idx >= 0) editorWidgets.value.splice(idx, 1, widget);
    if (prevKey !== widget.key) renameWidgetInLayout(prevKey, widget.key);
  } else {
    editorWidgets.value.push(widget);
  }
  editingWidget.value = null;
}

function removeWidget(w: OcDashboardWidgetDef) {
  editorWidgets.value = editorWidgets.value.filter((x) => x.key !== w.key);
  clearWidgetFromLayout(w.key);
}

function renameWidgetInLayout(oldKey: string, newKey: string) {
  const walk = (rows: OcDashboardLayout['rows']) => {
    for (const r of rows) {
      for (const c of r.cols) {
        if (c.widgetId === oldKey) c.widgetId = newKey;
        if (c.rows) walk(c.rows);
      }
    }
  };
  walk(editorLayout.value.rows);
  editorLayout.value = { type: 'rows', rows: [...editorLayout.value.rows] };
}

function clearWidgetFromLayout(key: string) {
  const walk = (rows: OcDashboardLayout['rows']) => {
    for (const r of rows) {
      for (const c of r.cols) {
        if (c.widgetId === key) c.widgetId = undefined;
        if (c.rows) walk(c.rows);
      }
    }
  };
  walk(editorLayout.value.rows);
  editorLayout.value = { type: 'rows', rows: [...editorLayout.value.rows] };
}

function widgetToRaw(w: OcDashboardWidgetDef): Record<string, unknown> {
  const raw: Record<string, unknown> = {
    key: w.key,
    type: w.type,
    dataset: w.dataset || 'op_work_items',
    queryKey: w.queryKey || null,
  };
  if (w.title) raw.title = w.title;
  if (w.parameters && Object.keys(w.parameters).length) raw.parameters = w.parameters;
  if (w.take != null) raw.take = w.take;
  if (w.type === 'chart') {
    if (w.chartType) raw.chartType = w.chartType;
    if (w.groupBy) raw.groupBy = w.groupBy;
  }
  if (w.type === 'summaryCard') {
    const cfg = buildSummaryCardConfig(w.accentColor ?? null, w.icon ?? null);
    if (cfg) raw.config = cfg;
  }
  return raw;
}

async function saveDashboard() {
  const name = editorForm.value.name.trim();
  if (!name) {
    errorLocal.value = t('operationCore.dashboards.editor.nameRequired');
    return;
  }
  saving.value = true;
  errorLocal.value = null;
  successLocal.value = null;
  try {
    const body: Record<string, unknown> = {
      name,
      description: editorForm.value.description.trim() || null,
      workspaceId: props.workspaceId,
      scope: 'workspace',
      isDefault: editorForm.value.isDefault,
      isActive: editorForm.value.isActive,
      layout: { type: 'rows', rows: editorLayout.value.rows },
      widgets: editorWidgets.value.map(widgetToRaw),
    };
    if (editingId.value) {
      await ocUpdateDashboard(editingId.value, body);
    } else {
      await ocCreateDashboard(body);
    }
    editorOpen.value = false;
    await loadAll();
    successLocal.value = t('operationCore.workspaceDefinitions.saveSuccess');
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.dashboards.editor.saveError');
  } finally {
    saving.value = false;
  }
}

function confirmDelete(item: OcDashboardListItem) {
  deleteTarget.value = item;
  deleteDialogOpen.value = true;
}

async function doDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await ocDeleteDashboard(deleteTarget.value.id);
    deleteDialogOpen.value = false;
    deleteTarget.value = null;
    await loadAll();
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.dashboards.editor.deleteError');
  } finally {
    deleting.value = false;
  }
}

function widgetTypeColor(type: string): string {
  if (type === 'chart') return 'deep-purple';
  if (type === 'summaryCard') return 'teal';
  return 'primary';
}
</script>

<template>
  <div class="oc-ws-dashboards-tab pa-4 pa-md-6">
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

    <div class="d-flex align-start justify-space-between ga-3 flex-wrap mb-4">
      <div>
        <h3 class="text-subtitle-1 font-weight-medium mb-1">
          {{ t('operationCore.dashboards.editor.title') }}
        </h3>
        <p class="text-body-2 text-medium-emphasis mb-0">
          {{ t('operationCore.dashboards.editor.subtitle') }}
        </p>
      </div>
      <v-btn color="primary" rounded="lg" class="text-none" prepend-icon="mdi-plus" @click="openCreate">
        {{ t('operationCore.dashboards.editor.new') }}
      </v-btn>
    </div>

    <div v-if="loading" class="d-flex justify-center py-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <template v-else>
      <v-alert v-if="!dashboards.length" type="info" variant="tonal" density="compact">
        {{ t('operationCore.dashboards.editor.empty') }}
      </v-alert>

      <v-card v-else variant="outlined" rounded="lg">
        <v-list lines="two" density="comfortable">
          <v-list-item
            v-for="(dash, idx) in dashboards"
            :key="dash.id"
            :border="idx > 0 ? 'top' : undefined"
          >
            <template #prepend>
              <v-icon icon="mdi-view-dashboard-outline" color="primary" />
            </template>
            <v-list-item-title class="d-flex align-center ga-2">
              {{ dash.name }}
              <v-chip v-if="dash.isDefault" size="x-small" variant="tonal" color="primary">
                {{ t('operationCore.dashboards.defaultChip') }}
              </v-chip>
              <v-chip v-if="!dash.isActive" size="x-small" variant="tonal" color="warning">
                {{ t('operationCore.dashboards.editor.inactive') }}
              </v-chip>
            </v-list-item-title>
            <v-list-item-subtitle v-if="dash.description">
              {{ dash.description }}
            </v-list-item-subtitle>
            <template #append>
              <v-btn
                icon="mdi-pencil-outline"
                variant="text"
                size="small"
                :aria-label="t('operationCore.dashboards.editor.edit')"
                @click="openEdit(dash)"
              />
              <v-btn
                icon="mdi-delete-outline"
                variant="text"
                size="small"
                color="error"
                :aria-label="t('operationCore.dashboards.editor.delete')"
                @click="confirmDelete(dash)"
              />
            </template>
          </v-list-item>
        </v-list>
      </v-card>
    </template>

    <!-- Editor dialog -->
    <v-dialog v-model="editorOpen" max-width="980" persistent scrollable>
      <v-card rounded="lg">
        <v-card-title class="text-h6 d-flex align-center">
          {{
            editingId
              ? t('operationCore.dashboards.editor.editTitle')
              : t('operationCore.dashboards.editor.newTitle')
          }}
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" @click="editorOpen = false" />
        </v-card-title>
        <v-divider />
        <v-card-text style="max-height: 76vh">
          <!-- Metadata -->
          <div class="d-flex ga-3 flex-wrap mb-3">
            <v-text-field
              v-model="editorForm.name"
              :label="t('operationCore.dashboards.editor.name')"
              variant="outlined"
              density="comfortable"
              style="flex: 1; min-width: 240px"
              hide-details
            />
            <div class="d-flex align-center ga-4">
              <v-switch
                v-model="editorForm.isDefault"
                :label="t('operationCore.dashboards.editor.isDefault')"
                color="primary"
                density="compact"
                hide-details
                inset
              />
              <v-switch
                v-model="editorForm.isActive"
                :label="t('operationCore.dashboards.editor.isActive')"
                color="primary"
                density="compact"
                hide-details
                inset
              />
            </div>
          </div>
          <v-textarea
            v-model="editorForm.description"
            :label="t('operationCore.dashboards.editor.description')"
            variant="outlined"
            density="comfortable"
            rows="2"
            auto-grow
            class="mb-4"
            hide-details
          />

          <v-card v-if="editingId" variant="tonal" class="mb-4 pa-3">
            <div class="text-subtitle-2 font-weight-medium mb-1">
              {{ t('operationCore.dashboards.editor.linkedBoardsTitle') }}
            </div>
            <p class="text-caption text-medium-emphasis mb-3">
              {{ t('operationCore.dashboards.editor.linkedBoardsHint') }}
            </p>
            <div v-if="linkedBoards.length" class="d-flex flex-wrap ga-2 mb-3">
              <v-chip
                v-for="b in linkedBoards"
                :key="b.__dataId"
                size="small"
                variant="tonal"
                color="primary"
                closable
                :disabled="linkingBoard"
                @click:close="unlinkBoardFromDashboard(b)"
              >
                {{ b.name }}
              </v-chip>
            </div>
            <p v-else class="text-caption text-medium-emphasis mb-3">
              {{ t('operationCore.dashboards.editor.noLinkedBoards') }}
            </p>
            <div class="d-flex flex-wrap ga-2 align-end">
              <v-select
                v-model="linkBoardId"
                :items="linkableBoardItems"
                item-title="title"
                item-value="value"
                :label="t('operationCore.dashboards.editor.selectBoard')"
                variant="outlined"
                density="compact"
                hide-details
                clearable
                style="min-width: 220px; flex: 1"
                :disabled="!linkableBoardItems.length || linkingBoard"
              />
              <v-btn
                color="primary"
                variant="flat"
                size="small"
                class="text-none"
                :loading="linkingBoard"
                :disabled="!linkBoardId"
                @click="linkBoardToDashboard"
              >
                {{ t('operationCore.dashboards.editor.linkBoard') }}
              </v-btn>
            </div>
          </v-card>

          <!-- Widgets -->
          <div class="d-flex align-center justify-space-between mb-2">
            <span class="text-subtitle-2 font-weight-medium">
              {{ t('operationCore.dashboards.editor.widgetsTitle') }}
            </span>
            <v-btn size="small" variant="tonal" color="primary" prepend-icon="mdi-plus" @click="openAddWidget">
              {{ t('operationCore.dashboards.editor.addWidget') }}
            </v-btn>
          </div>
          <v-alert
            v-if="!editorWidgets.length"
            type="info"
            variant="tonal"
            density="compact"
            class="mb-4"
          >
            {{ t('operationCore.dashboards.editor.noWidgets') }}
          </v-alert>
          <div v-else class="d-flex flex-wrap ga-2 mb-4">
            <v-card
              v-for="w in editorWidgets"
              :key="w.key"
              variant="outlined"
              class="pa-2 px-3"
              style="flex: 0 0 auto"
            >
              <div class="d-flex align-center ga-2">
                <v-chip size="x-small" variant="tonal" :color="widgetTypeColor(w.type)">{{ w.type }}</v-chip>
                <div class="d-flex flex-column">
                  <span class="text-body-2 font-weight-medium">{{ w.title || w.key }}</span>
                  <span class="text-caption text-medium-emphasis">{{ w.key }} · {{ w.queryKey }}</span>
                </div>
                <v-btn icon="mdi-pencil-outline" variant="text" size="x-small" @click="openEditWidget(w)" />
                <v-btn icon="mdi-close" variant="text" size="x-small" color="error" @click="removeWidget(w)" />
              </div>
            </v-card>
          </div>

          <!-- Layout -->
          <div class="text-subtitle-2 font-weight-medium mb-2">
            {{ t('operationCore.dashboards.editor.layoutTitle') }}
          </div>
          <p class="text-caption text-medium-emphasis mb-2">
            {{ t('operationCore.dashboards.editor.layoutHint') }}
          </p>
          <OcDashboardLayoutEditor
            v-model="editorLayout"
            :widget-keys="widgetKeys"
          />
        </v-card-text>
        <v-divider />
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="editorOpen = false">
            {{ t('operationCore.dashboards.editor.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            class="text-none"
            :loading="saving"
            :disabled="!editorForm.name.trim()"
            @click="saveDashboard"
          >
            {{ t('operationCore.dashboards.editor.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <OcDashboardWidgetForm
      v-model="widgetFormOpen"
      :widget="editingWidget"
      :existing-keys="widgetKeys"
      :workspace-id="workspaceId"
      :states="states"
      :priorities="priorities"
      @save="onWidgetSaved"
    />

    <!-- Delete confirm -->
    <v-dialog v-model="deleteDialogOpen" max-width="420">
      <v-card rounded="lg">
        <v-card-title class="text-h6">{{ t('operationCore.dashboards.editor.deleteTitle') }}</v-card-title>
        <v-card-text>
          {{ t('operationCore.dashboards.editor.deleteConfirm', { name: deleteTarget?.name ?? '' }) }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteDialogOpen = false">
            {{ t('operationCore.dashboards.editor.cancel') }}
          </v-btn>
          <v-btn color="error" variant="flat" class="text-none" :loading="deleting" @click="doDelete">
            {{ t('operationCore.dashboards.editor.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
