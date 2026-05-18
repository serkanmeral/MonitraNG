<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import { projectUsesKanban } from '@/utils/taskManagerWorkflow';

definePageMeta({ layout: 'default' });

const route = useRoute();
const projectId = computed(() => String(route.params.id ?? ''));

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
const project = computed(() => store.projects.find((p) => p.__dataId === projectId.value));
const boards = computed(() => store.boardsForProject(projectId.value));
const usesKanban = computed(() => (project.value ? projectUsesKanban(project.value) : true));

const TM_BOARD_FORM_PROJECT_DEFAULT = '__tm_project_default__';

const boardDialog = ref(false);
const boardName = ref('');
const newBoardIssueFormId = ref<string>(TM_BOARD_FORM_PROJECT_DEFAULT);
const newBoardIssueProfileFormId = ref<string>(TM_BOARD_FORM_PROJECT_DEFAULT);
const savingBoard = ref(false);

const deleteProjectDialog = ref(false);
const deleteBoardDialog = ref(false);
const deleteBoardId = ref<string | null>(null);
const deleting = ref(false);

const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('taskManager.pageTitle', 'Görevler'), disabled: false, href: '/apps/task-manager' },
  { text: mt('taskManager.projectsListTitle', 'Projeler'), disabled: false, href: '/apps/task-manager/projects' },
  { text: project.value?.name ?? '…', disabled: true, href: '#' },
]);

onMounted(async () => {
  try {
    await store.loadLookups();
    await store.loadProjects();
    await store.loadBoards(projectId.value);
  } catch (_) {}
});

const newBoardFormSelectItems = computed(() => {
  const p = project.value;
  const items: { title: string; value: string }[] = [
    { title: mt('taskManager.boardFormUseProjectDefault', 'Proje varsayılanı'), value: TM_BOARD_FORM_PROJECT_DEFAULT },
  ];
  for (const f of p?.issueCreateForms ?? []) {
    items.push({ title: f.name || f.id, value: f.id });
  }
  return items;
});

const newBoardProfileFormSelectItems = computed(() => {
  const p = project.value;
  const items: { title: string; value: string }[] = [
    { title: mt('taskManager.boardFormUseProjectDefault', 'Proje varsayılanı'), value: TM_BOARD_FORM_PROJECT_DEFAULT },
  ];
  for (const f of p?.issueProfileForms ?? []) {
    items.push({ title: f.name || f.id, value: f.id });
  }
  return items;
});

function openCreateBoard() {
  boardName.value = '';
  newBoardIssueFormId.value = TM_BOARD_FORM_PROJECT_DEFAULT;
  newBoardIssueProfileFormId.value = TM_BOARD_FORM_PROJECT_DEFAULT;
  boardDialog.value = true;
}

async function submitBoard() {
  if (!boardName.value.trim()) return;
  savingBoard.value = true;
  try {
    const formId =
      newBoardIssueFormId.value === TM_BOARD_FORM_PROJECT_DEFAULT ? null : newBoardIssueFormId.value;
    const profileFormId =
      newBoardIssueProfileFormId.value === TM_BOARD_FORM_PROJECT_DEFAULT ? null : newBoardIssueProfileFormId.value;
    await store.createBoard(projectId.value, boardName.value, usesKanban.value ? 'kanban' : 'list', formId, profileFormId);
    boardDialog.value = false;
  } finally {
    savingBoard.value = false;
  }
}

async function removeProject() {
  if (!project.value) return;
  deleting.value = true;
  try {
    await store.deleteProject(project.value.__dataId);
    deleteProjectDialog.value = false;
    await navigateTo('/apps/task-manager/projects');
  } finally {
    deleting.value = false;
  }
}

function openDeleteBoard(id: string) {
  deleteBoardId.value = id;
  deleteBoardDialog.value = true;
}

