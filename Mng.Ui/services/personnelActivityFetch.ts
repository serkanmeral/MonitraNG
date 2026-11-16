import { fetchFromDataGateway, fetchFromMngKeeper } from '@/services/apiService';
import type { WidgetDataResponse } from '@/services/widgetDataService';
import { useAuthStore } from '@/stores/auth';
import { istanbulDateRangeToTime } from '@/utils/widgets/dashboardSurfaceVariables';

interface GatewayTransport {
  accessToken?: string;
  request(
    path: string,
    options?: {
      method?: 'GET' | 'POST';
      body?: unknown;
      query?: Record<string, string | number | boolean | undefined>;
    },
  ): Promise<unknown>;
}

const PREFIX = 'mngui:personnel/';
const ROW_CAP = 80;

export function isPersonnelServiceRef(serviceRef: string): boolean {
  return serviceRef.startsWith(PREFIX);
}

interface PersonIdentity {
  personId: string;
  username: string;
  displayName: string;
  keycloakId: string;
}

interface ActivityBundle {
  meetings: Record<string, unknown>[];
  openItems: Record<string, unknown>[];
  closedItems: Record<string, unknown>[];
  openCount: number;
  closedCount: number;
  points: number;
  documents: Record<string, unknown>[];
}

const bundleCache = new WeakMap<GatewayTransport, Map<string, Promise<ActivityBundle>>>();
const documentCache = new WeakMap<GatewayTransport, Map<string, Promise<Record<string, unknown>[]>>>();

export interface PersonnelIdentity {
  name: string;
  department: string;
  photoUrl: string;
  manager: string;
  leaveBalance: number | null;
}

export interface PersonnelMeeting {
  id: string;
  title: string;
  startAt: string;
  endAt: string | null;
  status: string;
}

export async function fetchPersonnelIdentity(requestedPersonId: string): Promise<PersonnelIdentity> {
  const transport = browserTransport();
  const person = await resolvePerson(transport, requestedPersonId);
  const empty: PersonnelIdentity = { name: '', department: '', photoUrl: '', manager: '', leaveBalance: null };
  if (!person.personId) return empty;
  const [card, manager, leaveBalance] = await Promise.all([
    loadKeeperCard(transport, person.personId),
    loadManagerName(transport, person.personId),
    loadAnnualBalance(transport, person.personId),
  ]);
  return {
    name: card.name || person.displayName,
    department: card.department,
    photoUrl: card.photoUrl,
    manager,
    leaveBalance,
  };
}

export async function fetchPersonnelWeekMeetings(
  requestedPersonId: string,
  from: string,
  to: string,
): Promise<PersonnelMeeting[]> {
  const transport = browserTransport();
  const person = await resolvePerson(transport, requestedPersonId);
  if (!person.personId) return [];
  const span = istanbulDateRangeToTime(from.slice(0, 10), to.slice(0, 10));
  return loadMeetingEvents(transport, person.personId, span?.from ?? from, span?.to ?? to);
}

export async function fetchPersonnelServiceRef(
  serviceRef: string,
  parameters: Record<string, unknown>,
  transport: GatewayTransport,
): Promise<WidgetDataResponse> {
  const kind = serviceRef.slice(PREFIX.length);
  if (kind === 'documents') {
    const rows = await loadDocumentBundle(transport, parameters);
    return { data: rows, total: rows.length };
  }
  if (kind === 'meetings') {
    const person = await resolvePerson(transport, String(parameters.personId ?? '').trim());
    const rows = person.personId
      ? await loadMeetings(transport, person.personId, String(parameters.from ?? ''), String(parameters.to ?? ''))
      : [];
    return { data: rows, total: rows.length };
  }
  const bundle = await loadBundle(transport, parameters);
  if (kind === 'story-points') {
    return { data: [{ value: bundle.points }], total: 1 };
  }
  if (kind === 'work-open-count') {
    return { data: [{ value: bundle.openCount }], total: 1 };
  }
  if (kind === 'work-closed-count') {
    return { data: [{ value: bundle.closedCount }], total: 1 };
  }
  const rows =
    kind === 'meetings'
      ? bundle.meetings
      : kind === 'work-open'
        ? bundle.openItems
        : kind === 'work-closed'
          ? bundle.closedItems
      : [];
  return { data: rows, total: rows.length };
}

