import type { ScenarioSource } from '@/types/apps/scenario';
import type { DiscoveryHostDto } from '@/services/siemDiscoveryService';
import type { EventCatalogSelection } from '@/utils/alarm/eventCatalog';
import {
  deriveMatchKeysFromEvents,
  eventCodesFromEvents,
} from '@/utils/alarm/eventCatalog';

export type SimpleSourcePlatform = 'windows' | 'linux' | 'other';
export type SimpleSourceChannel = 'eventlog' | 'metric' | 'app';
export type SimpleMetricOperator = 'gt' | 'gte' | 'lt' | 'lte' | 'eq' | 'neq';

/** Sentinel value for “all hosts” in the multiselect UI. */
export const SIMPLE_SOURCE_ALL_HOSTS = '__all__';

export const SIMPLE_METRIC_OPERATORS: SimpleMetricOperator[] = [
  'gt', 'gte', 'lt', 'lte', 'eq', 'neq',
];

export interface SimpleMetricComparison {
  key: string;
  operator: SimpleMetricOperator;
  threshold: number;
}

export interface SimpleSourceState {
  platform: SimpleSourcePlatform;
  channel: SimpleSourceChannel;
  /** Empty = all hosts (no host filter). */
  hosts: string[];
  /** EventLog selections from channel dictionary (multi). */
  events: EventCatalogSelection[];
  /** Metric channel: value comparison that becomes a managed condition node. */
  metric: SimpleMetricComparison | null;
}

export interface SimpleSourcePreset {
  value: string;
  labelKey: string;
  platforms: SimpleSourcePlatform[];
  channel: SimpleSourceChannel;
}

export const SIMPLE_SOURCE_PRESETS: SimpleSourcePreset[] = [
  // Other / network Event-like
  { value: 'denied_flow', labelKey: 'alarmCenter.rules.matchKeys.denied_flow', platforms: ['other'], channel: 'eventlog' },
  { value: 'allowed_flow', labelKey: 'alarmCenter.rules.matchKeys.allowed_flow', platforms: ['other'], channel: 'eventlog' },
  { value: 'rule_change', labelKey: 'alarmCenter.rules.matchKeys.rule_change', platforms: ['other'], channel: 'eventlog' },
  { value: 'new_flow', labelKey: 'alarmCenter.rules.matchKeys.new_flow', platforms: ['other'], channel: 'eventlog' },
  // Metrics (all platforms)
  { value: 'cpu_usage', labelKey: 'alarmCenter.rules.matchKeys.cpu_usage', platforms: ['windows', 'linux', 'other'], channel: 'metric' },
  { value: 'disk_free_percent', labelKey: 'alarmCenter.rules.matchKeys.disk_free_percent', platforms: ['windows', 'linux', 'other'], channel: 'metric' },
  { value: 'memory_usage', labelKey: 'alarmCenter.scenarioStudio.simpleSource.presets.memory_usage', platforms: ['windows', 'linux', 'other'], channel: 'metric' },
  { value: 'agent_heartbeat', labelKey: 'alarmCenter.scenarioStudio.simpleSource.presets.agent_heartbeat', platforms: ['windows', 'linux', 'other'], channel: 'metric' },
  // App
  { value: 'service_health', labelKey: 'alarmCenter.scenarioStudio.simpleSource.presets.service_health', platforms: ['windows', 'linux', 'other'], channel: 'app' },
  { value: 'app.error', labelKey: 'alarmCenter.scenarioStudio.simpleSource.presets.app_error', platforms: ['windows', 'linux', 'other'], channel: 'app' },
];

export function defaultMetricComparison(key = 'cpu_usage'): SimpleMetricComparison {
  // disk_free_percent is typically "below N%"
  if (key === 'disk_free_percent') {
    return { key, operator: 'lt', threshold: 10 };
  }
  return { key, operator: 'gte', threshold: 90 };
}

