<script setup lang="ts">
import { ref, computed } from 'vue';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import type { TmProjectWorkflow } from '@/types/apps/taskManager';
import { normalizeWorkflow, buildDefaultWorkflow } from '@/utils/taskManagerWorkflow';

const props = defineProps<{
  modelValue: TmProjectWorkflow;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: TmProjectWorkflow];
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
const addStatusId = ref<string | null>(null);

const draft = computed({
  get: () => props.modelValue,
  set: (v: TmProjectWorkflow) => emit('update:modelValue', v),
});

const poolAvailable = computed(() => {
  const sel = new Set(draft.value?.statusIds ?? []);
  return store.statuses.filter((s) => !sel.has(s.__dataId));
});

function statusTitle(id: string): string {
  return store.statusById(id)?.name ?? id;
}

function moveStatusOrder(index: number, delta: -1 | 1) {
  const ids = draft.value.statusIds;
  const j = index + delta;
  if (j < 0 || j >= ids.length) return;
  const next = [...ids];
  [next[index], next[j]] = [next[j], next[index]];
  emit('update:modelValue', { ...draft.value, statusIds: next });
}

function addSelectedStatus() {
  if (!addStatusId.value) return;
  const id = addStatusId.value;
  if (draft.value.statusIds.includes(id)) return;
  const statusIds = [...draft.value.statusIds, id];
  const transitions = { ...draft.value.transitions, [id]: [] };
  for (const k of Object.keys(transitions)) {
    if (!statusIds.includes(k)) delete transitions[k];
  }
  let initialStatusId = draft.value.initialStatusId;
  let closedStatusId = draft.value.closedStatusId;
  if (!initialStatusId) initialStatusId = id;
  if (!closedStatusId) closedStatusId = id;
  emit('update:modelValue', {
    ...draft.value,
    statusIds,
    transitions,
    initialStatusId,
    closedStatusId,
  });
  addStatusId.value = null;
}

function removeStatus(id: string) {
  if (draft.value.statusIds.length <= 1) return;
  const statusIds = draft.value.statusIds.filter((x) => x !== id);
  const transitions = { ...draft.value.transitions };
  delete transitions[id];
  for (const k of Object.keys(transitions)) {
    transitions[k] = (transitions[k] ?? []).filter((t) => t !== id);
  }
  let initialStatusId = draft.value.initialStatusId;
  let closedStatusId = draft.value.closedStatusId;
  if (initialStatusId === id) initialStatusId = statusIds[0] ?? '';
  if (closedStatusId === id) closedStatusId = statusIds[statusIds.length - 1] ?? '';
  emit('update:modelValue', {
    ...draft.value,
    statusIds,
    transitions,
    initialStatusId,
    closedStatusId,
  });
}

function applyLinearTransitions() {
  const ids = draft.value.statusIds;
  const tr: Record<string, string[]> = {};
  for (let i = 0; i < ids.length; i++) {
    const cur = ids[i];
    tr[cur] = i < ids.length - 1 ? [ids[i + 1]] : [];
  }
  emit('update:modelValue', { ...draft.value, transitions: tr });
}

function resetFromPoolDefault() {
  const raw = buildDefaultWorkflow(store.statuses);
  const norm = normalizeWorkflow(raw, store.statuses);
  emit('update:modelValue', JSON.parse(JSON.stringify(norm)) as TmProjectWorkflow);
}

function targetItems(forStatusId: string) {
  return draft.value.statusIds
    .filter((id) => id !== forStatusId)
    .map((id) => ({ title: statusTitle(id), value: id }));
}

function setTransitionTargets(statusId: string, value: unknown) {
  const transitions = { ...draft.value.transitions, [statusId]: Array.isArray(value) ? (value as string[]) : [] };
  emit('update:modelValue', { ...draft.value, transitions });
}
</script>

