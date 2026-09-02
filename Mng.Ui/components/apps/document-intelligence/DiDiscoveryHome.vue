<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { diGetRecent, diGetDrafts } from '@/services/documentIntelligenceService';
import { diPageResourceIcon, diPageResourceLabel } from '@/utils/diPageResource';
import type { DiResource, DiTreeNode } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  tree: DiTreeNode[];
}>();

const emit = defineEmits<{
  'select-folder': [folderId: string];
  'open-resource': [resource: DiResource];
  search: [query: string];
}>();

const { t } = useAppI18n();

const recentLoading = ref(false);
const draftsLoading = ref(false);
const recentItems = ref<DiResource[]>([]);
const draftItems = ref<DiResource[]>([]);
const discoverySearch = ref('');

const SHORTCUT_NAMES = new Set(['Sayfalar', 'Dökümanlar']);

const areaShortcuts = computed(() =>
  props.tree.filter((n) => SHORTCUT_NAMES.has(n.name))
);

const showRecent = computed(() => recentLoading.value || recentItems.value.length > 0);
const showDrafts = computed(() => draftsLoading.value || draftItems.value.length > 0);
const showShortcuts = computed(() => areaShortcuts.value.length > 0);

function shortcutIcon(name: string): string {
  if (name === 'Sayfalar') return 'mdi-book-open-page-variant-outline';
  if (name === 'Dökümanlar') return 'mdi-file-document-multiple-outline';
  return 'mdi-folder-outline';
}

function shortcutDescription(name: string): string {
  if (name === 'Sayfalar') return t('documentIntelligence.discovery.shortcutPagesHint');
  if (name === 'Dökümanlar') return t('documentIntelligence.discovery.shortcutDocumentsHint');
  return '';
}

