<script setup lang="ts">
import { computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useTaskManagerStore } from '@/stores/apps/taskManager';

definePageMeta({ layout: 'default' });

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const store = useTaskManagerStore();

const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('taskManager.pageTitle', 'Görevler'), disabled: true, href: '#' },
]);

onMounted(async () => {
  try {
    await store.loadLookups();
  } catch (e: any) {
    store.error = e?.message ?? 'Yükleme hatası';
  }
});
</script>

<template>
  <div class="tm-flow">
    <BaseBreadcrumb :title="mt('taskManager.pageTitle', 'Görevler')" :breadcrumbs="breadcrumbs" />
    <v-alert v-if="store.error" type="error" variant="tonal" class="mb-4" closable @click:close="store.error = null">
      {{ store.error }}
    </v-alert>

    <div class="tm-hero d-flex flex-column flex-md-row flex-wrap align-start align-md-center justify-space-between gap-4 mb-8">
      <div>
        <h1 class="tm-hero-title text-h4">{{ mt('taskManager.workspaceTitle', 'Çalışma alanı') }}</h1>
        <p class="tm-hero-sub mb-0">
          {{ mt('taskManager.hubIntro', 'Projeleri liste veya ağaçtan açın; havuz ekranlarından ortak tanımları yönetin.') }}
        </p>
      </div>
    </div>

    <v-row>
      <v-col cols="12" md="6" lg="4">
        <v-card class="tm-panel h-100 rounded-xl" flat :to="'/apps/task-manager/projects'">
          <v-card-text class="pa-6">
            <v-icon icon="mdi-folder-multiple-outline" size="40" class="text-primary mb-3" />
            <div class="text-h6 font-weight-bold mb-1">{{ mt('taskManager.projectsListTitle', 'Projeler') }}</div>
            <p class="text-body-2 text-medium-emphasis mb-0">
              {{ mt('taskManager.projectsListCardHint', 'Ekleme, düzenleme, silme ve durum akışı.') }}
            </p>
          </v-card-text>
          <v-card-actions class="px-6 pb-6 pt-0">
            <v-btn color="primary" variant="tonal" rounded="lg" class="text-none">
              {{ mt('taskManager.goToProjects', 'Projelere git') }}
              <v-icon icon="mdi-chevron-right" end />
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
      <v-col cols="12" md="6" lg="4">
        <v-card class="tm-panel h-100 rounded-xl" flat :to="'/apps/task-manager/workspace'">
          <v-card-text class="pa-6">
            <v-icon icon="mdi-view-list-outline" size="40" class="text-primary mb-3" />
            <div class="text-h6 font-weight-bold mb-1">{{ mt('taskManager.workspaceExplorerTitle', 'Çalışma alanı') }}</div>
            <p class="text-body-2 text-medium-emphasis mb-0">{{ mt('taskManager.workspaceTreePanel', 'Projeler & board’lar') }}</p>
          </v-card-text>
          <v-card-actions class="px-6 pb-6 pt-0">
            <v-btn variant="tonal" rounded="lg" class="text-none">
              {{ mt('taskManager.goToWorkspace', 'Çalışma alanına git') }}
              <v-icon icon="mdi-chevron-right" end />
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>

    <h2 class="text-subtitle-1 font-weight-bold mt-8 mb-3">{{ mt('taskManager.hubPoolsTitle', 'Ortak havuzlar') }}</h2>
    <div class="d-flex flex-wrap gap-2">
      <v-btn variant="tonal" size="large" rounded="lg" class="text-none" to="/apps/task-manager/statuses">
        <v-icon icon="mdi-format-list-checks" start />
        {{ mt('taskManager.statuses.title', 'Durum havuzu') }}
      </v-btn>
      <v-btn variant="tonal" size="large" rounded="lg" class="text-none" to="/apps/task-manager/issue-types">
        <v-icon icon="mdi-shape-outline" start />
        {{ mt('taskManager.issueTypes.title', 'Görev tipleri') }}
      </v-btn>
      <v-btn variant="tonal" size="large" rounded="lg" class="text-none" to="/apps/task-manager/priorities">
        <v-icon icon="mdi-priority-high" start />
        {{ mt('taskManager.priorities.title', 'Öncelikler') }}
      </v-btn>
      <v-btn variant="tonal" size="large" rounded="lg" class="text-none" to="/apps/task-manager/field-pool">
        <v-icon icon="mdi-form-select" start />
        {{ mt('taskManager.fieldPool.title', 'Alan havuzu') }}
      </v-btn>
    </div>
  </div>
</template>
