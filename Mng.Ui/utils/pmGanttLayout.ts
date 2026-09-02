import type { PmDependency, PmWbsItem } from '@/types/apps/projectManagement';

export type PmGanttScale = 'day' | 'week';

export interface PmGanttRange {
  start: Date;
  end: Date;
  dayCount: number;
}

export interface PmGanttBar {
  id: string;
  kind: string;
  name: string;
  wbsCode: string;
  depth: number;
  percentComplete: number;
  drifted: boolean;
  undated: boolean;
  isMilestone: boolean;
  isSummary: boolean;
  startDay: number;
  endDay: number;
  baselineStartDay: number | null;
  baselineEndDay: number | null;
  actualStartDay: number | null;
  actualEndDay: number | null;
}

export interface PmGanttLink {
  id: string;
  fromId: string;
  toId: string;
  lagDays: number;
  fromDay: number;
  toDay: number;
  fromRow: number;
  toRow: number;
}

const MS_DAY = 86_400_000;

export function pmParseUtcDay(value?: string | null): Date | null {
  if (!value) return null;
  const slice = String(value).slice(0, 10);
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(slice);
  if (!match) return null;
  return new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])));
}

export function pmAddUtcDays(day: Date, days: number): Date {
  return new Date(day.getTime() + days * MS_DAY);
}

export function pmDiffUtcDays(a: Date, b: Date): number {
  return Math.round((a.getTime() - b.getTime()) / MS_DAY);
}

export function pmFormatUtcDay(day: Date): string {
  return day.toISOString().slice(0, 10);
}

function depthOf(code?: string | null): number {
  if (!code) return 0;
  return Math.max(0, code.split('.').length - 1);
}

function minDate(values: Array<Date | null | undefined>): Date | null {
  let best: Date | null = null;
  for (const value of values) {
    if (!value) continue;
    if (!best || value.getTime() < best.getTime()) best = value;
  }
  return best;
}

function maxDate(values: Array<Date | null | undefined>): Date | null {
  let best: Date | null = null;
  for (const value of values) {
    if (!value) continue;
    if (!best || value.getTime() > best.getTime()) best = value;
  }
  return best;
}

function childEnvelope(
  items: PmWbsItem[],
  rootId: string,
): { start: Date | null; finish: Date | null } {
  const byParent = new Map<string, PmWbsItem[]>();
  for (const item of items) {
    const parent = item.parentId || '';
    const list = byParent.get(parent) ?? [];
    list.push(item);
    byParent.set(parent, list);
  }
  const starts: Date[] = [];
  const finishes: Date[] = [];
  const stack = [...(byParent.get(rootId) ?? [])];
  const seen = new Set<string>();
  while (stack.length) {
    const node = stack.pop()!;
    if (!node.id || seen.has(node.id)) continue;
    seen.add(node.id);
    const start = pmParseUtcDay(node.plannedStart);
    const finish = pmParseUtcDay(node.plannedFinish) ?? start;
    if (start) starts.push(start);
    if (finish) finishes.push(finish);
    const kids = byParent.get(node.id);
    if (kids) stack.push(...kids);
  }
  return {
    start: minDate(starts),
    finish: maxDate(finishes),
  };
}

export function pmBuildGanttRange(
  items: PmWbsItem[],
  today = new Date(),
  padDays = 3,
): PmGanttRange {
  const collected: Date[] = [];
  for (const item of items) {
    for (const raw of [
      item.plannedStart,
      item.plannedFinish,
      item.baselineStart,
      item.baselineFinish,
      item.actualStart,
      item.actualFinish,
    ]) {
      const day = pmParseUtcDay(raw);
      if (day) collected.push(day);
    }
  }
  const todayUtc = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate()));
  const start = pmAddUtcDays(minDate(collected) ?? todayUtc, -padDays);
  const end = pmAddUtcDays(maxDate(collected) ?? todayUtc, padDays);
  const span = Math.max(1, pmDiffUtcDays(end, start));
  return { start, end, dayCount: span + 1 };
}

export function pmSuggestGanttScale(range: PmGanttRange): PmGanttScale {
  return range.dayCount > 42 ? 'week' : 'day';
}

export function pmGanttPxPerDay(scale: PmGanttScale): number {
  return scale === 'week' ? 14 : 28;
}

