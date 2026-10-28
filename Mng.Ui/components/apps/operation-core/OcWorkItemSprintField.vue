<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocListSprints, ocSetSprintMembership, type OcSprint } from '@/services/operationCore/sprints';

const props = defineProps<{
  workspaceId: string;
  workItemId: string;
  sprintId?: string | null;
  canEdit: boolean;
  canJoin: boolean;
}>();

const emit = defineEmits<{ changed: [] }>();
const { t } = useAppI18n();

const sprints = ref<OcSprint[]>([]);
const saving = ref(false);
const errorText = ref<string | null>(null);
const selected = ref<string>('');

const options = computed(() => {
  const rows = sprints.value.filter((sprint) => sprint.state !== 'closed' || sprint.id === props.sprintId);
  return [
    { title: t('operationCore.sprints.backlog'), value: '' },
    ...rows.map((sprint) => ({
      title: sprint.state === 'closed' ? `${sprint.name} (${t('operationCore.sprints.state.closed')})` : sprint.name,
      value: sprint.id,
    })),
  ];
});

const currentName = computed(() => sprints.value.find((sprint) => sprint.id === props.sprintId)?.name || t('operationCore.sprints.backlog'));

async function load() {
  if (!props.canJoin || !props.workspaceId) {
    sprints.value = [];
    return;
  }
  try {
    sprints.value = await ocListSprints(props.workspaceId);
  } catch (error: unknown) {
    errorText.value = error instanceof Error ? error.message : String(error);
  }
}

async function onChange(value: string | null) {
  const next = value || '';
  if (next === (props.sprintId || '')) return;
  saving.value = true;
  errorText.value = null;
  try {
    await ocSetSprintMembership(props.workspaceId, props.workItemId, next || null);
    emit('changed');
  } catch (error: unknown) {
    selected.value = props.sprintId || '';
    errorText.value = error instanceof Error ? error.message : String(error);
  } finally {
    saving.value = false;
  }
}

watch(
  () => [props.workspaceId, props.canJoin, props.sprintId] as const,
  () => {
    selected.value = props.sprintId || '';
    void load();
  },
  { immediate: true }
);
</script>

<template>
  <div v-if="canJoin" class="mb-3">
    <div class="text-caption text-medium-emphasis mb-1">{{ t('operationCore.sprints.title') }}</div>
    <v-select
      v-if="canEdit"
      :model-value="selected"
      :items="options"
      item-title="title"
      item-value="value"
      density="compact"
      hide-details
      variant="outlined"
      :loading="saving"
      :disabled="saving"
      @update:model-value="onChange"
    />
    <div v-else class="text-body-2">{{ currentName }}</div>
    <div v-if="errorText" class="text-caption text-error mt-1">{{ errorText }}</div>
  </div>
</template>
