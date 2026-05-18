<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import TmIssueComments from '@/components/apps/task-manager/TmIssueComments.vue';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import { useUserStore } from '@/stores/apps/user';
import { useAuthStore } from '@/stores/auth';
import { assigneeUserId } from '@/composables/useTaskManagerHelpers';
import { getEffectiveWorkflow, getAllowedTargetStatusIds, isTransitionAllowed } from '@/utils/taskManagerWorkflow';
import TmIssueDescriptionEditor from '@/components/apps/task-manager/TmIssueDescriptionEditor.client.vue';

definePageMeta({ layout: 'default' });

const route = useRoute();
const router = useRouter();
const issueKey = computed(() => decodeURIComponent(String(route.params.key ?? '')));

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
const userStore = useUserStore();
const auth = useAuthStore();

const currentUserId = computed(() => String(auth.userInfo?.sub ?? '').trim());

const issue = computed(() => store.issues.find((i) => i.key === issueKey.value));
const project = computed(() => (issue.value ? store.projects.find((p) => p.__dataId === issue.value!.projectId) : null));

const titleEdit = ref('');
const descEdit = ref('');
const statusIdEdit = ref('');
const priorityIdEdit = ref<string | null>(null);
const issueTypeIdEdit = ref('');
const assigneeIdEdit = ref<string | null>(null);
const labelsEdit = ref<string[]>([]);
const dueEdit = ref('');
const storyPointsEdit = ref<number | null>(null);
const saving = ref(false);
const deleting = ref(false);
const deleteDialog = ref(false);
const newLabelDialog = ref(false);
const newLabelName = ref('');
const statusError = ref('');

function toInputDate(iso: string | null | undefined): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return d.toISOString().slice(0, 10);
}

function fromInputDate(s: string): string | null {
  if (!s?.trim()) return null;
  return new Date(`${s}T12:00:00`).toISOString();
}

watch(
  issue,
  (i) => {
    if (i) {
      titleEdit.value = i.title;
      descEdit.value = i.description ?? '';
      statusIdEdit.value = i.statusId;
      priorityIdEdit.value = i.priorityId ?? null;
      issueTypeIdEdit.value = i.issueTypeId;
      assigneeIdEdit.value = assigneeUserId(i.assignee) || null;
      labelsEdit.value = i.labels ? [...i.labels] : [];
      dueEdit.value = toInputDate(i.dueDate);
      storyPointsEdit.value = i.storyPoints ?? null;
    }
  },
  { immediate: true }
);

const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('taskManager.pageTitle', 'Görevler'), disabled: false, href: '/apps/task-manager' },
  { text: mt('taskManager.projectsListTitle', 'Projeler'), disabled: false, href: '/apps/task-manager/projects' },
  { text: project.value?.name ?? '…', disabled: false, href: project.value ? `/apps/task-manager/projects/${project.value.__dataId}` : '#' },
  { text: issueKey.value, disabled: true, href: '#' },
]);

const labelItems = computed(() => store.labels.map((l) => ({ title: l.name, value: l.__dataId, color: l.color })));

/** Mevcut durum + iş akışında izin verilen hedefler (aynı kalırsa seçenek tek başına kalır). */
const statusSelectItems = computed(() => {
  if (!issue.value) return [];
  const wf = getEffectiveWorkflow(project.value ?? null, store.statuses);
  const cur = issue.value.statusId;
  const allowed = new Set([cur, ...getAllowedTargetStatusIds(wf, cur)]);
  const order = new Map(wf.statusIds.map((id, i) => [id, i]));
  return store.statuses
    .filter((s) => allowed.has(s.__dataId))
    .sort((a, b) => (order.get(a.__dataId) ?? 999) - (order.get(b.__dataId) ?? 999));
});

onMounted(async () => {
  try {
    await userStore.fetchUsers({ page: 1, pageSize: 500, isActive: true }).catch(() => {});
    await store.loadLookups();
    await store.loadProjects();
    const found = await store.fetchIssueByKey(issueKey.value);
    if (found?.projectId) {
      await store.loadLabels(found.projectId);
      await store.loadBoards(found.projectId);
      await store.loadIssues(found.projectId);
      await store.hydrateIssueWithHistory(found.__dataId);
    }
  } catch (_) {}
});

