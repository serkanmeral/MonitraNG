<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import type { TmBoard, TmIssue, TmProject } from '@/types/apps/taskManager';
import TmNewIssueFormFields from '@/components/apps/task-manager/TmNewIssueFormFields.vue';
import TmIssueComments from '@/components/apps/task-manager/TmIssueComments.vue';
import TmIssueHistoryPanel from '@/components/apps/task-manager/TmIssueHistoryPanel.vue';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import { useUserStore } from '@/stores/apps/user';
import { useAuthStore } from '@/stores/auth';
import { getEffectiveWorkflow, getAllowedTargetStatusIds, isTransitionAllowed } from '@/utils/taskManagerWorkflow';
import {
  emptyIssueForm,
  issueToIssueFormModel,
  normalizeDueDateInput,
  pruneIssueExtraFields,
  resolveEffectiveIssueCreateLayout,
  resolveEffectiveIssueProfileLayout,
  resolveNewIssueFormRows,
  type IssueFormModel,
} from '@/utils/taskManagerNewIssueForm';

const props = withDefaults(
  defineProps<{
    issue: TmIssue;
    project: TmProject | null;
    /** `?board=` ile gelen board; şablona göre profil düzeni seçilir. */
    board?: TmBoard | null;
    /** Sağ panel (yorumlar / geçmiş) sekmesi — URL `?tab=history` ile uyumlu. */
    initialRightTab?: 'comments' | 'history';
    /** Geri — liste (`workspace`) veya kanban (`boards/...`) hedefi */
    backTo: string;
  }>(),
  { board: null, initialRightTab: 'comments', backTo: '/apps/task-manager/workspace' }
);

const router = useRouter();
const store = useTaskManagerStore();
const userStore = useUserStore();
const auth = useAuthStore();

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const formModel = ref<IssueFormModel>(emptyIssueForm());
const statusIdEdit = ref('');
const saving = ref(false);
const statusError = ref('');

const effectiveBoard = computed(() => {
  const p = props.project;
  const b = props.board ?? null;
  if (!p || !b || b.projectId !== p.__dataId) return null;
  return b;
});

watch(
  () => [props.issue, store.fieldDefinitions.length] as const,
  () => {
    formModel.value = issueToIssueFormModel(props.issue, store.fieldDefinitions);
    statusIdEdit.value = props.issue.statusId;
  },
  { immediate: true }
);

const profileLayout = computed(() => {
  const p = props.project;
  if (!p) return null;
  const b = effectiveBoard.value;
  return resolveEffectiveIssueProfileLayout(p, b) ?? resolveEffectiveIssueCreateLayout(p, b);
});

const profileRows = computed(() =>
  resolveNewIssueFormRows(props.project ?? undefined, store.fieldDefinitions, profileLayout.value)
);

const issueTypeItems = computed(() => {
  const p = props.project;
  const ids = p?.selections?.issueTypeIds;
  let list = store.issueTypes;
  if (ids?.length) list = list.filter((t) => ids.includes(t.__dataId));
  return list.map((t) => ({ title: t.name, value: t.__dataId }));
});

const priorityItems = computed(() => {
  const p = props.project;
  const ids = p?.selections?.priorityIds;
  let list = store.priorities;
  if (ids?.length) list = list.filter((x) => ids.includes(x.__dataId));
  return list.map((x) => ({ title: x.name, value: x.__dataId }));
});

const labelItems = computed(() => {
  const pid = props.issue.projectId;
  return store.labels.filter((l) => l.projectId === pid).map((l) => ({ title: l.name, value: l.__dataId }));
});

const userItems = computed(() =>
  userStore.activeUsers.map((u) => ({
    title: `${u.firstName} ${u.lastName}`.trim() || u.username,
    value: u.id,
  }))
);

const currentUserId = computed(() => String(auth.userInfo?.sub ?? '').trim());

const historyEntries = computed(() => props.issue.issueHistory ?? []);

const rightTab = ref<'comments' | 'history'>('comments');

watch(
  () => props.initialRightTab,
  (v) => {
    if (v === 'history' || v === 'comments') rightTab.value = v;
  },
  { immediate: true }
);

watch(
  () => [rightTab.value, props.issue.__dataId] as const,
  async ([tab, id]) => {
    if (tab === 'comments' && id) {
      try {
        await store.loadIssueComments(id);
      } catch (_) {}
    }
  },
  { immediate: true }
);

const statusSelectItems = computed(() => {
  const i = props.issue;
  const wf = getEffectiveWorkflow(props.project ?? null, store.statuses);
  const cur = i.statusId;
  const allowed = new Set([cur, ...getAllowedTargetStatusIds(wf, cur)]);
  const order = new Map(wf.statusIds.map((sid, idx) => [sid, idx]));
  return store.statuses
    .filter((s) => allowed.has(s.__dataId))
    .sort((a, b) => (order.get(a.__dataId) ?? 999) - (order.get(b.__dataId) ?? 999));
});

