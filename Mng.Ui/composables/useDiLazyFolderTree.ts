import { ref } from 'vue';
import type { DiTreeNode, DiTreePath } from '@/types/apps/documentIntelligence';
import { diGetTreeChildren, diGetTreePath, diGetTreeRoots } from '@/services/documentIntelligenceService';

/** Cache key for root-level folder children. */
export const DI_TREE_ROOT_CACHE_KEY = '__root__';

function cacheKeyForParent(parentId: string | null): string {
  return parentId ?? DI_TREE_ROOT_CACHE_KEY;
}

function cloneTreeNodes(nodes: DiTreeNode[]): DiTreeNode[] {
  return nodes.map((n) => ({
    ...n,
    children: cloneTreeNodes(n.children ?? []),
  }));
}

function findTreeNode(nodes: DiTreeNode[], id: string): DiTreeNode | null {
  for (const node of nodes) {
    if (node.id === id) return node;
    const nested = findTreeNode(node.children ?? [], id);
    if (nested) return nested;
  }
  return null;
}

function attachTreeChildren(nodes: DiTreeNode[], parentId: string, children: DiTreeNode[]): boolean {
  for (const node of nodes) {
    if (node.id === parentId) {
      node.children = children;
      return true;
    }
    if (node.children?.length && attachTreeChildren(node.children, parentId, children)) {
      return true;
    }
  }
  return false;
}

/**
 * Lazy folder tree state: kök seviye + parentId bazlı children cache.
 */
export function useDiLazyFolderTree() {
  const treeRoots = ref<DiTreeNode[]>([]);
  const childrenCache = ref(new Map<string, DiTreeNode[]>());
  const loadingParentIds = ref(new Set<string>());

  function setRoots(roots: DiTreeNode[]) {
    treeRoots.value = cloneTreeNodes(roots);
    childrenCache.value.set(DI_TREE_ROOT_CACHE_KEY, cloneTreeNodes(roots));
  }

  function invalidateParent(parentId: string | null) {
    childrenCache.value.delete(cacheKeyForParent(parentId));
    if (parentId === null) {
      treeRoots.value = [];
      return;
    }
    const node = findTreeNode(treeRoots.value, parentId);
    if (node) node.children = [];
  }

  function invalidateAll() {
    childrenCache.value = new Map();
    treeRoots.value = [];
  }

  async function loadChildren(parentId: string | null, force = false): Promise<DiTreeNode[]> {
    const key = cacheKeyForParent(parentId);
    if (!force && childrenCache.value.has(key)) {
      return childrenCache.value.get(key)!;
    }
    if (loadingParentIds.value.has(key)) {
      return childrenCache.value.get(key) ?? [];
    }

    loadingParentIds.value.add(key);
    try {
      const nodes = cloneTreeNodes(await diGetTreeChildren(parentId));
      childrenCache.value.set(key, nodes);
      if (parentId === null) {
        treeRoots.value = nodes;
      } else {
        attachTreeChildren(treeRoots.value, parentId, nodes);
      }
      return nodes;
    } finally {
      loadingParentIds.value.delete(key);
    }
  }

  async function refreshRoots(force = true) {
    await loadChildren(null, force);
  }

  function applyTreePath(path: DiTreePath) {
    for (const segment of path.segments) {
      const key = cacheKeyForParent(segment.parentId);
      const nodes = cloneTreeNodes(segment.nodes);
      childrenCache.value.set(key, nodes);
      if (segment.parentId === null) {
        treeRoots.value = nodes;
      } else {
        attachTreeChildren(treeRoots.value, segment.parentId, nodes);
      }
    }
  }

  async function hydrateTreePath(folderId: string): Promise<DiTreePath> {
    const path = await diGetTreePath(folderId);
    applyTreePath(path);
    return path;
  }

  async function ensureRootsLoaded() {
    if (treeRoots.value.length) return;
    await loadChildren(null);
  }

  function isParentLoading(parentId: string | null): boolean {
    return loadingParentIds.value.has(cacheKeyForParent(parentId));
  }

  return {
    treeRoots,
    childrenCache,
    loadingParentIds,
    setRoots,
    invalidateParent,
    invalidateAll,
    loadChildren,
    refreshRoots,
    applyTreePath,
    hydrateTreePath,
    ensureRootsLoaded,
    isParentLoading,
    diGetTreeRoots,
  };
}
