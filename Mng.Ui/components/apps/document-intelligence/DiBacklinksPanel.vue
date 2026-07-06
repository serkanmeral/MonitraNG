<script setup lang="ts">
import { onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { buildDiResourceUrl } from '@/utils/diResourceLink';
import { diGetMarkdownBacklinks } from '@/services/documentIntelligenceService';
import { diPageResourceIcon } from '@/utils/diPageResource';
import type { DiResource } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  resourceId: string;
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(false);
const error = ref<string | null>(null);
const items = ref<DiResource[]>([]);

function pageLabel(r: DiResource): string {
  return r.title || r.name || r.id;
}

function pagePath(r: DiResource): string {
  return buildDiResourceUrl(r.id);
}

async function load() {
  const id = props.resourceId.trim();
  if (!id) {
    items.value = [];
    return;
  }
  loading.value = true;
  error.value = null;
  try {
    const res = await diGetMarkdownBacklinks(id);
    items.value = res.items;
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.backlinks.loadError');
    items.value = [];
  } finally {
    loading.value = false;
  }
}

watch(() => props.resourceId, () => load(), { immediate: false });
onMounted(() => load());
</script>

<template>
  <v-card variant="outlined" class="rounded-lg">
    <v-card-text class="pa-4">
      <div class="text-subtitle-2 font-weight-bold mb-2">
        {{ t('documentIntelligence.backlinks.title') }}
      </div>

      <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-2" />
      <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mb-2 rounded-lg">
        {{ error }}
      </v-alert>

      <div v-if="!loading && !items.length && !error" class="text-body-2 text-medium-emphasis">
        {{ t('documentIntelligence.backlinks.empty') }}
      </div>

      <v-list v-else-if="items.length" density="compact" class="py-0">
        <v-list-item
          v-for="item in items"
          :key="item.id"
          :to="pagePath(item)"
          class="px-0 rounded-lg"
          rounded="lg"
        >
          <template #prepend>
            <v-icon :icon="diPageResourceIcon(item)" size="20" class="mr-2" />
          </template>
          <v-list-item-title class="text-body-2">{{ pageLabel(item) }}</v-list-item-title>
          <v-list-item-subtitle v-if="item.status === 'draft'" class="text-caption">
            {{ t('documentIntelligence.draft') }}
          </v-list-item-subtitle>
        </v-list-item>
      </v-list>
    </v-card-text>
  </v-card>
</template>