async function save() {
  const i = props.issue;
  statusError.value = '';
  const wf = getEffectiveWorkflow(props.project ?? null, store.statuses);
  if (statusIdEdit.value !== i.statusId && !isTransitionAllowed(wf, i.statusId, statusIdEdit.value)) {
    statusError.value = mt(
      'taskManager.transitionDenied',
      'Bu durum geçişine izin verilmiyor. İş akışı ayarlarını kontrol edin.'
    );
    return;
  }
  saving.value = true;
  try {
    const f = formModel.value;
    const it = f.issueTypeId ?? i.issueTypeId;
    await store.updateIssue(i.__dataId, {
      title: f.title.trim(),
      description: f.description,
      statusId: statusIdEdit.value,
      priorityId: f.priorityId,
      issueTypeId: it,
      assignee: f.assignee,
      labels: f.labels?.length ? f.labels : null,
      dueDate: normalizeDueDateInput(f.dueDate),
      storyPoints:
        f.storyPoints === null || f.storyPoints === undefined || (f.storyPoints as unknown) === ''
          ? null
          : Number(f.storyPoints),
      projectId: i.projectId,
      extraFields: pruneIssueExtraFields(f.extra),
    });
  } finally {
    saving.value = false;
  }
}

function goBack() {
  void router.push(props.backTo);
}
</script>

<template>
  <div class="tm-issue-profile-view">
    <main class="tm-profile-main">
      <v-card flat rounded="lg" class="tm-panel pa-4 pa-md-6">
        <div class="text-subtitle-2 font-weight-medium mb-2">
          {{ mt('taskManager.status', 'Durum') }}
        </div>
        <v-select
          v-model="statusIdEdit"
          :items="statusSelectItems.map((s) => ({ title: s.name, value: s.__dataId }))"
          item-title="title"
          item-value="value"
          :label="mt('taskManager.status', 'Durum')"
          density="comfortable"
          variant="outlined"
          hide-details="auto"
        />
        <v-alert v-if="statusError" type="error" variant="tonal" density="compact" class="mt-2">
          {{ statusError }}
        </v-alert>

        <TmNewIssueFormFields
          v-model="formModel"
          class="mt-4"
          :rows="profileRows"
          :field-definitions="store.fieldDefinitions"
          :issue-type-items="issueTypeItems"
          :priority-items="priorityItems"
          :label-items="labelItems"
          :user-items="userItems"
          :issue-create-layout="profileLayout"
        />

        <div class="d-flex flex-wrap align-center justify-space-between gap-3 mt-6">
          <v-btn
            variant="text"
            size="large"
            rounded="lg"
            class="text-none"
            prepend-icon="mdi-arrow-left"
            @click="goBack"
          >
            {{ mt('taskManager.back', 'Geri') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            size="large"
            rounded="lg"
            class="text-none px-6"
            :loading="saving"
            :disabled="!formModel.title.trim()"
            @click="save"
          >
            {{ mt('taskManager.save', 'Kaydet') }}
          </v-btn>
        </div>
      </v-card>
    </main>

    <aside class="tm-profile-sidebar">
      <v-card flat rounded="lg" class="tm-panel tm-profile-side d-flex flex-column overflow-hidden">
        <v-tabs v-model="rightTab" color="primary" density="comfortable" grow>
          <v-tab value="comments">{{ mt('taskManager.issueProfileTabComments', 'Yorumlar') }}</v-tab>
          <v-tab value="history">{{ mt('taskManager.issueProfileTabHistory', 'Geçmiş') }}</v-tab>
        </v-tabs>
        <v-divider />
        <v-window v-model="rightTab" class="tm-profile-side-window flex-grow-1 d-flex flex-column" style="min-height: 320px">
          <v-window-item value="comments" class="fill-height">
            <div class="tm-profile-side-scroll pa-0">
              <TmIssueComments
                :issue-id="issue.__dataId"
                :project-id="issue.projectId"
                :current-user-id="currentUserId"
                :is-manager="auth.isManager"
              />
            </div>
          </v-window-item>
          <v-window-item value="history" class="fill-height">
            <div class="tm-profile-side-scroll">
              <TmIssueHistoryPanel :entries="historyEntries" :field-definitions="store.fieldDefinitions" />
            </div>
          </v-window-item>
        </v-window>
      </v-card>
    </aside>
  </div>
</template>

<style scoped>
.tm-issue-profile-view {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(360px, min(46vw, 560px));
  gap: 24px;
  align-items: start;
  width: 100%;
  max-width: 1600px;
  margin-inline: auto;
}

@media (max-width: 959px) {
  .tm-issue-profile-view {
    grid-template-columns: 1fr;
  }
}

.tm-profile-side-window :deep(.v-window__container) {
  flex: 1 1 auto;
  min-height: 0;
}

.tm-profile-side-scroll {
  max-height: min(72vh, 720px);
  overflow: auto;
}
</style>
