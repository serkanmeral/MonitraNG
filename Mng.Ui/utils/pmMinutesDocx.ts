/** Reads a BV-shaped meeting minutes docx in the browser. No model. */

const W = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main';
const BV_CODE = 'BV-MEETING-MINUTES';

export interface ParsedMinutesParticipant {
  name: string;
  organization: string;
  title: string;
}

export interface ParsedMinutesAction {
  code: string;
  title: string;
  firms: string[];
  dueDate: string | null;
}

export interface ParsedMinutes {
  readable: boolean;
  name: string;
  startAt: string | null;
  endAt: string | null;
  location: string;
  topics: string;
  participants: ParsedMinutesParticipant[];
  actions: ParsedMinutesAction[];
  warnings: string[];
}

export function minutesReaderFor(code?: string | null): 'bv' | null {
  return (code || '').trim().toUpperCase() === BV_CODE ? 'bv' : null;
}

export function fileTitle(fileName: string): string {
  return fileName.replace(/\.docx$/i, '').trim() || fileName;
}

export async function parseMinutesDocx(file: File, templateCode?: string | null): Promise<ParsedMinutes> {
  if (minutesReaderFor(templateCode) !== 'bv') {
    return unreadMinutes(file.name);
  }
  const xml = await readZipText(await file.arrayBuffer(), 'word/document.xml');
  const doc = new DOMParser().parseFromString(xml, 'application/xml');
  if (doc.getElementsByTagName('parsererror').length) {
    throw new Error('docx-xml');
  }
  const body = doc.getElementsByTagNameNS(W, 'body')[0];
  if (!body) throw new Error('docx-body');
  return parseBvBody(body, file.name);
}

function unreadMinutes(fileName: string): ParsedMinutes {
  return {
    readable: false,
    name: fileTitle(fileName).slice(0, 256),
    startAt: null,
    endAt: null,
    location: '',
    topics: '',
    participants: [],
    actions: [],
    warnings: ['noReader'],
  };
}

function parseBvBody(body: Element, fileName: string): ParsedMinutes {
  const warnings: string[] = [];
  let name = '';
  let dateText = '';
  let timeText = '';
  let location = '';
  const participants: ParsedMinutesParticipant[] = [];
  const topics: string[] = [];
  const actions: ParsedMinutesAction[] = [];

  for (const child of elementChildren(body)) {
    if (child.localName !== 'tbl') continue;
    const rows = tableRows(child);
    if (looksLikePeople(rows)) {
      for (const row of rows.slice(1)) {
        const person = cell(row, 1);
        if (!person || /^\d+$/.test(person)) continue;
        participants.push({
          name: person,
          organization: cell(row, 2),
          title: cell(row, 3),
        });
      }
      continue;
    }
    if (looksLikeActions(rows)) continue;
    const mapped = new Map<string, string>();
    for (const row of rows) pairMap(row, mapped);
    name = mapped.get(trKey('TOPLANTI ADI / KONUSU'))
      || mapped.get(trKey('TOPLANTI ADI'))
      || mapped.get(trKey('KONU'))
      || name;
    if (mapped.has(trKey('TARİH'))) dateText = mapped.get(trKey('TARİH')) || '';
    if (mapped.has(trKey('SAAT'))) timeText = mapped.get(trKey('SAAT')) || '';
    if (mapped.has(trKey('YER'))) location = mapped.get(trKey('YER')) || '';
  }

  for (const table of body.getElementsByTagNameNS(W, 'tbl')) {
    const rows = tableRows(table);
    const blob = trKey(rows.map((row) => row.join(' ')).join(' '));
    if (!blob.includes(trKey('GÖRÜŞÜLEN KONULAR')) && !blob.includes(trKey('İŞLEM MADDELERİ'))) continue;
    if (looksLikeActions(rows) || looksLikePeople(rows)) continue;
    for (const cellEl of directCells(table)) {
      const cellBlob = trKey(elementChildren(cellEl).filter((el) => el.localName === 'p').map(paraText).join(' '));
      if (!cellBlob.includes(trKey('GÖRÜŞÜLEN KONULAR')) && !cellBlob.includes(trKey('İŞLEM MADDELERİ'))) continue;
      let mode: 'topics' | 'actions' | null = null;
      for (const child of elementChildren(cellEl)) {
        if (child.localName === 'p') {
          const text = paraText(child);
          const key = trKey(text).replace(/:$/, '');
          if (key.startsWith(trKey('GÖRÜŞÜLEN KONULAR'))) {
            mode = 'topics';
            continue;
          }
          if (key.startsWith(trKey('İŞLEM MADDELERİ'))) {
            mode = 'actions';
            continue;
          }
          if (key.startsWith(trKey('HAZIRLAYAN'))) {
            mode = null;
            continue;
          }
          if (mode === 'topics' && meaningful(text)) topics.push(meaningful(text));
        } else if (child.localName === 'tbl' && mode === 'actions') {
          const actionRows = tableRows(child);
          if (!looksLikeActions(actionRows)) continue;
          for (const row of actionRows.slice(1)) {
            const title = cell(row, 1);
            if (!title) continue;
            const due = parseDate(cell(row, 3));
            actions.push({
              code: cell(row, 0).slice(0, 32),
              title: title.slice(0, 4000),
              firms: splitFirms(cell(row, 2)),
              dueDate: due
                ? `${due.year}-${String(due.month).padStart(2, '0')}-${String(due.day).padStart(2, '0')}`
                : null,
            });
          }
        }
      }
      break;
    }
    break;
  }

  name = meaningful(name);
  location = meaningful(location).slice(0, 256);
  if (!name) {
    warnings.push('noName');
    name = fileTitle(fileName);
  }
  name = name.slice(0, 256);

  const date = parseDate(dateText);
  const clock = parseTime(timeText);
  if (!date) warnings.push('noDate');
  if (date && !clock) warnings.push('noTime');
  let startAt: string | null = null;
  let endAt: string | null = null;
  if (date) {
    const [hour, minute] = clock || [9, 0];
    const start = new Date(date.year, date.month - 1, date.day, hour, minute, 0, 0);
    startAt = start.toISOString();
    endAt = new Date(start.getTime() + 60 * 60 * 1000).toISOString();
  }
  const topicText = topics.join('\n\n').trim();
  if (!topicText) warnings.push('noTopics');

  return {
    readable: true,
    name,
    startAt,
    endAt,
    location,
    topics: topicText,
    participants,
    actions,
    warnings,
  };
}