function loadBundle(
  transport: GatewayTransport,
  parameters: Record<string, unknown>,
): Promise<ActivityBundle> {
  const from = String(parameters.from ?? '');
  const to = String(parameters.to ?? '');
  const requested = String(parameters.personId ?? '').trim();
  const key = `${requested}|${from}|${to}`;
  let bucket = bundleCache.get(transport);
  if (!bucket) {
    bucket = new Map();
    bundleCache.set(transport, bucket);
  }
  const cached = bucket.get(key);
  if (cached) return cached;
  const pending = buildBundle(transport, requested, from, to).catch((error) => {
    bucket?.delete(key);
    throw error;
  });
  bucket.set(key, pending);
  return pending;
}

async function buildBundle(
  transport: GatewayTransport,
  requestedPersonId: string,
  from: string,
  to: string,
): Promise<ActivityBundle> {
  const person = await resolvePerson(transport, requestedPersonId);
  if (!person.personId) return emptyBundle();

  const work = await loadWork(transport, person.personId, from, to);

  return {
    meetings: [],
    openItems: work.openItems,
    closedItems: work.closedItems,
    openCount: work.openCount,
    closedCount: work.closedCount,
    points: work.points,
    documents: [],
  };
}

function loadDocumentBundle(
  transport: GatewayTransport,
  parameters: Record<string, unknown>,
): Promise<Record<string, unknown>[]> {
  const from = String(parameters.from ?? '');
  const to = String(parameters.to ?? '');
  const requested = String(parameters.personId ?? '').trim();
  const key = `${requested}|${from}|${to}`;
  let bucket = documentCache.get(transport);
  if (!bucket) {
    bucket = new Map();
    documentCache.set(transport, bucket);
  }
  const cached = bucket.get(key);
  if (cached) return cached;
  const pending = resolvePerson(transport, requested)
    .then((person) => (person.personId ? loadDocuments(transport, person, from, to) : []))
    .catch((error) => {
      bucket?.delete(key);
      throw error;
    });
  bucket.set(key, pending);
  return pending;
}

function browserTransport(): GatewayTransport {
  const authStore = useAuthStore();
  return {
    accessToken: authStore.accessToken ?? undefined,
    request(path, options) {
      const q = new URLSearchParams();
      if (options?.query) {
        for (const [key, value] of Object.entries(options.query)) {
          if (value === undefined || value === null || value === '') continue;
          q.set(key, String(value));
        }
      }
      const qs = q.toString();
      if (path.startsWith('/keeper/api/')) {
        const rest = path.slice('/keeper/api'.length);
        return fetchFromMngKeeper(`${rest}${qs ? `?${qs}` : ''}`, options?.method ?? 'GET', options?.body);
      }
      const rest = path.startsWith('/data/api') ? path.slice('/data/api'.length) : path;
      return fetchFromDataGateway(`/api${rest}${qs ? `?${qs}` : ''}`, options?.method ?? 'GET', options?.body);
    },
  };
}

async function loadKeeperCard(
  transport: GatewayTransport,
  personId: string,
): Promise<{ name: string; department: string; photoUrl: string }> {
  try {
    const raw = await transport.request(`/keeper/api/user/${encodeURIComponent(personId)}`);
    const user = userRecord(raw);
    const first = text(user.firstName ?? user.FirstName);
    const last = text(user.lastName ?? user.LastName);
    return {
      name: `${first} ${last}`.trim(),
      department: text(user.department ?? user.Department),
      photoUrl: keeperPhotoUrl(text(user.photoUrl ?? user.PhotoUrl)),
    };
  } catch {
    return { name: '', department: '', photoUrl: '' };
  }
}

