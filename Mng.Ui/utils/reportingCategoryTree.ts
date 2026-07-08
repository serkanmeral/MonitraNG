import type { DiTreeNode } from '@/types/apps/documentIntelligence';
import type { ReportingCategory } from '@/types/apps/reporting';

export function buildReportingCategoryTree(categories: ReportingCategory[]): DiTreeNode[] {
  const nodes = new Map<string, DiTreeNode>();
  for (const cat of categories) {
    nodes.set(cat.id, {
      id: cat.id,
      name: cat.name,
      parentId: cat.parentId,
      hasChildren: false,
      children: [],
    });
  }

  const roots: DiTreeNode[] = [];
  for (const cat of categories) {
    const node = nodes.get(cat.id);
    if (!node) continue;
    if (cat.parentId && nodes.has(cat.parentId)) {
      const parent = nodes.get(cat.parentId)!;
      parent.children.push(node);
      parent.hasChildren = true;
    } else {
      roots.push(node);
    }
  }

  sortTreeNodes(roots);
  return roots;
}

function sortTreeNodes(nodes: DiTreeNode[]) {
  nodes.sort((a, b) => a.name.localeCompare(b.name, 'tr', { sensitivity: 'base' }));
  for (const node of nodes) sortTreeNodes(node.children);
}

export function flattenReportingCategoryOptions(
  nodes: DiTreeNode[],
  prefix = ''
): { value: string; title: string }[] {
  const items: { value: string; title: string }[] = [];
  for (const node of nodes) {
    const title = prefix ? `${prefix} / ${node.name}` : node.name;
    items.push({ value: node.id, title });
    if (node.children.length) {
      items.push(...flattenReportingCategoryOptions(node.children, title));
    }
  }
  return items;
}

export function findReportingCategoryNodeName(nodes: DiTreeNode[], id: string): string | null {
  for (const node of nodes) {
    if (node.id === id) return node.name;
    const nested = findReportingCategoryNodeName(node.children, id);
    if (nested) return nested;
  }
  return null;
}

export function reportingCategoryHasChild(categories: ReportingCategory[], parentId: string): boolean {
  return categories.some((c) => c.parentId === parentId);
}
