<script setup lang="ts">
import { onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { diExtractMessage, diGetLinkedWorkItems } from '@/services/documentIntelligenceService';
import type { DiLinkedWorkItem } from '@/types/apps/documentIntelligence';
import { buildWorkItemProfilePath } from '@/utils/ocWorkItemProfileNav';

const props = defineProps<{
  resourceId: string;
}>();

const { t } = useAppI18n();

const loading = ref(false);
const error = ref<string | null>(null);
const items = ref<DiLinkedWorkItem[]>([]);

async function load() {
  const id = props.resourceId.trim();
  if (!id) {
    items.value = [];
    return;
  }
  loading.value = true;
  error.value = null;
  try {
    const res = await diGetLinkedWorkItems(id);
    items.value = res.items;
  } catch (e: unknown) {
    error.value = diExtractMessage(e, t('documentIntelligence.linkedWorkItems.loadError'));
    items.value = [];
  } finally {
    loading.value = false;
  }
}

function profilePath(item: DiLinkedWorkItem): string {
  return buildWorkItemProfilePath(item.workItemId, {
    boardId: item.boardId,
    workspaceId: item.workspaceId,
    from: item.workspaceId ? 'workspace' : 'board',
  });
}

function relationLabel(type: string): string {
  const key = `documentIntelligence.linkRelationTypes.${type}`;
  const label = t(key);
  return label === key ? type : label;
}

watch(() => props.resourceId, () => load(), { immediate: false });
onMounted(() => load());
</script>

<template>
  <v-card variant="outlined" class="rounded-lg">
    <v-card-text class="pa-4">
      <div class="text-subtitle-2 font-weight-bold mb-2">
        {{ t('documentIntelligence.linkedWorkItems.title') }}
      </div>

      <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-2" />
      <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mb-2 rounded-lg">
        {{ error }}
      </v-alert>

      <div v-if="!loading && !items.length && !error" class="text-body-2 text-medium-emphasis">
        {{ t('documentIntelligence.linkedWorkItems.empty') }}
      </div>

      <v-list v-else-if="items.length" density="compact" class="py-0">
        <v-list-item
          v-for="item in items"
          :key="item.linkId"
          :to="profilePath(item)"
          class="px-0 rounded-lg"
          rounded="lg"
        >
          <template #prepend>
            <v-icon icon="mdi-clipboard-text-outline" size="20" class="mr-1" />
          </template>
          <v-list-item-title class="text-body-2 font-weight-medium">
            {{ item.workItemKey || item.workItemId }}
            <span v-if="item.workItemTitle" class="text-medium-emphasis font-weight-regular">
              — {{ item.workItemTitle }}
            </span>
          </v-list-item-title>
          <v-list-item-subtitle class="text-caption">
            {{ relationLabel(item.relationType) }}
          </v-list-item-subtitle>
        </v-list-item>
      </v-list>
    </v-card-text>
  </v-card>
</template>
