export type SeatRow = {
  id: string;
  ad: string;
  ust: string;
  departman: string;
  oturan: string;
  calisanlar: string[];
  sira: number;
};

export const SEAT_W = 248;
export const SEAT_H = 112;
export const STAFF_PREVIEW = 3;
const STAFF_HEAD = 36;
const STAFF_LINE = 26;
const STAFF_MORE = 22;
const GAP_X = 28;
const GAP_Y = 72;

export type SeatBox = { id: string; x: number; y: number; h: number };

export function seatHeight(staffCount: number) {
  const count = Math.max(staffCount, 0);
  if (!count) return SEAT_H;
  const shown = Math.min(count, STAFF_PREVIEW);
  const more = count > STAFF_PREVIEW ? STAFF_MORE : 0;
  return SEAT_H + STAFF_HEAD + shown * STAFF_LINE + more;
}

function childrenOf(rows: SeatRow[], parentId: string) {
  return rows
    .filter((row) => row.ust === parentId)
    .sort((a, b) => a.sira - b.sira || a.ad.localeCompare(b.ad, 'tr'));
}

export function seatRoots(rows: SeatRow[]) {
  const ids = new Set(rows.map((row) => row.id));
  return rows
    .filter((row) => !row.ust || !ids.has(row.ust) || row.ust === row.id)
    .sort((a, b) => a.sira - b.sira || a.ad.localeCompare(b.ad, 'tr'));
}

export function descendantIds(rows: SeatRow[], id: string) {
  const found = new Set<string>();
  const walk = (parentId: string) => {
    for (const child of childrenOf(rows, parentId)) {
      if (found.has(child.id)) continue;
      found.add(child.id);
      walk(child.id);
    }
  };
  walk(id);
  return found;
}

function subtreeWidth(rows: SeatRow[], id: string, ancestors: Set<string>): number {
  if (ancestors.has(id)) return SEAT_W;
  const next = new Set(ancestors);
  next.add(id);
  const children = childrenOf(rows, id).filter((child) => !next.has(child.id));
  if (!children.length) return SEAT_W;
  const inner = children.reduce((sum, child) => sum + subtreeWidth(rows, child.id, next), 0);
  return inner + GAP_X * (children.length - 1);
}

export function layoutSeats(rows: SeatRow[]): SeatBox[] {
  const boxes: SeatBox[] = [];
  const place = (row: SeatRow, left: number, top: number, seen: Set<string>) => {
    if (seen.has(row.id)) return;
    const width = subtreeWidth(rows, row.id, seen);
    const height = seatHeight(row.calisanlar.length);
    seen.add(row.id);
    boxes.push({
      id: row.id,
      x: left + (width - SEAT_W) / 2,
      y: top,
      h: height,
    });
    let cursor = left;
    for (const child of childrenOf(rows, row.id)) {
      const childWidth = subtreeWidth(rows, child.id, new Set(seen));
      place(child, cursor, top + height + GAP_Y, seen);
      cursor += childWidth + GAP_X;
    }
  };

  let left = 0;
  const seen = new Set<string>();
  for (const root of seatRoots(rows)) {
    const width = subtreeWidth(rows, root.id, new Set());
    place(root, left, 0, seen);
    left += width + GAP_X * 2;
  }
  return boxes;
}

export function seatAtPoint(boxes: SeatBox[], x: number, y: number, ignoreId: string) {
  return boxes.find((box) =>
    box.id !== ignoreId
    && x >= box.x
    && x <= box.x + SEAT_W
    && y >= box.y
    && y <= box.y + box.h,
  ) || null;
}
