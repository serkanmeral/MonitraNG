import { defineStore } from 'pinia';
import { fetchFromDataGateway } from '@/services/apiService';
import type {
  MonItem,
  MonAsset,
  OrganizationTreeNode,
  OrganizationSelectedNode,
} from '@/types/apps/organization';

const MON_ITEMS_DATASET = 'mon_items';
const MON_ASSETS_DATASET = 'mon_assets';

interface OrganizationState {
  items: MonItem[];
  assets: MonAsset[];
  treeNodes: OrganizationTreeNode[];
  selectedNode: OrganizationSelectedNode | null;
  loading: boolean;
  error: string | null;
  searchQuery: string;
}

function parseArrayResponse(response: unknown): unknown[] {
  if (response && Array.isArray(response)) return response;
  if (response && typeof response === 'object' && 'items' in response && Array.isArray((response as any).items))
    return (response as any).items;
  if (response && typeof response === 'object' && 'data' in response && Array.isArray((response as any).data))
    return (response as any).data;
  return [];
}

function normalizeRelationId(value: any): string {
  if (value == null) return '';
  if (typeof value === 'string') return value;
  if (typeof value === 'object' && value !== null && ('__dataId' in value || 'dataId' in value))
    return (value as any).__dataId ?? (value as any).dataId ?? '';
  return String(value);
}

function mapItem(raw: any): MonItem {
  const parentIdVal = raw.parentId ?? raw.ParentId ?? null;
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    parentId: parentIdVal != null ? normalizeRelationId(parentIdVal) || null : null,
    description: raw.description ?? raw.Description ?? null,
    location: raw.location ?? raw.Location ?? null,
    kind: raw.kind ?? raw.Kind ?? null,
    tags: raw.tags ?? raw.Tags ?? null,
  };
}

function mapAsset(raw: any): MonAsset {
  return {
    __dataId: raw.__dataId ?? raw.DataId ?? raw.dataId ?? '',
    name: raw.name ?? raw.Name ?? '',
    type: normalizeRelationId(raw.type ?? raw.Type),
    itemId: normalizeRelationId(raw.itemId ?? raw.ItemId),
    description: raw.description ?? raw.Description ?? null,
    tags: raw.tags ?? raw.Tags ?? null,
    status: raw.status ?? raw.Status ?? 'active',
    connection_info: raw.connection_info ?? raw.ConnectionInfo ?? raw.connectionInfo ?? {},
    collectible_config: raw.collectible_config ?? raw.CollectibleConfig ?? null,
  };
}

function buildTree(items: MonItem[], assets: MonAsset[]): OrganizationTreeNode[] {
  const byParent = new Map<string | null, MonItem[]>();
  const assetsByItem = new Map<string, MonAsset[]>();

  items.forEach((i) => {
    const p = i.parentId ?? null;
    if (!byParent.has(p)) byParent.set(p, []);
    byParent.get(p)!.push(i);
  });
  assets.forEach((a) => {
    if (!assetsByItem.has(a.itemId)) assetsByItem.set(a.itemId, []);
    assetsByItem.get(a.itemId)!.push(a);
  });

  function build(parentId: string | null): OrganizationTreeNode[] {
    const childItems = (byParent.get(parentId) ?? []).sort((a, b) => a.name.localeCompare(b.name));
    const result: OrganizationTreeNode[] = [];
    for (const item of childItems) {
      const itemAssets = (assetsByItem.get(item.__dataId) ?? []).sort((a, b) => a.name.localeCompare(b.name));
      const children: OrganizationTreeNode[] = [
        ...build(item.__dataId),
        ...itemAssets.map((a) => ({ type: 'asset' as const, data: a, children: [] })),
      ];
      result.push({ type: 'item', data: item, children });
    }
    return result;
  }
  return build(null);
}

function filterTreeRecursive(nodes: OrganizationTreeNode[], query: string): OrganizationTreeNode[] {
  const q = query.toLowerCase().trim();
  const result: OrganizationTreeNode[] = [];
  for (const node of nodes) {
    const name = node.data.name ?? '';
    const desc = 'description' in node.data ? (node.data.description ?? '') : '';
    const match = name.toLowerCase().includes(q) || (typeof desc === 'string' && desc.toLowerCase().includes(q));
    if (node.type === 'item') {
      const filteredChildren = filterTreeRecursive(node.children, query);
      if (match || filteredChildren.length > 0) {
        result.push({ ...node, children: filteredChildren });
      }
    } else {
      if (match) result.push(node);
    }
  }
  return result;
}

