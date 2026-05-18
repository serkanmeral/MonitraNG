<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import {
  resolveBoardTableColumnIds,
  selectableBoardColumnIdsForProject,
  defaultBoardTableColumnIdsForProject,
  boardTableColumnTitle,
} from '@/utils/boardTableColumns';

definePageMeta({
  layout: 'default',
  middleware: ['task-manager-board-settings'],
});

const route = useRoute();
const router = useRouter();
const boardId = computed(() => String(route.params.boardId ?? ''));

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const store = useTaskManagerStore();
const board = computed(() => store.boards.find((b) => b.__dataId === boardId.value));
const project = computed(() => (board.value ? store.projects.find((p) => p.__dataId === board.value!.projectId) : null));
const projectId = computed(() => board.value?.projectId ?? '');

const TM_BOARD_FORM_PROJECT_DEFAULT = '__tm_project_default__';

const orderedColumns = ref<string[]>([]);
const addPicker = ref<string | null>(null);
const boardIssueCreateFormSelect = ref(TM_BOARD_FORM_PROJECT_DEFAULT);
const boardIssueProfileFormSelect = ref(TM_BOARD_FORM_PROJECT_DEFAULT);
const saving = ref(false);
const errorMsg = ref<string | null>(null);

const selectableAll = computed(() =>
  selectableBoardColumnIdsForProject(project.value ?? null, store.fieldDefinitions)
);

const availableToAdd = computed(() => {
  const set = new Set(orderedColumns.value);
  return selectableAll.value.filter((id) => !set.has(id));
});

const addPickerItems = computed(() =>
  availableToAdd.value.map((id) => ({ title: boardTableColumnTitle(id, store.fieldDefinitions, mt), value: id }))
);

function syncFromBoard() {
  orderedColumns.value = [
    ...resolveBoardTableColumnIds(board.value ?? null, project.value ?? null, store.fieldDefinitions),
  ];
}

watch(
  () => [board.value, project.value?.selections, store.fieldDefinitions],
  () => syncFromBoard(),
  { immediate: true, deep: true }
);

watch(
  () => board.value?.issueCreateFormId,
  (fid) => {
    boardIssueCreateFormSelect.value = fid?.trim() ? String(fid).trim() : TM_BOARD_FORM_PROJECT_DEFAULT;
  },
  { immediate: true }
);

watch(
  () => board.value?.issueProfileFormId,
  (fid) => {
    boardIssueProfileFormSelect.value = fid?.trim() ? String(fid).trim() : TM_BOARD_FORM_PROJECT_DEFAULT;
  },
  { immediate: true }
);

const boardFormSelectItems = computed(() => {
  const p = project.value;
  const items: { title: string; value: string }[] = [
    { title: mt('taskManager.boardFormUseProjectDefault', 'Proje varsayılanı'), value: TM_BOARD_FORM_PROJECT_DEFAULT },
  ];
  for (const f of p?.issueCreateForms ?? []) {
    items.push({ title: f.name || f.id, value: f.id });
  }
  return items;
});

const boardProfileFormSelectItems = computed(() => {
  const p = project.value;
  const items: { title: string; value: string }[] = [
    { title: mt('taskManager.boardFormUseProjectDefault', 'Proje varsayılanı'), value: TM_BOARD_FORM_PROJECT_DEFAULT },
  ];
  for (const f of p?.issueProfileForms ?? []) {
    items.push({ title: f.name || f.id, value: f.id });
  }
  return items;
});

onMounted(async () => {
  try {
    await store.loadLookups();
    await store.loadFieldDefinitions().catch(() => {});
    await store.loadProjects();
    await store.loadBoard(boardId.value);
    if (projectId.value) await store.loadBoards(projectId.value);
    syncFromBoard();
  } catch (e: any) {
    errorMsg.value = e?.message ?? 'Yükleme hatası';
  }
});

function addColumn() {
  const id = addPicker.value;
  if (!id) return;
  if (!orderedColumns.value.includes(id)) orderedColumns.value.push(id);
  addPicker.value = null;
}

function removeColumn(id: string) {
  orderedColumns.value = orderedColumns.value.filter((x) => x !== id);
}

function moveColumn(idx: number, dir: -1 | 1) {
  const j = idx + dir;
  if (j < 0 || j >= orderedColumns.value.length) return;
  const next = [...orderedColumns.value];
  [next[idx], next[j]] = [next[j], next[idx]];
  orderedColumns.value = next;
}

function resetDefaults() {
  orderedColumns.value = [...defaultBoardTableColumnIdsForProject(project.value ?? null, store.fieldDefinitions)];
}

async function save() {
  if (!board.value || !projectId.value) return;
  if (orderedColumns.value.length === 0) {
    errorMsg.value = mt('taskManager.boardSettingsColumnsEmpty', 'En az bir sütun seçin.');
    return;
  }
  saving.value = true;
  errorMsg.value = null;
  try {
    const formId =
      boardIssueCreateFormSelect.value === TM_BOARD_FORM_PROJECT_DEFAULT ? null : boardIssueCreateFormSelect.value;
    const profileFormId =
      boardIssueProfileFormSelect.value === TM_BOARD_FORM_PROJECT_DEFAULT ? null : boardIssueProfileFormSelect.value;
    await store.updateBoard(boardId.value, projectId.value, {
      config: {
        ...(board.value.config || {}),
        tableColumns: [...orderedColumns.value],
      },
      issueCreateFormId: formId,
      issueProfileFormId: profileFormId,
    });
    await store.loadBoard(boardId.value);
    await router.push({
      path: '/apps/task-manager/workspace',
      query: { project: projectId.value, board: boardId.value },
    });
  } catch (e: any) {
    errorMsg.value = e?.message ?? mt('taskManager.editorSaveError', 'Kaydedilemedi.');
  } finally {
    saving.value = false;
  }
}

