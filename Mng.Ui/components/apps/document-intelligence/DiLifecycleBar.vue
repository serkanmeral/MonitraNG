<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { diChangeResourceLifecycle, diSetResourceBaseline } from '@/services/documentIntelligenceService';
import { diLifecycleChipColor, diLifecycleStatus } from '@/utils/diPageResource';
import type { DiResource } from '@/types/apps/documentIntelligence';

const props = withDefaults(
  defineProps<{
    resource: DiResource;
    canEdit?: boolean;
    showActions?: boolean;
  }>(),
  { canEdit: false, showActions: true }
);

const emit = defineEmits<{
  updated: [resource: DiResource];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const busy = ref(false);
const error = ref<string | null>(null);
const noteOpen = ref(false);
const noteText = ref('');
const pendingAction = ref<'submit' | 'approve' | 'reject' | 'revise' | 'baseline' | null>(null);

const status = computed(() => diLifecycleStatus(props.resource.status));
const statusColor = computed(() => diLifecycleChipColor(props.resource.status));
const statusLabel = computed(() => t(`documentIntelligence.lifecycle.statuses.${status.value}`));

const showStatusChip = computed(() => status.value !== 'published' || props.showActions);

const canSubmit = computed(() => props.canEdit && (status.value === 'draft' || status.value === 'published'));
const canApprove = computed(() => props.canEdit && (status.value === 'draft' || status.value === 'inReview'));
const canReject = computed(() => props.canEdit && status.value === 'inReview');
const canRevise = computed(() => props.canEdit && status.value !== 'draft');
const canBaseline = computed(() => props.canEdit);

function statusIcon(): string {
  if (status.value === 'draft') return 'mdi-file-document-edit-outline';
  if (status.value === 'inReview') return 'mdi-clock-outline';
  return 'mdi-check-decagram-outline';
}

function openNote(action: 'submit' | 'approve' | 'reject' | 'revise' | 'baseline') {
  pendingAction.value = action;
  noteText.value = '';
  error.value = null;
  noteOpen.value = true;
}

async function confirmNote() {
  const action = pendingAction.value;
  if (!action) return;
  busy.value = true;
  error.value = null;
  try {
    const note = noteText.value.trim() || null;
    const updated =
      action === 'baseline'
        ? await diSetResourceBaseline(props.resource.id, { note })
        : await diChangeResourceLifecycle(props.resource.id, { action, note });
    emit('updated', updated);
    noteOpen.value = false;
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.lifecycle.error');
  } finally {
    busy.value = false;
  }
}

function noteTitle(): string {
  const action = pendingAction.value;
  if (!action) return '';
  return t(`documentIntelligence.lifecycle.noteTitle.${action}`);
}
</script>

<template>
  <div class="d-flex flex-wrap align-center ga-2">
    <v-chip
      v-if="showStatusChip"
      size="small"
      variant="flat"
      :color="statusColor"
      :prepend-icon="statusIcon()"
      class="text-none"
    >
      {{ statusLabel }}
    </v-chip>

    <v-chip
      v-if="resource.baselineVersionNumber"
      size="small"
      variant="tonal"
      :color="resource.baselineDrifted ? 'warning' : 'primary'"
      prepend-icon="mdi-flag-checkered"
      class="text-none"
    >
      {{ t('documentIntelligence.lifecycle.baselineChip', { n: resource.baselineVersionNumber }) }}
      <span v-if="resource.baselineDrifted" class="ml-1">
        · {{ t('documentIntelligence.lifecycle.drifted') }}
      </span>
    </v-chip>

    <template v-if="showActions && canEdit">
      <v-btn
        v-if="canSubmit && status !== 'inReview'"
        size="small"
        variant="tonal"
        class="text-none"
        prepend-icon="mdi-send-outline"
        :disabled="busy"
        @click="openNote('submit')"
      >
        {{ t('documentIntelligence.lifecycle.submit') }}
      </v-btn>
      <v-btn
        v-if="canApprove"
        size="small"
        color="success"
        variant="tonal"
        class="text-none"
        prepend-icon="mdi-check"
        :disabled="busy"
        @click="openNote('approve')"
      >
        {{ t('documentIntelligence.lifecycle.approve') }}
      </v-btn>
      <v-btn
        v-if="canReject"
        size="small"
        variant="text"
        class="text-none"
        prepend-icon="mdi-undo"
        :disabled="busy"
        @click="openNote('reject')"
      >
        {{ t('documentIntelligence.lifecycle.reject') }}
      </v-btn>
      <v-btn
        v-if="canRevise"
        size="small"
        variant="text"
        class="text-none"
        prepend-icon="mdi-file-restore-outline"
        :disabled="busy"
        @click="openNote('revise')"
      >
        {{ t('documentIntelligence.lifecycle.revise') }}
      </v-btn>
      <v-btn
        v-if="canBaseline"
        size="small"
        variant="text"
        class="text-none"
        prepend-icon="mdi-flag-checkered"
        :disabled="busy"
        @click="openNote('baseline')"
      >
        {{ t('documentIntelligence.lifecycle.setBaseline') }}
      </v-btn>
    </template>

    <v-dialog v-model="noteOpen" max-width="460">
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold">{{ noteTitle() }}</v-card-title>
        <v-card-text>
          <p class="text-caption text-medium-emphasis mb-3">
            {{ t('documentIntelligence.lifecycle.noteHint') }}
          </p>
          <v-textarea
            v-model="noteText"
            :label="t('documentIntelligence.lifecycle.noteLabel')"
            variant="outlined"
            density="comfortable"
            rows="3"
            auto-grow
          />
          <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-2">
            {{ error }}
          </v-alert>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" :disabled="busy" @click="noteOpen = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn color="primary" variant="flat" class="text-none" :loading="busy" @click="confirmNote">
            {{ t('documentIntelligence.lifecycle.confirm') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