function elementChildren(el: Element): Element[] {
  return [...el.children].filter((child) => child.namespaceURI === W);
}

function directCells(table: Element): Element[] {
  const cells: Element[] = [];
  for (const row of elementChildren(table).filter((el) => el.localName === 'tr')) {
    for (const cellEl of elementChildren(row).filter((el) => el.localName === 'tc')) cells.push(cellEl);
  }
  return cells;
}

function paraText(paragraph: Element): string {
  return [...paragraph.getElementsByTagNameNS(W, 't')].map((node) => node.textContent || '').join('').replace(/\s+/g, ' ').trim();
}

function cellLines(cellEl: Element): string {
  return elementChildren(cellEl)
    .filter((el) => el.localName === 'p')
    .map(paraText)
    .filter(Boolean)
    .join(' ')
    .trim();
}

function tableRows(table: Element): string[][] {
  const rows: string[][] = [];
  for (const row of elementChildren(table).filter((el) => el.localName === 'tr')) {
    const cells = elementChildren(row).filter((el) => el.localName === 'tc').map(cellLines);
    if (cells.some(Boolean)) rows.push(cells);
  }
  return rows;
}

function looksLikePeople(rows: string[][]): boolean {
  if (!rows.length || rows[0].length < 3) return false;
  return rows[0].slice(0, 4).some((value) => {
    const key = trKey(value);
    return key.includes(trKey('ADI SOYADI')) || key.includes(trKey('AD SOYAD'));
  });
}

function looksLikeActions(rows: string[][]): boolean {
  if (!rows.length || rows[0].length < 3) return false;
  const head = rows[0].slice(0, 4).map(trKey);
  const hasNo = head.some((value) => value === 'no' || value === 'sira' || value === trKey('SIRA'));
  const hasAction = head.some((value) => value.includes(trKey('İŞLEM')));
  return hasNo && hasAction;
}

