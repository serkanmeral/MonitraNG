<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import DiPickResourceDialog from '@/components/apps/document-intelligence/DiPickResourceDialog.vue';
import {
  diCreateResourceLink,
  diDeleteResourceLink,
  diGetRelatedResources,
  diListRelationTypes,
} from '@/services/documentIntelligenceService';
import {
  DI_RESOURCE_DOC_LINK_RELATION_TYPES,
  DI_RESOURCE_TYPE,
  type DiLinkedResource,
  type DiRelationType,
  type DiResource,
} from '@/types/apps/documentIntelligence';
import { buildDiResourceUrl } from '@/utils/diResourceLink';

const props = withDefaults(
  defineProps<{
    resourceId: string;
    canEdit?: boolean;
  }>(),
  { canEdit: false }
);

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(false);
const error = ref<string | null>(null);
const items = ref<DiLinkedResource[]>([]);
const pickOpen = ref(false);
const relationType = ref('reference');
const relationOptions = ref<DiRelationType[]>([]);
const deletingId = ref<string | null>(null);

const relationItems = computed(() => {
  if (relationOptions.value.length) {
    return relationOptions.value
      .filter((x) => x.code)
      .map((x) => ({ value: x.code, title: x.displayName || relationLabel(x.code) }));
  }
  return DI_RESOURCE_DOC_LINK_RELATION_TYPES.map((value) => ({
    value,
    title: relationLabel(value),
  }));
});

const excludedIds = computed(() => {
  const ids = items.value.map((x) => x.resourceId);
  if (props.resourceId) ids.push(props.resourceId);
  return ids;
});

function relationLabel(type: string): string {
  const key = `documentIntelligence.linkRelationTypes.${type}`;
  const label = t(key);
  return label === key ? type : label;
}

function kindLabel(kind: string | null | undefined): string {
  const value = kind?.trim();
  if (!value) return '';
  const key = `documentIntelligence.resourceKinds.${value}`;
  const label = t(key);
  return label === key ? value : label;
}

function directionLabel(direction: string | null | undefined): string {
  if (direction === 'incoming') return t('documentIntelligence.relatedResources.incoming');
  if (direction === 'outgoing') return t('documentIntelligence.relatedResources.outgoing');
  return '';
}

function resourceLabel(item: DiLinkedResource): string {
  return item.title?.trim() || item.name?.trim() || item.resourceId;
}

function resourcePath(item: DiLinkedResource): string {
  return buildDiResourceUrl(item.resourceId);
}

function resourceIcon(item: DiLinkedResource): string {
  if (item.resourceType === DI_RESOURCE_TYPE.markdown) return 'mdi-language-markdown-outline';
  if (item.kind === 'diagram') return 'mdi-graph-outline';
  return 'mdi-file-outline';
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
    const res = await diGetRelatedResources(id);
    items.value = res.items;
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.relatedResources.loadError');
    items.value = [];
  } finally {
    loading.value = false;
  }
}

async function loadRelations() {
  try {
    const res = await diListRelationTypes(true);
    relationOptions.value = res.items.filter((x) => {
      const applies = (x.appliesTo ?? '').toLowerCase();
      return !applies || applies === 'both' || applies === 'resource';
    });
  } catch {
    relationOptions.value = [];
  }
}

async function onPicked(target: DiResource) {
  const sourceId = props.resourceId.trim();
  const targetId = target.id.trim();
  if (!sourceId || !targetId) return;
  try {
    await diCreateResourceLink({
      resourceId: sourceId,
      targetModule: 'documentIntelligence',
      targetType: 'resource',
      targetId,
      relationType: relationType.value,
    });
    await load();
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.relatedResources.linkError');
  }
}

async function unlink(item: DiLinkedResource) {
  deletingId.value = item.linkId;
  error.value = null;
  try {
    await diDeleteResourceLink(item.linkId);
    await load();
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.relatedResources.unlinkError');
  } finally {
    deletingId.value = null;
  }
}

watch(() => props.resourceId, () => load(), { immediate: false });
onMounted(() => {
  void load();
  void loadRelations();
});
</script>

<template>
  <v-card variant="outlined" class="rounded-lg">
    <v-card-text class="pa-4">
      <div class="d-flex align-center justify-space-between ga-2 mb-2">
        <div class="text-subtitle-2 font-weight-bold">
          {{ t('documentIntelligence.relatedResources.title') }}
        </div>
        <div v-if="canEdit" class="d-flex align-center ga-2">
          <v-select
            v-model="relationType"
            :items="relationItems"
            item-title="title"
            item-value="value"
            density="compact"
            variant="outlined"
            hide-details
            class="di-related-rel"
          />
          <v-btn
            size="small"
            color="primary"
            variant="tonal"
            class="text-none"
            prepend-icon="mdi-link-plus"
            @click="pickOpen = true"
          >
            {{ t('documentIntelligence.relatedResources.linkAction') }}
          </v-btn>
        </div>
      </div>

      <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-2" />
      <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mb-2 rounded-lg">
        {{ error }}
      </v-alert>

      <div v-if="!loading && !items.length && !error" class="text-body-2 text-medium-emphasis">
        {{ t('documentIntelligence.relatedResources.empty') }}
      </div>

      <v-list v-else-if="items.length" density="compact" class="py-0">
        <v-list-item
          v-for="item in items"
          :key="item.linkId"
          :to="resourcePath(item)"
          class="px-0 rounded-lg"
          rounded="lg"
        >
          <template #prepend>
            <v-icon :icon="resourceIcon(item)" size="20" class="mr-1" />
          </template>
          <v-list-item-title class="text-body-2 font-weight-medium">
            {{ resourceLabel(item) }}
          </v-list-item-title>
          <v-list-item-subtitle class="text-caption">
            {{ relationLabel(item.relationType) }}
            <span v-if="directionLabel(item.direction)"> · {{ directionLabel(item.direction) }}</span>
            <span v-if="kindLabel(item.kind)"> · {{ kindLabel(item.kind) }}</span>
          </v-list-item-subtitle>
          <template v-if="canEdit" #append>
            <v-btn
              size="x-small"
              variant="text"
              color="error"
              icon="mdi-link-off"
              :loading="deletingId === item.linkId"
              :title="t('documentIntelligence.relatedResources.unlink')"
              @click.prevent="unlink(item)"
            />
          </template>
        </v-list-item>
      </v-list>
    </v-card-text>

    <DiPickResourceDialog
      v-model="pickOpen"
      :exclude-resource-ids="excludedIds"
      @pick="onPicked"
    />
  </v-card>
</template>

<style scoped>
.di-related-rel {
  min-width: 160px;
  max-width: 220px;
}
</style>
