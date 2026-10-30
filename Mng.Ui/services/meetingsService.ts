import {
  ocCreateRecordId,
  ocDelete,
  ocListDataset,
  ocUpdate,
} from '@/services/operationCoreService';

export const MT_MEETINGS = 'mt_meetings';
export const MT_MEETING_ACTIONS = 'mt_meeting_actions';
export const MT_MEETING_SERIES = 'mt_meeting_series';
export const MT_MEETING_PEOPLE = 'mt_meeting_people';
export const MT_MEETING_ATTENDANCE = 'mt_meeting_attendance';
export const MAX_SERIES_OCCURRENCES = 26;
const OPEN_SERIES_DAYS = 84;

export type OrgMeetingStatus = 'scheduled' | 'held' | 'cancelled';
export type OrgMeetingActionStatus = 'open' | 'done';
export type OrgAttendanceKind = 'user' | 'external';
export type OrgAttendancePresence = 'invited' | 'confirmed' | 'attended' | 'absent' | 'excused';

export interface OrgMeeting {
  id: string;
  title: string;
  startAt: string;
  endAt: string | null;
  status: OrgMeetingStatus;
  location: string;
  meetingUrl: string;
  organizerId: string | null;
  attendeeIds: string[];
  agenda: string;
  agendaResourceId: string | null;
  minutesResourceId: string | null;
  seriesId: string | null;
  occurrenceDate: string | null;
  detached: boolean;
}

export interface OrgMeetingAction {
  id: string;
  meetingId: string;
  title: string;
  assigneeId: string | null;
  dueAt: string | null;
  status: OrgMeetingActionStatus;
  note: string;
}

export interface OrgMeetingPerson {
  id: string;
  name: string;
  organization: string;
  email: string;
  note: string;
}

export interface OrgMeetingAttendance {
  id: string;
  meetingId: string;
  kind: OrgAttendanceKind;
  userId: string | null;
  personId: string | null;
  displayName: string;
  organization: string;
  email: string;
  presence: OrgAttendancePresence;
}

export interface OrgMeetingSeries {
  id: string;
  name: string;
  weekday: number;
  weekdays: number[];
  startTime: string;
  durationMinutes: number;
  anchorStart: string;
  until: string;
  location: string;
  meetingUrl: string;
  organizerId: string | null;
  agenda: string;
  agendaResourceId: string | null;
  memberUserIds: string[];
  memberPersonIds: string[];
}

export interface OrgMeetingInput {
  title: string;
  startAt: string;
  endAt?: string | null;
  status: OrgMeetingStatus;
  location?: string | null;
  meetingUrl?: string | null;
  organizerId?: string | null;
  agenda?: string | null;
  agendaResourceId?: string | null;
  minutesResourceId?: string | null;
  seriesId?: string | null;
  occurrenceDate?: string | null;
  detached?: boolean;
}

export interface OrgMeetingActionInput {
  meetingId: string;
  title: string;
  assigneeId?: string | null;
  dueAt?: string | null;
  status: OrgMeetingActionStatus;
  note?: string | null;
}

export interface OrgMeetingPersonInput {
  name: string;
  organization?: string | null;
  email?: string | null;
  note?: string | null;
}

export interface OrgMeetingAttendanceInput {
  meetingId: string;
  kind: OrgAttendanceKind;
  userId?: string | null;
  personId?: string | null;
  displayName: string;
  organization?: string | null;
  email?: string | null;
  presence: OrgAttendancePresence;
}

export interface OrgAttendanceDraft extends OrgMeetingAttendanceInput {
  id?: string;
}

export interface OrgMeetingSeriesInput {
  name: string;
  weekday: number;
  weekdays?: number[];
  startTime: string;
  durationMinutes: number;
  firstDate: string;
  until?: string | null;
  location?: string | null;
  meetingUrl?: string | null;
  organizerId?: string | null;
  agenda?: string | null;
  agendaResourceId?: string | null;
  memberUserIds?: string[];
  memberPersonIds?: string[];
}

const ISTANBUL_OFFSET_MS = 3 * 60 * 60 * 1000;

function asRecord(raw: unknown): Record<string, unknown> {
  return raw && typeof raw === 'object' ? (raw as Record<string, unknown>) : {};
}

