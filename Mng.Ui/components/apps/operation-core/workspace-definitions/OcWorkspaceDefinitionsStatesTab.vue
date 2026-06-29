<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {

  ocGetWorkspace,
  ocListStates,
  ocSaveWorkspaceEnabledStateIds,
} from '@/services/operationCoreService';
import type { OpState } from '@/types/apps/operationCore';
import { OC_STATE_CATEGORIES } from '@/types/apps/operationCore';
import { isTmStatusThemeColor } from '@/utils/taskManagerStatusColor';

const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(true);
const savingSelection = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);

const globalStates = ref<OpState[]>([]);
const selectedStateIds = ref<string[]>([]);

const statesByCategory = computed(() => {
  const map = new Map<string, OpState[]>();
  for (const state of globalStates.value) {
    const cat = state.category || 'open';
    if (!map.has(cat)) map.set(cat, []);
    map.get(cat)!.push(state);
  }
  const order = [...OC_STATE_CATEGORIES];
  return [...map.entries()].sort(([a], [b]) => {
    const ai = order.indexOf(a as (typeof OC_STATE_CATEGORIES)[number]);
    const bi = order.indexOf(b as (typeof OC_STATE_CATEGORIES)[number]);
    if (ai >= 0 && bi >= 0) return ai - bi;
    return a.localeCompare(b);
  });
});

function categoryLabel(value: string) {
  const key = `operationCore.definitions.states.category.${value}`;
  const translated = t(key);
  return translated !== key ? translated : value;
}

function toggleStateId(id: string, enabled: boolean) {
  if (enabled) {
    if (!selectedStateIds.value.includes(id)) {
      selectedStateIds.value = [...selectedStateIds.value, id];
    }
  } else {
    selectedStateIds.value = selectedStateIds.value.filter((x) => x !== id);
  }
}

async function loadAll() {
  if (!props.workspaceId) return;
  loading.value = true;
  errorLocal.value = null;
  try {
    const [ws, states] = await Promise.all([
      ocGetWorkspace(props.workspaceId),
      ocListStates(),
    ]);
    globalStates.value = states;
    selectedStateIds.value = ws?.enabledStateIds ? [...ws.enabledStateIds] : [];
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.workspaceDefinitions.states.loadError');
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
    await ocSaveWorkspaceEnabledStateIds(props.workspaceId, selectedStateIds.value);
    await loadAll();
    successLocal.value = t('operationCore.workspaceDefinitions.saveSuccess');
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.workspaceDefinitions.states.saveSelectionError');
  } finally {
    savingSelection.value = false;
  }
}
</script>

<template>
  <div class="oc-ws-states-tab pa-4 pa-md-6">
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
          {{ t('operationCore.workspaceDefinitions.states.catalogTitle') }}
        </h3>
        <p class="text-body-2 text-medium-emphasis mb-4">
          {{ t('operationCore.workspaceDefinitions.states.catalogSubtitle') }}
        </p>

        <v-alert
          v-if="!selectedStateIds.length"
          type="info"
          variant="tonal"
          density="compact"
          class="mb-4"
        >
          {{ t('operationCore.workspaceDefinitions.states.noneSelectedHint') }}
        </v-alert>

        <v-card
          v-for="[category, states] in statesByCategory"
          :key="category"
          variant="outlined"
          rounded="lg"
          class="mb-3"
        >
          <v-card-title class="text-subtitle-2 py-3">
            {{ categoryLabel(category) }}
          </v-card-title>
          <v-divider />
          <v-card-text class="pt-3">
            <div class="d-flex flex-column ga-1">
              <v-checkbox
                v-for="state in states"
                :key="state.__dataId"
                :model-value="selectedStateIds.includes(state.__dataId)"
                hide-details
                density="compact"
                @update:model-value="(v) => toggleStateId(state.__dataId, !!v)"
              >
                <template #label>
                  <span>{{ state.name }}</span>
                  <v-chip
                    v-if="state.color && isTmStatusThemeColor(state.color)"
                    :color="state.color"
                    size="x-small"
                    variant="tonal"
                    class="ml-2 text-none"
                  >
                    {{ state.color }}
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
            {{ t('operationCore.workspaceDefinitions.states.saveSelection') }}
          </v-btn>
        </div>
      </section>
    </template>
  </div>
</template>