<template>
  <div>
    <p class="text-body-2 text-medium-emphasis mb-4">
      {{ mt('taskManager.workflowIntro', 'Bu projede kullanılacak durumları havuzdan seçin, sıralayın; başlangıç ve kapalı durumunu belirleyin.') }}
    </p>

    <v-card class="tm-panel pa-4 mb-4" rounded="xl" flat>
      <div class="text-subtitle-2 font-weight-bold mb-2">{{ mt('taskManager.workflowAddStatuses', 'Havuzdan durum ekle') }}</div>
      <div class="d-flex flex-wrap gap-2 align-center">
        <v-select
          v-model="addStatusId"
          :items="poolAvailable.map((s) => ({ title: s.name, value: s.__dataId }))"
          item-title="title"
          item-value="value"
          density="comfortable"
          variant="outlined"
          hide-details
          style="min-width: 220px; max-width: 360px"
          :label="mt('taskManager.workflowPickStatus', 'Durum')"
          clearable
        />
        <v-btn color="primary" variant="tonal" rounded="lg" class="text-none" :disabled="!addStatusId" @click="addSelectedStatus">
          {{ mt('taskManager.workflowAdd', 'Ekle') }}
        </v-btn>
        <v-spacer />
        <v-btn variant="text" class="text-none" @click="applyLinearTransitions">
          {{ mt('taskManager.workflowLinearReset', 'Geçişleri sıralı komşu yap') }}
        </v-btn>
        <v-btn variant="text" class="text-none" @click="resetFromPoolDefault">
          {{ mt('taskManager.workflowResetDefault', 'Havuz sırasına sıfırla') }}
        </v-btn>
      </div>
    </v-card>

    <v-card class="tm-panel pa-4 mb-4" rounded="xl" flat>
      <div class="text-subtitle-2 font-weight-bold mb-2">{{ mt('taskManager.workflowOrderTitle', 'Sıra (Kanban kolonları)') }}</div>
      <p class="text-caption text-medium-emphasis mb-3">{{ mt('taskManager.workflowOrderHint', 'Yukarı / aşağı ile sırayı değiştirin.') }}</p>
      <div class="d-flex flex-column ga-2">
        <div
          v-for="(id, idx) in draft.statusIds"
          :key="id"
          class="d-flex align-center ga-2 tm-workflow-row pa-2 rounded-lg"
          style="border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity))"
        >
          <div class="d-flex flex-column">
            <v-btn icon variant="text" size="x-small" :disabled="idx === 0" aria-label="up" @click="moveStatusOrder(idx, -1)">
              <v-icon icon="mdi-chevron-up" size="20" />
            </v-btn>
            <v-btn
              icon
              variant="text"
              size="x-small"
              :disabled="idx === draft.statusIds.length - 1"
              aria-label="down"
              @click="moveStatusOrder(idx, 1)"
            >
              <v-icon icon="mdi-chevron-down" size="20" />
            </v-btn>
          </div>
          <span class="font-weight-medium flex-grow-1">{{ statusTitle(id) }}</span>
          <v-btn icon variant="text" color="error" size="small" :disabled="draft.statusIds.length <= 1" @click="removeStatus(id)">
            <v-icon icon="mdi-close" />
          </v-btn>
        </div>
      </div>
    </v-card>

    <v-row>
      <v-col cols="12" md="6">
        <v-card class="tm-panel pa-4 h-100" rounded="xl" flat>
          <div class="text-subtitle-2 font-weight-bold mb-3">{{ mt('taskManager.workflowInitialClosed', 'Başlangıç ve kapalı') }}</div>
          <div class="text-caption text-medium-emphasis mb-2">{{ mt('taskManager.workflowInitialLabel', 'Yeni görevlerin başlayacağı durum') }}</div>
          <v-radio-group :model-value="draft.initialStatusId" hide-details density="comfortable" @update:model-value="(v) => emit('update:modelValue', { ...draft, initialStatusId: String(v) })">
            <v-radio v-for="sid in draft.statusIds" :key="`i-${sid}`" :label="statusTitle(sid)" :value="sid" />
          </v-radio-group>
          <div class="text-caption text-medium-emphasis mt-4 mb-2">{{ mt('taskManager.workflowClosedLabel', 'Kapalı / terminal durum (raporlama)') }}</div>
          <v-radio-group :model-value="draft.closedStatusId" hide-details density="comfortable" @update:model-value="(v) => emit('update:modelValue', { ...draft, closedStatusId: String(v) })">
            <v-radio v-for="sid in draft.statusIds" :key="`c-${sid}`" :label="statusTitle(sid)" :value="sid" />
          </v-radio-group>
        </v-card>
      </v-col>
      <v-col cols="12" md="6">
        <v-card class="tm-panel pa-4" rounded="xl" flat>
          <div class="text-subtitle-2 font-weight-bold mb-2">{{ mt('taskManager.workflowTransitionsTitle', 'İzin verilen geçişler') }}</div>
          <p class="text-caption text-medium-emphasis mb-3">
            {{ mt('taskManager.workflowTransitionsHint', 'Her durumdan hangi durumlara kart sürüklenebilir veya detayda seçilebilir.') }}
          </p>
          <div class="d-flex flex-column ga-4" style="max-height: 420px; overflow-y: auto">
            <div v-for="sid in draft.statusIds" :key="`tr-${sid}`">
              <div class="text-body-2 font-weight-medium mb-1">{{ statusTitle(sid) }}</div>
              <v-select
                :model-value="draft.transitions[sid] ?? []"
                :items="targetItems(sid)"
                item-title="title"
                item-value="value"
                multiple
                chips
                closable-chips
                density="compact"
                variant="outlined"
                hide-details
                @update:model-value="(v) => setTransitionTargets(sid, v)"
              />
            </div>
          </div>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>
