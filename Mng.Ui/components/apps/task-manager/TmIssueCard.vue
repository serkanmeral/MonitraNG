<script setup lang="ts">
import { computed } from 'vue';
import type { TmIssue } from '@/types/apps/taskManager';

const props = withDefaults(
  defineProps<{
    issue: TmIssue;
    priorityColor?: string | null;
    typeName?: string;
    assigneeInitials?: string;
    /** Kanban board — profil URL’sine `?board=` eklenir */
    boardId?: string | null;
  }>(),
  {
    priorityColor: '#94a3b8',
    typeName: '',
    assigneeInitials: '',
    boardId: null,
  }
);

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const priorityStyle = computed(() => ({
  '--tm-priority': props.priorityColor || '#94a3b8',
}));

const issueDetailTo = computed(
  () => `/apps/task-manager/issues/${encodeURIComponent(props.issue.key)}`
);

const profileTo = computed(() => {
  const base = `/apps/task-manager/issues/${encodeURIComponent(props.issue.key)}/profile`;
  const bid = props.boardId?.trim();
  const qs = new URLSearchParams();
  if (bid) {
    qs.set('board', bid);
    qs.set('from', 'board');
  } else {
    qs.set('from', 'workspace');
  }
  return `${base}?${qs.toString()}`;
});
</script>

<template>
  <div class="tm-issue-card pa-3 position-relative" :style="priorityStyle">
    <v-btn
      icon="mdi-card-account-details-outline"
      size="x-small"
      variant="text"
      density="compact"
      class="tm-issue-card__profile-btn position-absolute"
      color="primary"
      :to="profileTo"
      :title="mt('taskManager.openIssueProfile', 'Profil')"
      :aria-label="mt('taskManager.openIssueProfile', 'Profil')"
      @click.stop
    />
    <NuxtLink :to="issueDetailTo" class="tm-issue-card__link text-decoration-none text-reset d-block pr-7">
      <div class="tm-issue-card__meta">{{ issue.key }}</div>
      <div class="tm-issue-card__title">{{ issue.title }}</div>
      <div class="tm-issue-card__foot">
        <span v-if="typeName" class="tm-issue-card__chip">{{ typeName }}</span>
        <span v-else />
        <span v-if="assigneeInitials" class="tm-avatar-tiny">{{ assigneeInitials }}</span>
      </div>
    </NuxtLink>
  </div>
</template>

<style scoped>
.tm-issue-card__profile-btn {
  top: 2px;
  right: 2px;
  z-index: 2;
}
</style>