async function save() {
  if (!issue.value) return;
  statusError.value = '';
  const wf = getEffectiveWorkflow(project.value ?? null, store.statuses);
  if (
    statusIdEdit.value !== issue.value.statusId &&
    !isTransitionAllowed(wf, issue.value.statusId, statusIdEdit.value)
  ) {
    statusError.value = mt(
      'taskManager.transitionDenied',
      'Bu durum geçişine izin verilmiyor. İş akışı ayarlarını kontrol edin.'
    );
    return;
  }
  saving.value = true;
  try {
    await store.updateIssue(issue.value.__dataId, {
      title: titleEdit.value,
      description: descEdit.value,
      statusId: statusIdEdit.value,
      priorityId: priorityIdEdit.value,
      issueTypeId: issueTypeIdEdit.value,
      assignee: assigneeIdEdit.value,
      labels: labelsEdit.value.length ? labelsEdit.value : null,
      dueDate: fromInputDate(dueEdit.value),
      storyPoints:
        storyPointsEdit.value === null || storyPointsEdit.value === undefined || storyPointsEdit.value === ('' as unknown as number)
          ? null
          : Number(storyPointsEdit.value),
      projectId: issue.value.projectId,
    });
  } finally {
    saving.value = false;
  }
}

async function removeIssue() {
  if (!issue.value) return;
  deleting.value = true;
  try {
    const pid = issue.value.projectId;
    await store.deleteIssue(issue.value.__dataId, pid);
    deleteDialog.value = false;
    router.push(`/apps/task-manager/projects/${pid}`);
  } finally {
    deleting.value = false;
  }
}

async function addLabel() {
  if (!issue.value || !project.value || !newLabelName.value.trim()) return;
  await store.createLabel(project.value.__dataId, newLabelName.value.trim());
  newLabelName.value = '';
  newLabelDialog.value = false;
}

const boardLink = computed(() => {
  const boards = issue.value ? store.boardsForProject(issue.value.projectId) : [];
  return boards[0] ? `/apps/task-manager/boards/${boards[0].__dataId}` : null;
});
</script>

