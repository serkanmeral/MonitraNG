<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  ocExtractDgErrorMessage,
  ocGetWorkspace,
  ocListPriorities,
  ocSaveWorkspaceEnabledPriorityIds,
} from '@/services/operationCoreService';
import type { OpPriority } from '@/types/apps/operationCore';
import { isTmStatusThemeColor } from '@/utils/taskManagerStatusColor';

const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();

const loading = ref(true);
const savingSelection = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);

const globalPriorities = ref<OpPriority[]>([]);
const selectedPriorityIds = ref<string[]>([]);

const sortedPriorities = computed(() =>
  [...globalPriorities.value].sort((a, b) => {
    const la = a.level ?? 999;
    const lb = b.level ?? 999;
    if (la !== lb) return la - lb;
    return a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });
  })
);

function togglePriorityId(id: string, enabled: boolean) {
  if (enabled) {
    if (!selectedPriorityIds.value.includes(id)) {
      selectedPriorityIds.value = [...selectedPriorityIds.value, id];
    }
  } else {
    selectedPriorityIds.value = selectedPriorityIds.value.filter((x) => x !== id);
  }
}

async function loadAll() {
  if (!props.workspaceId) return;
  loading.value = true;
  errorLocal.value = null;
  try {
    const [ws, priorities] = await Promise.all([
      ocGetWorkspace(props.workspaceId),
      ocListPriorities(),
    ]);
    globalPriorities.value = priorities;
    selectedPriorityIds.value = ws?.enabledPriorityIds ? [...ws.enabledPriorityIds] : [];
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.priorities.loadError')
    );
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.workspaceId,
  () => {
    void loadAll();
  },
  { immediate: true }
);

async function saveSelection() {
  if (!props.workspaceId) return;
  savingSelection.value = true;
  errorLocal.value = null;
  successLocal.value = null;
  try {
    await ocSaveWorkspaceEnabledPriorityIds(props.workspaceId, selectedPriorityIds.value);
    await loadAll();
    successLocal.value = t('operationCore.workspaceDefinitions.saveSuccess');
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.priorities.saveSelectionError')
    );
  } finally {
    savingSelection.value = false;
  }
}
</script>

<template>
  <div class="oc-ws-priorities-tab pa-4 pa-md-6">
    <v-alert
      v-if="errorLocal"
      type="error"
      variant="tonal"
      class="mb-4"
      closable
      @click:close="errorLocal = null"
    >
      {{ errorLocal }}
    </v-alert>

    <v-alert
      v-if="successLocal"
      type="success"
      variant="tonal"
      class="mb-4"
      closable
      @click:close="successLocal = null"
    >
      {{ successLocal }}
    </v-alert>

    <div v-if="loading" class="d-flex justify-center py-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <template v-else>
      <section>
        <h3 class="text-subtitle-1 font-weight-medium mb-1">
          {{ t('operationCore.workspaceDefinitions.priorities.catalogTitle') }}
        </h3>
        <p class="text-body-2 text-medium-emphasis mb-4">
          {{ t('operationCore.workspaceDefinitions.priorities.catalogSubtitle') }}
        </p>

        <v-alert
          v-if="!selectedPriorityIds.length"
          type="info"
          variant="tonal"
          density="compact"
          class="mb-4"
        >
          {{ t('operationCore.workspaceDefinitions.priorities.noneSelectedHint') }}
        </v-alert>

        <v-card variant="outlined" rounded="lg">
          <v-card-text class="pt-4">
            <div class="d-flex flex-column ga-1">
              <v-checkbox
                v-for="priority in sortedPriorities"
                :key="priority.__dataId"
                :model-value="selectedPriorityIds.includes(priority.__dataId)"
                hide-details
                density="compact"
                @update:model-value="(v) => togglePriorityId(priority.__dataId, !!v)"
              >
                <template #label>
                  <span>{{ priority.name }}</span>
                  <span v-if="priority.level != null" class="text-caption text-medium-emphasis ml-2">
                    ({{ t('operationCore.definitions.priorities.colLevel') }}: {{ priority.level }})
                  </span>
                  <v-chip
                    v-if="priority.color && isTmStatusThemeColor(priority.color)"
                    :color="priority.color"
                    size="x-small"
                    variant="tonal"
                    class="ml-2 text-none"
                  >
                    {{ priority.color }}
                  </v-chip>
                </template>
              </v-checkbox>
            </div>
          </v-card-text>
        </v-card>

        <div class="d-flex justify-end mt-4">
          <v-btn
            color="primary"
            rounded="lg"
            class="text-none"
            :loading="savingSelection"
            @click="saveSelection"
          >
            {{ t('operationCore.workspaceDefinitions.priorities.saveSelection') }}
          </v-btn>
        </div>
      </section>
    </template>
  </div>
</template>
