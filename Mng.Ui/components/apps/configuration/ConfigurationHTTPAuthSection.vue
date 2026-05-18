<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useHttpAuthConfigStore } from '@/stores/apps/httpAuthConfig';
import { useAuthStore } from '@/stores/auth';
import type { MonHttpAuthConfig } from '@/types/apps/httpAuthConfig';
import HttpAuthConfigFormModal from '@/components/apps/http-auth-config/HttpAuthConfigFormModal.vue';
import { PlusIcon, RefreshIcon, EditIcon, TrashIcon } from 'vue-tabler-icons';

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const store = useHttpAuthConfigStore();
const authStore = useAuthStore();
const canEdit = computed(() => authStore.isManager);

const formOpen = ref(false);
const formModel = ref<MonHttpAuthConfig | null>(null);
const deleteDialogOpen = ref(false);
const deleteTarget = ref<MonHttpAuthConfig | null>(null);
const searchQuery = ref('');

const tableHeaders = [
  { title: 'Ad', key: 'name', sortable: true },
  { title: 'Token URL', key: 'tokenUrl', sortable: false },
  { title: 'Metot', key: 'tokenMethod', sortable: false },
  { title: 'Response Path', key: 'tokenResponsePath', sortable: false },
  { title: 'İşlemler', key: 'actions', sortable: false, align: 'end' as const },
];

const filteredItems = computed(() => {
  const q = searchQuery.value.toLowerCase().trim();
  if (!q) return store.items;
  return store.items.filter(
    (c) =>
      (c.name ?? '').toLowerCase().includes(q) ||
      (c.tokenUrl ?? '').toLowerCase().includes(q) ||
      (c.tokenResponsePath ?? '').toLowerCase().includes(q)
  );
});

function truncate(s: string | null | undefined, max = 40) {
  if (!s) return '—';
  return s.length <= max ? s : s.slice(0, max) + '…';
}

onMounted(() => {
  store.loadAll();
});

function openNew() {
  formModel.value = null;
  formOpen.value = true;
}

function openEdit(item: MonHttpAuthConfig) {
  formModel.value = { ...item };
  formOpen.value = true;
}

function openDelete(item: MonHttpAuthConfig) {
  deleteTarget.value = item;
  deleteDialogOpen.value = true;
}

function confirmDelete() {
  if (!deleteTarget.value) return;
  store.remove(deleteTarget.value.__dataId).finally(() => {
    deleteTarget.value = null;
    deleteDialogOpen.value = false;
  });
}

async function handleSave(data: Partial<MonHttpAuthConfig>) {
  const id = (data as any).__dataId;
  if (id) {
    await store.update(id, data);
  } else {
    await store.create(data);
  }
  formOpen.value = false;
  formModel.value = null;
}
</script>

<template>
  <div>
  <div class="configuration-section">
    <div class="section-header">
      <h2 class="section-title">{{ mt('httpAuthConfig.sectionTitle', 'HTTP Auth Tanımları') }}</h2>
      <p class="section-desc">
        {{ mt('httpAuthConfig.sectionDesc', 'HTTP Collector için Bearer token endpoint tanımları. Bu tanımlar HTTP isteklerinde auth type "bearer_token" seçildiğinde kullanılır.') }}
      </p>
    </div>
    <div class="section-content">
      <v-card variant="outlined">
        <v-card-text class="pa-4">
          <div class="d-flex flex-wrap align-center gap-3 mb-4">
            <v-text-field
              v-model="searchQuery"
              density="compact"
              variant="outlined"
              hide-details
              placeholder="Ara..."
              style="max-width: 260px"
              clearable
            />
            <v-spacer />
            <v-btn size="small" variant="tonal" :icon="RefreshIcon" @click="store.loadAll()" :loading="store.loading" />
            <v-btn v-if="canEdit" color="primary" size="small" :prepend-icon="PlusIcon" @click="openNew">Yeni tanım</v-btn>
          </div>

          <v-alert v-if="store.error" type="error" variant="tonal" density="compact" class="mb-4" dismissible @click:close="store.clearError">
            {{ store.error }}
          </v-alert>

          <v-table v-if="filteredItems.length > 0" density="comfortable">
            <thead>
              <tr>
                <th v-for="h in tableHeaders" :key="h.key" :class="{ 'text-end': h.align === 'end' }">{{ h.title }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in filteredItems" :key="item.__dataId">
                <td>{{ item.name }}</td>
                <td>
                  <span class="text-body-2 font-monospace">{{ truncate(item.tokenUrl, 50) }}</span>
                </td>
                <td>
                  <v-chip size="x-small" variant="tonal">{{ item.tokenMethod }}</v-chip>
                </td>
                <td>
                  <span class="text-body-2 font-monospace">{{ truncate(item.tokenResponsePath, 30) }}</span>
                </td>
                <td class="text-end">
                  <v-btn v-if="canEdit" icon size="x-small" variant="text" @click="openEdit(item)">
                    <EditIcon size="18" />
                  </v-btn>
                  <v-btn v-if="canEdit" icon size="x-small" variant="text" color="error" @click="openDelete(item)">
                    <TrashIcon size="18" />
                  </v-btn>
                </td>
              </tr>
            </tbody>
          </v-table>

          <v-card v-else variant="tonal" class="pa-8 text-center">
            <p class="text-body-2 text-medium-emphasis mb-3">
              {{ searchQuery ? 'Arama kriterlerine uygun tanım bulunamadı.' : 'Henüz HTTP Auth tanımı eklenmemiş.' }}
            </p>
            <v-btn v-if="canEdit && !searchQuery" color="primary" variant="flat" :prepend-icon="PlusIcon" @click="openNew">
              İlk tanımı ekle
            </v-btn>
          </v-card>
        </v-card-text>
      </v-card>
    </div>
  </div>

  <HttpAuthConfigFormModal
    v-model="formOpen"
    :config="formModel"
    :loading="store.loading"
    :can-edit="canEdit"
    @save="handleSave"
  />

  <v-dialog v-model="deleteDialogOpen" max-width="400" persistent>
    <v-card>
      <v-card-title>Tanımı sil</v-card-title>
      <v-card-text>
        <strong>{{ deleteTarget?.name }}</strong> tanımını silmek istediğinize emin misiniz?
        Bu tanımı kullanan HTTP Collector yapılandırmaları çalışmayı durdurabilir.
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="deleteDialogOpen = false">İptal</v-btn>
        <v-btn color="error" variant="flat" :loading="store.loading" @click="confirmDelete">Sil</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
  </div>
</template>
