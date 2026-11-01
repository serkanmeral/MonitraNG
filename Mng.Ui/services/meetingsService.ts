import { fetchFromDataGateway } from '@/services/apiService';
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
export const MT_MEETING_TOPICS = 'mt_meeting_topics';
export const MT_MEETING_ROOMS = 'mt_meeting_rooms';
export const MT_MEETING_ATTACHMENTS = 'mt_meeting_attachments';
export const MT_MEETING_NOTICE_SETTINGS = 'mt_meeting_notice_settings';
export const MT_MEETING_NOTICE_LOGS = 'mt_meeting_notice_logs';
export const MEETING_NOTICE_KINDS = ['invite', 'update', 'cancel'] as const;
export type MeetingNoticeKind = (typeof MEETING_NOTICE_KINDS)[number];
export type MeetingNoticeResult = 'sent' | 'skipped' | 'failed';

export const MEETING_TOPIC_COLORS = ['blue', 'teal', 'green', 'orange', 'purple', 'red', 'brown', 'slate'] as const;
export type MeetingTopicColor = (typeof MEETING_TOPIC_COLORS)[number];

const TOPIC_COLOR_HEX: Record<MeetingTopicColor, string> = {
  blue: '#1565c0',
  teal: '#00897b',
  green: '#2e7d32',
  orange: '#ef6c00',
  purple: '#6a1b9a',
  red: '#c62828',
  brown: '#6d4c41',
  slate: '#546e7a',
};

export function meetingTopicColorHex(color: string | null | undefined): string {
  if (color && color in TOPIC_COLOR_HEX) return TOPIC_COLOR_HEX[color as MeetingTopicColor];
  return TOPIC_COLOR_HEX.blue;
}
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
  topicId: string | null;
  topicCustom: boolean;
  roomId: string | null;
  roomCustom: boolean;
}

export interface OrgMeetingTopic {
  id: string;
  name: string;
  code: string;
  color: MeetingTopicColor;
  archived: boolean;
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
  note: string;
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
  topicId: string | null;
  roomId: string | null;
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
  topicId?: string | null;
  topicCustom?: boolean;
  roomId?: string | null;
  roomCustom?: boolean;
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
  note?: string | null;
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
  topicId?: string | null;
  roomId?: string | null;
}

export interface OrgMeetingRoom {
  id: string;
  name: string;
  capacity: number | null;
  archived: boolean;
}

export interface OrgMeetingRoomInput {
  name: string;
  capacity?: number | null;
  archived?: boolean;
}

export interface OrgMeetingTopicInput {
  name: string;
  code?: string | null;
  color?: string | null;
  archived?: boolean;
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

function topicColor(raw: unknown): MeetingTopicColor {
  const value = text(raw);
  if ((MEETING_TOPIC_COLORS as readonly string[]).includes(value)) return value as MeetingTopicColor;
  return 'blue';
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
    topicId: optionalText(row.topicId),
    topicCustom: asBool(row.topicCustom),
    roomId: optionalText(row.roomId),
    roomCustom: asBool(row.roomCustom),
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
    note: text(row.note),
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
    topicId: optionalText(row.topicId),
    roomId: optionalText(row.roomId),
  };
}

function mapTopic(raw: unknown): OrgMeetingTopic | null {
  const row = asRecord(raw);
  const id = text(row.__dataId ?? row.dataId);
  const name = text(row.name);
  if (!id || !name) return null;
  return {
    id,
    name,
    code: text(row.code),
    color: topicColor(row.color),
    archived: asBool(row.archived),
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
    topicId: input.topicId || null,
    topicCustom: Boolean(input.topicCustom),
    roomId: input.roomId || null,
    roomCustom: Boolean(input.roomCustom),
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
    note: input.note?.trim() || null,
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
    topicId: input.topicId || null,
    roomId: input.roomId || null,
  };
}

