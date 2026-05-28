/** Quartz cron: saniye dakika saat günAy ay haftanınGünü [yıl] */

export const OC_SCHEDULE_TIMEZONE_PRESETS = [
  'Europe/Istanbul',
  'UTC',
  'Europe/London',
  'Europe/Berlin',
  'America/New_York',
] as const;

export const OC_SCHEDULE_WEEKDAY_KEYS = [
  'mon',
  'tue',
  'wed',
  'thu',
  'fri',
  'sat',
  'sun',
] as const;

export type OcScheduleWeekdayKey = (typeof OC_SCHEDULE_WEEKDAY_KEYS)[number];

const QUARTZ_WEEKDAY: Record<OcScheduleWeekdayKey, string> = {
  mon: 'MON',
  tue: 'TUE',
  wed: 'WED',
  thu: 'THU',
  fri: 'FRI',
  sat: 'SAT',
  sun: 'SUN',
};

const QUARTZ_TO_KEY = Object.fromEntries(
  (Object.entries(QUARTZ_WEEKDAY) as [OcScheduleWeekdayKey, string][]).map(([k, v]) => [v, k])
) as Record<string, OcScheduleWeekdayKey>;

/** Zamanlama sihirbazı — kullanıcı dostu modlar */
export type OcScheduleWizardType =
  | 'everyMinutes'
  | 'everyHours'
  | 'dailyAt'
  | 'weeklyDays'
  | 'advanced';

export type OcScheduleWizardState = {
  type: OcScheduleWizardType;
  everyN: number;
  hour: number;
  minute: number;
  weekdays: OcScheduleWeekdayKey[];
  advancedCron: string;
};

export type OcScheduleSummary =
  | { type: 'everyMinutes'; n: number }
  | { type: 'everyHours'; n: number }
  | { type: 'dailyAt'; hour: number; minute: number }
  | { type: 'weeklyDays'; weekdays: OcScheduleWeekdayKey[]; hour: number; minute: number }
  | { type: 'advanced'; cron: string };

function clampHour(h: number): number {
  return Math.min(23, Math.max(0, Math.floor(h)));
}

function clampMinute(m: number): number {
  return Math.min(59, Math.max(0, Math.floor(m)));
}

function sortWeekdays(keys: OcScheduleWeekdayKey[]): OcScheduleWeekdayKey[] {
  const set = new Set(keys);
  return OC_SCHEDULE_WEEKDAY_KEYS.filter((k) => set.has(k));
}

function parseDowField(dow: string): OcScheduleWeekdayKey[] | null {
  const raw = (dow ?? '').trim().toUpperCase();
  if (!raw || raw === '?' || raw === '*') return null;

  const keys: OcScheduleWeekdayKey[] = [];
  for (const token of raw.split(',')) {
    const t = token.trim();
    if (!t) continue;
    if (t.includes('-')) {
      const [start, end] = t.split('-').map((s) => s.trim());
      const startKey = QUARTZ_TO_KEY[start ?? ''];
      const endKey = QUARTZ_TO_KEY[end ?? ''];
      if (!startKey || !endKey) return null;
      const startIdx = OC_SCHEDULE_WEEKDAY_KEYS.indexOf(startKey);
      const endIdx = OC_SCHEDULE_WEEKDAY_KEYS.indexOf(endKey);
      if (startIdx < 0 || endIdx < 0) return null;
      for (let i = startIdx; i <= endIdx; i++) {
        keys.push(OC_SCHEDULE_WEEKDAY_KEYS[i]!);
      }
    } else {
      const key = QUARTZ_TO_KEY[t];
      if (!key) return null;
      keys.push(key);
    }
  }
  return keys.length ? sortWeekdays(keys) : null;
}

export function defaultScheduleWizardState(): OcScheduleWizardState {
  return {
    type: 'weeklyDays',
    everyN: 2,
    hour: 9,
    minute: 0,
    weekdays: ['mon'],
    advancedCron: '',
  };
}

