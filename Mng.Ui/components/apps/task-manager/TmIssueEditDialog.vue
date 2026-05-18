<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import type { TmBoard, TmIssue, TmProject } from '@/types/apps/taskManager';
import TmIssueComments from '@/components/apps/task-manager/TmIssueComments.vue';
import TmNewIssueFormFields from '@/components/apps/task-manager/TmNewIssueFormFields.vue';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import { useUserStore } from '@/stores/apps/user';
import { useAuthStore } from '@/stores/auth';
import { getEffectiveWorkflow, getAllowedTargetStatusIds, isTransitionAllowed } from '@/utils/taskManagerWorkflow';
import {
  emptyIssueForm,
  issueCreateDialogMaxWidth,
  issueToIssueFormModel,
  normalizeDueDateInput,
  pruneIssueExtraFields,
  resolveEffectiveIssueCreateLayout,
  resolveNewIssueFormRows,
  type IssueFormModel,
} from '@/utils/taskManagerNewIssueForm';

const props = defineProps<{
  /** v-dialog görünürlüğü */
  modelValue: boolean;
  issue: TmIssue | null;
  project: TmProject | null;
  /** Liste/board bağlamı — görevin `projectId` ile aynı projeye ait olmalı; aksi halde yok sayılır */
  board?: TmBoard | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [v: boolean];
  saved: [];
}>();

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

const editForm = ref<IssueFormModel>(emptyIssueForm());
const statusIdEdit = ref('');
const saving = ref(false);
const statusError = ref('');

const effectiveBoard = computed(() => {
  const p = props.project;
  const b = props.board ?? null;
  if (!p || !b || b.projectId !== p.__dataId) return null;
  return b;
});

const effectiveIssueCreateLayout = computed(() =>
  resolveEffectiveIssueCreateLayout(props.project ?? null, effectiveBoard.value)
);

const issueDialogMaxWidth = computed(() => issueCreateDialogMaxWidth(effectiveIssueCreateLayout.value));

const editFormRows = computed(() =>
  resolveNewIssueFormRows(props.project ?? null, store.fieldDefinitions, effectiveIssueCreateLayout.value)
);

const issueTypeSelectItems = computed(() => {
  const p = props.project;
  const ids = p?.selections?.issueTypeIds;
  let list = store.issueTypes;
  if (ids?.length) list = list.filter((t) => ids.includes(t.__dataId));
  return list.map((t) => ({ title: t.name, value: t.__dataId }));
});

const prioritySelectItems = computed(() => {
  const p = props.project;
  const ids = p?.selections?.priorityIds;
  let list = store.priorities;
  if (ids?.length) list = list.filter((x) => ids.includes(x.__dataId));
  return list.map((x) => ({ title: x.name, value: x.__dataId }));
});

const labelSelectItems = computed(() => {
  const pid = props.project?.__dataId;
  if (!pid) return [];
  return store.labels
    .filter((l) => l.projectId === pid)
    .map((l) => ({ title: l.name, value: l.__dataId }));
});

const userSelectItems = computed(() =>
  userStore.activeUsers.map((u) => ({
    title: `${u.firstName} ${u.lastName}`.trim() || u.username,
    value: u.id,
  }))
);

watch(
  () => [props.modelValue, props.issue, store.fieldDefinitions] as const,
  () => {
    const i = props.issue;
    if (!props.modelValue || !i) return;
    editForm.value = issueToIssueFormModel(i, store.fieldDefinitions);
    statusIdEdit.value = i.statusId;
  },
  { immediate: true }
);

watch(
  () => [props.modelValue, props.issue?.__dataId] as const,
  async ([open, id]) => {
    if (open && id) {
      try {
        await store.loadIssueComments(id);
      } catch (_) {}
    }
  }
);

const statusSelectItems = computed(() => {
  const i = props.issue;
  if (!i) return [];
  const wf = getEffectiveWorkflow(props.project ?? null, store.statuses);
  const cur = i.statusId;
  const allowed = new Set([cur, ...getAllowedTargetStatusIds(wf, cur)]);
  const order = new Map(wf.statusIds.map((sid, idx) => [sid, idx]));
  return store.statuses
    .filter((s) => allowed.has(s.__dataId))
    .sort((a, b) => (order.get(a.__dataId) ?? 999) - (order.get(b.__dataId) ?? 999));
});

function close() {
  emit('update:modelValue', false);
}

async function save() {
  const i = props.issue;
  if (!i) return;
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
    const f = editForm.value;
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
    emit('saved');
  } finally {
    saving.value = false;
  }
}

const fullPageTo = computed(() =>
  props.issue?.key ? `/apps/task-manager/issues/${encodeURIComponent(props.issue.key)}` : null
);
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    :max-width="issueDialogMaxWidth"
    scrollable
    content-class="tm-new-issue-dialog-overlay"
    @update:model-value="emit('update:modelValue', $event)"
    @keyup.escape="close"
  >
    <v-card v-if="issue" class="tm-new-issue-dialog tm-flow" rounded="xl" elevation="12">
      <v-card-item class="pb-0 pt-6 px-6">
        <div class="d-flex flex-wrap align-start justify-space-between gap-2">
          <div class="min-w-0">
            <v-card-title class="text-h6 font-weight-bold pa-0 text-wrap">
              {{ mt('taskManager.editIssue', 'Görevi düzenle') }}
            </v-card-title>
            <v-card-subtitle class="text-body-2 pa-0 mt-2 text-medium-emphasis text-wrap">
              {{ issue.key }}
              <template v-if="project"> · {{ project.name }}</template>
            </v-card-subtitle>
          </div>
          <div class="d-flex flex-wrap gap-1 shrink-0">
            <v-btn
              v-if="fullPageTo"
              variant="text"
              size="small"
              class="text-none"
              rounded="lg"
              :to="fullPageTo"
              @click="close"
            >
              {{ mt('taskManager.openIssueFullPage', 'Tam sayfada aç') }}
            </v-btn>
          </div>
        </div>
      </v-card-item>

      <v-card-text class="px-6 pt-4 pb-2">
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
          v-model="editForm"
          class="mt-4"
          :rows="editFormRows"
          :field-definitions="store.fieldDefinitions"
          :issue-type-items="issueTypeSelectItems"
          :priority-items="prioritySelectItems"
          :label-items="labelSelectItems"
          :user-items="userSelectItems"
          :issue-create-layout="effectiveIssueCreateLayout"
        />

        <div class="mt-6">
          <TmIssueComments
            :issue-id="issue.__dataId"
            :project-id="issue.projectId"
            :current-user-id="currentUserId"
            :is-manager="auth.isManager"
          />
        </div>
      </v-card-text>

      <v-divider class="border-opacity-25" />
      <v-card-actions class="px-6 py-4 d-flex align-center flex-wrap gap-2">
        <v-spacer />
        <v-btn variant="text" class="text-none" rounded="lg" @click="close">
          {{ mt('taskManager.cancel', 'İptal') }}
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          size="large"
          rounded="lg"
          class="text-none px-6"
          :loading="saving"
          :disabled="!editForm.title.trim()"
          @click="save"
        >
          {{ mt('taskManager.save', 'Kaydet') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

</template>