function text(raw: unknown): string {
  if (raw == null) return '';
  return String(raw).trim();
}

function optionalText(raw: unknown): string | null {
  const value = text(raw);
  return value || null;
}

function idList(raw: unknown): string[] {
  if (Array.isArray(raw)) return raw.map((item) => text(item)).filter(Boolean);
  const single = text(raw);
  return single ? [single] : [];
}

function asInt(raw: unknown, fallback: number): number {
  const value = Number(raw);
  return Number.isFinite(value) ? value : fallback;
}

function asBool(raw: unknown): boolean {
  return raw === true || raw === 1 || raw === '1' || raw === 'true';
}

function meetingStatus(raw: unknown): OrgMeetingStatus {
  const value = text(raw);
  if (value === 'held' || value === 'cancelled' || value === 'scheduled') return value;
  return 'scheduled';
}

function actionStatus(raw: unknown): OrgMeetingActionStatus {
  return text(raw) === 'done' ? 'done' : 'open';
}

function attendanceKind(raw: unknown): OrgAttendanceKind {
  return text(raw) === 'external' ? 'external' : 'user';
}

function attendancePresence(raw: unknown): OrgAttendancePresence {
  const value = text(raw);
  if (value === 'confirmed' || value === 'attended' || value === 'absent' || value === 'excused' || value === 'invited') {
    return value;
  }
  return 'invited';
}

function clampWeekday(value: number): number {
  return value >= 1 && value <= 7 ? value : 1;
}

function clampDuration(value: number): number {
  if (!Number.isFinite(value) || value < 15) return 60;
  if (value > 480) return 480;
  return Math.round(value);
}