async function loadManagerName(transport: GatewayTransport, personId: string): Promise<string> {
  const occupied = await dgList(transport, 'org_mevkiler', {
    filter: `oturan:eq:${personId}`,
    limit: 1,
    expand: false,
  });
  const ownSeat = occupied[0];
  let managerId = '';
  if (ownSeat) {
    const parentId = relationId(ownSeat.ust);
    if (!parentId) return '';
    const parents = await dgList(transport, 'org_mevkiler', {
      filter: `__dataId:eq:${parentId}`,
      limit: 1,
      expand: false,
    });
    managerId = relationId(parents[0]?.oturan);
  } else {
    const staffed = await dgList(transport, 'org_mevkiler', {
      filter: `calisanlar:eq:${personId}`,
      limit: 1,
      expand: false,
    });
    const seat = staffed[0];
    managerId = relationId(seat?.oturan);
    const parentId = relationId(seat?.ust);
    if (!managerId && parentId) {
      const parents = await dgList(transport, 'org_mevkiler', {
        filter: `__dataId:eq:${parentId}`,
        limit: 1,
        expand: false,
      });
      managerId = relationId(parents[0]?.oturan);
    }
  }
  if (!managerId) return '';
  try {
    const raw = await transport.request('/keeper/api/user/by-ids', {
      method: 'POST',
      body: { Ids: [managerId] },
    });
    const user = asRows(raw, 'users')[0];
    if (!user) return '';
    return `${text(user.firstName ?? user.FirstName)} ${text(user.lastName ?? user.LastName)}`.trim();
  } catch {
    return '';
  }
}

async function loadAnnualBalance(transport: GatewayTransport, personId: string): Promise<number | null> {
  const cards = await dgList(transport, 'izin_personel', {
    filter: `keeperKisiId:eq:${personId}`,
    limit: 1,
    expand: false,
  });
  const cardId = text(cards[0]?.__dataId ?? cards[0]?.dataId);
  if (!cardId) return null;
  const rows = await dgList(transport, 'izin_ozet', {
    filter: `personelId:eq:${cardId}`,
    limit: 40,
    expand: false,
  });
  const annual = rows
    .filter((row) => text(row.turKod) === 'yillik' && truthy(row.acikDonem))
    .sort((a, b) => instant(text(b.donemBaslangic)) - instant(text(a.donemBaslangic)));
  const balance = annual[0]?.bakiye;
  const value = typeof balance === 'number' ? balance : Number(balance);
  return Number.isFinite(value) ? value : null;
}

async function loadMeetingEvents(
  transport: GatewayTransport,
  personId: string,
  from: string,
  to: string,
): Promise<PersonnelMeeting[]> {
  if (!from || !to) return [];
  const attendance = await dgList(transport, 'mt_meeting_attendance', {
    filter: `userId:eq:${personId}`,
    limit: 1000,
    expand: false,
  });
  const meetingIds = [...new Set(attendance.map((row) => relationId(row.meetingId)).filter(Boolean))];
  if (!meetingIds.length) return [];
  const meetings: Record<string, unknown>[] = [];
  for (let index = 0; index < meetingIds.length; index += 40) {
    const chunk = meetingIds.slice(index, index + 40);
    const filter = `__dataId:in:[${chunk.map((id) => JSON.stringify(id)).join(',')}]`;
    meetings.push(...(await dgList(transport, 'mt_meetings', { filter, limit: chunk.length, expand: false })));
  }
  return meetings
    .filter((row) => text(row.status) !== 'cancelled')
    .filter((row) => inRange(text(row.startAt), from, to))
    .map((row) => ({
      id: text(row.__dataId ?? row.dataId),
      title: text(row.title) || 'Toplantı',
      startAt: text(row.startAt),
      endAt: text(row.endAt) || null,
      status: text(row.status) || 'scheduled',
    }));
}

let workNames: Promise<{ workspaces: Map<string, string>; states: Map<string, string> }> | null = null;

function loadWorkNames(transport: GatewayTransport): Promise<{ workspaces: Map<string, string>; states: Map<string, string> }> {
  if (!workNames) {
    workNames = Promise.all([
      dgList(transport, 'op_workspaces', { limit: 300, expand: false }),
      dgList(transport, 'op_states', { limit: 500, expand: false }),
    ])
      .then(([workspaces, states]) => ({
        workspaces: nameMap(workspaces),
        states: nameMap(states),
      }))
      .catch((error) => {
        workNames = null;
        throw error;
      });
  }
  return workNames;
}

