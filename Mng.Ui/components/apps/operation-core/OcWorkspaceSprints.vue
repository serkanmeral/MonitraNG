<script setup lang="ts">
import { onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  ocActivateSprint,
  ocCloseSprint,
  ocCreateSprint,
  ocDeleteSprint,
  ocListSprintItems,
  ocListSprints,
  type OcSprint,
  type OcSprintItem,
} from '@/services/operationCore/sprints';

const props = defineProps<{ workspaceId: string }>();
const { t } = useAppI18n();

const rows = ref<OcSprint[]>([]);
const loading = ref(false);
const saving = ref(false);
const errorText = ref<string | null>(null);
const name = ref('');
const goal = ref('');
const startDate = ref('');
const endDate = ref('');
const openId = ref<string | null>(null);
const items = ref<OcSprintItem[]>([]);
const itemsLoading = ref(false);

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

async function load() {
  const workspaceId = props.workspaceId.trim();
  if (!workspaceId) return;
  loading.value = true;
  errorText.value = null;
  try {
    rows.value = await ocListSprints(workspaceId);
  } catch (error: unknown) {
    rows.value = [];
    errorText.value = messageOf(error);
  } finally {
    loading.value = false;
  }
}

async function createSprint() {
  saving.value = true;
  errorText.value = null;
  try {
    await ocCreateSprint(props.workspaceId, {
      name: name.value,
      goal: goal.value,
      startDate: startDate.value,
      endDate: endDate.value,
    });
    name.value = '';
    goal.value = '';
    startDate.value = '';
    endDate.value = '';
    await load();
  } catch (error: unknown) {
    errorText.value = messageOf(error);
  } finally {
    saving.value = false;
  }
}

async function run(action: () => Promise<unknown>) {
  saving.value = true;
  errorText.value = null;
  try {
    await action();
    await load();
  } catch (error: unknown) {
    errorText.value = messageOf(error);
  } finally {
    saving.value = false;
  }
}

async function toggleItems(sprintId: string) {
  if (openId.value === sprintId) {
    openId.value = null;
    items.value = [];
    return;
  }
  openId.value = sprintId;
  itemsLoading.value = true;
  items.value = [];
  try {
    items.value = await ocListSprintItems(props.workspaceId, sprintId);
  } catch (error: unknown) {
    errorText.value = messageOf(error);
  } finally {
    itemsLoading.value = false;
  }
}

function stateColor(state: OcSprint['state']): string {
  if (state === 'active') return 'primary';
  if (state === 'closed') return 'secondary';
  return 'default';
}

watch(() => props.workspaceId, () => {
  openId.value = null;
  items.value = [];
  void load();
});

onMounted(() => {
  void load();
});
</script>

<template>
  <div>
    <p class="text-body-2 text-medium-emphasis mb-4">{{ t('operationCore.sprints.hint') }}</p>

    <v-alert v-if="errorText" type="error" variant="tonal" density="compact" class="mb-4 rounded-lg" closable @click:close="errorText = null">
      {{ errorText }}
    </v-alert>

    <v-card variant="outlined" class="rounded-lg mb-4">
      <v-card-text class="pa-4">
        <div class="text-subtitle-2 font-weight-bold mb-3">{{ t('operationCore.sprints.create') }}</div>
        <v-row dense>
          <v-col cols="12" md="4">
            <v-text-field v-model="name" :label="t('operationCore.sprints.name')" density="compact" hide-details="auto" />
          </v-col>
          <v-col cols="6" md="2">
            <v-text-field v-model="startDate" type="date" :label="t('operationCore.sprints.start')" density="compact" hide-details="auto" />
          </v-col>
          <v-col cols="6" md="2">
            <v-text-field v-model="endDate" type="date" :label="t('operationCore.sprints.end')" density="compact" hide-details="auto" />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model="goal" :label="t('operationCore.sprints.goal')" density="compact" hide-details="auto" />
          </v-col>
        </v-row>
        <v-btn class="mt-3 text-none" color="primary" size="small" :loading="saving" :disabled="!name.trim() || !startDate || !endDate" @click="createSprint">
          {{ t('operationCore.sprints.create') }}
        </v-btn>
      </v-card-text>
    </v-card>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />
    <div v-else-if="!rows.length" class="text-body-2 text-medium-emphasis">{{ t('operationCore.sprints.empty') }}</div>

    <v-card v-for="sprint in rows" :key="sprint.id" variant="outlined" class="rounded-lg mb-3">
      <v-card-text class="pa-4">
        <div class="d-flex align-center flex-wrap ga-2 mb-2">
          <span class="text-subtitle-1 font-weight-medium">{{ sprint.name }}</span>
          <v-chip size="x-small" :color="stateColor(sprint.state)" variant="tonal">
            {{ t(`operationCore.sprints.state.${sprint.state}`) }}
          </v-chip>
          <span class="text-caption text-medium-emphasis">{{ sprint.startDate }} – {{ sprint.endDate }}</span>
          <v-spacer />
          <span class="text-caption">{{ t('operationCore.sprints.itemCount', { count: sprint.itemCount }) }}</span>
        </div>
        <p v-if="sprint.goal" class="text-body-2 text-medium-emphasis mb-3">{{ sprint.goal }}</p>
        <div class="d-flex flex-wrap ga-2">
          <v-btn size="small" variant="text" class="text-none" @click="toggleItems(sprint.id)">
            {{ openId === sprint.id ? t('operationCore.sprints.hideItems') : t('operationCore.sprints.showItems') }}
          </v-btn>
          <v-btn
            v-if="sprint.state === 'planned'"
            size="small"
            variant="tonal"
            color="primary"
            class="text-none"
            :loading="saving"
            @click="run(() => ocActivateSprint(workspaceId, sprint.id))"
          >
            {{ t('operationCore.sprints.activate') }}
          </v-btn>
          <v-btn
            v-if="sprint.state === 'active'"
            size="small"
            variant="tonal"
            class="text-none"
            :loading="saving"
            @click="run(() => ocCloseSprint(workspaceId, sprint.id))"
          >
            {{ t('operationCore.sprints.close') }}
          </v-btn>
          <v-btn
            v-if="sprint.state === 'planned' && sprint.itemCount === 0"
            size="small"
            variant="text"
            color="error"
            class="text-none"
            :loading="saving"
            @click="run(() => ocDeleteSprint(workspaceId, sprint.id))"
          >
            {{ t('operationCore.sprints.delete') }}
          </v-btn>
        </div>
        <div v-if="openId === sprint.id" class="mt-3">
          <v-progress-linear v-if="itemsLoading" indeterminate color="primary" />
          <div v-else-if="!items.length" class="text-caption text-medium-emphasis">{{ t('operationCore.sprints.noItems') }}</div>
          <v-list v-else density="compact" class="py-0">
            <v-list-item v-for="item in items" :key="item.id" :title="item.title" :subtitle="item.key" />
          </v-list>
        </div>
      </v-card-text>
    </v-card>
  </div>
</template>
