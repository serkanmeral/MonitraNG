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
  skip?: number;
  limit?: number;
}

export type SecEventTimeRange = '1h' | '24h' | '7d';