function formatDateTime(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function submitDiscoverySearch() {
  const q = discoverySearch.value.trim();
  if (!q) return;
  emit('search', q);
}

async function loadRecent() {
  recentLoading.value = true;
  try {
    const res = await diGetRecent(12);
    recentItems.value = res.items.filter((r) => r.type === 'markdown');
  } catch {
    recentItems.value = [];
  } finally {
    recentLoading.value = false;
  }
}

async function loadDrafts() {
  draftsLoading.value = true;
  try {
    const res = await diGetDrafts(20);
    draftItems.value = res.items.filter((r) => r.type === 'markdown');
  } catch {
    draftItems.value = [];
  } finally {
    draftsLoading.value = false;
  }
}

onMounted(() => {
  void loadRecent();
  void loadDrafts();
});
</script>

<template>
  <div class="di-discovery mb-6">
    <div class="mb-4">
      <h2 class="text-h6 font-weight-bold mb-1">{{ t('documentIntelligence.discovery.title') }}</h2>
      <p class="text-body-2 text-medium-emphasis mb-0">{{ t('documentIntelligence.discovery.subtitle') }}</p>
    </div>

    <!-- Keşif araması -->
    <section class="mb-5">
      <div class="d-flex ga-2 align-start">
        <v-text-field
          v-model="discoverySearch"
          density="comfortable"
          variant="outlined"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          :placeholder="t('documentIntelligence.discovery.searchPlaceholder')"
          class="flex-grow-1"
          @keydown.enter="submitDiscoverySearch"
        />
        <v-btn
          color="primary"
          variant="flat"
          class="text-none mt-1"
          :disabled="!discoverySearch.trim()"
          @click="submitDiscoverySearch"
        >
          {{ t('documentIntelligence.discovery.searchAction') }}
        </v-btn>
      </div>
      <div class="text-caption text-medium-emphasis mt-1">{{ t('documentIntelligence.discovery.searchHint') }}</div>
    </section>

    <!-- Alan kısayolları -->
    <section v-if="showShortcuts" class="mb-5">
      <div class="text-caption text-medium-emphasis mb-2">{{ t('documentIntelligence.discovery.shortcutsTitle') }}</div>
      <v-row dense>
        <v-col
          v-for="node in areaShortcuts"
          :key="node.id"
          cols="12"
          sm="6"
          md="4"
        >
          <v-card
            variant="outlined"
            rounded="lg"
            class="di-discovery-shortcut pa-4 h-100"
            @click="emit('select-folder', node.id)"
          >
            <div class="d-flex align-start ga-3">
              <v-avatar color="primary" variant="tonal" size="40" rounded="lg">
                <v-icon :icon="shortcutIcon(node.name)" size="22" />
              </v-avatar>
              <div class="min-w-0">
                <div class="text-subtitle-2 font-weight-bold">{{ node.name }}</div>
                <div class="text-caption text-medium-emphasis mt-1">{{ shortcutDescription(node.name) }}</div>
              </div>
            </div>
          </v-card>
        </v-col>
      </v-row>
    </section>

    <v-row dense>
      <!-- Son güncellenenler -->
      <v-col v-if="showRecent" cols="12" md="showDrafts ? 6 : 12">
        <section>
          <div class="d-flex align-center mb-2">
            <v-icon size="18" class="mr-1 text-medium-emphasis">mdi-clock-outline</v-icon>
            <span class="text-caption text-medium-emphasis">{{ t('documentIntelligence.discovery.recentTitle') }}</span>
          </div>
          <v-card variant="outlined" rounded="lg" class="di-discovery-panel">
            <v-progress-linear v-if="recentLoading" indeterminate color="primary" />
            <v-list v-else-if="recentItems.length" density="compact" class="py-1">
              <v-list-item
                v-for="item in recentItems"
                :key="item.id"
                rounded="lg"
                class="di-discovery-item"
                @click="emit('open-resource', item)"
              >
                <template #prepend>
                  <v-icon :icon="diPageResourceIcon(item)" color="primary" size="20" />
                </template>
                <v-list-item-title class="text-body-2">{{ diPageResourceLabel(item) }}</v-list-item-title>
                <v-list-item-subtitle v-if="item.updatedAt" class="text-caption">
                  {{ formatDateTime(item.updatedAt) }}
                </v-list-item-subtitle>
              </v-list-item>
            </v-list>
            <div v-else class="text-body-2 text-medium-emphasis pa-4 text-center">
              {{ t('documentIntelligence.discovery.emptyRecent') }}
            </div>
          </v-card>
        </section>
      </v-col>

      <!-- Taslaklarım -->
      <v-col v-if="showDrafts" cols="12" md="showRecent ? 6 : 12">
        <section>
          <div class="d-flex align-center mb-2">
            <v-icon size="18" class="mr-1 text-medium-emphasis">mdi-file-document-edit-outline</v-icon>
            <span class="text-caption text-medium-emphasis">{{ t('documentIntelligence.discovery.draftsTitle') }}</span>
          </div>
          <v-card variant="outlined" rounded="lg" class="di-discovery-panel">
            <v-progress-linear v-if="draftsLoading" indeterminate color="primary" />
            <v-list v-else-if="draftItems.length" density="compact" class="py-1">
              <v-list-item
                v-for="item in draftItems"
                :key="item.id"
                rounded="lg"
                class="di-discovery-item"
                @click="emit('open-resource', item)"
              >
                <template #prepend>
                  <v-icon :icon="diPageResourceIcon(item)" color="warning" size="20" />
                </template>
                <v-list-item-title class="text-body-2">
                  {{ diPageResourceLabel(item) }}
                  <v-chip
                    size="x-small"
                    variant="flat"
                    :color="item.status === 'inReview' ? 'info' : 'warning'"
                    class="ml-1"
                  >
                    {{ t(`documentIntelligence.lifecycle.statuses.${item.status === 'inReview' ? 'inReview' : 'draft'}`) }}
                  </v-chip>
                </v-list-item-title>
                <v-list-item-subtitle v-if="item.updatedAt" class="text-caption">
                  {{ formatDateTime(item.updatedAt) }}
                </v-list-item-subtitle>
              </v-list-item>
            </v-list>
            <div v-else class="text-body-2 text-medium-emphasis pa-4 text-center">
              {{ t('documentIntelligence.discovery.emptyDrafts') }}
            </div>
          </v-card>
        </section>
      </v-col>
    </v-row>
  </div>
</template>

<style scoped>
.di-discovery-shortcut {
  cursor: pointer;
  transition: border-color 0.15s ease, background-color 0.15s ease;
}
.di-discovery-shortcut:hover {
  border-color: rgb(var(--v-theme-primary));
  background-color: rgba(var(--v-theme-primary), 0.04);
}
.di-discovery-panel {
  min-height: 120px;
}
.di-discovery-item {
  cursor: pointer;
}
</style>
