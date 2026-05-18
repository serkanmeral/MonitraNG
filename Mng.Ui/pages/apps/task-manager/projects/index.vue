<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import { useAuthStore } from '@/stores/auth';

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
const auth = useAuthStore();

const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('taskManager.pageTitle', 'Görevler'), disabled: false, href: '/apps/task-manager' },
  { text: mt('taskManager.projectsListTitle', 'Projeler'), disabled: true, href: '#' },
]);

const search = ref('');
const filteredProjects = computed(() => {
  const q = search.value.trim().toLowerCase();
  let list = store.visibleProjects;
  if (q) {
    list = list.filter(
      (p) =>
        p.name.toLowerCase().includes(q) ||
        p.key.toLowerCase().includes(q) ||
        (p.description && p.description.toLowerCase().includes(q))
    );
  }
  return list;
});

const headers = computed(() => [
  { title: mt('taskManager.projectsColKey', 'Kod'), key: 'key', sortable: true, width: '120px' },
  { title: mt('taskManager.projectsColName', 'Ad'), key: 'name', sortable: true },
  { title: mt('taskManager.projectsColDescription', 'Açıklama'), key: 'description', sortable: false },
  {
    title: mt('taskManager.projectsColActions', 'İşlemler'),
    key: 'actions',
    sortable: false,
    align: 'end' as const,
    width: '140px',
  },
]);

const deleteDialog = ref(false);
const deleteTargetId = ref<string | null>(null);
const deleting = ref(false);

async function refresh() {
  await store.loadProjects();
  const uid = auth.userInfo?.sub;
  /** Yalnızca normal kullanıcılar: proje permissions.view ile süzülür. Admin/manager tam liste görür. */
  if (uid && !auth.isAdmin && !auth.isManager) store.filterProjectsForUser(uid);
}

onMounted(async () => {
  try {
    await store.loadLookups();
    await refresh();
  } catch (e: any) {
    store.error = e?.message ?? 'Yükleme hatası';
  }
});

function openDelete(id: string) {
  deleteTargetId.value = id;
  deleteDialog.value = true;
}

async function confirmDelete() {
  if (!deleteTargetId.value) return;
  deleting.value = true;
  try {
    await store.deleteProject(deleteTargetId.value);
    deleteDialog.value = false;
    deleteTargetId.value = null;
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="tm-flow">
    <BaseBreadcrumb :title="mt('taskManager.projectsListTitle', 'Projeler')" :breadcrumbs="breadcrumbs" />
    <v-alert v-if="store.error" type="error" variant="tonal" class="mb-4" closable @click:close="store.error = null">
      {{ store.error }}
    </v-alert>

    <div class="d-flex flex-column flex-md-row flex-wrap align-start align-md-center justify-space-between gap-4 mb-6">
      <div>
        <h1 class="text-h5 font-weight-bold mb-1">{{ mt('taskManager.projectsListTitle', 'Projeler') }}</h1>
        <p class="text-body-2 text-medium-emphasis mb-0">
          {{ mt('taskManager.projectsListSubtitle', 'Tam tanım için düzenleyiciyi kullanın.') }}
        </p>
      </div>
      <v-btn color="primary" rounded="lg" class="text-none" prepend-icon="mdi-plus" to="/apps/task-manager/projects/new">
        {{ mt('taskManager.newProject', 'Yeni proje') }}
      </v-btn>
    </div>

    <v-card class="tm-panel rounded-xl" flat>
      <v-card-text class="pb-2">
        <v-text-field
          v-model="search"
          variant="outlined"
          density="comfortable"
          clearable
          hide-details
          :placeholder="mt('taskManager.searchProjects', 'Proje ara…')"
          prepend-inner-icon="mdi-magnify"
          class="mb-4"
          style="max-width: 400px"
        />
        <v-data-table
          :headers="headers"
          :items="filteredProjects"
          :loading="store.loading"
          item-value="__dataId"
          class="tm-projects-table"
          density="comfortable"
          hover
        >
          <template #[`item.key`]="{ item }">
            <span class="font-weight-medium text-primary">{{ item.key }}</span>
          </template>
          <template #[`item.description`]="{ item }">
            <span class="text-medium-emphasis text-truncate d-inline-block" style="max-width: min(420px, 40vw)">
              {{ item.description || '—' }}
            </span>
          </template>
          <template #[`item.actions`]="{ item }">
            <div class="d-flex justify-end align-center ga-1 flex-wrap">
              <v-tooltip location="top">
                <template #activator="{ props: tip }">
                  <v-btn
                    v-bind="tip"
                    :to="`/apps/task-manager/projects/${item.__dataId}/edit`"
                    icon
                    variant="text"
                    size="small"
                    :aria-label="mt('taskManager.editProject', 'Düzenle')"
                  >
                    <v-icon icon="mdi-pencil-outline" size="22" />
                  </v-btn>
                </template>
                <span>{{ mt('taskManager.editProject', 'Düzenle') }}</span>
              </v-tooltip>
              <v-tooltip location="top">
                <template #activator="{ props: tip }">
                  <v-btn
                    v-bind="tip"
                    icon
                    variant="text"
                    size="small"
                    color="error"
                    @click="openDelete(item.__dataId)"
                    :aria-label="mt('taskManager.delete', 'Sil')"
                  >
                    <v-icon icon="mdi-delete-outline" size="22" />
                  </v-btn>
                </template>
                <span>{{ mt('taskManager.delete', 'Sil') }}</span>
              </v-tooltip>
            </div>
          </template>
          <template #[`no-data`]>
            <div class="py-8 text-center text-medium-emphasis">
              {{ mt('taskManager.noProjects', 'Henüz proje yok.') }}
            </div>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <v-dialog v-model="deleteDialog" max-width="420">
      <v-card rounded="xl">
        <v-card-title>{{ mt('taskManager.deleteProjectTitle', 'Proje silinsin mi?') }}</v-card-title>
        <v-card-text>{{ mt('taskManager.deleteProjectBody', 'Projeye bağlı board ve görevler de silinir.') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">{{ mt('taskManager.cancel', 'İptal') }}</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="confirmDelete">{{ mt('taskManager.delete', 'Sil') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.tm-projects-table :deep(th) {
  font-weight: 600 !important;
}
</style>
