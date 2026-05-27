<script setup lang="ts">
import { computed } from 'vue';
import type { OcWorkItemCard } from '@/types/apps/operationCore';
import { useAppI18n } from '@/composables/useAppI18n';

const props = withDefaults(
  defineProps<{
    card: OcWorkItemCard;
    boardId?: string | null;
    showAssignee?: boolean;
  }>(),
  {
    boardId: null,
    showAssignee: true,
  }
);

const { t } = useAppI18n();

const assigneeInitials = computed(() => {
  const a = props.card.assignee?.trim();
  if (!a) return '';
  const parts = a.split(/[\s@._-]+/).filter(Boolean);
  if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase();
  return a.slice(0, 2).toUpperCase();
});

const profileTo = computed(() => {
  const qs = new URLSearchParams();
  qs.set('from', 'board');
  if (props.boardId) qs.set('boardId', props.boardId);
  return `/apps/operation-core/work-items/${encodeURIComponent(props.card.id)}/profile?${qs.toString()}`;
});
</script>

<template>
  <div class="oc-wi-card pa-3 position-relative">
    <v-btn
      icon="mdi-card-account-details-outline"
      size="x-small"
      variant="text"
      density="compact"
      class="oc-wi-card__profile-btn position-absolute"
      color="primary"
      :to="profileTo"
      :title="t('operationCore.board.openProfile')"
      :aria-label="t('operationCore.board.openProfile')"
      @click.stop
    />
    <NuxtLink :to="profileTo" class="oc-wi-card__link text-decoration-none text-reset d-block pr-7">
      <div class="oc-wi-card__meta">{{ card.key }}</div>
      <div class="oc-wi-card__title">{{ card.title }}</div>
      <div class="oc-wi-card__foot">
        <span />
        <span v-if="showAssignee && assigneeInitials" class="oc-avatar-tiny">{{ assigneeInitials }}</span>
      </div>
    </NuxtLink>
  </div>
</template>

<style scoped>
.oc-wi-card {
  display: block;
  background: rgb(var(--v-theme-surface));
  border-radius: 12px;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.08);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
  transition: box-shadow 0.15s ease, border-color 0.15s ease;
}
.oc-wi-card:hover {
  border-color: rgba(var(--v-theme-primary), 0.35);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
}
.oc-wi-card__profile-btn {
  top: 2px;
  right: 2px;
  z-index: 2;
}
.oc-wi-card__meta {
  font-size: 0.7rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  color: rgba(var(--v-theme-on-surface), 0.55);
  margin-bottom: 4px;
}
.oc-wi-card__title {
  font-size: 0.875rem;
  font-weight: 500;
  line-height: 1.35;
  color: rgb(var(--v-theme-on-surface));
}
.oc-wi-card__foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 10px;
  min-height: 22px;
}
.oc-avatar-tiny {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  font-size: 0.65rem;
  font-weight: 700;
  background: rgba(var(--v-theme-primary), 0.14);
  color: rgb(var(--v-theme-primary));
}
</style>
