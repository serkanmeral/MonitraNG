<script setup lang="ts">
import { computed, ref } from 'vue';
import type { WidgetActionConfig } from '@/utils/widgets/surfaceInteractions';
import { actionLabel } from '@/utils/widgets/surfaceInteractions';
import { canRunWidgetAction } from '@/utils/widgets/widgetActionExecutor';

const props = defineProps<{
  actions: WidgetActionConfig[];
  row?: Record<string, unknown> | null;
  isAdmin?: boolean;
  userGroups?: string[];
  loadingId?: string | null;
  t?: (key: string) => string;
}>();

const emit = defineEmits<{
  action: [action: WidgetActionConfig];
}>();

const lbl = (key: string) => props.t?.(`widgets.actions.${key}`) ?? key;

const visibleActions = computed(() =>
  props.actions.filter((a) =>
    canRunWidgetAction(a, {
      isAdmin: props.isAdmin,
      userGroups: props.userGroups,
      hasRow: !!props.row,
    }),
  ),
);
</script>

<template>
  <div v-if="visibleActions.length" class="d-flex flex-wrap ga-1 widget-action-bar">
    <v-btn
      v-for="action in visibleActions"
      :key="action.id"
      size="x-small"
      variant="tonal"
      class="text-none"
      :prepend-icon="action.icon"
      :loading="loadingId === action.id"
      @click.stop="emit('action', action)"
    >
      {{ actionLabel(action) }}
    </v-btn>
  </div>
</template>

<style scoped>
.widget-action-bar {
  margin-bottom: 4px;
}
</style>
