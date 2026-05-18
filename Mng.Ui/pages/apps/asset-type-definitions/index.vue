<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAssetTypeDefinitionsStore } from '@/stores/apps/assetTypeDefinitions';
import { useAuthStore } from '@/stores/auth';
import type { MonAssetTypeFamily, MonAssetTypeFull, MonCollectibleTemplate } from '@/types/apps/assetTypeDefinitions';
import AssetTypeFamilyFormModal from '@/components/apps/asset-type-definitions/AssetTypeFamilyFormModal.vue';
import AssetTypeFormModal from '@/components/apps/asset-type-definitions/AssetTypeFormModal.vue';
import CollectibleTemplateFormModal from '@/components/apps/asset-type-definitions/CollectibleTemplateFormModal.vue';
import { PlusIcon, RefreshIcon, EditIcon, TrashIcon, AlertTriangleIcon } from 'vue-tabler-icons';

definePageMeta({
  layout: 'default',
});

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const authStore = useAuthStore();
const store = useAssetTypeDefinitionsStore();
const canEdit = computed(() => authStore.isManager);

const page = computed(() => ({ title: mt('assetTypeDefinitions.pageTitle', 'Asset Tür Tanımları') }));
const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Ana Sayfa'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('assetTypeDefinitions.breadcrumbs.title', 'Asset Tür Tanımları'), disabled: true, href: '#' },
]);

const activeTab = ref<'families' | 'types' | 'templates'>('families');
const searchFamily = ref('');
const searchType = ref('');
const searchTemplate = ref('');
const familyFilter = ref<string | null>(null);

const familyFormOpen = ref(false);
const familyFormModel = ref<MonAssetTypeFamily | null>(null);
const typeFormOpen = ref(false);
const typeFormModel = ref<MonAssetTypeFull | null>(null);
const templateFormOpen = ref(false);
const templateFormModel = ref<MonCollectibleTemplate | null>(null);
const deleteDialogOpen = ref(false);
const deleteTarget = ref<{ kind: 'family' | 'type' | 'template'; id: string; name: string } | null>(null);

const familyOptionsWithAll = computed(() => {
  const opts = [{ title: 'Tümü', value: null }];
  store.families.forEach((f) => opts.push({ title: f.name, value: f.__dataId }));
  return opts;
});

const familyTableHeaders = [
  { title: 'Ad', key: 'name', sortable: true },
  { title: 'Kod', key: 'code', sortable: false },
  { title: 'Açıklama', key: 'description', sortable: false },
  { title: 'İşlemler', key: 'actions', sortable: false, align: 'end' as const },
];

const typeTableHeaders = [
  { title: 'Ad', key: 'name', sortable: true },
  { title: 'Aile', key: 'familyName', sortable: false },
  { title: 'Toplama metodu', key: 'collection_method', sortable: false },
  { title: 'Açıklama', key: 'description', sortable: false },
  { title: 'İşlemler', key: 'actions', sortable: false, align: 'end' as const },
];

const templateTableHeaders = [
  { title: 'Ad', key: 'name', sortable: true },
  { title: 'Toplama metodu', key: 'collection_method', sortable: false },
  { title: 'Açıklama', key: 'description', sortable: false },
  { title: 'İşlemler', key: 'actions', sortable: false, align: 'end' as const },
];

const filteredTemplates = computed(() => {
  const q = searchTemplate.value.toLowerCase().trim();
  if (!q) return store.templates;
  return store.templates.filter(
    (t) =>
      (t.name ?? '').toLowerCase().includes(q) ||
      (t.collection_method ?? '').toLowerCase().includes(q) ||
      (t.description ?? '').toLowerCase().includes(q)
  );
});

const familyNameById = computed(() => {
  const m = new Map<string, string>();
  store.families.forEach((f) => m.set(f.__dataId, f.name));
  return m;
});

const filteredFamilies = computed(() => {
  const q = searchFamily.value.toLowerCase().trim();
  if (!q) return store.families;
  return store.families.filter(
    (f) =>
      (f.name ?? '').toLowerCase().includes(q) ||
      (f.code ?? '').toLowerCase().includes(q) ||
      (f.description ?? '').toLowerCase().includes(q)
  );
});