function nameMap(rows: Record<string, unknown>[]): Map<string, string> {
  const map = new Map<string, string>();
  for (const row of rows) {
    const id = text(row.__dataId ?? row.dataId);
    const name = text(row.name ?? row.title);
    if (id && name) map.set(id, name);
  }
  return map;
}

function workRow(
  row: Record<string, unknown>,
  names: { workspaces: Map<string, string>; states: Map<string, string> },
): Record<string, unknown> {
  const id = text(row.__dataId ?? row.dataId);
  return {
    id,
    key: text(row.key),
    title: text(row.title) || 'İş',
    workspace: names.workspaces.get(relationId(row.workspaceId)) || '—',
    state: names.states.get(relationId(row.stateId)) || '—',
    opened: istanbulDateLabel(text(row.createdAt ?? row.__createdAt)),
    due: istanbulDateLabel(text(row.dueDate)),
    profileHref: id ? `/apps/operation-core/work-items/${encodeURIComponent(id)}/profile` : '',
  };
}

function istanbulDateLabel(value: string): string {
  const ymd = value.slice(0, 10);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(ymd)) return '—';
  const [year, month, day] = ymd.split('-');
  return `${day}.${month}.${year}`;
}

function userRecord(raw: unknown): Record<string, unknown> {
  if (!raw || typeof raw !== 'object') return {};
  const row = raw as Record<string, unknown>;
  const user = row.User ?? row.user;
  return user && typeof user === 'object' ? (user as Record<string, unknown>) : row;
}

function keeperPhotoUrl(photoUrl: string): string {
  if (!photoUrl) return '';
  if (photoUrl.startsWith('http://') || photoUrl.startsWith('https://') || photoUrl.includes('/keeper/api/')) {
    return photoUrl.replace('/keeper/api/', '/api/keeper/');
  }
  if (photoUrl.startsWith('/api/keeper/')) return photoUrl;
  if (photoUrl.startsWith('/')) return `/api/keeper${photoUrl}`;
  return `/api/keeper/${photoUrl}`;
}

function truthy(value: unknown): boolean {
  return value === true || value === 'true' || value === 1 || value === '1';
}

function emptyBundle(): ActivityBundle {
  return { meetings: [], openItems: [], closedItems: [], openCount: 0, closedCount: 0, points: 0, documents: [] };
}

async function resolvePerson(transport: GatewayTransport, requestedPersonId: string): Promise<PersonIdentity> {
  const claims = decodeJwt(transport.accessToken);
  const selfId = text(claims.mng_person_id);
  const elevated = claims.is_admin === true || claims.is_manager === true || claims.isAdmin === true || claims.isManager === true;
  const personId = elevated ? requestedPersonId || selfId : selfId || requestedPersonId;
  const self: PersonIdentity = {
    personId,
    username: text(claims.preferred_username),
    displayName: text(claims.name),
    keycloakId: text(claims.sub),
  };
  if (!personId || (selfId && personId === selfId && (self.username || self.displayName))) {
    return self;
  }

  try {
    const raw = await transport.request('/keeper/api/user/by-ids', {
      method: 'POST',
      body: { Ids: [personId] },
    });
    const users = asRows(raw, 'users');
    const user = users[0];
    if (!user) return { ...self, personId };
    const first = text(user.firstName ?? user.FirstName);
    const last = text(user.lastName ?? user.LastName);
    const displayName = `${first} ${last}`.trim();
    return {
      personId,
      username: text(user.username ?? user.Username) || self.username,
      displayName: displayName || self.displayName,
      keycloakId: text(user.keycloakUserId ?? user.KeycloakUserId) || self.keycloakId,
    };
  } catch {
    return { ...self, personId };
  }
}