export function defaultSimpleSourceState(): SimpleSourceState {
  return { platform: 'windows', channel: 'eventlog', hosts: [], events: [], metric: null };
}

export function coerceMetricComparison(
  value: unknown,
  fallbackKey = 'cpu_usage',
): SimpleMetricComparison {
  if (value && typeof value === 'object') {
    const raw = value as Partial<SimpleMetricComparison>;
    const key = String(raw.key ?? fallbackKey).trim() || fallbackKey;
    const operator = SIMPLE_METRIC_OPERATORS.includes(raw.operator as SimpleMetricOperator)
      ? (raw.operator as SimpleMetricOperator)
      : defaultMetricComparison(key).operator;
    const threshold = Number(raw.threshold);
    return {
      key,
      operator,
      threshold: Number.isFinite(threshold) ? threshold : defaultMetricComparison(key).threshold,
    };
  }
  return defaultMetricComparison(fallbackKey);
}

export function normalizeSimpleHosts(hosts: unknown): string[] {
  const raw = Array.isArray(hosts)
    ? hosts
    : (typeof hosts === 'string' && hosts.trim() ? [hosts] : []);
  const unique: string[] = [];
  const seen = new Set<string>();
  for (const item of raw) {
    const value = String(item ?? '').trim();
    if (!value || value === SIMPLE_SOURCE_ALL_HOSTS) continue;
    const key = value.toLowerCase();
    if (seen.has(key)) continue;
    seen.add(key);
    unique.push(value);
  }
  return unique;
}

function normalizeEvents(events: unknown): EventCatalogSelection[] {
  if (!Array.isArray(events)) return [];
  const unique: EventCatalogSelection[] = [];
  const seen = new Set<string>();
  for (const item of events) {
    if (!item || typeof item !== 'object') continue;
    const row = item as EventCatalogSelection;
    const value = String(row.value ?? '').trim();
    if (!value || seen.has(value)) continue;
    seen.add(value);
    unique.push({
      value,
      eventId: Number(row.eventId) || 0,
      channel: String(row.channel ?? ''),
      channelLabel: String(row.channelLabel ?? row.channel ?? ''),
      label: String(row.label ?? ''),
      matchKey: String(row.matchKey ?? ''),
    });
  }
  return unique;
}

/** Migrate legacy `host: string` and normalize hosts[]. */
export function coerceSimpleSourceState(
  state?: Partial<SimpleSourceState> & { host?: string } | null,
): SimpleSourceState {
  const base = defaultSimpleSourceState();
  if (!state) return base;
  const hosts = normalizeSimpleHosts(
    Array.isArray(state.hosts)
      ? state.hosts
      : (state.host ?? []),
  );
  return {
    platform: state.platform ?? base.platform,
    channel: state.channel ?? base.channel,
    hosts,
    events: normalizeEvents(state.events),
    metric: (state.channel ?? base.channel) === 'metric'
      ? coerceMetricComparison(state.metric, state.metric?.key || 'cpu_usage')
      : null,
  };
}

export function channelsForPlatform(platform: SimpleSourcePlatform): SimpleSourceChannel[] {
  if (platform === 'other') return ['eventlog', 'metric', 'app'];
  return ['eventlog', 'metric', 'app'];
}

export function presetsForSimpleSource(
  platform: SimpleSourcePlatform,
  channel: SimpleSourceChannel,
): SimpleSourcePreset[] {
  return SIMPLE_SOURCE_PRESETS.filter(
    item => item.channel === channel && item.platforms.includes(platform),
  );
}

export function sourceTypeForPlatform(
  platform: SimpleSourcePlatform,
  channel: SimpleSourceChannel,
): string | null {
  if (channel !== 'eventlog') return null;
  if (platform === 'windows') return 'windows-eventlog';
  if (platform === 'linux') return 'linux-journal';
  return null;
}