function pairMap(cells: string[], found: Map<string, string>) {
  const labels = new Set([
    trKey('TOPLANTI ADI / KONUSU'),
    trKey('TOPLANTI ADI'),
    trKey('KONU'),
    trKey('TARİH'),
    trKey('SAAT'),
    trKey('YER'),
  ]);
  for (let index = 0; index < cells.length - 1; index += 1) {
    const label = trKey(cells[index]).replace(/:$/, '');
    if (!labels.has(label)) continue;
    const value = meaningful(cells[index + 1]);
    if (value) found.set(label, value);
    index += 1;
  }
}

function cell(row: string[], index: number): string {
  return meaningful(row[index] || '');
}

function meaningful(value: string): string {
  const text = (value || '').replace(/\s+/g, ' ').trim();
  if (!text || /^\{\{[^}]+\}\}$/.test(text)) return '';
  return text;
}

function trKey(value: string): string {
  return (value || '').replace(/\s+/g, ' ').trim().toLocaleLowerCase('tr');
}

function splitFirms(value: string): string[] {
  const seen = new Set<string>();
  const firms: string[] = [];
  for (const part of value.split('/')) {
    const name = meaningful(part);
    const key = trKey(name);
    if (!name || seen.has(key)) continue;
    seen.add(key);
    firms.push(name);
  }
  return firms;
}

function parseDate(value: string): { year: number; month: number; day: number } | null {
  const match = /(\d{1,2})[./](\d{1,2})[./](\d{4})/.exec(value || '');
  if (!match) return null;
  const day = Number(match[1]);
  const month = Number(match[2]);
  const year = Number(match[3]);
  const probe = new Date(year, month - 1, day);
  if (probe.getFullYear() !== year || probe.getMonth() !== month - 1 || probe.getDate() !== day) return null;
  return { year, month, day };
}

function parseTime(value: string): [number, number] | null {
  const match = /(\d{1,2})[:.](\d{2})/.exec(value || '');
  if (!match) return null;
  const hour = Number(match[1]);
  const minute = Number(match[2]);
  if (hour > 23 || minute > 59) return null;
  return [hour, minute];
}

async function readZipText(data: ArrayBuffer, entryName: string): Promise<string> {
  const bytes = new Uint8Array(data);
  const view = new DataView(data);
  let eocd = -1;
  const start = Math.max(0, bytes.length - 22 - 0xffff);
  for (let index = bytes.length - 22; index >= start; index -= 1) {
    if (view.getUint32(index, true) === 0x06054b50) {
      eocd = index;
      break;
    }
  }
  if (eocd < 0) throw new Error('zip');
  const count = view.getUint16(eocd + 10, true);
  let cursor = view.getUint32(eocd + 16, true);
  const wanted = entryName.replace(/\\/g, '/');
  for (let index = 0; index < count; index += 1) {
    if (view.getUint32(cursor, true) !== 0x02014b50) throw new Error('zip');
    const method = view.getUint16(cursor + 10, true);
    const compressedSize = view.getUint32(cursor + 20, true);
    const nameLength = view.getUint16(cursor + 28, true);
    const extraLength = view.getUint16(cursor + 30, true);
    const commentLength = view.getUint16(cursor + 32, true);
    const localOffset = view.getUint32(cursor + 42, true);
    const name = new TextDecoder().decode(bytes.subarray(cursor + 46, cursor + 46 + nameLength));
    if (name.replace(/\\/g, '/') === wanted) {
      const payload = await readLocalEntry(bytes, view, localOffset, method, compressedSize);
      return new TextDecoder().decode(payload).replace(/^\uFEFF/, '');
    }
    cursor += 46 + nameLength + extraLength + commentLength;
  }
  throw new Error('zip-entry');
}

async function readLocalEntry(
  bytes: Uint8Array,
  view: DataView,
  offset: number,
  method: number,
  compressedSize: number,
): Promise<Uint8Array> {
  if (view.getUint32(offset, true) !== 0x04034b50) throw new Error('zip');
  const nameLength = view.getUint16(offset + 26, true);
  const extraLength = view.getUint16(offset + 28, true);
  const dataStart = offset + 30 + nameLength + extraLength;
  const slice = bytes.slice(dataStart, dataStart + compressedSize);
  if (method === 0) return slice;
  if (method !== 8) throw new Error('zip-method');
  const stream = new Blob([slice]).stream().pipeThrough(new DecompressionStream('deflate-raw'));
  return new Uint8Array(await new Response(stream).arrayBuffer());
}
