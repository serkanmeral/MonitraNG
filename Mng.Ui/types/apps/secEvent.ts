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
}

export interface SecEventQueryResponse {
  items: SecEventListItem[];
  total: number;
}

export interface SecEventQuery {
  from?: string;
  to?: string;
  sourceType?: string;
  eventAction?: string;
  srcIp?: string;
  actorUser?: string;
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
