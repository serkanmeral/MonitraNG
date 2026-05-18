<script setup lang="ts">
import { computed, watch } from 'vue';
import { useRouter } from 'vue-router';
import type { OrganizationTreeNode, MonItem, MonAsset } from '@/types/apps/organization';
import { useAssetTypeDefinitionsStore } from '@/stores/apps/assetTypeDefinitions';
import { FolderIcon, DeviceDesktopIcon } from 'vue-tabler-icons';

const props = defineProps<{
  open: boolean;
  itemId: string | null;
  treeNodes: OrganizationTreeNode[];
  mt?: (key: string, fallback: string) => string;
}>();

const emit = defineEmits<{
  'update:open': [value: boolean];
}>();

function t(key: string, fallback: string): string {
  return props.mt?.(key, fallback) ?? fallback;
}

const assetTypeStore = useAssetTypeDefinitionsStore();
const router = useRouter();

watch(() => props.open, (isOpen) => {
  if (isOpen && assetTypeStore.types.length === 0) {
    assetTypeStore.loadTypes();
  }
});

function findNodeById(nodes: OrganizationTreeNode[], id: string): OrganizationTreeNode | null {
  for (const n of nodes) {
    if (n.data.__dataId === id) return n;
    if (n.type === 'item' && n.children?.length) {
      const found = findNodeById(n.children, id);
      if (found) return found;
    }
  }
  return null;
}

const itemNode = computed(() => {
  if (!props.itemId || !props.treeNodes.length) return null;
  return findNodeById(props.treeNodes, props.itemId);
});

const itemData = computed(() => (itemNode.value?.type === 'item' ? (itemNode.value.data as MonItem) : null));

const childItems = computed(() => {
  const node = itemNode.value;
  if (!node || node.type !== 'item') return [];
  return node.children.filter((c): c is OrganizationTreeNode & { type: 'item' } => c.type === 'item');
});

const childAssets = computed(() => {
  const node = itemNode.value;
  if (!node || node.type !== 'item') return [];
  return node.children.filter((c): c is OrganizationTreeNode & { type: 'asset' } => c.type === 'asset');
});

function typeDisplayName(typeId: string): string {
  const def = assetTypeStore.types.find((x) => x.__dataId === typeId);
  return def?.name ?? typeId;
}

function statusLabel(s: string): string {
  const map: Record<string, string> = {
    active: t('monitoring.engines.statusActive', 'Aktif'),
    maintenance: t('monitoring.engines.statusMaintenance', 'Bakımda'),
    decommissioned: t('monitoring.control.decommissioned', 'Devre dışı'),
  };
  return map[s] ?? s;
}

function close() {
  emit('update:open', false);
}

function goToControl() {
  close();
  router.push('/apps/monitoring/control');
}

function goToOrganization() {
  close();
  router.push('/apps/monitoring/organization');
}
</script>

<template>
  <v-dialog
    :model-value="open"
    max-width="520"
    persistent
    @update:model-value="emit('update:open', $event)"
  >
    <v-card v-if="itemData">
      <v-card-title class="d-flex align-center py-3">
        <FolderIcon size="24" class="mr-2 text-primary" />
        <span>{{ itemData.name }}</span>
        <v-spacer />
        <v-btn icon variant="text" size="small" @click="close">
          <v-icon>mdi-close</v-icon>
        </v-btn>
      </v-card-title>
      <v-divider />
      <v-card-text class="pt-4">
        <v-list density="compact" class="bg-transparent">
          <v-list-item v-if="itemData.description">
            <template #prepend>
              <span class="text-caption text-medium-emphasis" style="min-width: 100px">{{ t('monitoring.control.description', 'Açıklama') }}</span>
            </template>
            <v-list-item-title class="text-body-2">{{ itemData.description }}</v-list-item-title>
          </v-list-item>
          <v-list-item v-if="itemData.kind">
            <template #prepend>
              <span class="text-caption text-medium-emphasis" style="min-width: 100px">{{ t('monitoring.control.kind', 'Tür') }}</span>
            </template>
            <v-list-item-title class="text-body-2">{{ itemData.kind }}</v-list-item-title>
          </v-list-item>
          <v-list-item v-if="itemData.location && typeof itemData.location.lat === 'number' && typeof itemData.location.lon === 'number'">
            <template #prepend>
              <span class="text-caption text-medium-emphasis" style="min-width: 100px">{{ t('monitoring.control.location', 'Konum') }}</span>
            </template>
            <v-list-item-title class="text-body-2">
              {{ itemData.location?.lat?.toFixed(6) ?? '—' }}, {{ itemData.location?.lon?.toFixed(6) ?? '—' }}
            </v-list-item-title>
          </v-list-item>
          <v-list-item>
            <template #prepend>
              <span class="text-caption text-medium-emphasis" style="min-width: 100px">{{ t('monitoring.control.childCount', 'Alt öğe sayısı') }}</span>
            </template>
            <v-list-item-title class="text-body-2">
              {{ childItems.length + childAssets.length }} {{ t('monitoring.control.itemsOrAssets', 'item/asset') }}
            </v-list-item-title>
          </v-list-item>
        </v-list>

        <!-- Alt Item'lar -->
        <div v-if="childItems.length > 0" class="mt-4">
          <div class="text-caption text-medium-emphasis mb-2">{{ t('organization.mapModal.childItems', 'Alt item\'lar') }}</div>
          <v-list density="compact" class="bg-transparent">
            <v-list-item v-for="c in childItems" :key="c.data.__dataId" class="pl-0">
              <template #prepend>
                <FolderIcon size="18" class="mr-2 text-primary" />
              </template>
              <v-list-item-title class="text-body-2">{{ c.data.name }}</v-list-item-title>
            </v-list-item>
          </v-list>
        </div>

        <!-- Alt Asset'ler -->
        <div v-if="childAssets.length > 0" class="mt-4">
          <div class="text-caption text-medium-emphasis mb-2">{{ t('organization.mapModal.childAssets', 'Alt asset\'ler') }}</div>
          <v-list density="compact" class="bg-transparent">
            <v-list-item v-for="c in childAssets" :key="c.data.__dataId" class="pl-0">
              <template #prepend>
                <DeviceDesktopIcon size="18" class="mr-2 text-secondary" />
              </template>
              <v-list-item-title class="text-body-2">{{ c.data.name }}</v-list-item-title>
              <template #append>
                <v-chip size="x-small" variant="tonal" class="ml-1">
                  {{ typeDisplayName(c.data.type) }}
                </v-chip>
                <v-chip size="x-small" variant="tonal" class="ml-1" :color="c.data.status === 'active' ? 'success' : undefined">
                  {{ statusLabel(c.data.status) }}
                </v-chip>
              </template>
            </v-list-item>
          </v-list>
        </div>

        <div v-if="childItems.length === 0 && childAssets.length === 0" class="text-caption text-medium-emphasis mt-4">
          {{ t('organization.mapModal.noChildren', 'Alt öğe yok') }}
        </div>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-3">
        <v-spacer />
        <v-btn variant="text" size="small" @click="goToOrganization">
          {{ t('organization.mapModal.goToOrg', 'Organizasyona git') }}
        </v-btn>
        <v-btn color="primary" variant="flat" size="small" @click="goToControl">
          {{ t('organization.mapModal.goToControl', 'Kontrol sayfasına git') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
