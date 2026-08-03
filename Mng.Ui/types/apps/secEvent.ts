export interface SecEventListItem {
  id: string;
  timestamp: string;
  ingestedAt: string;
  sourceType?: string | null;
  sourceProduct?: string | null;
  sourceHost?: string | null;
  eventAction: string;
  eventOutcome?: string | null;
  eventCode?: string | null;
  actorUser?: string | null;
  networkSrcIp?: string | null;
  networkDstIp?: string | null;
  parserId?: string | null;
  rawPreview?: string | null;
  /** Tam ham mesaj — yalnızca GET /sec-events/{id} yanıtında */
  raw?: string | null;
  /** U7: baseline sonrası ilk kez görülen src→dst çifti */
  baselineNewFlowPair?: boolean;
  /** Collector metric enrichment (host.up fields, etc.) */
  fields?: Record<string, unknown> | null;
}

export interface SecEventQueryResponse {
  items: SecEventListItem[];
  total: number;
}

export interface SecEventQuery {
  from?: string;
  to?: string;
  sourceType?: string;
  /** Exact match on source.product */
  sourceProduct?: string;
  eventAction?: string;
  /** Comma-separated event.action OR list (ignored when eventAction is set). */
  eventActions?: string;
  /** Prefix match on event.action (e.g. rdp.). Ignored when eventAction/eventActions set. */
  eventActionPrefix?: string;
  eventOutcome?: string;
  srcIp?: string;
  dstIp?: string;
  dstPort?: string;
  actorUser?: string;
  sourceHost?: string;
  /** Comma-separated source.host OR list (ignored when sourceHost is set). */
  sourceHosts?: string;
  eventCode?: string;
  /** Comma-separated event.code OR list (ignored when eventCode is set). */
  eventCodes?: string;
  search?: string;
  excludeUnknown?: boolean;
  skip?: number;
  limit?: number;
}

export type SecEventTimeRange = '1h' | '24h' | '7d';

export type SecEventRangeMode = 'preset' | 'custom';

export interface SecEventHourlyBucket {
  hourStart: string;
  count: number;
}

export interface SecEventDashboardSummary {
  range: string;
  from: string;
  to: string;
  eventsTotal: number;
  byAction: Record<string, number>;
  hourly: SecEventHourlyBucket[];
}