function normalizeStartTime(value: string | null | undefined): string {
  const match = text(value).match(/^(\d{1,2}):(\d{2})/);
  if (!match) return '09:00';
  const hours = Math.min(23, Math.max(0, Number(match[1])));
  const minutes = Math.min(59, Math.max(0, Number(match[2])));
  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`;
}

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

function ymd(year: number, month: number, day: number): string {
  return `${year}-${pad(month)}-${pad(day)}`;
}

function weekdayList(raw: unknown, fallback: number): number[] {
  const values = idList(raw).map((item) => clampWeekday(Number(item))).filter((item) => item >= 1 && item <= 7);
  const unique = [...new Set(values)];
  return unique.length ? unique : [clampWeekday(fallback)];
}

function seriesDays(series: OrgMeetingSeries): number[] {
  return series.weekdays.length ? series.weekdays : [series.weekday];
}

function openHorizon(through?: string | null): string {
  const ahead = addDaysYmd(istanbulYmd(new Date()), OPEN_SERIES_DAYS);
  const extra = text(through);
  return extra && extra > ahead ? extra : ahead;
}

function istanbulParts(instant: Date): { year: number; month: number; day: number; weekday: number } {
  const shifted = new Date(instant.getTime() + ISTANBUL_OFFSET_MS);
  const sundayBased = shifted.getUTCDay();
  return {
    year: shifted.getUTCFullYear(),
    month: shifted.getUTCMonth() + 1,
    day: shifted.getUTCDate(),
    weekday: sundayBased === 0 ? 7 : sundayBased,
  };
}

export function istanbulYmd(value: string | Date): string {
  const instant = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(instant.getTime())) return '';
  const parts = istanbulParts(instant);
  return ymd(parts.year, parts.month, parts.day);
}

function addDaysYmd(value: string, days: number): string {
  const [year, month, day] = value.split('-').map(Number);
  const next = new Date(Date.UTC(year, month - 1, day) + days * 86400000);
  return ymd(next.getUTCFullYear(), next.getUTCMonth() + 1, next.getUTCDate());
}

function istanbulInstant(year: number, month: number, day: number, hours: number, minutes: number): Date {
  return new Date(Date.UTC(year, month - 1, day, hours - 3, minutes, 0, 0));
}

function clockOnDate(firstDate: string, startTime: string): Date {
  const [year, month, day] = firstDate.split('-').map(Number);
  const [hours, minutes] = normalizeStartTime(startTime).split(':').map(Number);
  return istanbulInstant(year, month, day, hours, minutes);
}

function seriesAnchor(firstDate: string, series: OrgMeetingSeries): Date {
  if (series.weekdays.length > 1) return clockOnDate(firstDate, series.startTime);
  return resolveSeriesAnchor(firstDate, series.weekday, series.startTime);
}

export function resolveSeriesAnchor(firstDate: string, weekday: number, startTime: string): Date {
  const [year, month, day] = firstDate.split('-').map(Number);
  const [hours, minutes] = normalizeStartTime(startTime).split(':').map(Number);
  let cursor = istanbulInstant(year, month, day, hours, minutes);
  const target = clampWeekday(weekday);
  for (let step = 0; step < 7; step += 1) {
    if (istanbulParts(cursor).weekday === target) return cursor;
    cursor = new Date(cursor.getTime() + 86400000);
  }
  return cursor;
}

function mapMeeting(raw: unknown): OrgMeeting | null {
  const row = asRecord(raw);
  const id = text(row.__dataId ?? row.dataId);
  const title = text(row.title);
  if (!id || !title) return null;
  return {
    id,
    title,
    startAt: text(row.startAt),
    endAt: optionalText(row.endAt),
    status: meetingStatus(row.status),
    location: text(row.location),
    meetingUrl: text(row.meetingUrl),
    organizerId: optionalText(row.organizerId),
    attendeeIds: idList(row.attendeeIds),
    agenda: text(row.agenda),
    agendaResourceId: optionalText(row.agendaResourceId),
    minutesResourceId: optionalText(row.minutesResourceId),
    seriesId: optionalText(row.seriesId),
    occurrenceDate: optionalText(row.occurrenceDate),
    detached: asBool(row.detached),
  };
}

function mapAction(raw: unknown): OrgMeetingAction | null {
  const row = asRecord(raw);
  const id = text(row.__dataId ?? row.dataId);
  const meetingId = text(row.meetingId);
  const title = text(row.title);
  if (!id || !meetingId || !title) return null;
  return {
    id,
    meetingId,
    title,
    assigneeId: optionalText(row.assigneeId),
    dueAt: optionalText(row.dueAt),
    status: actionStatus(row.status),
    note: text(row.note),
  };
}

function mapPerson(raw: unknown): OrgMeetingPerson | null {
  const row = asRecord(raw);
  const id = text(row.__dataId ?? row.dataId);
  const name = text(row.name);
  if (!id || !name) return null;
  return {
    id,
    name,
    organization: text(row.organization),
    email: text(row.email),
    note: text(row.note),
  };
}

function mapAttendance(raw: unknown): OrgMeetingAttendance | null {
  const row = asRecord(raw);
  const id = text(row.__dataId ?? row.dataId);
  const meetingId = text(row.meetingId);
  const displayName = text(row.displayName);
  if (!id || !meetingId || !displayName) return null;
  return {
    id,
    meetingId,
    kind: attendanceKind(row.kind),
    userId: optionalText(row.userId),
    personId: optionalText(row.personId),
    displayName,
    organization: text(row.organization),
    email: text(row.email),
    presence: attendancePresence(row.presence),
  };
}

function mapSeries(raw: unknown): OrgMeetingSeries | null {
  const row = asRecord(raw);
  const id = text(row.__dataId ?? row.dataId);
  const name = text(row.name);
  if (!id || !name) return null;
  return {
    id,
    name,
    weekday: clampWeekday(asInt(row.weekday, 1)),
    weekdays: weekdayList(row.weekdays, asInt(row.weekday, 1)),
    startTime: normalizeStartTime(text(row.startTime)),
    durationMinutes: clampDuration(asInt(row.durationMinutes, 60)),
    anchorStart: text(row.anchorStart),
    until: text(row.until),
    location: text(row.location),
    meetingUrl: text(row.meetingUrl),
    organizerId: optionalText(row.organizerId),
    agenda: text(row.agenda),
    agendaResourceId: optionalText(row.agendaResourceId),
    memberUserIds: idList(row.memberUserIds),
    memberPersonIds: idList(row.memberPersonIds),
  };
}

function meetingPayload(input: OrgMeetingInput): Record<string, unknown> {
  return {
    title: input.title.trim(),
    startAt: input.startAt,
    endAt: input.endAt || null,
    status: input.status,
    location: input.location?.trim() || null,
    meetingUrl: input.meetingUrl?.trim() || null,
    organizerId: input.organizerId || null,
    agenda: input.agenda?.trim() || null,
    agendaResourceId: input.agendaResourceId || null,
    minutesResourceId: input.minutesResourceId || null,
    seriesId: input.seriesId || null,
    occurrenceDate: input.occurrenceDate || null,
    detached: Boolean(input.detached),
  };
}

function actionPayload(input: OrgMeetingActionInput): Record<string, unknown> {
  return {
    meetingId: input.meetingId,
    title: input.title.trim(),
    assigneeId: input.assigneeId || null,
    dueAt: input.dueAt || null,
    status: input.status,
    note: input.note?.trim() || null,
  };
}

function personPayload(input: OrgMeetingPersonInput): Record<string, unknown> {
  return {
    name: input.name.trim(),
    organization: input.organization?.trim() || null,
    email: input.email?.trim() || null,
    note: input.note?.trim() || null,
  };
}

function attendancePayload(input: OrgMeetingAttendanceInput): Record<string, unknown> {
  return {
    meetingId: input.meetingId,
    kind: input.kind,
    userId: input.userId || null,
    personId: input.personId || null,
    displayName: input.displayName.trim(),
    organization: input.organization?.trim() || null,
    email: input.email?.trim() || null,
    presence: input.presence,
  };
}

function seriesPayload(series: OrgMeetingSeries, input: OrgMeetingSeriesInput, anchor: Date): Record<string, unknown> {
  return {
    name: input.name.trim(),
    weekday: series.weekday,
    weekdays: series.weekdays,
    startTime: series.startTime,
    durationMinutes: series.durationMinutes,
    anchorStart: anchor.toISOString(),
    until: text(input.until) || null,
    location: input.location?.trim() || null,
    meetingUrl: input.meetingUrl?.trim() || null,
    organizerId: input.organizerId || null,
    agenda: input.agenda?.trim() || null,
    agendaResourceId: input.agendaResourceId || null,
    memberUserIds: input.memberUserIds ?? [],
    memberPersonIds: input.memberPersonIds ?? [],
  };
}

function normalizeSeriesInput(input: OrgMeetingSeriesInput): OrgMeetingSeries {
  return {
    id: '',
    name: input.name.trim(),
    weekday: clampWeekday(input.weekday),
    weekdays: weekdayList(input.weekdays, input.weekday),
    startTime: normalizeStartTime(input.startTime),
    durationMinutes: clampDuration(input.durationMinutes),
    anchorStart: '',
    until: text(input.until),
    location: text(input.location),
    meetingUrl: text(input.meetingUrl),
    organizerId: input.organizerId || null,
    agenda: text(input.agenda),
    agendaResourceId: input.agendaResourceId || null,
    memberUserIds: input.memberUserIds ?? [],
    memberPersonIds: input.memberPersonIds ?? [],
  };
}

export async function listOrgMeetings(): Promise<OrgMeeting[]> {
  const rows = await ocListDataset(MT_MEETINGS, {
    sort: 'startAt:desc',
    limit: 1000,
    expand: false,
  });
  return rows.map(mapMeeting).filter((row): row is OrgMeeting => row != null);
}

export async function createOrgMeeting(input: OrgMeetingInput): Promise<string> {
  const id = await ocCreateRecordId(MT_MEETINGS, meetingPayload(input));
  if (!id) throw new Error('Meeting create did not return an id.');
  return id;
}

export async function updateOrgMeeting(id: string, input: OrgMeetingInput): Promise<void> {
  await ocUpdate(MT_MEETINGS, id, meetingPayload(input));
}

export async function deleteOrgMeeting(id: string): Promise<void> {
  const [actions, attendance] = await Promise.all([
    listOrgMeetingActions(id),
    listOrgMeetingAttendance(id),
  ]);
  for (const action of actions) await ocDelete(MT_MEETING_ACTIONS, action.id);
  for (const row of attendance) await ocDelete(MT_MEETING_ATTENDANCE, row.id);
  await ocDelete(MT_MEETINGS, id);
}

export async function listOrgMeetingActions(meetingId: string): Promise<OrgMeetingAction[]> {
  const rows = await ocListDataset(MT_MEETING_ACTIONS, {
    filter: `meetingId:eq:${meetingId}`,
    sort: 'dueAt:asc,title:asc',
    limit: 200,
    expand: false,
  });
  return rows.map(mapAction).filter((row): row is OrgMeetingAction => row != null);
}

export async function createOrgMeetingAction(input: OrgMeetingActionInput): Promise<string> {
  const id = await ocCreateRecordId(MT_MEETING_ACTIONS, actionPayload(input));
  if (!id) throw new Error('Meeting action create did not return an id.');
  return id;
}

export async function updateOrgMeetingAction(id: string, input: OrgMeetingActionInput): Promise<void> {
  await ocUpdate(MT_MEETING_ACTIONS, id, actionPayload(input));
}

export async function deleteOrgMeetingAction(id: string): Promise<void> {
  await ocDelete(MT_MEETING_ACTIONS, id);
}

export async function listOrgMeetingPeople(): Promise<OrgMeetingPerson[]> {
  const rows = await ocListDataset(MT_MEETING_PEOPLE, { sort: 'name:asc', limit: 500, expand: false });
  return rows.map(mapPerson).filter((row): row is OrgMeetingPerson => row != null);
}

export async function createOrgMeetingPerson(input: OrgMeetingPersonInput): Promise<OrgMeetingPerson> {
  const id = await ocCreateRecordId(MT_MEETING_PEOPLE, personPayload(input));
  if (!id) throw new Error('Meeting person create did not return an id.');
  return {
    id,
    name: input.name.trim(),
    organization: text(input.organization),
    email: text(input.email),
    note: text(input.note),
  };
}

export async function deleteOrgMeetingPerson(id: string): Promise<void> {
  const [attendance, series] = await Promise.all([
    ocListDataset(MT_MEETING_ATTENDANCE, { filter: `personId:eq:${id}`, limit: 1, expand: false }),
    listOrgMeetingSeries(),
  ]);
  if (attendance.length || series.some((row) => row.memberPersonIds.includes(id))) {
    throw new Error('PERSON_IN_USE');
  }
  await ocDelete(MT_MEETING_PEOPLE, id);
}

export async function listOrgMeetingAttendance(meetingId: string): Promise<OrgMeetingAttendance[]> {
  const rows = await ocListDataset(MT_MEETING_ATTENDANCE, {
    filter: `meetingId:eq:${meetingId}`,
    limit: 200,
    expand: false,
  });
  return rows.map(mapAttendance).filter((row): row is OrgMeetingAttendance => row != null);
}

export async function createOrgMeetingAttendance(input: OrgMeetingAttendanceInput): Promise<string> {
  const id = await ocCreateRecordId(MT_MEETING_ATTENDANCE, attendancePayload(input));
  if (!id) throw new Error('Attendance create did not return an id.');
  return id;
}

export async function updateOrgMeetingAttendance(id: string, input: OrgMeetingAttendanceInput): Promise<void> {
  await ocUpdate(MT_MEETING_ATTENDANCE, id, attendancePayload(input));
}

export async function deleteOrgMeetingAttendance(id: string): Promise<void> {
  await ocDelete(MT_MEETING_ATTENDANCE, id);
}

export async function syncOrgMeetingAttendance(
  meetingId: string,
  drafts: OrgAttendanceDraft[],
  existing: OrgMeetingAttendance[],
): Promise<void> {
  const keep = new Set(drafts.map((row) => text(row.id)).filter(Boolean));
  for (const row of existing) {
    if (!keep.has(row.id)) await deleteOrgMeetingAttendance(row.id);
  }
  for (const draft of drafts) {
    const payload = { ...draft, meetingId };
    if (draft.id) await updateOrgMeetingAttendance(draft.id, payload);
    else await createOrgMeetingAttendance(payload);
  }
}

export async function listOrgMeetingSeries(): Promise<OrgMeetingSeries[]> {
  const rows = await ocListDataset(MT_MEETING_SERIES, { sort: 'name:asc', limit: 200, expand: false });
  return rows.map(mapSeries).filter((row): row is OrgMeetingSeries => row != null);
}

async function copySeriesAttendance(meetingId: string, series: OrgMeetingSeries, people: OrgMeetingPerson[]): Promise<void> {
  const byId = new Map(people.map((person) => [person.id, person]));
  for (const userId of series.memberUserIds) {
    await createOrgMeetingAttendance({
      meetingId,
      kind: 'user',
      userId,
      displayName: userId,
      presence: 'invited',
    });
  }
  for (const personId of series.memberPersonIds) {
    const person = byId.get(personId);
    await createOrgMeetingAttendance({
      meetingId,
      kind: 'external',
      personId,
      displayName: person?.name || personId,
      organization: person?.organization || null,
      email: person?.email || null,
      presence: 'invited',
    });
  }
}

function occurrenceClock(date: string, startTime: string, durationMinutes: number): { startAt: string; endAt: string } {
  const [year, month, day] = date.split('-').map(Number);
  const [hours, minutes] = normalizeStartTime(startTime).split(':').map(Number);
  const start = istanbulInstant(year, month, day, hours, minutes);
  const end = new Date(start.getTime() + durationMinutes * 60000);
  return { startAt: start.toISOString(), endAt: end.toISOString() };
}

async function applySeriesForward(series: OrgMeetingSeries, fromDate: string): Promise<void> {
  const [meetings, people] = await Promise.all([listOrgMeetings(), listOrgMeetingPeople()]);
  const rows = meetings.filter(
    (row) => row.seriesId === series.id && row.occurrenceDate && row.occurrenceDate >= fromDate && !row.detached && row.status === 'scheduled' && !row.minutesResourceId,
  );
  for (const meeting of rows) {
    const actions = await listOrgMeetingActions(meeting.id);
    if (actions.length) continue;
    const attendance = await listOrgMeetingAttendance(meeting.id);
    if (attendance.some((row) => row.presence !== 'invited')) continue;
    const clock = occurrenceClock(meeting.occurrenceDate as string, series.startTime, series.durationMinutes);
    await ocUpdate(MT_MEETINGS, meeting.id, {
      title: series.name,
      startAt: clock.startAt,
      endAt: clock.endAt,
      location: series.location || null,
      meetingUrl: series.meetingUrl || null,
      organizerId: series.organizerId,
      agenda: series.agenda || null,
      agendaResourceId: series.agendaResourceId,
    });
    for (const row of attendance) await deleteOrgMeetingAttendance(row.id);
    await copySeriesAttendance(meeting.id, series, people);
  }
}

async function generateSeriesOccurrences(
  series: OrgMeetingSeries,
  anchor: Date,
  through?: string | null,
  notBefore?: string | null,
): Promise<void> {
  const [meetings, people] = await Promise.all([listOrgMeetings(), listOrgMeetingPeople()]);
  const existing = new Set(
    meetings
      .filter((row) => row.seriesId === series.id && row.occurrenceDate)
      .map((row) => row.occurrenceDate as string),
  );
  const days = new Set(seriesDays(series));
  const limit = series.until || openHorizon(through);
  let cursor = anchor;
  let steps = 0;
  while (steps < 500) {
    const parts = istanbulParts(cursor);
    const key = ymd(parts.year, parts.month, parts.day);
    if (key > limit) break;
    if (notBefore && key < notBefore) {
      steps += 1;
      cursor = new Date(cursor.getTime() + 86400000);
      continue;
    }
    if (days.has(parts.weekday) && !existing.has(key)) {
      const end = new Date(cursor.getTime() + series.durationMinutes * 60000);
      const meetingId = await createOrgMeeting({
        title: series.name,
        startAt: cursor.toISOString(),
        endAt: end.toISOString(),
        status: 'scheduled',
        location: series.location,
        meetingUrl: series.meetingUrl,
        organizerId: series.organizerId,
        agenda: series.agenda,
        agendaResourceId: series.agendaResourceId,
        seriesId: series.id,
        occurrenceDate: key,
        detached: false,
      });
      await copySeriesAttendance(meetingId, series, people);
      existing.add(key);
    }
    steps += 1;
    cursor = new Date(cursor.getTime() + 86400000);
  }
}

async function cancelSeriesTail(series: OrgMeetingSeries, fromDate: string): Promise<void> {
  if (!series.until) return;
  const meetings = (await listOrgMeetings()).filter((row) => row.seriesId === series.id);
  for (const meeting of meetings) {
    if (meeting.detached || meeting.status !== 'scheduled' || meeting.minutesResourceId) continue;
    if (!meeting.occurrenceDate || meeting.occurrenceDate < fromDate || meeting.occurrenceDate <= series.until) continue;
    const actions = await listOrgMeetingActions(meeting.id);
    if (actions.length) continue;
    const attendance = await listOrgMeetingAttendance(meeting.id);
    if (attendance.some((row) => row.presence !== 'invited')) continue;
    await ocUpdate(MT_MEETINGS, meeting.id, { status: 'cancelled' });
  }
}

export async function createOrgMeetingSeries(input: OrgMeetingSeriesInput): Promise<string> {
  const series = normalizeSeriesInput(input);
  const anchor = seriesAnchor(input.firstDate, series);
  const id = await ocCreateRecordId(MT_MEETING_SERIES, seriesPayload({ ...series, id: '' }, input, anchor));
  if (!id) throw new Error('Meeting series create did not return an id.');
  const stored = { ...series, id, anchorStart: anchor.toISOString() };
  await generateSeriesOccurrences(stored, anchor, null, input.firstDate);
  return id;
}

export async function updateOrgMeetingSeries(
  id: string,
  input: OrgMeetingSeriesInput,
  existing: OrgMeetingSeries,
  fromDate: string,
): Promise<void> {
  const series = normalizeSeriesInput(input);
  const firstDate = istanbulYmd(existing.anchorStart) || input.firstDate;
  const anchor = seriesAnchor(firstDate, series);
  const next = { ...series, id, anchorStart: anchor.toISOString() };
  const start = text(fromDate) || istanbulYmd(new Date());
  await ocUpdate(MT_MEETING_SERIES, id, seriesPayload(next, input, anchor));
  await generateSeriesOccurrences(next, anchor, null, start);
  await applySeriesForward(next, start);
  await cancelSeriesTail(next, start);
}

async function releaseSeriesMeeting(meetingId: string): Promise<void> {
  await ocUpdate(MT_MEETINGS, meetingId, { seriesId: null, detached: true });
}

export async function deleteOrgMeetingSeries(id: string): Promise<void> {
  const meetings = (await listOrgMeetings()).filter((row) => row.seriesId === id);
  const today = istanbulYmd(new Date());
  for (const meeting of meetings) {
    const future = meeting.occurrenceDate
      ? meeting.occurrenceDate >= today
      : Date.parse(meeting.startAt) >= Date.now();
    const actions = future && meeting.status !== 'held' && !meeting.minutesResourceId
      ? await listOrgMeetingActions(meeting.id)
      : [];
    const removable = future && meeting.status !== 'held' && !meeting.minutesResourceId && actions.length === 0;
    if (removable) await deleteOrgMeeting(meeting.id);
    else await releaseSeriesMeeting(meeting.id);
  }
  await ocDelete(MT_MEETING_SERIES, id);
}

export async function endOrgMeetingSeries(id: string): Promise<void> {
  const meetings = (await listOrgMeetings()).filter((row) => row.seriesId === id);
  const now = Date.now();
  for (const meeting of meetings) {
    if (meeting.status !== 'scheduled' || meeting.detached || meeting.minutesResourceId) continue;
    const start = Date.parse(meeting.startAt);
    if (!Number.isNaN(start) && start < now) continue;
    const actions = await listOrgMeetingActions(meeting.id);
    if (actions.length) continue;
    const attendance = await listOrgMeetingAttendance(meeting.id);
    if (attendance.some((row) => row.presence !== 'invited')) continue;
    await ocUpdate(MT_MEETINGS, meeting.id, { status: 'cancelled' });
  }
  const today = istanbulYmd(new Date());
  await ocUpdate(MT_MEETING_SERIES, id, { until: addDaysYmd(today, -1) });
}

export async function extendOpenSeries(through: string): Promise<void> {
  const limit = text(through);
  if (!limit) return;
  const seriesRows = (await listOrgMeetingSeries()).filter((row) => !row.until);
  for (const series of seriesRows) {
    const anchor = series.anchorStart ? new Date(series.anchorStart) : new Date();
    if (Number.isNaN(anchor.getTime())) continue;
    await generateSeriesOccurrences(series, anchor, limit, istanbulYmd(new Date()));
  }
}
