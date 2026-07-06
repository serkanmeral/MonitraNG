<script setup lang="ts">
import { ref, watch } from 'vue';
import DiMarkdownViewer from '@/components/apps/document-intelligence/DiMarkdownViewer.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  diGetMarkdownVersionContent,
  diGetMarkdownVersions,
  diRestoreMarkdownVersion,
} from '@/services/documentIntelligenceService';
import type { DiMarkdownVersion, DiResource } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  modelValue: boolean;
  resourceId: string | null;
  canRestore?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  restored: [resource: DiResource];
}>();

const { t } = useAppI18n();
const { push } = useAppToast();
const panelError = usePanelErrorNotify('errors.dg.generic');

const versions = ref<DiMarkdownVersion[]>([]);
const versionsLoading = ref(false);
const selectedVersion = ref<number | null>(null);
const versionContent = ref('');
const versionContentLoading = ref(false);
const restoringVersion = ref<number | null>(null);

function close() {
  emit('update:modelValue', false);
}

function formatDateTime(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('tr-TR', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

async function loadVersions() {
  const id = props.resourceId?.trim();
  if (!id) {
    versions.value = [];
    return;
  }
  versionsLoading.value = true;
  selectedVersion.value = null;
  versionContent.value = '';
  try {
    versions.value = await diGetMarkdownVersions(id);
  } catch (e: unknown) {
    push({
      title: t('errors.dg.toastTitle'),
      message: panelError(e, 'documentIntelligence.errors.versionsLoad'),
      severity: 'error',
    });
    versions.value = [];
  } finally {
    versionsLoading.value = false;
  }
}

async function previewVersion(v: DiMarkdownVersion) {
  const id = props.resourceId?.trim();
  if (!id) return;
  selectedVersion.value = v.versionNumber;
  versionContentLoading.value = true;
  try {
    const c = await diGetMarkdownVersionContent(id, v.versionNumber);
    versionContent.value = c.content;
  } catch (e: unknown) {
    push({
      title: t('errors.dg.toastTitle'),
      message: panelError(e, 'documentIntelligence.errors.versionLoad'),
      severity: 'error',
    });
    versionContent.value = '';
  } finally {
    versionContentLoading.value = false;
  }
}

async function restoreVersion(v: DiMarkdownVersion) {
  const id = props.resourceId?.trim();
  if (!id || v.isCurrent || !props.canRestore) return;
  restoringVersion.value = v.versionNumber;
  try {
    const restored = await diRestoreMarkdownVersion(id, v.versionNumber);
    close();
    push({
      title: t('documentIntelligence.notify.successTitle'),
      message: t('documentIntelligence.versionRestored', { n: v.versionNumber }),
      severity: 'success',
    });
    emit('restored', restored);
  } catch (e: unknown) {
    push({
      title: t('errors.dg.toastTitle'),
      message: panelError(e, 'documentIntelligence.errors.versionRestore'),
      severity: 'error',
    });
  } finally {
    restoringVersion.value = null;
  }
}

watch(
  () => [props.modelValue, props.resourceId] as const,
  ([open]) => {
    if (open) void loadVersions();
  }
);
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="980" scrollable @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center text-subtitle-1 font-weight-bold">
        <v-icon size="20" class="mr-2">mdi-history</v-icon>
        {{ t('documentIntelligence.versionHistory') }}
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="close" />
      </v-card-title>
      <v-divider />
      <v-card-text class="pa-0">
        <div class="d-flex di-history">
          <div class="di-history-list">
            <div v-if="versionsLoading" class="d-flex justify-center pa-6">
              <v-progress-circular indeterminate size="28" color="primary" />
            </div>
            <div v-else-if="!versions.length" class="text-medium-emphasis text-body-2 pa-4 text-center">
              {{ t('documentIntelligence.noVersions') }}
            </div>
            <v-list v-else density="compact" nav>
              <v-list-item
                v-for="v in versions"
                :key="v.versionNumber"
                :active="selectedVersion === v.versionNumber"
                rounded="lg"
                @click="previewVersion(v)"
              >
                <template #prepend>
                  <v-avatar size="28" :color="v.isCurrent ? 'primary' : 'grey-lighten-1'" class="text-caption">
                    v{{ v.versionNumber }}
                  </v-avatar>
                </template>
                <v-list-item-title class="text-body-2">
                  {{ formatDateTime(v.createdAt) || ('v' + v.versionNumber) }}
                  <v-chip v-if="v.isCurrent" size="x-small" color="primary" variant="tonal" class="ml-1">
                    {{ t('documentIntelligence.currentVersion') }}
                  </v-chip>
                </v-list-item-title>
                <v-list-item-subtitle class="text-caption">
                  <span v-if="v.createdBy">{{ v.createdBy }}</span>
                  <span v-if="v.changeNote"> · {{ v.changeNote }}</span>
                </v-list-item-subtitle>
                <template v-if="canRestore" #append>
                  <v-btn
                    v-if="!v.isCurrent"
                    size="x-small"
                    variant="tonal"
                    color="primary"
                    class="text-none"
                    :loading="restoringVersion === v.versionNumber"
                    @click.stop="restoreVersion(v)"
                  >
                    {{ t('documentIntelligence.restore') }}
                  </v-btn>
                </template>
              </v-list-item>
            </v-list>
          </div>
          <v-divider vertical />
          <div class="di-history-preview pa-4">
            <div v-if="versionContentLoading" class="d-flex justify-center pa-6">
              <v-progress-circular indeterminate size="28" color="primary" />
            </div>
            <div
              v-else-if="selectedVersion === null"
              class="text-medium-emphasis text-body-2 d-flex align-center justify-center fill-height"
            >
              {{ t('documentIntelligence.selectVersionHint') }}
            </div>
            <DiMarkdownViewer v-else :content="versionContent" :empty-label="t('documentIntelligence.emptyPage')" />
          </div>
        </div>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.di-history {
  height: 65vh;
}
.di-history-list {
  width: 340px;
  flex-shrink: 0;
  overflow: auto;
}
.di-history-preview {
  flex: 1 1 auto;
  overflow: auto;
}
</style>