async function removeBoard() {
  if (!deleteBoardId.value) return;
  deleting.value = true;
  try {
    await store.deleteBoard(deleteBoardId.value, projectId.value);
    deleteBoardDialog.value = false;
    deleteBoardId.value = null;
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="tm-flow">
    <BaseBreadcrumb :title="project?.name ?? 'Project'" :breadcrumbs="breadcrumbs" />
    <v-alert v-if="!project" type="warning" variant="tonal" class="mb-4">
      {{ mt('taskManager.projectNotFound', 'Proje bulunamadı.') }}
    </v-alert>

    <template v-else>
      <div class="tm-hero mb-6">
        <div class="d-flex flex-column flex-md-row flex-wrap justify-space-between gap-4">
          <div>
            <div class="text-overline text-primary font-weight-bold">{{ project.key }}</div>
            <h1 class="tm-hero-title text-h4">{{ project.name }}</h1>
            <p class="tm-hero-sub mb-0">{{ project.description || mt('taskManager.noDescription', 'Açıklama yok') }}</p>
          </div>
          <div class="d-flex flex-wrap gap-2">
            <v-btn
              variant="tonal"
              rounded="lg"
              class="text-none"
              :to="{ path: `/apps/task-manager/projects/${projectId}/edit`, query: { tab: 'workflow' } }"
            >
              <v-icon icon="mdi-state-machine" start />
              {{ mt('taskManager.workflowOpen', 'Durum akışı') }}
            </v-btn>
            <v-btn
              variant="tonal"
              rounded="lg"
              class="text-none"
              :to="`/apps/task-manager/projects/${projectId}/labels`"
            >
              <v-icon icon="mdi-label-outline" start />
              {{ mt('taskManager.projectLabelsManage', 'Etiketler') }}
            </v-btn>
            <v-btn variant="tonal" rounded="lg" class="text-none" :to="`/apps/task-manager/projects/${projectId}/edit`">
              <v-icon icon="mdi-pencil" start />
              {{ mt('taskManager.editProject', 'Düzenle') }}
            </v-btn>
            <v-btn color="error" variant="outlined" rounded="lg" class="text-none" @click="deleteProjectDialog = true">
              {{ mt('taskManager.deleteProject', 'Projeyi sil') }}
            </v-btn>
          </div>
        </div>
      </div>

      <v-alert v-if="!usesKanban" type="info" variant="tonal" density="comfortable" class="mb-4">
        {{ mt('taskManager.projectListOnlyInfo', 'Bu projede Kanban kapalı; board’lar yalnızca liste görünümü olarak açılır.') }}
      </v-alert>

      <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
        <h2 class="text-h6 font-weight-bold mb-0">
          {{ usesKanban ? mt('taskManager.boardsTitle', 'Board\'lar') : mt('taskManager.boardsTitleList', 'Görev listeleri') }}
        </h2>
        <v-btn color="primary" rounded="lg" class="text-none" @click="openCreateBoard">
          <v-icon :icon="usesKanban ? 'mdi-view-column' : 'mdi-format-list-bulleted'" start />
          {{ mt('taskManager.newBoard', 'Yeni board') }}
        </v-btn>
      </div>

      <v-row v-if="boards.length">
        <v-col v-for="b in boards" :key="b.__dataId" cols="12" sm="6" md="4">
          <v-card class="tm-proj-card h-100" rounded="xl" flat :to="`/apps/task-manager/boards/${b.__dataId}`">
            <v-card-text>
              <div class="d-flex align-center justify-space-between">
                <div class="text-h6 font-weight-bold">{{ b.name }}</div>
                <v-btn
                  icon
                  size="small"
                  variant="text"
                  color="error"
                  @click.prevent.stop="openDeleteBoard(b.__dataId)"
                >
                  <v-icon icon="mdi-trash-can-outline" />
                </v-btn>
              </div>
              <div class="text-caption text-medium-emphasis text-uppercase mt-1">{{ b.type }}</div>
            </v-card-text>
            <v-card-actions class="pt-0 px-4 pb-4">
              <span class="text-caption text-primary">{{
                usesKanban ? mt('taskManager.openBoardHint', 'Kanban\'a git →') : mt('taskManager.openBoardListHint', 'Listeyi aç →')
              }}</span>
            </v-card-actions>
          </v-card>
        </v-col>
      </v-row>

      <v-card v-else class="tm-panel pa-8 text-center" rounded="xl" flat>
        <p class="text-body-1">{{ mt('taskManager.noBoards', 'Henüz board yok.') }}</p>
        <v-btn color="primary" rounded="lg" class="text-none mt-2" @click="openCreateBoard">{{ mt('taskManager.newBoard', 'Yeni board') }}</v-btn>
      </v-card>
    </template>

    <v-dialog v-model="boardDialog" max-width="480">
      <v-card rounded="xl">
        <v-card-title>{{ mt('taskManager.newBoard', 'Yeni board') }}</v-card-title>
        <v-card-text>
          <v-text-field v-model="boardName" :label="mt('taskManager.boardName', 'Board adı')" density="comfortable" />
          <v-select
            v-model="newBoardIssueFormId"
            class="mt-3"
            :items="newBoardFormSelectItems"
            item-title="title"
            item-value="value"
            density="comfortable"
            variant="outlined"
            :label="mt('taskManager.boardIssueCreateForm', 'Yeni görev formu')"
            :hint="mt('taskManager.boardIssueCreateFormHint', 'İlk seçenek proje varsayılan formunu kullanır.')"
            persistent-hint
          />
          <v-select
            v-model="newBoardIssueProfileFormId"
            class="mt-3"
            :items="newBoardProfileFormSelectItems"
            item-title="title"
            item-value="value"
            density="comfortable"
            variant="outlined"
            :label="mt('taskManager.boardIssueProfileForm', 'Profil ekranı şablonu')"
            :hint="mt('taskManager.boardIssueProfileFormHint', 'İlk seçenek proje varsayılan profil şablonunu kullanır.')"
            persistent-hint
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="boardDialog = false">{{ mt('taskManager.cancel', 'İptal') }}</v-btn>
          <v-btn color="primary" :loading="savingBoard" :disabled="!boardName.trim()" @click="submitBoard">{{ mt('taskManager.save', 'Kaydet') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteProjectDialog" max-width="420">
      <v-card rounded="xl">
        <v-card-title>{{ mt('taskManager.deleteProjectTitle', 'Proje silinsin mi?') }}</v-card-title>
        <v-card-text>{{ mt('taskManager.deleteProjectBody', 'Projeye bağlı board ve görevler de silinir.') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteProjectDialog = false">{{ mt('taskManager.cancel', 'İptal') }}</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="removeProject">{{ mt('taskManager.delete', 'Sil') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteBoardDialog" max-width="420">
      <v-card rounded="xl">
        <v-card-title>{{ mt('taskManager.deleteBoardTitle', 'Board silinsin mi?') }}</v-card-title>
        <v-card-text>{{ mt('taskManager.deleteBoardBody', 'Görevler projede kalır; yalnızca board kaldırılır.') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteBoardDialog = false">{{ mt('taskManager.cancel', 'İptal') }}</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="removeBoard">{{ mt('taskManager.delete', 'Sil') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
