<script setup lang="ts">
import { computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import TmIssueProfileView from '@/components/apps/task-manager/TmIssueProfileView.vue';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import { useUserStore } from '@/stores/apps/user';

definePageMeta({ layout: 'default' });

const route = useRoute();
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

const issue = computed(() => store.issues.find((i) => i.key === issueKey.value));
const project = computed(() => (issue.value ? store.projects.find((p) => p.__dataId === issue.value!.projectId) : null));

function queryParamOne(v: string | string[] | undefined | null): string | null {
  if (v == null) return null;
  const s = Array.isArray(v) ? v[0] : v;
  return s ? String(s) : null;
}

const boardIdFromQuery = computed(() => queryParamOne(route.query.board));
const profileBoard = computed(() => {
  const bid = boardIdFromQuery.value;
  const iss = issue.value;
  if (!bid || !iss) return null;
  return store.boards.find((b) => b.__dataId === bid && b.projectId === iss.projectId) ?? null;
});

const routeRightTab = computed((): 'comments' | 'history' =>
  queryParamOne(route.query.tab) === 'history' ? 'history' : 'comments'
);

const profileBackTo = computed(() => {
  const from = queryParamOne(route.query.from);
  const bid = boardIdFromQuery.value;
  if (from === 'board' && bid) return `/apps/task-manager/boards/${encodeURIComponent(bid)}`;
  return '/apps/task-manager/workspace';
});

const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('taskManager.pageTitle', 'Görevler'), disabled: false, href: '/apps/task-manager' },
  { text: mt('taskManager.projectsListTitle', 'Projeler'), disabled: false, href: '/apps/task-manager/projects' },
  {
    text: project.value?.name ?? '…',
    disabled: false,
    href: project.value ? `/apps/task-manager/projects/${project.value.__dataId}` : '#',
  },
  { text: issueKey.value, disabled: true, href: '#' },
  { text: mt('taskManager.issueProfileTitle', 'Profil'), disabled: true, href: '#' },
]);

onMounted(async () => {
  try {
    await userStore.fetchUsers({ page: 1, pageSize: 500, isActive: true }).catch(() => {});
    await store.loadLookups();
    await store.loadFieldDefinitions().catch(() => {});
    await store.loadProjects();
    const found = await store.fetchIssueByKey(issueKey.value);
    if (found?.projectId) {
      await store.loadLabels(found.projectId);
      await store.loadIssues(found.projectId);
      await store.loadBoards(found.projectId);
      await store.hydrateIssueWithHistory(found.__dataId);
    }
  } catch (_) {}
});
</script>

<template>
  <div class="tm-flow tm-issue-profile-page">
    <BaseBreadcrumb class="mb-4" :title="mt('taskManager.issueProfileTitle', 'Profil')" :breadcrumbs="breadcrumbs" />
    <v-alert v-if="!issue" type="info" variant="tonal" class="mb-4">
      {{ mt('taskManager.issueNotFound', 'Görev bulunamadı veya henüz yüklenmedi.') }}
    </v-alert>
    <TmIssueProfileView
      v-else
      :issue="issue"
      :project="project"
      :board="profileBoard"
      :initial-right-tab="routeRightTab"
      :back-to="profileBackTo"
    />
  </div>
</template>
