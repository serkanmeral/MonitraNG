import type { WidgetDataResponse } from '@/services/widgetDataService';

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
  points: number;
  documents: Record<string, unknown>[];
}

const bundleCache = new WeakMap<GatewayTransport, Map<string, Promise<ActivityBundle>>>();

export async function fetchPersonnelServiceRef(
  serviceRef: string,
  parameters: Record<string, unknown>,
  transport: GatewayTransport,
): Promise<WidgetDataResponse> {
  const kind = serviceRef.slice(PREFIX.length);
  const bundle = await loadBundle(transport, parameters);
  if (kind === 'story-points') {
    return { data: [{ value: bundle.points }], total: 1 };
  }
  const rows =
    kind === 'meetings'
      ? bundle.meetings
      : kind === 'work-open'
        ? bundle.openItems
        : kind === 'work-closed'
          ? bundle.closedItems
          : kind === 'documents'
            ? bundle.documents
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

  const [meetings, work, documents] = await Promise.all([
    loadMeetings(transport, person.personId, from, to),
    loadWork(transport, person.personId, from, to),
    loadDocuments(transport, person, from, to),
  ]);

  return {
    meetings,
    openItems: work.openItems,
    closedItems: work.closedItems,
    points: work.points,
    documents,
  };
}

function emptyBundle(): ActivityBundle {
  return { meetings: [], openItems: [], closedItems: [], points: 0, documents: [] };
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
      return {
        id,
        title: text(row.title) || 'Toplantı',
        when: formatWhen(text(row.startAt)),
        status: meetingStatusLabel(text(row.status)),
        presence: presenceLabel(presenceByMeeting.get(id) ?? ''),
      };
    });
}

async function loadWork(
  transport: GatewayTransport,
  personId: string,
  from: string,
  to: string,
): Promise<{ openItems: Record<string, unknown>[]; closedItems: Record<string, unknown>[]; points: number }> {
  const rows = await dgList(transport, 'op_work_items', {
    filter: `assignee:eq:${personId}`,
    limit: 1000,
    expand: false,
  });
  const openItems = rows
    .filter((row) => !text(row.closedAt))
    .slice(0, ROW_CAP)
    .map((row) => ({
      id: text(row.__dataId ?? row.dataId),
      key: text(row.key),
      title: text(row.title) || 'İş',
    }));
  const closed = rows
    .filter((row) => text(row.closedAt) && inRange(text(row.closedAt), from, to))
    .sort((a, b) => instant(text(b.closedAt)) - instant(text(a.closedAt)));
  const points = closed.reduce((sum, row) => sum + storyPoints(row), 0);
  const closedItems = closed.slice(0, ROW_CAP).map((row) => ({
    id: text(row.__dataId ?? row.dataId),
    key: text(row.key),
    title: text(row.title) || 'İş',
    points: storyPoints(row),
    closedAt: formatWhen(text(row.closedAt)),
  }));
  return { openItems, closedItems, points };
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
    .map((item) => ({
      id: text(item.row.__dataId ?? item.row.dataId),
      title: text(item.row.title ?? item.row.name) || 'Belge',
      kind: text(item.row.type) || 'file',
      when: formatWhen(item.created!.at),
    }));
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

function formatWhen(value: string): string {
  const time = instant(value);
  if (!time) return value;
  return new Intl.DateTimeFormat('tr-TR', {
    dateStyle: 'short',
    timeStyle: 'short',
    timeZone: 'Europe/Istanbul',
  }).format(new Date(time));
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