export function pmBuildGanttBars(items: PmWbsItem[], range: PmGanttRange): PmGanttBar[] {
  return items.map((item) => {
    const kind = (item.kind || 'task').toLowerCase();
    const isMilestone = kind === 'milestone';
    const isSummary = kind === 'summary';
    let start = pmParseUtcDay(item.plannedStart);
    let finish = pmParseUtcDay(item.plannedFinish) ?? start;
    if ((!start || !finish) && isSummary) {
      const envelope = childEnvelope(items, item.id);
      start = start ?? envelope.start;
      finish = finish ?? envelope.finish ?? start;
    }
    if (isMilestone) {
      start = start ?? finish;
      finish = start;
    }
    const undated = !start;
    const startDay = start ? Math.max(0, pmDiffUtcDays(start, range.start)) : 0;
    const rawEnd = finish ? pmDiffUtcDays(finish, range.start) : startDay;
    const endDay = undated ? 0 : Math.max(startDay, rawEnd);
    const baselineStart = pmParseUtcDay(item.baselineStart);
    const baselineFinish = pmParseUtcDay(item.baselineFinish) ?? baselineStart;
    const actualStart = pmParseUtcDay(item.actualStart);
    const actualFinish = pmParseUtcDay(item.actualFinish) ?? actualStart;
    return {
      id: item.id,
      kind,
      name: item.name,
      wbsCode: item.wbsCode || '',
      depth: depthOf(item.wbsCode),
      percentComplete: Math.max(0, Math.min(100, item.percentComplete ?? 0)),
      drifted: !!item.baselineDrifted,
      undated,
      isMilestone,
      isSummary,
      startDay,
      endDay,
      baselineStartDay: baselineStart ? pmDiffUtcDays(baselineStart, range.start) : null,
      baselineEndDay: baselineFinish ? pmDiffUtcDays(baselineFinish, range.start) : null,
      actualStartDay: actualStart ? pmDiffUtcDays(actualStart, range.start) : null,
      actualEndDay: actualFinish ? pmDiffUtcDays(actualFinish, range.start) : null,
    };
  });
}

export function pmBuildGanttLinks(
  dependencies: PmDependency[],
  bars: PmGanttBar[],
): PmGanttLink[] {
  const indexById = new Map(bars.map((bar, index) => [bar.id, index]));
  const links: PmGanttLink[] = [];
  for (const dep of dependencies) {
    const fromRow = indexById.get(dep.predecessorId);
    const toRow = indexById.get(dep.successorId);
    const fromBar = fromRow == null ? undefined : bars[fromRow];
    const toBar = toRow == null ? undefined : bars[toRow];
    if (fromRow == null || toRow == null || !fromBar || !toBar || fromBar.undated || toBar.undated) continue;
    links.push({
      id: dep.id,
      fromId: dep.predecessorId,
      toId: dep.successorId,
      lagDays: dep.lagDays ?? 0,
      fromDay: fromBar.endDay,
      toDay: toBar.startDay,
      fromRow,
      toRow,
    });
  }
  return links;
}

export function pmGanttHeaderTicks(range: PmGanttRange, scale: PmGanttScale): Array<{
  day: number;
  label: string;
  monthLabel: string | null;
  isMonthStart: boolean;
}> {
  const ticks: Array<{ day: number; label: string; monthLabel: string | null; isMonthStart: boolean }> = [];
  const step = scale === 'week' ? 7 : 1;
  for (let day = 0; day < range.dayCount; day += step) {
    const date = pmAddUtcDays(range.start, day);
    const utcDay = date.getUTCDate();
    const isMonthStart = utcDay === 1 || day === 0;
    ticks.push({
      day,
      label: scale === 'week' ? pmFormatUtcDay(date).slice(5) : String(utcDay),
      monthLabel: isMonthStart
        ? date.toLocaleString('tr-TR', { month: 'short', year: 'numeric', timeZone: 'UTC' })
        : null,
      isMonthStart,
    });
  }
  return ticks;
}

export function pmGanttTodayDay(range: PmGanttRange, today = new Date()): number | null {
  const todayUtc = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate()));
  const day = pmDiffUtcDays(todayUtc, range.start);
  if (day < 0 || day > range.dayCount) return null;
  return day;
}

export function pmFsConnectorPath(
  link: PmGanttLink,
  pxPerDay: number,
  rowHeight: number,
  headerHeight: number,
): string {
  const stub = 10;
  const x1 = (link.fromDay + 1) * pxPerDay;
  const x2 = link.toDay * pxPerDay;
  const y1 = headerHeight + link.fromRow * rowHeight + rowHeight / 2;
  const y2 = headerHeight + link.toRow * rowHeight + rowHeight / 2;
  const midX = Math.max(x1 + stub, x2 - stub, x1 + 8);
  if (Math.abs(y2 - y1) < 1) {
    return `M ${x1} ${y1} L ${Math.max(x2, x1 + stub)} ${y2}`;
  }
  return `M ${x1} ${y1} L ${midX} ${y1} L ${midX} ${y2} L ${x2} ${y2}`;
}