function topicPayload(input: OrgMeetingTopicInput): Record<string, unknown> {
  return {
    name: input.name.trim(),
    code: input.code?.trim() || null,
    color: topicColor(input.color),
    archived: Boolean(input.archived),
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
    topicId: input.topicId || null,
    roomId: input.roomId || null,
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
  const [actions, attendance, attachments, notices] = await Promise.all([
    listOrgMeetingActions(id),
    listOrgMeetingAttendance(id),
    listOrgMeetingAttachments(id),
    listOrgMeetingNoticeLogs(id).catch(() => [] as OrgMeetingNoticeLog[]),
  ]);
  for (const action of actions) await ocDelete(MT_MEETING_ACTIONS, action.id);
  for (const row of attendance) await ocDelete(MT_MEETING_ATTENDANCE, row.id);
  for (const row of attachments) await ocDelete(MT_MEETING_ATTACHMENTS, row.id);
  for (const row of notices) await ocDelete(MT_MEETING_NOTICE_LOGS, row.id);
  await ocDelete(MT_MEETINGS, id);
}

export async function listOrgMeetingActionRows(): Promise<OrgMeetingAction[]> {
  const rows = await ocListDataset(MT_MEETING_ACTIONS, { limit: 2000, expand: false });
  return rows.map(mapAction).filter((row): row is OrgMeetingAction => row != null);
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

export async function updateOrgMeetingPerson(id: string, input: OrgMeetingPersonInput): Promise<void> {
  await ocUpdate(MT_MEETING_PEOPLE, id, personPayload(input));
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

export async function listOrgMeetingAttendanceRows(): Promise<OrgMeetingAttendance[]> {
  const rows = await ocListDataset(MT_MEETING_ATTENDANCE, { limit: 2000, expand: false });
  return rows.map(mapAttendance).filter((row): row is OrgMeetingAttendance => row != null);
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

const SERIES_WRITE_BATCH = 5;
const BULK_ROW_LIMIT = 2000;

async function runInBatches<T>(items: T[], work: (item: T) => Promise<void>): Promise<void> {
  for (let index = 0; index < items.length; index += SERIES_WRITE_BATCH) {
    await Promise.all(items.slice(index, index + SERIES_WRITE_BATCH).map((item) => work(item)));
  }
}

function groupByMeeting<T extends { meetingId: string }>(rows: T[]): Map<string, T[]> {
  const grouped = new Map<string, T[]>();
  for (const row of rows) {
    const list = grouped.get(row.meetingId);
    if (list) list.push(row);
    else grouped.set(row.meetingId, [row]);
  }
  return grouped;
}

async function bulkMeetingActions(): Promise<Map<string, OrgMeetingAction[]> | null> {
  const rows = await listOrgMeetingActionRows();
  if (rows.length >= BULK_ROW_LIMIT) return null;
  return groupByMeeting(rows);
}

async function bulkMeetingAttendance(): Promise<Map<string, OrgMeetingAttendance[]> | null> {
  const rows = await listOrgMeetingAttendanceRows();
  if (rows.length >= BULK_ROW_LIMIT) return null;
  return groupByMeeting(rows);
}

async function actionsFor(index: Map<string, OrgMeetingAction[]> | null, meetingId: string): Promise<OrgMeetingAction[]> {
  if (index) return index.get(meetingId) || [];
  return listOrgMeetingActions(meetingId);
}

async function attendanceFor(index: Map<string, OrgMeetingAttendance[]> | null, meetingId: string): Promise<OrgMeetingAttendance[]> {
  if (index) return index.get(meetingId) || [];
  return listOrgMeetingAttendance(meetingId);
}

function sameIdSet(left: Array<string | null | undefined>, right: string[]): boolean {
  const a = left.filter((id): id is string => Boolean(id)).slice().sort();
  const b = right.slice().sort();
  return a.length === b.length && a.every((id, index) => id === b[index]);
}

function sameInstant(left: string | null | undefined, right: string | null | undefined): boolean {
  const a = text(left);
  const b = text(right);
  if (!a && !b) return true;
  const leftMs = Date.parse(a);
  const rightMs = Date.parse(b);
  if (Number.isNaN(leftMs) || Number.isNaN(rightMs)) return a === b;
  return leftMs === rightMs;
}

function seriesFieldsMatch(
  meeting: OrgMeeting,
  series: OrgMeetingSeries,
  clock: { startAt: string; endAt: string },
): boolean {
  if (meeting.title !== series.name) return false;
  if (!sameInstant(meeting.startAt, clock.startAt) || !sameInstant(meeting.endAt, clock.endAt)) return false;
  if (meeting.location !== text(series.location)) return false;
  if (meeting.meetingUrl !== text(series.meetingUrl)) return false;
  if ((meeting.organizerId || null) !== (series.organizerId || null)) return false;
  if (meeting.agenda !== text(series.agenda)) return false;
  if ((meeting.agendaResourceId || null) !== (series.agendaResourceId || null)) return false;
  if (!meeting.topicCustom && (meeting.topicId || null) !== (series.topicId || null)) return false;
  if (!meeting.roomCustom && (meeting.roomId || null) !== (series.roomId || null)) return false;
  return true;
}

function attendanceMatchesSeries(rows: OrgMeetingAttendance[], series: OrgMeetingSeries): boolean {
  const users = rows.filter((row) => row.kind === 'user').map((row) => row.userId);
  const persons = rows.filter((row) => row.kind === 'external').map((row) => row.personId);
  return sameIdSet(users, series.memberUserIds) && sameIdSet(persons, series.memberPersonIds);
}

async function copySeriesAttendance(meetingId: string, series: OrgMeetingSeries, people: OrgMeetingPerson[]): Promise<void> {
  const byId = new Map(people.map((person) => [person.id, person]));
  const drafts: OrgMeetingAttendanceInput[] = [
    ...series.memberUserIds.map((userId) => ({
      meetingId,
      kind: 'user' as const,
      userId,
      displayName: userId,
      presence: 'invited' as const,
    })),
    ...series.memberPersonIds.map((personId) => {
      const person = byId.get(personId);
      return {
        meetingId,
        kind: 'external' as const,
        personId,
        displayName: person?.name || personId,
        organization: person?.organization || null,
        email: person?.email || null,
        presence: 'invited' as const,
      };
    }),
  ];
  await runInBatches(drafts, async (draft) => {
    await createOrgMeetingAttendance(draft);
  });
}

function occurrenceClock(date: string, startTime: string, durationMinutes: number): { startAt: string; endAt: string } {
  const [year, month, day] = date.split('-').map(Number);
  const [hours, minutes] = normalizeStartTime(startTime).split(':').map(Number);
  const start = istanbulInstant(year, month, day, hours, minutes);
  const end = new Date(start.getTime() + durationMinutes * 60000);
  return { startAt: start.toISOString(), endAt: end.toISOString() };
}

function attendanceKeepsOccurrence(rows: OrgMeetingAttendance[]): boolean {
  return rows.some((row) => row.presence !== 'invited' || Boolean(row.note.trim()));
}

async function applySeriesForward(series: OrgMeetingSeries, fromDate: string): Promise<void> {
  const [meetings, people, actionIndex, attendanceIndex] = await Promise.all([
    listOrgMeetings(),
    listOrgMeetingPeople(),
    bulkMeetingActions(),
    bulkMeetingAttendance(),
  ]);
  const rows = meetings.filter(
    (row) => row.seriesId === series.id && row.occurrenceDate && row.occurrenceDate >= fromDate && !row.detached && row.status === 'scheduled' && !row.minutesResourceId,
  );
  const work: Array<{
    meeting: OrgMeeting;
    attendance: OrgMeetingAttendance[];
    clock: { startAt: string; endAt: string };
    fieldsSame: boolean;
    peopleSame: boolean;
  }> = [];
  for (const meeting of rows) {
    if ((await actionsFor(actionIndex, meeting.id)).length) continue;
    const attendance = await attendanceFor(attendanceIndex, meeting.id);
    if (attendanceKeepsOccurrence(attendance)) continue;
    const clock = occurrenceClock(meeting.occurrenceDate as string, series.startTime, series.durationMinutes);
    const fieldsSame = seriesFieldsMatch(meeting, series, clock);
    const peopleSame = attendanceMatchesSeries(attendance, series);
    if (fieldsSame && peopleSame) continue;
    work.push({ meeting, attendance, clock, fieldsSame, peopleSame });
  }
  await runInBatches(work, async (item) => {
    if (!item.fieldsSame) {
      await ocUpdate(MT_MEETINGS, item.meeting.id, {
        title: series.name,
        startAt: item.clock.startAt,
        endAt: item.clock.endAt,
        location: series.location || null,
        meetingUrl: series.meetingUrl || null,
        organizerId: series.organizerId,
        agenda: series.agenda || null,
        agendaResourceId: series.agendaResourceId,
        ...(item.meeting.topicCustom ? {} : { topicId: series.topicId }),
        ...(item.meeting.roomCustom ? {} : { roomId: series.roomId }),
      });
    }
    if (!item.peopleSame) {
      await runInBatches(item.attendance, async (row) => {
        await deleteOrgMeetingAttendance(row.id);
      });
      await copySeriesAttendance(item.meeting.id, series, people);
    }
  });
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
  const pending: Array<{ key: string; start: Date }> = [];
  let cursor = anchor;
  let steps = 0;
  while (steps < 500) {
    const parts = istanbulParts(cursor);
    const key = ymd(parts.year, parts.month, parts.day);
    if (key > limit) break;
    if (!(notBefore && key < notBefore) && days.has(parts.weekday) && !existing.has(key)) {
      pending.push({ key, start: new Date(cursor.getTime()) });
      existing.add(key);
    }
    steps += 1;
    cursor = new Date(cursor.getTime() + 86400000);
  }
  await runInBatches(pending, async (day) => {
    const end = new Date(day.start.getTime() + series.durationMinutes * 60000);
    const meetingId = await createOrgMeeting({
      title: series.name,
      startAt: day.start.toISOString(),
      endAt: end.toISOString(),
      status: 'scheduled',
      location: series.location,
      meetingUrl: series.meetingUrl,
      organizerId: series.organizerId,
      agenda: series.agenda,
      agendaResourceId: series.agendaResourceId,
      seriesId: series.id,
      occurrenceDate: day.key,
      detached: false,
      topicId: series.topicId,
      topicCustom: false,
      roomId: series.roomId,
      roomCustom: false,
    });
    await copySeriesAttendance(meetingId, series, people);
  });
}

async function cancelSeriesTail(series: OrgMeetingSeries, fromDate: string): Promise<void> {
  if (!series.until) return;
  const [meetings, actionIndex, attendanceIndex] = await Promise.all([
    listOrgMeetings(),
    bulkMeetingActions(),
    bulkMeetingAttendance(),
  ]);
  const targets: OrgMeeting[] = [];
  for (const meeting of meetings.filter((row) => row.seriesId === series.id)) {
    if (meeting.detached || meeting.status !== 'scheduled' || meeting.minutesResourceId) continue;
    if (!meeting.occurrenceDate || meeting.occurrenceDate < fromDate || meeting.occurrenceDate <= series.until) continue;
    if ((await actionsFor(actionIndex, meeting.id)).length) continue;
    if (attendanceKeepsOccurrence(await attendanceFor(attendanceIndex, meeting.id))) continue;
    targets.push(meeting);
  }
  await runInBatches(targets, async (meeting) => {
    await ocUpdate(MT_MEETINGS, meeting.id, { status: 'cancelled' });
  });
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
  const [meetings, actionIndex, attendanceIndex] = await Promise.all([
    listOrgMeetings(),
    bulkMeetingActions(),
    bulkMeetingAttendance(),
  ]);
  const now = Date.now();
  const targets: OrgMeeting[] = [];
  for (const meeting of meetings.filter((row) => row.seriesId === id)) {
    if (meeting.status !== 'scheduled' || meeting.detached || meeting.minutesResourceId) continue;
    const start = Date.parse(meeting.startAt);
    if (!Number.isNaN(start) && start < now) continue;
    if ((await actionsFor(actionIndex, meeting.id)).length) continue;
    if (attendanceKeepsOccurrence(await attendanceFor(attendanceIndex, meeting.id))) continue;
    targets.push(meeting);
  }
  await runInBatches(targets, async (meeting) => {
    await ocUpdate(MT_MEETINGS, meeting.id, { status: 'cancelled' });
  });
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

export async function listOrgMeetingTopics(): Promise<OrgMeetingTopic[]> {
  const rows = await ocListDataset(MT_MEETING_TOPICS, { sort: 'name:asc', limit: 500, expand: false });
  return rows.map(mapTopic).filter((row): row is OrgMeetingTopic => row != null);
}

export async function createOrgMeetingTopic(input: OrgMeetingTopicInput): Promise<string> {
  const id = await ocCreateRecordId(MT_MEETING_TOPICS, topicPayload(input));
  if (!id) throw new Error('Topic create did not return an id.');
  return id;
}

export async function updateOrgMeetingTopic(id: string, input: OrgMeetingTopicInput): Promise<void> {
  await ocUpdate(MT_MEETING_TOPICS, id, topicPayload(input));
}

function mapRoom(raw: unknown): OrgMeetingRoom | null {
  const row = asRecord(raw);
  const id = text(row.__dataId ?? row.dataId);
  const name = text(row.name);
  if (!id || !name) return null;
  const capacity = asInt(row.capacity, 0);
  return { id, name, capacity: capacity > 0 ? capacity : null, archived: asBool(row.archived) };
}

function roomPayload(input: OrgMeetingRoomInput): Record<string, unknown> {
  const capacity = Number(input.capacity);
  return {
    name: input.name.trim(),
    capacity: Number.isFinite(capacity) && capacity > 0 ? String(Math.round(capacity)) : null,
    archived: Boolean(input.archived),
  };
}

export async function listOrgMeetingRooms(): Promise<OrgMeetingRoom[]> {
  const rows = await ocListDataset(MT_MEETING_ROOMS, { sort: 'name:asc', limit: 500, expand: false });
  return rows.map(mapRoom).filter((row): row is OrgMeetingRoom => row != null);
}

export async function createOrgMeetingRoom(input: OrgMeetingRoomInput): Promise<string> {
  const id = await ocCreateRecordId(MT_MEETING_ROOMS, roomPayload(input));
  if (!id) throw new Error('Room create did not return an id.');
  return id;
}

export async function updateOrgMeetingRoom(id: string, input: OrgMeetingRoomInput): Promise<void> {
  await ocUpdate(MT_MEETING_ROOMS, id, roomPayload(input));
}

export async function deleteOrgMeetingRoom(id: string): Promise<void> {
  const [meetings, series] = await Promise.all([
    ocListDataset(MT_MEETINGS, { filter: `roomId:eq:${id}`, limit: 1, expand: false }),
    ocListDataset(MT_MEETING_SERIES, { filter: `roomId:eq:${id}`, limit: 1, expand: false }),
  ]);
  if (meetings.length || series.length) throw new Error('ROOM_IN_USE');
  await ocDelete(MT_MEETING_ROOMS, id);
}

export function meetingRangesOverlap(
  startAt: string,
  endAt: string | null | undefined,
  otherStart: string,
  otherEnd: string | null | undefined,
): boolean {
  const start = Date.parse(startAt);
  const other = Date.parse(otherStart);
  if (Number.isNaN(start) || Number.isNaN(other)) return false;
  const end = endAt ? Date.parse(endAt) : start + 60 * 60000;
  const otherFinish = otherEnd ? Date.parse(otherEnd) : other + 60 * 60000;
  if (Number.isNaN(end) || Number.isNaN(otherFinish)) return false;
  return start < otherFinish && other < end;
}

export function roomConflicts(
  meetings: OrgMeeting[],
  roomId: string,
  startAt: string,
  endAt: string | null | undefined,
  ignoreIds: string[] = [],
): OrgMeeting[] {
  if (!roomId) return [];
  const skip = new Set(ignoreIds);
  return meetings.filter((meeting) =>
    meeting.roomId === roomId
    && meeting.status !== 'cancelled'
    && !skip.has(meeting.id)
    && meetingRangesOverlap(startAt, endAt, meeting.startAt, meeting.endAt),
  );
}

export function plannedSeriesSlots(input: OrgMeetingSeriesInput, fromDate?: string | null): Array<{ startAt: string; endAt: string }> {
  const series = normalizeSeriesInput(input);
  const anchor = seriesAnchor(input.firstDate, series);
  const days = new Set(seriesDays(series));
  const limit = series.until || openHorizon(null);
  const slots: Array<{ startAt: string; endAt: string }> = [];
  let cursor = anchor;
  let steps = 0;
  while (steps < 500 && slots.length < 120) {
    const parts = istanbulParts(cursor);
    const key = ymd(parts.year, parts.month, parts.day);
    if (key > limit) break;
    if (!(fromDate && key < fromDate) && days.has(parts.weekday)) {
      const end = new Date(cursor.getTime() + series.durationMinutes * 60000);
      slots.push({ startAt: cursor.toISOString(), endAt: end.toISOString() });
    }
    steps += 1;
    cursor = new Date(cursor.getTime() + 86400000);
  }
  return slots;
}

function datasetField(field: Record<string, unknown>): Record<string, unknown> {
  const slim: Record<string, unknown> = {
    fieldType: field.fieldType,
    name: field.name,
    title: field.title,
    mandatory: Boolean(field.mandatory),
    unique: Boolean(field.unique),
    isArray: Boolean(field.isArray),
  };
  if (field.relationDataset) slim.relationDataset = field.relationDataset;
  if (field.incrementalOptions) slim.incrementalOptions = field.incrementalOptions;
  return slim;
}

async function ensureDatasetFields(name: string, additions: Record<string, unknown>[]): Promise<void> {
  const current = await fetchFromDataGateway(`/api/v1/datasets/${name}`);
  const fields = Array.isArray(current?.fields) ? current.fields as Record<string, unknown>[] : [];
  const names = new Set(fields.map((field) => text(field.name)));
  const missing = additions.filter((field) => !names.has(text(field.name)));
  if (!missing.length) return;
  await fetchFromDataGateway(`/api/v1/datasets/${name}`, 'PUT', {
    fields: [...fields.map(datasetField), ...missing],
  });
}

function topicDatasetAlreadyExists(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error ?? '');
  return message.includes('zaten mevcut') || message.toLowerCase().includes('already exists');
}

export async function ensureOrgMeetingTopics(): Promise<void> {
  let exists = false;
  try {
    await fetchFromDataGateway(`/api/v1/datasets/${MT_MEETING_TOPICS}`);
    exists = true;
  } catch {
    exists = false;
  }
  if (!exists) {
    try {
      await fetchFromDataGateway('/api/v1/datasets', 'POST', {
        name: MT_MEETING_TOPICS,
        description: 'Kurum toplantı konuları',
        forceSchema: true,
        logging: 'none',
        publishMode: 'none',
        fields: [
          { fieldType: 'text', name: 'name', title: 'Ad', mandatory: true, unique: false, isArray: false },
          { fieldType: 'text', name: 'code', title: 'Kod', mandatory: false, unique: false, isArray: false },
          { fieldType: 'text', name: 'color', title: 'Renk', mandatory: false, unique: false, isArray: false },
          { fieldType: 'bool', name: 'archived', title: 'Arşiv', mandatory: false, unique: false, isArray: false },
        ],
      });
    } catch (error) {
      if (!topicDatasetAlreadyExists(error)) throw error;
    }
  }
  await ensureDatasetFields(MT_MEETINGS, [
    { fieldType: 'text', name: 'topicId', title: 'Konu', mandatory: false, unique: false, isArray: false },
    { fieldType: 'bool', name: 'topicCustom', title: 'Konu güne özel', mandatory: false, unique: false, isArray: false },
  ]);
  await ensureDatasetFields(MT_MEETING_SERIES, [
    { fieldType: 'text', name: 'topicId', title: 'Konu', mandatory: false, unique: false, isArray: false },
  ]);
}

export async function ensureOrgMeetingRooms(): Promise<void> {
  await ensureNamedDataset(MT_MEETING_ROOMS, 'Toplantı odaları', [
    { fieldType: 'text', name: 'name', title: 'Ad', mandatory: true, unique: false, isArray: false },
    { fieldType: 'text', name: 'capacity', title: 'Kapasite', mandatory: false, unique: false, isArray: false },
    { fieldType: 'bool', name: 'archived', title: 'Arşiv', mandatory: false, unique: false, isArray: false },
  ]);
  await ensureDatasetFields(MT_MEETINGS, [
    { fieldType: 'text', name: 'roomId', title: 'Oda', mandatory: false, unique: false, isArray: false },
    { fieldType: 'bool', name: 'roomCustom', title: 'Oda güne özel', mandatory: false, unique: false, isArray: false },
  ]);
  await ensureDatasetFields(MT_MEETING_SERIES, [
    { fieldType: 'text', name: 'roomId', title: 'Oda', mandatory: false, unique: false, isArray: false },
  ]);
}

export interface OrgMeetingAttachment {
  id: string;
  meetingId: string;
  resourceId: string;
}

function mapAttachment(raw: unknown): OrgMeetingAttachment | null {
  const row = asRecord(raw);
  const id = text(row.__dataId ?? row.dataId);
  const meetingId = text(row.meetingId);
  const resourceId = text(row.resourceId);
  if (!id || !meetingId || !resourceId) return null;
  return { id, meetingId, resourceId };
}

export async function listOrgMeetingAttachmentRows(): Promise<OrgMeetingAttachment[]> {
  const rows = await ocListDataset(MT_MEETING_ATTACHMENTS, { limit: 2000, expand: false });
  return rows.map(mapAttachment).filter((row): row is OrgMeetingAttachment => row != null);
}

export async function listOrgMeetingAttachments(meetingId: string): Promise<OrgMeetingAttachment[]> {
  const rows = await ocListDataset(MT_MEETING_ATTACHMENTS, {
    filter: `meetingId:eq:${meetingId}`,
    limit: 200,
    expand: false,
  });
  return rows.map(mapAttachment).filter((row): row is OrgMeetingAttachment => row != null);
}

export async function createOrgMeetingAttachment(meetingId: string, resourceId: string): Promise<string> {
  const existing = await listOrgMeetingAttachments(meetingId);
  const found = existing.find((row) => row.resourceId === resourceId);
  if (found) return found.id;
  const id = await ocCreateRecordId(MT_MEETING_ATTACHMENTS, { meetingId, resourceId });
  if (!id) throw new Error('Attachment create did not return an id.');
  return id;
}

export async function deleteOrgMeetingAttachment(id: string): Promise<void> {
  await ocDelete(MT_MEETING_ATTACHMENTS, id);
}

export async function ensureOrgMeetingAttendance(): Promise<void> {
  await ensureDatasetFields(MT_MEETING_ATTENDANCE, [
    { fieldType: 'text', name: 'note', title: 'Not', mandatory: false, unique: false, isArray: false },
  ]);
}

export async function ensureOrgMeetingAttachments(): Promise<void> {
  let exists = false;
  try {
    await fetchFromDataGateway(`/api/v1/datasets/${MT_MEETING_ATTACHMENTS}`);
    exists = true;
  } catch {
    exists = false;
  }
  if (exists) return;
  try {
    await fetchFromDataGateway('/api/v1/datasets', 'POST', {
      name: MT_MEETING_ATTACHMENTS,
      description: 'Toplantıya bağlı belgeler',
      forceSchema: true,
      logging: 'none',
      publishMode: 'none',
      fields: [
        { fieldType: 'text', name: 'meetingId', title: 'Toplantı', mandatory: true, unique: false, isArray: false },
        { fieldType: 'text', name: 'resourceId', title: 'Belge', mandatory: true, unique: false, isArray: false },
      ],
    });
  } catch (error) {
    if (!topicDatasetAlreadyExists(error)) throw error;
  }
}

export interface OrgMeetingNoticeSetting {
  id: string;
  kind: MeetingNoticeKind;
  enabled: boolean;
  templateKey: string;
}

export interface OrgMeetingNoticeRecipient {
  name: string;
  email: string;
  result: MeetingNoticeResult;
}

export interface OrgMeetingNoticeLog {
  id: string;
  meetingId: string;
  kind: MeetingNoticeKind;
  sentAt: string;
  title: string;
  startAt: string;
  endAt: string;
  location: string;
  recipients: OrgMeetingNoticeRecipient[];
}

function noticeKind(raw: unknown): MeetingNoticeKind | null {
  const value = text(raw);
  return (MEETING_NOTICE_KINDS as readonly string[]).includes(value) ? value as MeetingNoticeKind : null;
}

function noticeRecipients(raw: unknown): OrgMeetingNoticeRecipient[] {
  try {
    const parsed = JSON.parse(text(raw) || '[]') as unknown;
    if (!Array.isArray(parsed)) return [];
    return parsed.flatMap((item) => {
      const row = asRecord(item);
      const result = text(row.result);
      if (result !== 'sent' && result !== 'skipped' && result !== 'failed') return [];
      return [{ name: text(row.name), email: text(row.email), result }];
    });
  } catch {
    return [];
  }
}

function mapNoticeSetting(raw: unknown): OrgMeetingNoticeSetting | null {
  const row = asRecord(raw);
  const id = text(row.__dataId ?? row.dataId);
  const kind = noticeKind(row.kind);
  if (!id || !kind) return null;
  return { id, kind, enabled: asBool(row.enabled), templateKey: text(row.templateKey) };
}

function mapNoticeLog(raw: unknown): OrgMeetingNoticeLog | null {
  const row = asRecord(raw);
  const id = text(row.__dataId ?? row.dataId);
  const meetingId = text(row.meetingId);
  const kind = noticeKind(row.kind);
  if (!id || !meetingId || !kind) return null;
  return {
    id,
    meetingId,
    kind,
    sentAt: text(row.sentAt),
    title: text(row.title),
    startAt: text(row.startAt),
    endAt: text(row.endAt),
    location: text(row.location),
    recipients: noticeRecipients(row.detail),
  };
}

async function ensureNamedDataset(name: string, description: string, fields: Record<string, unknown>[]): Promise<void> {
  try {
    await fetchFromDataGateway(`/api/v1/datasets/${name}`);
    return;
  } catch {
    // The dataset is created below.
  }
  try {
    await fetchFromDataGateway('/api/v1/datasets', 'POST', {
      name,
      description,
      forceSchema: true,
      logging: 'none',
      publishMode: 'none',
      fields,
    });
  } catch (error) {
    if (!topicDatasetAlreadyExists(error)) throw error;
  }
}

export async function listOrgMeetingNoticeSettings(): Promise<OrgMeetingNoticeSetting[]> {
  const rows = await ocListDataset(MT_MEETING_NOTICE_SETTINGS, { limit: 20, expand: false });
  return rows.map(mapNoticeSetting).filter((row): row is OrgMeetingNoticeSetting => row != null);
}

export async function updateOrgMeetingNoticeSetting(id: string, input: { kind: MeetingNoticeKind; enabled: boolean; templateKey: string }): Promise<void> {
  await ocUpdate(MT_MEETING_NOTICE_SETTINGS, id, {
    kind: input.kind,
    enabled: input.enabled,
    templateKey: input.templateKey.trim() || null,
  });
}

export async function listOrgMeetingNoticeLogs(meetingId: string): Promise<OrgMeetingNoticeLog[]> {
  const rows = await ocListDataset(MT_MEETING_NOTICE_LOGS, {
    filter: `meetingId:eq:${meetingId}`,
    sort: 'sentAt:desc',
    limit: 200,
    expand: false,
  });
  return rows.map(mapNoticeLog).filter((row): row is OrgMeetingNoticeLog => row != null);
}

export async function createOrgMeetingNoticeLog(input: {
  meetingId: string;
  kind: MeetingNoticeKind;
  sentAt: string;
  title: string;
  startAt: string;
  endAt: string;
  location: string;
  recipients: OrgMeetingNoticeRecipient[];
}): Promise<string> {
  const id = await ocCreateRecordId(MT_MEETING_NOTICE_LOGS, {
    meetingId: input.meetingId,
    kind: input.kind,
    sentAt: input.sentAt,
    title: input.title.trim() || null,
    startAt: input.startAt || null,
    endAt: input.endAt || null,
    location: input.location.trim() || null,
    detail: JSON.stringify(input.recipients),
  });
  if (!id) throw new Error('Notice log create did not return an id.');
  return id;
}

export async function ensureOrgMeetingNotices(): Promise<void> {
  await ensureNamedDataset(MT_MEETING_NOTICE_SETTINGS, 'Toplantı bildirim ayarları', [
    { fieldType: 'text', name: 'kind', title: 'Tür', mandatory: true, unique: true, isArray: false },
    { fieldType: 'bool', name: 'enabled', title: 'Açık', mandatory: false, unique: false, isArray: false },
    { fieldType: 'text', name: 'templateKey', title: 'Şablon', mandatory: false, unique: false, isArray: false },
  ]);
  await ensureNamedDataset(MT_MEETING_NOTICE_LOGS, 'Toplantı davet kayıtları', [
    { fieldType: 'text', name: 'meetingId', title: 'Toplantı', mandatory: true, unique: false, isArray: false },
    { fieldType: 'text', name: 'kind', title: 'Tür', mandatory: true, unique: false, isArray: false },
    { fieldType: 'text', name: 'sentAt', title: 'Zaman', mandatory: true, unique: false, isArray: false },
    { fieldType: 'text', name: 'title', title: 'Başlık', mandatory: false, unique: false, isArray: false },
    { fieldType: 'text', name: 'startAt', title: 'Başlangıç', mandatory: false, unique: false, isArray: false },
    { fieldType: 'text', name: 'endAt', title: 'Bitiş', mandatory: false, unique: false, isArray: false },
    { fieldType: 'text', name: 'location', title: 'Yer', mandatory: false, unique: false, isArray: false },
    { fieldType: 'text', name: 'detail', title: 'Sonuç', mandatory: false, unique: false, isArray: false },
  ]);
  const existing = await listOrgMeetingNoticeSettings();
  for (const kind of MEETING_NOTICE_KINDS) {
    if (existing.some((row) => row.kind === kind)) continue;
    try {
      await ocCreateRecordId(MT_MEETING_NOTICE_SETTINGS, { kind, enabled: false, templateKey: null });
    } catch (error) {
      if (!topicDatasetAlreadyExists(error)) throw error;
    }
  }
}
