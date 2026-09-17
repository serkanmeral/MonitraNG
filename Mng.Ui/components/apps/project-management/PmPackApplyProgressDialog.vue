<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';

export type PmPackProgressStatus = 'pending' | 'running' | 'done' | 'skip' | 'error';

export interface PmPackProgressStep {
  id: string;
  label: string;
  status: PmPackProgressStatus;
  detail?: string;
}

const props = defineProps<{
  modelValue: boolean;
  packName: string;
  running: boolean;
  failed: boolean;
  steps: PmPackProgressStep[];
  mode?: 'apply' | 'detach';
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
}>();

const { t } = useAppI18n();

const title = computed(() => {
  const detach = props.mode === 'detach';
  if (props.failed) {
    return t(
      detach ? 'projectManagement.packCatalog.progressDetachFailedTitle' : 'projectManagement.packCatalog.progressFailedTitle',
      { name: props.packName },
    );
  }
  if (props.running) {
    return t(
      detach ? 'projectManagement.packCatalog.progressDetachTitle' : 'projectManagement.packCatalog.progressTitle',
      { name: props.packName },
    );
  }
  return t(
    detach ? 'projectManagement.packCatalog.progressDetachDoneTitle' : 'projectManagement.packCatalog.progressDoneTitle',
    { name: props.packName },
  );
});

function onToggle(open: boolean) {
  if (props.running && !open) return;
  emit('update:modelValue', open);
}

function stepIcon(status: PmPackProgressStatus) {
  if (status === 'running') return 'mdi-loading';
  if (status === 'done') return 'mdi-check-circle-outline';
  if (status === 'skip') return 'mdi-minus-circle-outline';
  if (status === 'error') return 'mdi-alert-circle-outline';
  return 'mdi-circle-outline';
}

function stepColor(status: PmPackProgressStatus) {
  if (status === 'running') return 'primary';
  if (status === 'done') return 'success';
  if (status === 'skip') return 'warning';
  if (status === 'error') return 'error';
  return undefined;
}
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    max-width="520"
    persistent
    scrollable
    @update:model-value="onToggle"
  >
    <v-card rounded="lg">
      <v-card-title class="px-6 py-4">{{ title }}</v-card-title>
      <v-divider />
      <v-card-text class="px-6 py-4">
        <div class="d-flex flex-column ga-2">
          <div
            v-for="step in steps"
            :key="step.id"
            class="d-flex align-start ga-3"
          >
            <v-icon
              :icon="stepIcon(step.status)"
              :color="stepColor(step.status)"
              :class="{ 'pack-progress-spin': step.status === 'running' }"
              size="22"
            />
            <div class="flex-grow-1">
              <div
                class="text-body-2"
                :class="{ 'text-medium-emphasis': step.status === 'pending' || step.status === 'skip' }"
              >
                {{ step.label }}
              </div>
              <div v-if="step.detail" class="text-caption text-medium-emphasis">{{ step.detail }}</div>
            </div>
          </div>
        </div>
      </v-card-text>
      <v-divider />
      <v-card-actions class="px-6 py-3">
        <v-spacer />
        <v-btn variant="text" :disabled="running" @click="onToggle(false)">
          {{ t('projectManagement.packCatalog.progressClose') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.pack-progress-spin {
  animation: pack-progress-spin 0.9s linear infinite;
}

@keyframes pack-progress-spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