export function applySimpleSourceToConfig(
  source: ScenarioSource,
  state: SimpleSourceState,
  matchKey: string,
): ScenarioSource {
  const next: ScenarioSource = {
    ...source,
    dependsOnScenarioIds: source.dependsOnScenarioIds ?? [],
    maxChainDepth: source.maxChainDepth || 5,
    matchKey: matchKey.trim() || source.matchKey || '',
    matchKeys: [],
  };

  if (state.channel === 'metric') {
    next.kind = 'observation';
    next.observationKind = 'metric';
    const metricKey = state.metric?.key?.trim() || matchKey.trim() || 'cpu_usage';
    next.matchKey = metricKey;
    next.matchKeys = [metricKey];
    return next;
  }

  if (state.channel === 'app') {
    next.kind = 'observation';
    next.observationKind = 'signal';
    if (!next.matchKey) next.matchKey = 'service_health';
    return next;
  }

  next.kind = 'observation';
  next.observationKind = 'event';

  if ((state.platform === 'windows' || state.platform === 'linux') && state.events.length) {
    const keys = deriveMatchKeysFromEvents(state.events);
    next.matchKeys = keys;
    next.matchKey = keys[0] || (state.platform === 'linux' ? 'login_failed' : 'unknown');
    return next;
  }

  if (!next.matchKey) {
    next.matchKey = state.platform === 'linux' ? 'login_failed' : 'denied_flow';
  }
  next.matchKeys = next.matchKey ? [next.matchKey] : [];
  return next;
}

export function inferSimpleSourceState(
  source?: Partial<ScenarioSource> | null,
  hosts: string[] | string = [],
  events: EventCatalogSelection[] = [],
): SimpleSourceState {
  const normalizedHosts = normalizeSimpleHosts(hosts);
  const normalizedEvents = normalizeEvents(events);
  const key = String(source?.matchKey || '');
  const preset = SIMPLE_SOURCE_PRESETS.find(item => item.value === key);
  if (preset) {
    return {
      platform: preset.platforms[0],
      channel: preset.channel,
      hosts: normalizedHosts,
      events: normalizedEvents,
      metric: preset.channel === 'metric' ? defaultMetricComparison(preset.value) : null,
    };
  }
  const observationKind = String(source?.observationKind || 'event').toLowerCase();
  if (observationKind === 'metric' || source?.kind === 'scheduled-staleness') {
    return {
      platform: 'windows',
      channel: 'metric',
      hosts: normalizedHosts,
      events: [],
      metric: defaultMetricComparison(key || 'cpu_usage'),
    };
  }
  if (observationKind === 'signal') {
    return { platform: 'windows', channel: 'app', hosts: normalizedHosts, events: [], metric: null };
  }
  const hasLinuxEvents = normalizedEvents.some(item => String(item.value).startsWith('linux::'));
  if (hasLinuxEvents) {
    return {
      platform: 'linux',
      channel: 'eventlog',
      hosts: normalizedHosts,
      events: normalizedEvents,
      metric: null,
    };
  }
  return {
    platform: 'windows',
    channel: 'eventlog',
    hosts: normalizedHosts,
    events: normalizedEvents,
    metric: null,
  };
}

export function simpleSourceSubtitle(
  state: SimpleSourceState,
  matchKey: string,
  translate: (key: string) => string,
): string {
  const platform = translate(`alarmCenter.scenarioStudio.simpleSource.platform.${state.platform}`);
  const channel = translate(`alarmCenter.scenarioStudio.simpleSource.channel.${state.channel}`);
  const hosts = normalizeSimpleHosts(state.hosts);
  const parts = [platform, channel];
  if (state.channel === 'eventlog'
    && (state.platform === 'windows' || state.platform === 'linux')
    && state.events.length) {
    if (state.events.length === 1) {
      const ev = state.events[0];
      parts.push(ev.eventId > 0 ? `${ev.label} (${ev.eventId})` : `${ev.label} (${ev.matchKey})`);
    } else {
      parts.push(
        translate('alarmCenter.scenarioStudio.eventSelector.eventCount')
          .replace('{count}', String(state.events.length)),
      );
    }
  } else if (state.channel === 'metric' && state.metric) {
    const opLabel = translate(`alarmCenter.scenarioStudio.simpleSource.metricOperator.${state.metric.operator}`);
    parts.push(`${state.metric.key} ${opLabel} ${state.metric.threshold}`);
  } else if (matchKey.trim()) {
    parts.push(matchKey.trim());
  }
  if (hosts.length === 1) parts.push(hosts[0]);
  else if (hosts.length > 1) {
    parts.push(translate('alarmCenter.scenarioStudio.simpleSource.hostCount').replace('{count}', String(hosts.length)));
  } else {
    parts.push(translate('alarmCenter.scenarioStudio.simpleSource.allHosts'));
  }
  return parts.join(' · ');
}

