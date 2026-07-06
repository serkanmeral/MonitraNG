<script setup lang="ts">
import { computed, onMounted, watch } from 'vue';
import { useRecentPages } from '@/composables/useRecentPages';
import { useWelcomePageTitle } from '@/composables/useWelcomePageTitle';
import { useLocaleStore } from '@/stores/locale';
import { useSideMenuStore } from '@/stores/apps/sideMenu';

const route = useRoute();
const localeStore = useLocaleStore();
const menuStore = useSideMenuStore();
const { recentPages, refreshRecentPages } = useRecentPages();
const { resolveTitleForPath } = useWelcomePageTitle();

onMounted(() => {
  refreshRecentPages();
});

watch(
  () => route.path,
  (path) => {
    if (path === '/' || path === '/welcome') {
      refreshRecentPages();
    }
  },
);

const entries = computed(() => {
  void localeStore.locale;
  void menuStore.menuItemsTree.length;
  return recentPages.value.map((entry) => ({
    ...entry,
    title: resolveTitleForPath(entry.path) || entry.title,
  }));
});
</script>

<template>
  <v-card class="action-widget rounded-xl h-100" variant="outlined">
    <v-card-text class="pa-4">
      <div class="d-flex align-center gap-2 mb-3">
        <v-icon icon="mdi-history" color="primary" size="22" />
        <span class="text-subtitle-2 font-weight-bold">
          {{ $t('welcome.recent.title') }}
        </span>
      </div>

      <v-list v-if="entries.length" density="compact" class="bg-transparent pa-0">
        <v-list-item
          v-for="entry in entries"
          :key="entry.path"
          :to="entry.path"
          rounded="lg"
          class="px-0"
        >
          <template #prepend>
            <v-icon icon="mdi-open-in-new" size="18" class="mr-1" />
          </template>
          <v-list-item-title class="text-body-2">
            {{ entry.title }}
          </v-list-item-title>
        </v-list-item>
      </v-list>

      <p v-else class="text-body-2 text-medium-emphasis mb-0">
        {{ $t('welcome.recent.empty') }}
      </p>
    </v-card-text>
  </v-card>
</template>