export const useOrganizationStore = defineStore('organization', {
  state: (): OrganizationState => ({
    items: [],
    assets: [],
    treeNodes: [],
    selectedNode: null,
    loading: false,
    error: null,
    searchQuery: '',
  }),

  getters: {
    filteredTreeNodes(state): OrganizationTreeNode[] {
      if (!state.searchQuery.trim()) return state.treeNodes;
      return filterTreeRecursive(state.treeNodes, state.searchQuery);
    },
  },

  actions: {
    setSearchQuery(query: string) {
      this.searchQuery = query;
    },

    async loadItems() {
      try {
        const response = await fetchFromDataGateway(
          `/api/v1/data/${MON_ITEMS_DATASET}?limit=5000`
        );
        const raw = parseArrayResponse(response);
        this.items = raw.map((r: any) => mapItem(r));
      } catch (e: any) {
        this.items = [];
        throw e;
      }
    },

    async loadAssets() {
      try {
        const response = await fetchFromDataGateway(
          `/api/v1/data/${MON_ASSETS_DATASET}?limit=5000`
        );
        const raw = parseArrayResponse(response);
        this.assets = raw.map((r: any) => mapAsset(r));
      } catch (e: any) {
        this.assets = [];
        throw e;
      }
    },

    async loadAll() {
      this.loading = true;
      this.error = null;
      try {
        await this.loadItems();
        await this.loadAssets();
        this.buildTree();
      } catch (error: any) {
        this.error = error.message || 'Veri yüklenirken hata oluştu';
      } finally {
        this.loading = false;
      }
    },

    buildTree() {
      this.treeNodes = buildTree(this.items, this.assets);
    },

    selectNode(node: OrganizationSelectedNode) {
      this.selectedNode = node;
    },

    resetSelection() {
      this.selectedNode = null;
    },

    async createItem(payload: Partial<MonItem>) {
      this.loading = true;
      this.error = null;
      try {
        const clean = { ...payload };
        delete (clean as any).__dataId;
        const res = await fetchFromDataGateway(`/api/v1/data/${MON_ITEMS_DATASET}`, 'POST', clean);
        await this.loadAll();
        return res;
      } catch (error: any) {
        this.error = error.message || 'Item oluşturulurken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async updateItem(dataId: string, payload: Partial<MonItem>) {
      this.loading = true;
      this.error = null;
      try {
        const clean = { ...payload };
        delete (clean as any).__dataId;
        await fetchFromDataGateway(`/api/v1/data/${MON_ITEMS_DATASET}/${dataId}`, 'PUT', clean);
        await this.loadAll();
      } catch (error: any) {
        this.error = error.message || 'Item güncellenirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async deleteItem(dataId: string) {
      this.loading = true;
      this.error = null;
      try {
        await fetchFromDataGateway(`/api/v1/data/${MON_ITEMS_DATASET}/${dataId}`, 'DELETE');
        if (this.selectedNode?.type === 'item' && this.selectedNode.data.__dataId === dataId) {
          this.selectedNode = null;
        }
        await this.loadAll();
      } catch (error: any) {
        this.error = error.message || 'Item silinirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async createAsset(payload: Partial<MonAsset>) {
      this.loading = true;
      this.error = null;
      try {
        const clean = { ...payload };
        delete (clean as any).__dataId;
        const res = await fetchFromDataGateway(`/api/v1/data/${MON_ASSETS_DATASET}`, 'POST', clean);
        await this.loadAll();
        return res;
      } catch (error: any) {
        this.error = error.message || 'Asset oluşturulurken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async updateAsset(dataId: string, payload: Partial<MonAsset>) {
      this.loading = true;
      this.error = null;
      try {
        const clean = { ...payload };
        delete (clean as any).__dataId;
        await fetchFromDataGateway(`/api/v1/data/${MON_ASSETS_DATASET}/${dataId}`, 'PUT', clean);
        await this.loadAll();
      } catch (error: any) {
        this.error = error.message || 'Asset güncellenirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async deleteAsset(dataId: string) {
      this.loading = true;
      this.error = null;
      try {
        await fetchFromDataGateway(`/api/v1/data/${MON_ASSETS_DATASET}/${dataId}`, 'DELETE');
        if (this.selectedNode?.type === 'asset' && this.selectedNode.data.__dataId === dataId) {
          this.selectedNode = null;
        }
        await this.loadAll();
      } catch (error: any) {
        this.error = error.message || 'Asset silinirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },
  },
});