export function discoveryOsFamily(host: DiscoveryHostDto): SimpleSourcePlatform | 'unknown' {
  const hint = String(host.osHint ?? '').trim().toLowerCase();
  if (!hint || hint === 'unknown') return 'unknown';
  if (hint.includes('windows') || hint === 'win') return 'windows';
  if (
    hint.includes('linux')
    || hint.includes('ubuntu')
    || hint.includes('debian')
    || hint.includes('centos')
    || hint.includes('rhel')
  ) {
    return 'linux';
  }
  return 'other';
}

/** Platform filter for discovery hosts; unknown OS is shown for every platform. */
export function filterDiscoveryHostsForPlatform(
  hosts: DiscoveryHostDto[],
  platform: SimpleSourcePlatform,
): DiscoveryHostDto[] {
  return hosts.filter((host) => {
    const family = discoveryOsFamily(host);
    if (family === 'unknown') return true;
    return family === platform;
  });
}

export function hostFilterCondition(hosts: string[]): {
  field: string;
  operator: string;
  value: unknown;
  subtitle: string;
} | null {
  const normalized = normalizeSimpleHosts(hosts);
  if (!normalized.length) return null;
  if (normalized.length === 1) {
    return {
      field: 'dimensions.sourceHost',
      operator: 'eq',
      value: normalized[0],
      subtitle: `sourceHost = ${normalized[0]}`,
    };
  }
  return {
    field: 'dimensions.sourceHost',
    operator: 'in',
    value: normalized,
    subtitle: `sourceHost in (${normalized.length})`,
  };
}

export function eventCodeFilterCondition(events: EventCatalogSelection[]): {
  field: string;
  operator: string;
  value: unknown;
  subtitle: string;
} | null {
  const codes = eventCodesFromEvents(events);
  if (!codes.length) return null;
  if (codes.length === 1) {
    return {
      field: 'dimensions.eventCode',
      operator: 'eq',
      value: codes[0],
      subtitle: `eventCode = ${codes[0]}`,
    };
  }
  return {
    field: 'dimensions.eventCode',
    operator: 'in',
    value: codes,
    subtitle: `eventCode in (${codes.length})`,
  };
}

export function metricConditionSpec(metric: SimpleMetricComparison | null): {
  field: string;
  operator: string;
  value: number;
  subtitle: string;
} | null {
  if (!metric?.key?.trim()) return null;
  const threshold = Number(metric.threshold);
  if (!Number.isFinite(threshold)) return null;
  return {
    field: 'value',
    operator: metric.operator,
    value: threshold,
    subtitle: `value ${metric.operator} ${threshold}`,
  };
}

export function managedOsFilterId(sourceNodeId: string): string {
  return `${sourceNodeId}__scope-os`;
}

export function managedHostFilterId(sourceNodeId: string): string {
  return `${sourceNodeId}__scope-host`;
}

export function managedEventCodeFilterId(sourceNodeId: string): string {
  return `${sourceNodeId}__scope-eventcode`;
}

export function managedMetricConditionId(sourceNodeId: string): string {
  return `${sourceNodeId}__scope-metric`;
}
