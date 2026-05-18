<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useOrganizationStore } from '@/stores/apps/organization';
import { useHttpAuthConfigStore } from '@/stores/apps/httpAuthConfig';
import type { OrganizationSelectedNode, MonAssetType } from '@/types/apps/organization';
import OrganizationTreeView from '@/components/apps/organization/OrganizationTreeView.vue';
import OrganizationToolbar from '@/components/apps/organization/OrganizationToolbar.vue';
import OrganizationItemForm from '@/components/apps/organization/OrganizationItemForm.vue';
import OrganizationAssetForm from '@/components/apps/organization/OrganizationAssetForm.vue';
import { ChevronDownIcon, ChevronUpIcon } from 'vue-tabler-icons';
import { fetchFromDataGateway } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';

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
const orgStore = useOrganizationStore();
const httpAuthStore = useHttpAuthConfigStore();
/** Ekleme/silme/güncelleme butonları sadece is_manager veya is_admin kullanıcıya gösterilir */
const canEdit = computed(() => authStore.isManager);
const treeViewRef = ref<InstanceType<typeof OrganizationTreeView> | null>(null);

const page = computed(() => ({ title: mt('organization.pageTitle', 'Organizasyon') }));
const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Ana Sayfa'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('monitoring.pageTitle', 'Monitoring tanımları'), disabled: false, href: '/apps/monitoring' },
  { text: mt('organization.breadcrumbs.title', 'Organizasyon'), disabled: true, href: '#' },
]);

const assetTypes = ref<MonAssetType[]>([]);

const parentOptions = computed(() => {
  const opts = [{ title: '(Kök)', value: null }];
  const currentId = orgStore.selectedNode?.type === 'item' && orgStore.selectedNode.data?.__dataId
    ? orgStore.selectedNode.data.__dataId
    : null;
  orgStore.items.forEach((i) => {
    if (i.__dataId !== currentId) opts.push({ title: i.name, value: i.__dataId });
  });
  return opts;
});

const itemOptions = computed(() =>
  orgStore.items.map((i) => ({ title: i.name, value: i.__dataId }))
);

const typeOptions = computed(() =>
  assetTypes.value.map((t: any) => ({
    title: t.name ?? t.Name ?? t.__dataId ?? t.dataId ?? '',
    value: t.__dataId ?? t.dataId ?? '',
  }))
);

const panelTitle = computed(() => {
  const sel = orgStore.selectedNode;
  if (!sel) return 'Öğe seçin veya Yeni Item / Yeni Asset ekleyin';
  if (sel.type === 'item') {
    return sel.data.__dataId ? `Item: ${sel.data.name}` : 'Yeni Item';
  }
  return sel.data.__dataId ? `Asset: ${sel.data.name}` : 'Yeni Asset';
});

onMounted(async () => {
  await orgStore.loadAll();
  try {
    const res = await fetchFromDataGateway('/api/v1/data/mon_asset_types?limit=500');
    assetTypes.value = Array.isArray(res) ? res : (res?.items ?? res?.data ?? []);
  } catch {
    assetTypes.value = [];
  }
  try {
    await httpAuthStore.loadAll();
  } catch {
    // mon_http_auth_configs yoksa sessizce devam et
  }
});

function handleNodeSelect(node: OrganizationSelectedNode) {
  orgStore.selectNode(node);
}

function handleNewItem() {
  const parentId = orgStore.selectedNode?.type === 'item' && orgStore.selectedNode.data?.__dataId
    ? orgStore.selectedNode.data.__dataId
    : null;
  orgStore.selectNode({ type: 'item', data: { name: '', parentId, description: null, location: null, kind: null, tags: null } });
}

function handleNewAsset() {
  const itemId = orgStore.selectedNode?.type === 'item' && orgStore.selectedNode.data?.__dataId
    ? orgStore.selectedNode.data.__dataId
    : '';
  orgStore.selectNode({
    type: 'asset',
    data: {
      name: '',
      type: typeOptions.value[0]?.value ?? '',
      itemId: itemId || (itemOptions.value[0]?.value ?? ''),
      description: null,
      status: 'active',
      connection_info: {},
      collectible_config: null,
    },
  });
}

function handleSearch(query: string) {
  orgStore.setSearchQuery(query);
}

