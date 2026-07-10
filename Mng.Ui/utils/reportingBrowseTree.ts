import type { DiTreeNode } from '@/types/apps/documentIntelligence';
import type { ReportingReportDefinition } from '@/types/apps/reporting';

export const REPORTING_BROWSE_REPORT_ID_PREFIX = 'rpt:';
export const REPORTING_BROWSE_UNCATEGORIZED_ID = '__uncategorized__';

export function reportingBrowseReportNodeId(reportId: string): string {
  return `${REPORTING_BROWSE_REPORT_ID_PREFIX}${reportId}`;
}

export function parseReportingBrowseReportId(nodeId: string | null): string | null {
  if (!nodeId?.startsWith(REPORTING_BROWSE_REPORT_ID_PREFIX)) return null;
  const id = nodeId.slice(REPORTING_BROWSE_REPORT_ID_PREFIX.length).trim();
  return id || null;
}

function cloneCategoryTree(nodes: DiTreeNode[]): DiTreeNode[] {
  return nodes.map((n) => ({
    id: n.id,
    name: n.name,
    parentId: n.parentId,
    hasChildren: false,
    children: cloneCategoryTree(n.children ?? []),
    kind: 'folder' as const,
  }));
}

function findCategoryNode(nodes: DiTreeNode[], id: string): DiTreeNode | null {
  for (const n of nodes) {
    if (n.id === id) return n;
    const nested = findCategoryNode(n.children, id);
    if (nested) return nested;
  }
  return null;
}

function sortBrowseNodes(nodes: DiTreeNode[]) {
  nodes.sort((a, b) => {
    const aFile = a.kind === 'file' ? 1 : 0;
    const bFile = b.kind === 'file' ? 1 : 0;
    if (aFile !== bFile) return aFile - bFile;
    return a.name.localeCompare(b.name, 'tr', { sensitivity: 'base' });
  });
  for (const n of nodes) sortBrowseNodes(n.children);
}

function pruneEmptyFolders(nodes: DiTreeNode[]): DiTreeNode[] {
  const next: DiTreeNode[] = [];
  for (const n of nodes) {
    if (n.kind === 'file') {
      next.push(n);
      continue;
    }
    const children = pruneEmptyFolders(n.children);
    if (!children.length) continue;
    next.push({
      ...n,
      children,
      hasChildren: children.length > 0,
    });
  }
  return next;
}

function markHasChildren(nodes: DiTreeNode[]) {
  for (const n of nodes) {
    markHasChildren(n.children);
    n.hasChildren = n.children.length > 0;
  }
}

/**
 * Kategori ağacı + canView rapor yaprakları.
 * Rapor düğüm id: `rpt:{reportId}`.
 */
export function buildReportingBrowseTree(options: {
  categoryTree: DiTreeNode[];
  reports: ReportingReportDefinition[];
  uncategorizedLabel: string;
}): DiTreeNode[] {
  const roots = cloneCategoryTree(options.categoryTree);
  const uncategorizedReports: ReportingReportDefinition[] = [];

  for (const report of options.reports) {
    const node: DiTreeNode = {
      id: reportingBrowseReportNodeId(report.id),
      name: report.title || report.id,
      parentId: report.categoryId,
      hasChildren: false,
      children: [],
      kind: 'file',
    };

    if (report.categoryId) {
      const parent = findCategoryNode(roots, report.categoryId);
      if (parent) {
        parent.children.push(node);
        continue;
      }
    }
    uncategorizedReports.push(report);
  }

  if (uncategorizedReports.length) {
    const folder: DiTreeNode = {
      id: REPORTING_BROWSE_UNCATEGORIZED_ID,
      name: options.uncategorizedLabel,
      parentId: null,
      hasChildren: true,
      children: uncategorizedReports.map((report) => ({
        id: reportingBrowseReportNodeId(report.id),
        name: report.title || report.id,
        parentId: REPORTING_BROWSE_UNCATEGORIZED_ID,
        hasChildren: false,
        children: [],
        kind: 'file' as const,
      })),
      kind: 'folder',
    };
    roots.push(folder);
  }

  sortBrowseNodes(roots);
  const pruned = pruneEmptyFolders(roots);
  markHasChildren(pruned);
  return pruned;
}

/** Seçili rapor düğümüne giden kategori id yolu (expand için). */
export function findReportingBrowseAncestorIds(
  nodes: DiTreeNode[],
  targetId: string,
  trail: string[] = []
): string[] | null {
  for (const n of nodes) {
    if (n.id === targetId) return trail;
    const found = findReportingBrowseAncestorIds(n.children, targetId, [...trail, n.id]);
    if (found) return found;
  }
  return null;
}