<template>
  <div class="tm-flow">
    <BaseBreadcrumb :title="issueKey" :breadcrumbs="breadcrumbs" />

    <v-alert v-if="!issue" type="info" variant="tonal" class="mb-4">
      {{ mt('taskManager.issueNotFound', 'Görev bulunamadı veya henüz yüklenmedi.') }}
    </v-alert>

    <div v-else class="tm-hero mb-4">
      <div class="d-flex flex-wrap align-start justify-space-between gap-3">
        <div>
          <div class="text-overline text-medium-emphasis">{{ issue.key }}</div>
          <h1 class="tm-hero-title text-h4">{{ titleEdit || issue.title }}</h1>
          <p v-if="project" class="tm-hero-sub mb-0">{{ project.name }} · {{ project.key }}</p>
        </div>
        <div class="d-flex flex-wrap gap-2">
          <v-btn v-if="boardLink" variant="tonal" rounded="lg" class="text-none" :to="boardLink">
            {{ mt('taskManager.openBoard', 'Board\'a dön') }}
          </v-btn>
          <v-btn color="error" variant="outlined" rounded="lg" class="text-none" @click="deleteDialog = true">
            {{ mt('taskManager.deleteIssue', 'Görevi sil') }}
          </v-btn>
        </div>
      </div>
    </div>

    <v-row v-if="issue">
      <v-col cols="12" lg="8">
        <v-card class="tm-panel pa-2 pa-md-4" rounded="xl" flat>
          <v-card-text>
            <v-text-field v-model="titleEdit" :label="mt('taskManager.issueTitle', 'Başlık')" density="comfortable" variant="outlined" />
            <ClientOnly class="mt-3">
              <TmIssueDescriptionEditor v-model="descEdit" :label="mt('taskManager.description', 'Açıklama')" />
              <template #fallback>
                <v-textarea
                  v-model="descEdit"
                  :label="mt('taskManager.description', 'Açıklama')"
                  rows="6"
                  density="comfortable"
                  variant="outlined"
                />
              </template>
            </ClientOnly>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="12" lg="4">
        <v-card class="tm-panel pa-2 pa-md-4" rounded="xl" flat>
          <v-card-subtitle class="text-uppercase font-weight-bold mb-2">{{
            mt('taskManager.detailsPanel', 'Ayrıntılar')
          }}</v-card-subtitle>
          <v-select
            v-model="statusIdEdit"
            :items="statusSelectItems.map((s) => ({ title: s.name, value: s.__dataId }))"
            item-title="title"
            item-value="value"
            :label="mt('taskManager.status', 'Durum')"
            density="comfortable"
            variant="outlined"
          />
          <v-alert v-if="statusError" type="error" variant="tonal" density="compact" class="mt-2">
            {{ statusError }}
          </v-alert>
          <v-select
            v-model="priorityIdEdit"
            :items="store.priorities.map((p) => ({ title: p.name, value: p.__dataId }))"
            item-title="title"
            item-value="value"
            clearable
            :label="mt('taskManager.priority', 'Öncelik')"
            density="comfortable"
            variant="outlined"
            class="mt-2"
          />
          <v-select
            v-model="issueTypeIdEdit"
            :items="store.issueTypes.map((t) => ({ title: t.name, value: t.__dataId }))"
            item-title="title"
            item-value="value"
            :label="mt('taskManager.issueType', 'Görev tipi')"
            density="comfortable"
            variant="outlined"
            class="mt-2"
          />
          <v-select
            v-model="assigneeIdEdit"
            :items="userStore.activeUsers.map((u) => ({ title: `${u.firstName} ${u.lastName}`.trim() || u.username, value: u.id }))"
            item-title="title"
            item-value="value"
            clearable
            :label="mt('taskManager.assignee', 'Atanan')"
            density="comfortable"
            variant="outlined"
            class="mt-2"
          />
          <v-autocomplete
            v-model="labelsEdit"
            :items="labelItems"
            item-title="title"
            item-value="value"
            multiple
            chips
            closable-chips
            clearable
            :label="mt('taskManager.labels', 'Etiketler')"
            density="comfortable"
            variant="outlined"
            class="mt-2"
          >
            <template #append>
              <v-btn icon size="small" variant="text" @click="newLabelDialog = true">
                <v-icon icon="mdi-plus" />
              </v-btn>
            </template>
          </v-autocomplete>
          <v-text-field
            v-model="dueEdit"
            type="date"
            :label="mt('taskManager.dueDate', 'Bitiş tarihi')"
            density="comfortable"
            variant="outlined"
            class="mt-2"
            clearable
            @click:clear="dueEdit = ''"
          />
          <v-text-field
            v-model.number="storyPointsEdit"
            type="number"
            step="0.5"
            min="0"
            :label="mt('taskManager.storyPoints', 'Story point')"
            density="comfortable"
            variant="outlined"
            class="mt-2"
            clearable
          />
          <v-btn block color="primary" rounded="lg" size="large" class="text-none mt-6" :loading="saving" @click="save">
            {{ mt('taskManager.save', 'Kaydet') }}
          </v-btn>
        </v-card>
      </v-col>
    </v-row>

    <v-row v-if="issue">
      <v-col cols="12">
        <TmIssueComments
          :issue-id="issue.__dataId"
          :project-id="issue.projectId"
          :current-user-id="currentUserId"
          :is-manager="auth.isManager"
        />
      </v-col>
    </v-row>

    <v-dialog v-model="deleteDialog" max-width="420">
      <v-card rounded="xl">
        <v-card-title>{{ mt('taskManager.deleteIssueTitle', 'Görev silinsin mi?') }}</v-card-title>
        <v-card-text>{{ mt('taskManager.deleteIssueBody', 'Bu işlem geri alınamaz.') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">{{ mt('taskManager.cancel', 'İptal') }}</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="removeIssue">{{ mt('taskManager.delete', 'Sil') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="newLabelDialog" max-width="400">
      <v-card rounded="xl">
        <v-card-title>{{ mt('taskManager.newLabel', 'Yeni etiket') }}</v-card-title>
        <v-card-text>
          <v-text-field v-model="newLabelName" :label="mt('taskManager.labelName', 'Etiket adı')" density="comfortable" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="newLabelDialog = false">{{ mt('taskManager.cancel', 'İptal') }}</v-btn>
          <v-btn color="primary" :disabled="!newLabelName.trim()" @click="addLabel">{{ mt('taskManager.save', 'Kaydet') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