export function buildEveryMinutesQuartzCron(minutes: number): string {
  const n = Math.min(59, Math.max(1, Math.floor(minutes)));
  return `0 0/${n} * * * ?`;
}

export function buildEveryHoursQuartzCron(hours: number): string {
  const n = Math.min(23, Math.max(1, Math.floor(hours)));
  return `0 0 0/${n} * * ?`;
}

export function buildDailyAtQuartzCron(hour: number, minute: number): string {
  const h = clampHour(hour);
  const m = clampMinute(minute);
  return `0 ${m} ${h} * * ?`;
}

export function buildMultiWeeklyQuartzCron(
  weekdays: OcScheduleWeekdayKey[],
  hour: number,
  minute: number
): string {
  const sorted = sortWeekdays(weekdays.length ? weekdays : ['mon']);
  const dow = sorted.map((k) => QUARTZ_WEEKDAY[k]).join(',');
  const h = clampHour(hour);
  const m = clampMinute(minute);
  return `0 ${m} ${h} ? * ${dow}`;
}

/** @deprecated Tek gün — çoklu gün sihirbazına yönlendirir */
export function buildWeeklyQuartzCron(
  weekday: OcScheduleWeekdayKey,
  hour: number,
  minute: number
): string {
  return buildMultiWeeklyQuartzCron([weekday], hour, minute);
}

/** @deprecated buildEveryMinutesQuartzCron kullanın */
export function buildIntervalQuartzCron(minutes: number): string {
  return buildEveryMinutesQuartzCron(minutes);
}

export function wizardStateToCron(state: OcScheduleWizardState): string {
  switch (state.type) {
    case 'everyMinutes':
      return buildEveryMinutesQuartzCron(state.everyN);
    case 'everyHours':
      return buildEveryHoursQuartzCron(state.everyN);
    case 'dailyAt':
      return buildDailyAtQuartzCron(state.hour, state.minute);
    case 'weeklyDays':
      return buildMultiWeeklyQuartzCron(state.weekdays, state.hour, state.minute);
    case 'advanced':
      return state.advancedCron.trim();
    default:
      return buildMultiWeeklyQuartzCron(['mon'], 9, 0);
  }
}

export function parseEveryMinutesQuartzCron(cronExpression: string): { minutes: number } | null {
  const parts = cronExpression.trim().split(/\s+/);
  if (parts.length < 6) return null;
  const [sec, min, hour, dom, mon, dow] = parts;
  if (sec !== '0') return null;
  const stepMatch = /^0\/(\d+)$/.exec(min ?? '') ?? /^\*\/(\d+)$/.exec(min ?? '');
  if (!stepMatch) return null;
  if (hour !== '*' || dom !== '*' || mon !== '*') return null;
  const dowNorm = (dow ?? '').toUpperCase();
  if (dowNorm !== '?' && dowNorm !== '*') return null;
  const minutes = Number(stepMatch[1]);
  if (!Number.isFinite(minutes) || minutes < 1 || minutes > 59) return null;
  return { minutes };
}

export function parseEveryHoursQuartzCron(cronExpression: string): { hours: number } | null {
  const parts = cronExpression.trim().split(/\s+/);
  if (parts.length < 6) return null;
  const [sec, min, hour, dom, mon, dow] = parts;
  if (sec !== '0' || min !== '0') return null;
  const stepMatch = /^0\/(\d+)$/.exec(hour ?? '') ?? /^\*\/(\d+)$/.exec(hour ?? '');
  if (!stepMatch) return null;
  if (dom !== '*' || mon !== '*') return null;
  const dowNorm = (dow ?? '').toUpperCase();
  if (dowNorm !== '?' && dowNorm !== '*') return null;
  const hours = Number(stepMatch[1]);
  if (!Number.isFinite(hours) || hours < 1 || hours > 23) return null;
  return { hours };
}