async function handleRefresh() {
  await orgStore.loadAll();
}

async function handleSaveItem(data: Partial<import('@/types/apps/organization').MonItem>) {
  const id = (data as any).__dataId;
  if (id) {
    await orgStore.updateItem(id, data);
  } else {
    await orgStore.createItem(data);
  }
  orgStore.resetSelection();
}

async function handleDeleteItem(id: string) {
  await orgStore.deleteItem(id);
}

async function handleSaveAsset(data: Partial<import('@/types/apps/organization').MonAsset>) {
  const id = (data as any).__dataId;
  if (id) {
    await orgStore.updateAsset(id, data);
  } else {
    await orgStore.createAsset(data);
  }
  orgStore.resetSelection();
}

async function handleDeleteAsset(id: string) {
  await orgStore.deleteAsset(id);
}

function handleExpandAll() {
  treeViewRef.value?.expandAll();
}

function handleCollapseAll() {
  treeViewRef.value?.collapseAll();
}
</script>

<template>
  <div class="organization-page">
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-container fluid>
      <v-row>
        <v-col cols="12">
          <OrganizationToolbar
            :can-edit="canEdit"
            @new-item="handleNewItem"
            @new-asset="handleNewAsset"
            @search="handleSearch"
            @refresh="handleRefresh"
            :loading="orgStore.loading"
          />
        </v-col>
      </v-row>

      <v-row v-if="orgStore.error">
        <v-col cols="12">
          <v-alert type="error" variant="tonal" dismissible @click:close="orgStore.error = null">
            {{ orgStore.error }}
          </v-alert>
        </v-col>
      </v-row>

      <v-row>
        <v-col cols="12" md="5" lg="4">
          <v-card elevation="2">
            <v-card-title class="d-flex align-center">
              <span>Organizasyon ağacı</span>
              <v-spacer />
              <v-btn icon size="small" variant="text" title="Tümünü aç" class="mr-1" @click="handleExpandAll">
                <ChevronDownIcon size="18" />
              </v-btn>
              <v-btn icon size="small" variant="text" title="Tümünü kapat" class="mr-2" @click="handleCollapseAll">
                <ChevronUpIcon size="18" />
              </v-btn>
              <v-chip size="small" color="primary">
                {{ orgStore.filteredTreeNodes.length }} kök
              </v-chip>
            </v-card-title>
            <v-divider />
            <v-card-text class="pa-0" style="max-height: calc(100vh - 280px); overflow-y: auto;">
              <OrganizationTreeView
                ref="treeViewRef"
                :items="orgStore.filteredTreeNodes"
                :selected-node="orgStore.selectedNode"
                :loading="orgStore.loading"
                @node-select="handleNodeSelect"
              />
            </v-card-text>
          </v-card>
        </v-col>

        <v-col cols="12" md="7" lg="8">
          <v-card elevation="2">
            <v-card-title>{{ panelTitle }}</v-card-title>
            <v-divider />
            <v-card-text>
              <template v-if="!orgStore.selectedNode">
                <p class="text-medium-emphasis">Sol ağaçtan bir Item veya Asset seçin ya da araç çubuğundan Yeni Item / Yeni Asset ile ekleyin.</p>
              </template>
              <template v-else-if="orgStore.selectedNode.type === 'item'">
                <OrganizationItemForm
                  :item="orgStore.selectedNode.data"
                  :parent-options="parentOptions"
                  :can-edit="canEdit"
                  :loading="orgStore.loading"
                  @save="handleSaveItem"
                  @delete="handleDeleteItem"
                  @cancel="orgStore.resetSelection()"
                />
              </template>
              <template v-else>
                <OrganizationAssetForm
                  :asset="orgStore.selectedNode.data"
                  :item-options="itemOptions"
                  :type-options="typeOptions"
                  :asset-types="assetTypes"
                  :http-auth-config-options="httpAuthStore.options"
                  :can-edit="canEdit"
                  :loading="orgStore.loading"
                  @save="handleSaveAsset"
                  @delete="handleDeleteAsset"
                  @cancel="orgStore.resetSelection()"
                />
              </template>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </v-container>
  </div>
</template>

<style scoped>
.organization-page {
  min-height: calc(100vh - 100px);
}
</style>
