<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { isDiManagedDocument } from '@/utils/diFilePreview';
import { diPageResourceLabel } from '@/utils/diPageResource';
import type { DiResource } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  modelValue: boolean;
  resource: DiResource | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
}>();

const { t } = useAppI18n();

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

function formatDateTime(iso: string | null): string {
  if (!iso) return '—';
  try {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

function formatSize(bytes: number | null): string {
  if (bytes == null || !Number.isFinite(bytes)) return '—';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function resourceTypeKey(r: DiResource): string {
  if (r.type === 'markdown') return 'documentIntelligence.typePage';
  if (isDiManagedDocument(r)) return 'documentIntelligence.typeDocument';
  if (r.type === 'file') return 'documentIntelligence.typeFile';
  return 'documentIntelligence.typePage';
}
</script>

<template>
  <v-dialog v-model="open" max-width="480">
    <v-card v-if="resource" rounded="lg">
      <v-card-title class="text-subtitle-1 font-weight-bold">
        {{ t('documentIntelligence.resourceInfoTitle') }}
      </v-card-title>
      <v-card-text class="pt-0">
        <v-list density="compact" class="py-0">
          <v-list-item v-if="isDiManagedDocument(resource)">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('documentIntelligence.documentNoLabel') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2 font-weight-medium">
              {{ resource.documentNo || '—' }}
            </v-list-item-subtitle>
          </v-list-item>
          <v-list-item>
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('documentIntelligence.resourceInfoName') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">
              {{ diPageResourceLabel(resource) }}
            </v-list-item-subtitle>
          </v-list-item>
          <v-list-item>
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('documentIntelligence.resourceInfoType') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">
              {{ t(resourceTypeKey(resource)) }}
            </v-list-item-subtitle>
          </v-list-item>
          <v-list-item v-if="resource.type === 'file' && resource.size">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('documentIntelligence.resourceInfoSize') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">
              {{ formatSize(resource.size) }}
              <span v-if="resource.currentVersionNumber"> · v{{ resource.currentVersionNumber }}</span>
            </v-list-item-subtitle>
          </v-list-item>
          <v-list-item>
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('documentIntelligence.metaCreated') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">
              {{ resource.createdBy || '—' }}
              <span v-if="resource.createdAt"> · {{ formatDateTime(resource.createdAt) }}</span>
            </v-list-item-subtitle>
          </v-list-item>
          <v-list-item>
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('documentIntelligence.metaUpdated') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">
              {{ resource.updatedBy || '—' }}
              <span v-if="resource.updatedAt"> · {{ formatDateTime(resource.updatedAt) }}</span>
            </v-list-item-subtitle>
          </v-list-item>
          <v-list-item v-if="resource.description">
            <v-list-item-title class="text-caption text-medium-emphasis">
              {{ t('documentIntelligence.resourceInfoDescription') }}
            </v-list-item-title>
            <v-list-item-subtitle class="text-body-2">
              {{ resource.description }}
            </v-list-item-subtitle>
          </v-list-item>
        </v-list>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" class="text-none" @click="open = false">
          {{ t('documentIntelligence.cancel') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