const filteredTypes = computed(() => {
  let list = store.types;
  if (familyFilter.value) {
    list = list.filter((t) => t.family === familyFilter.value);
  }
  const q = searchType.value.toLowerCase().trim();
  if (!q) return list;
  return list.filter(
    (t) =>
      (t.name ?? '').toLowerCase().includes(q) ||
      (familyNameById.value.get(t.family) ?? '').toLowerCase().includes(q) ||
      (t.collection_method ?? '').toLowerCase().includes(q) ||
      (t.description ?? '').toLowerCase().includes(q)
  );
});

const typesWithFamilyName = computed(() =>
  filteredTypes.value.map((t) => ({
    ...t,
    familyName: familyNameById.value.get(t.family) ?? t.family,
  }))
);

/** Altında en az bir tip tanımlı aile __dataId'leri (bu aileler silinemez) */
const familyIdsWithTypes = computed(() => {
  const set = new Set<string>();
  store.types.forEach((t) => set.add(t.family));
  return set;
});

function familyCanBeDeleted(familyId: string) {
  return !familyIdsWithTypes.value.has(familyId);
}

function truncate(s: string | null | undefined, max = 50) {
  if (!s) return '—';
  return s.length <= max ? s : s.slice(0, max) + '…';
}

onMounted(() => {
  store.loadAll();
});

function openNewFamily() {
  familyFormModel.value = null;
  familyFormOpen.value = true;
}

function openEditFamily(item: MonAssetTypeFamily) {
  familyFormModel.value = { ...item };
  familyFormOpen.value = true;
}

async function handleSaveFamily(data: Partial<MonAssetTypeFamily>) {
  const id = (data as any).__dataId;
  if (id) await store.updateFamily(id, data);
  else await store.createFamily(data);
  familyFormOpen.value = false;
}

function openDeleteFamily(item: MonAssetTypeFamily) {
  deleteTarget.value = { kind: 'family', id: item.__dataId, name: item.name };
  deleteDialogOpen.value = true;
}

function openNewType() {
  typeFormModel.value = null;
  typeFormOpen.value = true;
}

function openEditType(item: MonAssetTypeFull) {
  typeFormModel.value = { ...item };
  typeFormOpen.value = true;
}

async function handleSaveType(data: Partial<MonAssetTypeFull>) {
  const id = (data as any).__dataId;
  if (id) await store.updateType(id, data);
  else await store.createType(data);
  typeFormOpen.value = false;
}

function openDeleteType(item: MonAssetTypeFull) {
  deleteTarget.value = { kind: 'type', id: item.__dataId, name: item.name };
  deleteDialogOpen.value = true;
}

function openNewTemplate() {
  templateFormModel.value = null;
  templateFormOpen.value = true;
}

function openEditTemplate(item: MonCollectibleTemplate) {
  templateFormModel.value = { ...item };
  templateFormOpen.value = true;
}

async function handleSaveTemplate(data: Partial<MonCollectibleTemplate>) {
  const id = (data as any).__dataId;
  if (id) await store.updateTemplate(id, data);
  else await store.createTemplate(data);
  templateFormOpen.value = false;
}

function openDeleteTemplate(item: MonCollectibleTemplate) {
  deleteTarget.value = { kind: 'template', id: item.__dataId, name: item.name };
  deleteDialogOpen.value = true;
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  const { kind, id } = deleteTarget.value;
  if (kind === 'family') {
    if (!familyCanBeDeleted(id)) {
      store.error = 'Bu aile altında tanımlı asset tipleri var. Önce ilgili tipleri silin veya başka aileye taşıyın.';
      deleteDialogOpen.value = false;
      deleteTarget.value = null;
      return;
    }
    await store.deleteFamily(id);
  } else if (kind === 'type') {
    await store.deleteType(id);
  } else {
    await store.deleteTemplate(id);
  }
  deleteDialogOpen.value = false;
  deleteTarget.value = null;
}

function closeDeleteDialog() {
  deleteDialogOpen.value = false;
  deleteTarget.value = null;
}

async function refresh() {
  await store.loadAll();
}
</script>