async function loadMeetings(
  transport: GatewayTransport,
  personId: string,
  from: string,
  to: string,
): Promise<Record<string, unknown>[]> {
  if (!from || !to) return [];
  const attendance = await dgList(transport, 'mt_meeting_attendance', {
    filter: `userId:eq:${personId}`,
    limit: 1000,
    expand: false,
  });
  const presenceByMeeting = new Map<string, string>();
  for (const row of attendance) {
    const meetingId = relationId(row.meetingId);
    if (meetingId) presenceByMeeting.set(meetingId, text(row.presence));
  }
  const meetingIds = [...presenceByMeeting.keys()];
  if (!meetingIds.length) return [];

  const meetings: Record<string, unknown>[] = [];
  for (let index = 0; index < meetingIds.length; index += 40) {
    const chunk = meetingIds.slice(index, index + 40);
    const filter = `__dataId:in:[${chunk.map((id) => JSON.stringify(id)).join(',')}]`;
    meetings.push(...(await dgList(transport, 'mt_meetings', { filter, limit: chunk.length, expand: false })));
  }

  return meetings
    .filter((row) => text(row.status) !== 'cancelled')
    .filter((row) => inRange(text(row.startAt), from, to))
    .sort((a, b) => instant(text(b.startAt)) - instant(text(a.startAt)))
    .slice(0, ROW_CAP)
    .map((row) => {
      const id = text(row.__dataId ?? row.dataId);
      const presence = presenceByMeeting.get(id) ?? '';
      return {
        id,
        title: text(row.title) || 'Toplantı',
        subtitle: meetingStatusLabel(text(row.status)),
        when: text(row.startAt),
        badge: presenceLabel(presence),
        badgeColor: presenceColor(presence),
      };
    });
}

async function loadWork(
  transport: GatewayTransport,
  personId: string,
  from: string,
  to: string,
): Promise<{
  openItems: Record<string, unknown>[];
  closedItems: Record<string, unknown>[];
  openCount: number;
  closedCount: number;
  points: number;
}> {
  const rows = await dgList(transport, 'op_work_items', {
    filter: `assignee:eq:${personId}`,
    limit: 1000,
    expand: false,
  });
  const names = await loadWorkNames(transport);
  const open = rows.filter((row) => !text(row.closedAt));
  const openItems = open.slice(0, ROW_CAP).map((row) => workRow(row, names));
  const closed = rows
    .filter((row) => text(row.closedAt) && inRange(text(row.closedAt), from, to))
    .sort((a, b) => instant(text(b.closedAt)) - instant(text(a.closedAt)));
  const points = closed.reduce((sum, row) => sum + storyPoints(row), 0);
  const closedItems = closed.slice(0, ROW_CAP).map((row) => ({
    ...workRow(row, names),
    points: storyPoints(row),
    closedAt: text(row.closedAt),
  }));
  return { openItems, closedItems, openCount: open.length, closedCount: closed.length, points };
}

async function loadDocuments(
  transport: GatewayTransport,
  person: PersonIdentity,
  from: string,
  to: string,
): Promise<Record<string, unknown>[]> {
  if (!from || !to) return [];
  const rows = await dgList(transport, 'dm_resources', {
    limit: 400,
    expand: false,
    showHistory: true,
  });
  const keys = new Set(
    [person.personId, person.username, person.displayName, person.keycloakId].map(normalize).filter(Boolean),
  );
  if (!keys.size) return [];

  return rows
    .filter((row) => normalize(text(row.type)) !== 'folder')
    .map((row) => ({ row, created: createdStamp(row) }))
    .filter((item) => item.created && item.created.authors.some((author) => keys.has(author)) && inRange(item.created.at, from, to))
    .sort((a, b) => instant(b.created!.at) - instant(a.created!.at))
    .slice(0, ROW_CAP)
    .map((item) => {
      const kind = documentKind(text(item.row.type));
      return {
        id: text(item.row.__dataId ?? item.row.dataId),
        title: text(item.row.title ?? item.row.name) || 'Belge',
        subtitle: '',
        when: item.created!.at,
        badge: kind.label,
        badgeColor: kind.color,
      };
    });
}

function createdStamp(row: Record<string, unknown>): { at: string; authors: string[] } | null {
  const history = Array.isArray(row.__history) ? row.__history : [];
  const create = history.find((entry) => normalize(text((entry as Record<string, unknown>).operation)) === 'create') ?? history[0];
  const entry = create && typeof create === 'object' ? (create as Record<string, unknown>) : null;
  const at = text(entry?.timestamp ?? row.createdAt);
  const authors = [entry?.userId, entry?.userEmail, row.createdBy].map((value) => normalize(text(value))).filter(Boolean);
  if (!at || !authors.length) return null;
  return { at, authors };
}

