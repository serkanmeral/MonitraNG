import type { DiDocumentContextField } from '@/types/apps/documentIntelligence';

export interface DiContextPathTreeNode {
  id: string;
  title: string;
  path?: string;
  label?: string;
  dataType?: string;
  children: DiContextPathTreeNode[];
}

export interface DiContextPathTreeItem {
  title: string;
  value: string;
  selectable?: boolean;
  children?: DiContextPathTreeItem[];
}

export function buildContextPathTree(fields: DiDocumentContextField[]): DiContextPathTreeNode[] {
  const root: DiContextPathTreeNode[] = [];

  for (const field of fields) {
    const segments = field.path.split('.').filter(Boolean);
    if (!segments.length) continue;

    let siblings = root;
    let currentPath = '';

    for (let i = 0; i < segments.length; i += 1) {
      const segment = segments[i]!;
      currentPath = currentPath ? `${currentPath}.${segment}` : segment;
      let node = siblings.find((item) => item.id === currentPath);
      if (!node) {
        const isLeaf = i === segments.length - 1;
        node = {
          id: currentPath,
          title: isLeaf ? `${field.label} — ${field.path}` : segment,
          path: isLeaf ? field.path : undefined,
          label: isLeaf ? field.label : undefined,
          dataType: isLeaf ? field.dataType : undefined,
          children: [],
        };
        siblings.push(node);
      }
      siblings = node.children;
    }
  }

  return sortContextPathTree(root);
}

function sortContextPathTree(nodes: DiContextPathTreeNode[]): DiContextPathTreeNode[] {
  return nodes
    .map((node) => ({
      ...node,
      children: sortContextPathTree(node.children),
    }))
    .sort((a, b) => a.title.localeCompare(b.title, 'tr'));
}

export function filterContextPathTree(
  nodes: DiContextPathTreeNode[],
  query: string
): DiContextPathTreeNode[] {
  const q = query.trim().toLowerCase();
  if (!q) return nodes;

  function filterNode(node: DiContextPathTreeNode): DiContextPathTreeNode | null {
    const selfMatch =
      node.title.toLowerCase().includes(q) ||
      node.id.toLowerCase().includes(q) ||
      (node.path?.toLowerCase().includes(q) ?? false) ||
      (node.label?.toLowerCase().includes(q) ?? false);

    const filteredChildren = node.children
      .map(filterNode)
      .filter((item): item is DiContextPathTreeNode => item !== null);

    if (selfMatch || filteredChildren.length > 0) {
      return {
        ...node,
        children: filteredChildren.length > 0 ? filteredChildren : node.children,
      };
    }
    return null;
  }

  return nodes.map(filterNode).filter((item): item is DiContextPathTreeNode => item !== null);
}

export function contextPathTreeToVuetifyItems(nodes: DiContextPathTreeNode[]): DiContextPathTreeItem[] {
  return nodes.map((node) => ({
    title: node.title,
    value: node.path ?? node.id,
    selectable: Boolean(node.path),
    children: node.children.length ? contextPathTreeToVuetifyItems(node.children) : undefined,
  }));
}

export function isTableParameterKind(kind?: string | null): boolean {
  return (kind ?? 'scalar').toLowerCase() === 'table';
}

export function isImageParameterKind(kind?: string | null): boolean {
  return (kind ?? 'scalar').toLowerCase() === 'image';
}
