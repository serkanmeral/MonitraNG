<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import { useAuthStore } from '@/stores/auth';
import { tmListDataset, TM_DATASETS } from '@/services/taskManagerService';
import { assigneeUserId } from '@/composables/useTaskManagerHelpers';
import type { TmIssue } from '@/types/apps/taskManager';

definePageMeta({ layout: 'default' });

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

function mapIssue(raw: Record<string, unknown>): TmIssue {
  const rid = (v: unknown) => {
    if (v == null) return '';
    if (typeof v === 'string') return v;
    if (typeof v === 'object' && v !== null && ('__dataId' in v || 'dataId' in v))
      return String((v as { __dataId?: string; dataId?: string }).__dataId ?? (v as { dataId?: string }).dataId ?? '');
    return String(v);
  };
  const lx = raw.labels ?? raw.Labels;
  let labels: string[] | null = null;
  if (Array.isArray(lx)) {
    const ids = lx.map((x) => (typeof x === 'string' ? x : rid(x))).filter(Boolean);
    if (ids.length) labels = ids;
  }
  return {
    __dataId: rid(raw.__dataId ?? raw.DataId ?? raw.dataId),
    key: String(raw.key ?? raw.Key ?? ''),
    projectKey: String(raw.projectKey ?? raw.ProjectKey ?? ''),
    projectId: rid(raw.projectId ?? raw.ProjectId),
    issueTypeId: rid(raw.issueTypeId ?? raw.IssueTypeId),
    title: String(raw.title ?? raw.Title ?? ''),
    description: (raw.description ?? raw.Description) as string | null,
    statusId: rid(raw.statusId ?? raw.StatusId),
    priorityId: raw.priorityId != null ? rid(raw.priorityId) : null,
    assignee: raw.assignee ?? raw.Assignee,
    epicId: raw.epicId != null ? rid(raw.epicId) : null,
    sprintId: raw.sprintId != null ? rid(raw.sprintId) : null,
    labels,
    dueDate: (raw.dueDate ?? raw.DueDate) as string | null,
    storyPoints: raw.storyPoints ?? raw.StoryPoints ?? null,
    order: raw.order ?? raw.Order ?? null,
  };
}

const store = useTaskManagerStore();
const auth = useAuthStore();

const loading = ref(true);
const rows = ref<TmIssue[]>([]);

const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('taskManager.pageTitle', 'Görevler'), disabled: false, href: '/apps/task-manager' },
  { text: mt('taskManager.myWorkTitle', 'Bana atananlar'), disabled: true, href: '#' },
]);

function projectName(projectId: string): string {
  return store.projects.find((p) => p.__dataId === projectId)?.name ?? projectId.slice(0, 8);
}

async function load() {
  loading.value = true;
  store.error = null;
  rows.value = [];
  const uid = auth.userInfo?.sub ?? '';
  try {
    await store.loadLookups();
    await store.loadProjects();
    if (uid && !auth.isAdmin && !auth.isManager) store.filterProjectsForUser(uid);
    if (!uid) {
      loading.value = false;
      return;
    }
    const merged: TmIssue[] = [];
    for (const p of store.visibleProjects) {
      const raw = await tmListDataset(TM_DATASETS.issues, {
        limit: 2000,
        filter: `projectId:eq:${p.__dataId}`,
        sort: 'order:asc',
      });
      for (const x of raw as Record<string, unknown>[]) {
        const issue = mapIssue(x);
        if (assigneeUserId(issue.assignee) === uid) merged.push(issue);
      }
    }
    merged.sort((a, b) => (a.key || '').localeCompare(b.key || ''));
    rows.value = merged;
  } catch (e: unknown) {
    const err = e as { message?: string };
    store.error = err?.message ?? 'Yükleme hatası';
  } finally {
    loading.value = false;
  }
}

onMounted(() => load());
</script>

<template>
  <div class="tm-flow">
    <BaseBreadcrumb :title="mt('taskManager.myWorkTitle', 'Bana atananlar')" :breadcrumbs="breadcrumbs" />
    <v-alert v-if="store.error" type="error" variant="tonal" class="mb-4" closable @click:close="store.error = null">
      {{ store.error }}
    </v-alert>

    <div class="mb-6">
      <h1 class="text-h4 mb-2">{{ mt('taskManager.myWorkTitle', 'Bana atananlar') }}</h1>
      <p class="text-medium-emphasis mb-0">
        {{ mt('taskManager.myWorkSubtitle', 'Size atanan görevler, erişebildiğiniz projelerden listelenir.') }}
      </p>
    </div>

    <v-card rounded="lg" variant="outlined" class="pa-2">
      <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-2" />
      <div v-if="!loading && !rows.length" class="pa-8 text-center text-medium-emphasis">
        {{ mt('taskManager.myWorkEmpty', 'Size atanan görev yok.') }}
      </div>
      <v-list v-else-if="rows.length" lines="two" class="bg-transparent">
        <v-list-item
          v-for="issue in rows"
          :key="issue.__dataId"
          :to="`/apps/task-manager/issues/${encodeURIComponent(issue.key)}`"
          rounded="lg"
        >
          <template #prepend>
            <v-icon icon="mdi-checkbox-marked-circle-outline" color="primary" />
          </template>
          <v-list-item-title class="font-weight-medium">
            {{ issue.key }} — {{ issue.title }}
          </v-list-item-title>
          <v-list-item-subtitle>
            {{ issue.projectKey }} · {{ projectName(issue.projectId) }}
          </v-list-item-subtitle>
        </v-list-item>
      </v-list>
    </v-card>
  </div>
</template>