function storyPoints(row: Record<string, unknown>): number {
  const extra = row.extraFields ?? row.fields;
  if (!extra || typeof extra !== 'object' || Array.isArray(extra)) return 0;
  const value = Number((extra as Record<string, unknown>).storyPoints);
  return Number.isFinite(value) ? value : 0;
}

function meetingStatusLabel(status: string): string {
  if (status === 'held') return 'Yapıldı';
  if (status === 'scheduled') return 'Planlandı';
  return status;
}

function presenceLabel(presence: string): string {
  if (presence === 'attended') return 'Katıldı';
  if (presence === 'confirmed') return 'Onayladı';
  if (presence === 'absent') return 'Katılmadı';
  if (presence === 'excused') return 'Mazur';
  if (presence === 'invited') return 'Davetli';
  return presence;
}

function presenceColor(presence: string): string {
  if (presence === 'attended') return 'success';
  if (presence === 'confirmed') return 'info';
  if (presence === 'absent') return 'error';
  if (presence === 'excused') return 'warning';
  if (presence === 'invited') return 'secondary';
  return 'primary';
}

function documentKind(type: string): { label: string; color: string } {
  const key = normalize(type);
  if (key.includes('image') || key === 'görsel' || key === 'gorsel') return { label: 'Görsel', color: 'info' };
  if (key.includes('pdf')) return { label: 'PDF', color: 'error' };
  return { label: 'Belge', color: 'primary' };
}

async function dgList(
  transport: GatewayTransport,
  dataset: string,
  query: Record<string, string | number | boolean | undefined>,
): Promise<Record<string, unknown>[]> {
  try {
    const raw = await transport.request(`/data/api/v1/data/${encodeURIComponent(dataset)}`, { query });
    return asRows(raw).filter((row): row is Record<string, unknown> => !!row && typeof row === 'object');
  } catch {
    return [];
  }
}

function asRows(raw: unknown, key = 'items'): Record<string, unknown>[] {
  if (Array.isArray(raw)) return raw.filter((row): row is Record<string, unknown> => !!row && typeof row === 'object');
  if (!raw || typeof raw !== 'object') return [];
  const obj = raw as Record<string, unknown>;
  const nested = obj[key] ?? obj.items ?? obj.data ?? obj.users ?? obj.Users;
  return Array.isArray(nested) ? nested.filter((row): row is Record<string, unknown> => !!row && typeof row === 'object') : [];
}

function relationId(value: unknown): string {
  if (typeof value === 'string') return value.trim();
  if (value && typeof value === 'object') {
    const row = value as Record<string, unknown>;
    return text(row.__dataId ?? row.dataId ?? row.id);
  }
  return '';
}

function text(value: unknown): string {
  if (typeof value === 'string') return value.trim();
  if (typeof value === 'number' && Number.isFinite(value)) return String(value);
  return '';
}

function normalize(value: string): string {
  return value.trim().toLocaleLowerCase('tr-TR');
}

function instant(value: string): number {
  if (!value) return 0;
  if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?$/.test(value)) {
    const parsed = Date.parse(`${value}+03:00`);
    return Number.isNaN(parsed) ? 0 : parsed;
  }
  const parsed = Date.parse(value);
  return Number.isNaN(parsed) ? 0 : parsed;
}

function inRange(value: string, from: string, to: string): boolean {
  const time = instant(value);
  const start = instant(from);
  const end = instant(to);
  if (!time || !start || !end) return false;
  return time >= start && time <= end;
}

function decodeJwt(token: string | undefined): Record<string, unknown> {
  if (!token) return {};
  try {
    const part = token.split('.')[1] ?? '';
    const padded = part.replace(/-/g, '+').replace(/_/g, '/');
    const json =
      typeof Buffer !== 'undefined'
        ? Buffer.from(padded, 'base64').toString('utf8')
        : decodeURIComponent(escape(atob(padded)));
    const parsed = JSON.parse(json) as unknown;
    return parsed && typeof parsed === 'object' ? (parsed as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}
