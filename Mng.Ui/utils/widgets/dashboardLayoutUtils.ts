import type { DashboardLayout, LayoutCol, LayoutRow } from '@/stores/apps/dashboard';

export function collectWidgetIdsFromLayout(layout: DashboardLayout | null | undefined): string[] {
  if (!layout?.rows?.length) return [];
  const ids = new Set<string>();
  function walkCols(cols: LayoutCol[]) {
    for (const col of cols) {
      if (col.rows?.length) {
        for (const row of col.rows) walkCols(row.cols ?? []);
      }
      const id = (col.widgetId ?? '').trim();
      if (id) ids.add(id);
    }
  }
  for (const row of layout.rows) {
    walkCols(row.cols ?? []);
  }
  return Array.from(ids);
}

export function walkLayoutRows(rows: LayoutRow[], fn: (col: LayoutCol, rowIdx: number, colIdx: number) => void) {
  rows.forEach((row, rowIdx) => {
    (row.cols ?? []).forEach((col, colIdx) => fn(col, rowIdx, colIdx));
  });
}