<template>
  <div class="asset-type-definitions-page">
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-container fluid>
      <v-alert v-if="store.error" type="error" variant="tonal" dismissible class="mb-4" @click:close="store.clearError">
        {{ store.error }}
      </v-alert>

      <v-tabs v-model="activeTab" class="mb-4">
        <v-tab value="families">Aileler</v-tab>
        <v-tab value="types">Tipler</v-tab>
        <v-tab value="templates">Şablonlar</v-tab>
      </v-tabs>

      <v-window v-model="activeTab">
        <!-- Aileler sekmesi -->
        <v-window-item value="families">
          <v-card elevation="2">
            <v-card-text class="pa-4">
              <div class="d-flex align-center flex-wrap gap-2 mb-4">
                <v-text-field
                  v-model="searchFamily"
                  placeholder="Ara (ad, kod, açıklama)..."
                  variant="outlined"
                  density="compact"
                  hide-details
                  clearable
                  style="max-width: 280px;"
                />
                <v-spacer />
                <v-btn icon variant="outlined" size="small" :loading="store.loading" @click="refresh">
                  <RefreshIcon size="20" />
                </v-btn>
                <v-btn v-if="canEdit" color="primary" variant="flat" :disabled="store.loading" @click="openNewFamily">
                  <PlusIcon size="20" class="mr-1" />
                  Yeni Aile
                </v-btn>
              </div>
              <v-data-table
                :headers="familyTableHeaders"
                :items="filteredFamilies"
                :loading="store.loading"
                item-value="__dataId"
                class="border rounded"
              >
                <template #item.name="{ item }">
                  <span class="font-weight-medium">{{ item.name }}</span>
                </template>
                <template #item.code="{ item }">
                  <span class="text-medium-emphasis">{{ item.code || '—' }}</span>
                </template>
                <template #item.description="{ item }">
                  <span class="text-body-2">{{ truncate(item.description, 50) }}</span>
                </template>
                <template #item.actions="{ item }">
                  <v-btn v-if="canEdit" icon size="small" variant="text" @click="openEditFamily(item)">
                    <EditIcon size="18" />
                  </v-btn>
                  <v-tooltip v-if="canEdit" location="top">
                    <template #activator="{ props: tooltipProps }">
                      <span v-bind="tooltipProps">
                        <v-btn
                          icon
                          size="small"
                          variant="text"
                          color="error"
                          :disabled="!familyCanBeDeleted(item.__dataId)"
                          @click="openDeleteFamily(item)"
                        >
                          <TrashIcon size="18" />
                        </v-btn>
                      </span>
                    </template>
                    <span v-if="familyCanBeDeleted(item.__dataId)">Aileyi sil</span>
                    <span v-else>Bu aile altında tanımlı tipler var; önce tipleri silin veya başka aileye taşıyın.</span>
                  </v-tooltip>
                </template>
                <template #no-data>
                  <div class="text-center py-6 text-medium-emphasis">Henüz aile eklenmemiş.</div>
                </template>
              </v-data-table>
            </v-card-text>
          </v-card>
        </v-window-item>

        <!-- Tipler sekmesi -->
        <v-window-item value="types">
          <v-card elevation="2">
            <v-card-text class="pa-4">
              <div class="d-flex align-center flex-wrap gap-2 mb-4">
                <v-text-field
                  v-model="searchType"
                  placeholder="Ara..."
                  variant="outlined"
                  density="compact"
                  hide-details
                  clearable
                  style="max-width: 220px;"
                />
                <v-select
                  v-model="familyFilter"
                  :items="familyOptionsWithAll"
                  item-title="title"
                  item-value="value"
                  label="Aile"
                  variant="outlined"
                  density="compact"
                  hide-details
                  clearable
                  style="max-width: 200px;"
                />
                <v-spacer />
                <v-btn icon variant="outlined" size="small" :loading="store.loading" @click="refresh">
                  <RefreshIcon size="20" />
                </v-btn>
                <v-btn v-if="canEdit" color="primary" variant="flat" :disabled="store.loading || store.families.length === 0" @click="openNewType">
                  <PlusIcon size="20" class="mr-1" />
                  Yeni Tip
                </v-btn>
              </div>
              <v-data-table
                :headers="typeTableHeaders"
                :items="typesWithFamilyName"
                :loading="store.loading"
                item-value="__dataId"
                class="border rounded"
              >
                <template #item.name="{ item }">
                  <span class="font-weight-medium">{{ item.name }}</span>
                </template>
                <template #item.familyName="{ item }">
                  <span class="text-medium-emphasis">{{ item.familyName }}</span>
                </template>
                <template #item.collection_method="{ item }">
                  <v-chip size="small" variant="tonal">{{ item.collection_method }}</v-chip>
                </template>
                <template #item.description="{ item }">
                  <span class="text-body-2">{{ truncate(item.description, 40) }}</span>
                </template>
                <template #item.actions="{ item }">
                  <v-btn v-if="canEdit" icon size="small" variant="text" @click="openEditType(item)">
                    <EditIcon size="18" />
                  </v-btn>
                  <v-btn v-if="canEdit" icon size="small" variant="text" color="error" @click="openDeleteType(item)">
                    <TrashIcon size="18" />
                  </v-btn>
                </template>
                <template #no-data>
                  <div class="text-center py-6 text-medium-emphasis">
                    {{ store.families.length === 0 ? 'Önce en az bir aile ekleyin.' : 'Henüz tip eklenmemiş.' }}
                  </div>
                </template>
              </v-data-table>
            </v-card-text>
          </v-card>
        </v-window-item>

        <!-- Şablonlar sekmesi -->
        <v-window-item value="templates">
          <v-card elevation="2">
            <v-card-text class="pa-4">
              <div class="d-flex align-center flex-wrap gap-2 mb-4">
                <v-text-field
                  v-model="searchTemplate"
                  placeholder="Ara (ad, metot, açıklama)..."
                  variant="outlined"
                  density="compact"
                  hide-details
                  clearable
                  style="max-width: 280px;"
                />
                <v-spacer />
                <v-btn icon variant="outlined" size="small" :loading="store.loading" @click="refresh">
                  <RefreshIcon size="20" />
                </v-btn>
                <v-btn v-if="canEdit" color="primary" variant="flat" :disabled="store.loading" @click="openNewTemplate">
                  <PlusIcon size="20" class="mr-1" />
                  Yeni Şablon
                </v-btn>
              </div>
              <v-data-table
                :headers="templateTableHeaders"
                :items="filteredTemplates"
                :loading="store.loading"
                item-value="__dataId"
                class="border rounded"
              >
                <template #item.name="{ item }">
                  <span class="font-weight-medium">{{ item.name }}</span>
                </template>
                <template #item.collection_method="{ item }">
                  <v-chip size="small" variant="tonal">{{ item.collection_method }}</v-chip>
                </template>
                <template #item.description="{ item }">
                  <span class="text-body-2">{{ truncate(item.description, 50) }}</span>
                </template>
                <template #item.actions="{ item }">
                  <v-btn v-if="canEdit" icon size="small" variant="text" @click="openEditTemplate(item)">
                    <EditIcon size="18" />
                  </v-btn>
                  <v-btn v-if="canEdit" icon size="small" variant="text" color="error" @click="openDeleteTemplate(item)">
                    <TrashIcon size="18" />
                  </v-btn>
                </template>
                <template #no-data>
                  <div class="text-center py-6 text-medium-emphasis">Henüz collectible şablonu eklenmemiş.</div>
                </template>
              </v-data-table>
            </v-card-text>
          </v-card>
        </v-window-item>
      </v-window>
    </v-container>

    <AssetTypeFamilyFormModal
      v-model="familyFormOpen"
      :family="familyFormModel"
      :loading="store.loading"
      :can-edit="canEdit"
      @save="handleSaveFamily"
    />
    <AssetTypeFormModal
      v-model="typeFormOpen"
      :type="typeFormModel"
      :family-options="store.familyOptions"
      :templates="store.templates"
      :loading="store.loading"
      :can-edit="canEdit"
      @save="handleSaveType"
    />
    <CollectibleTemplateFormModal
      v-model="templateFormOpen"
      :template="templateFormModel"
      :loading="store.loading"
      :can-edit="canEdit"
      @save="handleSaveTemplate"
    />

    <v-dialog v-model="deleteDialogOpen" max-width="440" persistent>
      <v-card>
        <v-card-title class="d-flex align-center text-body-1">
          <AlertTriangleIcon size="24" class="mr-2 text-warning" />
          {{ deleteTarget?.kind === 'family' ? 'Aile' : deleteTarget?.kind === 'template' ? 'Şablon' : 'Tip' }} silinsin mi?
        </v-card-title>
        <v-card-text>
          <span class="text-body-2">"<strong>{{ deleteTarget?.name }}</strong>" {{ deleteTarget?.kind === 'family' ? 'ailesini' : deleteTarget?.kind === 'template' ? 'şablonunu' : 'tipini' }} silmek istediğinize emin misiniz?</span>
        </v-card-text>
        <v-card-actions class="pt-0">
          <v-spacer />
          <v-btn variant="text" @click="closeDeleteDialog">İptal</v-btn>
          <v-btn color="error" variant="flat" :loading="store.loading" @click="confirmDelete">Sil</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.asset-type-definitions-page {
  min-height: calc(100vh - 100px);
}
</style>