function colTitle(id: string) {
  return boardTableColumnTitle(id, store.fieldDefinitions, mt);
}

const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('taskManager.pageTitle', 'Görevler'), disabled: false, href: '/apps/task-manager' },
  { text: project.value?.name ?? '…', disabled: false, href: project.value ? `/apps/task-manager/workspace` : '#' },
  { text: board.value?.name ?? 'Board', disabled: false, href: `/apps/task-manager/boards/${boardId.value}` },
  { text: mt('taskManager.boardSettingsTitle', 'Tablo sütunları'), disabled: true, href: '#' },
]);
</script>

<template>
  <div class="tm-flow">
    <BaseBreadcrumb :title="mt('taskManager.boardSettingsTitle', 'Tablo sütunları')" :breadcrumbs="breadcrumbs" />
    <v-alert v-if="errorMsg" type="error" variant="tonal" class="mb-4" closable @click:close="errorMsg = null">{{ errorMsg }}</v-alert>
    <v-alert v-if="!board" type="warning" variant="tonal" class="mb-4">{{ mt('taskManager.boardNotFound', 'Board bulunamadı.') }}</v-alert>

    <template v-else>
      <p class="text-body-2 text-medium-emphasis mb-4">
        {{ mt('taskManager.boardSettingsIntro', 'Liste görünümünde (çalışma alanı ve board) gösterilecek sütunları seçin ve sırayı düzenleyin. Yalnızca bu projede seçilmiş öncelik, görev tipi ve alanlarla uyumlu sütunlar listelenir.') }}
      </p>

      <v-card class="tm-panel rounded-xl pa-4 mb-4" flat>
        <div class="text-subtitle-2 font-weight-bold mb-2">{{ mt('taskManager.boardIssueCreateForm', 'Yeni görev formu') }}</div>
        <p class="text-caption text-medium-emphasis mb-3">
          {{ mt('taskManager.boardSettingsIssueFormHint', 'Bu board’da “Yeni görev” penceresinde hangi şablon kullanılsın?') }}
        </p>
        <v-select
          v-model="boardIssueCreateFormSelect"
          :items="boardFormSelectItems"
          item-title="title"
          item-value="value"
          density="comfortable"
          variant="outlined"
          hide-details="auto"
        />
      </v-card>

      <v-card class="tm-panel rounded-xl pa-4 mb-4" flat>
        <div class="text-subtitle-2 font-weight-bold mb-2">{{ mt('taskManager.boardIssueProfileForm', 'Profil ekranı şablonu') }}</div>
        <p class="text-caption text-medium-emphasis mb-3">
          {{ mt('taskManager.boardSettingsIssueProfileHint', 'Bu board’dan açılan tam sayfa “Profil” görünümünde hangi şablon kullanılsın?') }}
        </p>
        <v-select
          v-model="boardIssueProfileFormSelect"
          :items="boardProfileFormSelectItems"
          item-title="title"
          item-value="value"
          density="comfortable"
          variant="outlined"
          hide-details="auto"
        />
      </v-card>

      <v-card class="tm-panel rounded-xl pa-4 mb-4" flat>
        <div class="text-subtitle-2 font-weight-bold mb-3">{{ mt('taskManager.boardSettingsOrder', 'Sütun sırası') }}</div>
        <div v-for="(element, idx) in orderedColumns" :key="element" class="d-flex align-center ga-2 mb-2 tm-board-col-row">
          <div class="d-flex flex-column">
            <v-btn icon size="x-small" variant="text" :disabled="idx === 0" @click="moveColumn(idx, -1)">
              <v-icon icon="mdi-chevron-up" size="18" />
            </v-btn>
            <v-btn icon size="x-small" variant="text" :disabled="idx === orderedColumns.length - 1" @click="moveColumn(idx, 1)">
              <v-icon icon="mdi-chevron-down" size="18" />
            </v-btn>
          </div>
          <v-chip class="flex-grow-1 justify-start" variant="outlined" size="large">{{ colTitle(element) }}</v-chip>
          <v-btn icon size="small" variant="text" color="error" @click="removeColumn(element)">
            <v-icon icon="mdi-close" />
          </v-btn>
        </div>

        <div class="d-flex flex-wrap align-center ga-2 mt-4">
          <v-select
            v-model="addPicker"
            :items="addPickerItems"
            item-title="title"
            item-value="value"
            :label="mt('taskManager.boardSettingsAddColumn', 'Sütun ekle')"
            density="comfortable"
            variant="outlined"
            hide-details
            clearable
            style="min-width: 240px; max-width: 400px"
          />
          <v-btn color="primary" variant="tonal" rounded="lg" class="text-none" :disabled="!addPicker" @click="addColumn">
            {{ mt('taskManager.workflowAdd', 'Ekle') }}
          </v-btn>
          <v-btn variant="text" class="text-none" @click="resetDefaults">
            {{ mt('taskManager.boardSettingsResetDefault', 'Varsayılan sıraya dön') }}
          </v-btn>
        </div>
      </v-card>

      <div class="d-flex ga-2">
        <v-btn color="primary" rounded="lg" class="text-none" :loading="saving" @click="save">{{ mt('taskManager.save', 'Kaydet') }}</v-btn>
        <v-btn variant="tonal" rounded="lg" class="text-none" :to="`/apps/task-manager/boards/${boardId}`">
          {{ mt('taskManager.openBoard', 'Board\'a dön') }}
        </v-btn>
      </div>
    </template>
  </div>
</template>

