<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { DiDocumentEditorLockStatus, DiEditorLockChoice } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  modelValue: boolean;
  status: DiDocumentEditorLockStatus | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  choose: [choice: DiEditorLockChoice];
}>();

const { t } = useAppI18n();

const open = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value),
});

const isSelfOnly = computed(
  () => Boolean(props.status?.isLockedBySelf && !props.status?.isLockedByOthers),
);

const dialogTitle = computed(() =>
  isSelfOnly.value
    ? t('documentIntelligence.editorLock.titleSelf')
    : t('documentIntelligence.editorLock.title'),
);

const dialogMessage = computed(() => {
  const s = props.status;
  if (!s) return '';
  if (s.isLockedBySelf && s.isLockedByOthers) {
    return t('documentIntelligence.editorLock.messageMixed');
  }
  if (s.isLockedBySelf) return t('documentIntelligence.editorLock.messageSelf');
  return t('documentIntelligence.editorLock.message');
});

const showEditAnyway = computed(() => {
  const s = props.status;
  if (!s || isSelfOnly.value) return false;
  if (s.canBypassLock) return true;
  return !s.enforceExclusiveLock;
});

const showReadOnly = computed(() =>
  Boolean(props.status?.isLocked || props.status?.isLockedByOthers || props.status?.isLockedBySelf),
);

function editorLabel(editor: { userName: string; userId: string; isCurrentUser: boolean }): string {
  if (editor.isCurrentUser) {
    return t('documentIntelligence.editorLock.youOtherTab');
  }
  return editor.userName || editor.userId;
}

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '';
  try {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'short',
      timeStyle: 'short',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

function choose(choice: DiEditorLockChoice) {
  emit('choose', choice);
  open.value = false;
}

function onCancel() {
  choose('cancel');
}
</script>

<template>
  <v-dialog v-model="open" max-width="520" persistent>
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center ga-2 py-3">
        <v-icon
          :icon="isSelfOnly ? 'mdi-tab-plus' : 'mdi-account-edit-outline'"
          color="warning"
        />
        <span class="text-subtitle-1 font-weight-bold">
          {{ dialogTitle }}
        </span>
      </v-card-title>

      <v-divider />

      <v-card-text class="pt-4">
        <p class="text-body-2 mb-3">
          {{ dialogMessage }}
        </p>

        <v-list
          v-if="status?.activeEditors?.length && !isSelfOnly"
          density="compact"
          class="rounded-lg border py-0 mb-2"
        >
          <v-list-item
            v-for="(editor, index) in status.activeEditors"
            :key="`${editor.userId}-${editor.lastSeenAt}-${index}`"
            :title="editorLabel(editor)"
            :subtitle="formatDateTime(editor.lastSeenAt) ? t('documentIntelligence.editorLock.lastActivity', { time: formatDateTime(editor.lastSeenAt) }) : undefined"
            prepend-icon="mdi-account-circle-outline"
          />
        </v-list>

        <v-alert
          v-if="isSelfOnly"
          type="warning"
          variant="tonal"
          density="compact"
          class="rounded-lg mb-0"
        >
          {{ t('documentIntelligence.editorLock.selfLockHint') }}
        </v-alert>
        <v-alert
          v-else-if="status?.enforceExclusiveLock && !status?.canBypassLock"
          type="warning"
          variant="tonal"
          density="compact"
          class="rounded-lg mb-0"
        >
          {{ t('documentIntelligence.editorLock.hardLockHint') }}
        </v-alert>
        <v-alert
          v-else-if="status?.canBypassLock"
          type="info"
          variant="tonal"
          density="compact"
          class="rounded-lg mb-0"
        >
          {{ t('documentIntelligence.editorLock.bypassHint') }}
        </v-alert>
      </v-card-text>

      <v-divider />

      <v-card-actions class="pa-3">
        <v-spacer />
        <v-btn variant="text" class="text-none" @click="onCancel">
          {{ t('documentIntelligence.editorLock.cancel') }}
        </v-btn>
        <v-btn
          v-if="showReadOnly"
          variant="tonal"
          color="warning"
          class="text-none"
          @click="choose('readOnly')"
        >
          {{ t('documentIntelligence.editorLock.openReadOnly') }}
        </v-btn>
        <v-btn
          v-if="showEditAnyway"
          variant="flat"
          color="primary"
          class="text-none"
          @click="choose('edit')"
        >
          {{
            status?.canBypassLock
              ? t('documentIntelligence.editorLock.editAnywayAdmin')
              : t('documentIntelligence.editorLock.continueEditing')
          }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.border {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}
</style>