export function parseDailyAtQuartzCron(cronExpression: string): { hour: number; minute: number } | null {
  const parts = cronExpression.trim().split(/\s+/);
  if (parts.length < 6) return null;
  const [sec, min, hour, dom, mon, dow] = parts;
  if (sec !== '0' || !/^\d+$/.test(min ?? '') || !/^\d+$/.test(hour ?? '')) return null;
  if (dom !== '*' || mon !== '*') return null;
  const dowNorm = (dow ?? '').toUpperCase();
  if (dowNorm !== '?' && dowNorm !== '*') return null;
  return { hour: Number(hour), minute: Number(min) };
}

export function parseWeeklyDaysQuartzCron(cronExpression: string): {
  weekdays: OcScheduleWeekdayKey[];
  hour: number;
  minute: number;
} | null {
  const parts = cronExpression.trim().split(/\s+/);
  if (parts.length < 6) return null;
  const [sec, min, hour, dom, mon, dow] = parts;
  if (sec !== '0' || !/^\d+$/.test(min ?? '') || !/^\d+$/.test(hour ?? '')) return null;
  if (dom !== '?' && dom !== '*') return null;
  if (mon !== '*') return null;
  const weekdays = parseDowField(dow ?? '');
  if (!weekdays?.length) return null;
  return { weekdays, hour: Number(hour), minute: Number(min) };
}

export function parseScheduleSummary(cronExpression: string): OcScheduleSummary | null {
  const trimmed = cronExpression.trim();
  if (!trimmed) return null;

  const everyMin = parseEveryMinutesQuartzCron(trimmed);
  if (everyMin) return { type: 'everyMinutes', n: everyMin.minutes };

  const everyHr = parseEveryHoursQuartzCron(trimmed);
  if (everyHr) return { type: 'everyHours', n: everyHr.hours };

  const weekly = parseWeeklyDaysQuartzCron(trimmed);
  if (weekly) {
    return {
      type: 'weeklyDays',
      weekdays: weekly.weekdays,
      hour: weekly.hour,
      minute: weekly.minute,
    };
  }

  const daily = parseDailyAtQuartzCron(trimmed);
  if (daily) return { type: 'dailyAt', hour: daily.hour, minute: daily.minute };

  if (isPlausibleQuartzCron(trimmed)) return { type: 'advanced', cron: trimmed };
  return null;
}

export function wizardStateFromCron(cronExpression: string): OcScheduleWizardState {
  const base = defaultScheduleWizardState();
  const trimmed = cronExpression.trim();
  if (!trimmed) return base;

  const everyMin = parseEveryMinutesQuartzCron(trimmed);
  if (everyMin) {
    return { ...base, type: 'everyMinutes', everyN: everyMin.minutes };
  }

  const everyHr = parseEveryHoursQuartzCron(trimmed);
  if (everyHr) {
    return { ...base, type: 'everyHours', everyN: everyHr.hours };
  }

  const weekly = parseWeeklyDaysQuartzCron(trimmed);
  if (weekly) {
    return {
      ...base,
      type: 'weeklyDays',
      weekdays: weekly.weekdays,
      hour: weekly.hour,
      minute: weekly.minute,
    };
  }

  const daily = parseDailyAtQuartzCron(trimmed);
  if (daily) {
    return {
      ...base,
      type: 'dailyAt',
      hour: daily.hour,
      minute: daily.minute,
    };
  }

  return { ...base, type: 'advanced', advancedCron: trimmed };
}

export function formatScheduleTime(hour: number, minute: number): string {
  return `${String(clampHour(hour)).padStart(2, '0')}:${String(clampMinute(minute)).padStart(2, '0')}`;
}

export function formatWeekdayList(
  weekdays: OcScheduleWeekdayKey[],
  labels: Record<OcScheduleWeekdayKey, string>
): string {
  return sortWeekdays(weekdays)
    .map((k) => labels[k])
    .join(', ');
}

/** @deprecated resolveScheduleCronMode yerine wizardStateFromCron */
export type OcScheduleCronMode = OcScheduleWizardType | 'weekly' | 'interval';

export function parseIntervalQuartzCron(cronExpression: string): { minutes: number } | null {
  return parseEveryMinutesQuartzCron(cronExpression);
}

export function parseWeeklyQuartzCron(cronExpression: string): {
  weekday: OcScheduleWeekdayKey;
  hour: number;
  minute: number;
} | null {
  const multi = parseWeeklyDaysQuartzCron(cronExpression);
  if (!multi || multi.weekdays.length !== 1) return null;
  return {
    weekday: multi.weekdays[0]!,
    hour: multi.hour,
    minute: multi.minute,
  };
}

export function resolveScheduleCronMode(cronExpression: string): OcScheduleCronMode {
  const s = wizardStateFromCron(cronExpression);
  if (s.type === 'everyMinutes') return 'interval';
  return s.type;
}

export function summarizeWeeklyCron(
  cronExpression: string,
  labels: Record<OcScheduleWeekdayKey, string>
): string | null {
  const summary = parseScheduleSummary(cronExpression);
  if (summary?.type !== 'weeklyDays' || summary.weekdays.length !== 1) return null;
  const time = formatScheduleTime(summary.hour, summary.minute);
  return `${labels[summary.weekdays[0]!]} ${time}`;
}

export function summarizeIntervalCron(cronExpression: string): string | null {
  const parsed = parseEveryMinutesQuartzCron(cronExpression);
  return parsed ? String(parsed.minutes) : null;
}

export function isPlausibleQuartzCron(expression: string): boolean {
  return getQuartzCronValidationIssue(expression) === null;
}

export function getQuartzCronValidationIssue(
  expression: string
): 'empty' | 'tooFew' | 'tooMany' | 'weeklyDaysEmpty' | null {
  const trimmed = expression.trim();
  if (!trimmed) return 'empty';
  const parts = trimmed.split(/\s+/);
  if (parts.length < 6) return 'tooFew';
  if (parts.length > 7) return 'tooMany';
  return null;
}

export function validateWizardState(state: OcScheduleWizardState): 'weeklyDaysEmpty' | null {
  if (state.type === 'weeklyDays' && state.weekdays.length === 0) return 'weeklyDaysEmpty';
  const cron = wizardStateToCron(state);
  return getQuartzCronValidationIssue(cron);
}

export function formatScheduleLastRun(iso: string | null | undefined, locale: string): string {
  if (!iso?.trim()) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString(locale, { dateStyle: 'short', timeStyle: 'short' });
}

export type ScheduleTranslateFn = (
  key: string,
  params?: Record<string, string | number>
) => string;

const I18N_PREFIX = 'operationCore.workspaceDefinitions.scheduled.';

/** Liste ve önizleme için insan okunur özet (i18n anahtarları scheduled.* altında) */
export function formatScheduleHumanSummary(
  cronExpression: string,
  timezone: string,
  weekdayLabels: Record<OcScheduleWeekdayKey, string>,
  tr: ScheduleTranslateFn
): string {
  const tz = timezone || 'Europe/Istanbul';
  const summary = parseScheduleSummary(cronExpression);
  if (!summary) return tr(`${I18N_PREFIX}livePreviewWhenEmpty`);
  switch (summary.type) {
    case 'everyMinutes':
      return tr(`${I18N_PREFIX}summaryEveryMinutes`, { n: summary.n, timezone: tz });
    case 'everyHours':
      return tr(`${I18N_PREFIX}summaryEveryHours`, { n: summary.n, timezone: tz });
    case 'dailyAt':
      return tr(`${I18N_PREFIX}summaryDailyAt`, {
        time: formatScheduleTime(summary.hour, summary.minute),
        timezone: tz,
      });
    case 'weeklyDays':
      return tr(`${I18N_PREFIX}summaryWeeklyDays`, {
        days: formatWeekdayList(summary.weekdays, weekdayLabels),
        time: formatScheduleTime(summary.hour, summary.minute),
        timezone: tz,
      });
    case 'advanced':
      return tr(`${I18N_PREFIX}livePreviewWhenCron`, { cron: summary.cron, timezone: tz });
    default:
      return tr(`${I18N_PREFIX}livePreviewWhenEmpty`);
  }
}
