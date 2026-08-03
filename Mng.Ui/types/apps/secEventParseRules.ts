export type SecEventParseExtractType =
  | 'event_data'
  | 'json_path'
  | 'regex'
  | 'kv'
  | 'constant';

export interface SecEventParseRuleWhen {
  field: string;
  op: string;
  value?: string | null;
  values?: string[] | null;
}

export interface SecEventParseRuleMessagePattern {
  family: string;
}

export interface SecEventParseRuleMatch {
  sourceProduct: string[];
  sourceType?: string[] | null;
  channel?: string[] | null;
  eventIds?: number[] | null;
  when?: SecEventParseRuleWhen[] | null;
  messagePatterns?: SecEventParseRuleMessagePattern[] | null;
}

export interface SecEventParseRuleExtractStep {
  type: string;
  from?: string | null;
  to?: string | null;
  value?: string | null;
  pattern?: string | null;
  groups?: Record<string, string> | null;
}

export interface SecEventParseRuleManageItem {
  id: string;
  ruleId: string;
  name: string;
  description?: string | null;
  enabled: boolean;
  priority: number;
  builtin: boolean;
  version: number;
  match: SecEventParseRuleMatch;
  extract: SecEventParseRuleExtractStep[];
  onConflict: string;
  updatedAtUtc: string;
}

export interface SecEventParseRuleManageListResponse {
  version: string;
  publishedUtc: string | null;
  hasUnpublishedChanges: boolean;
  items: SecEventParseRuleManageItem[];
}

export interface SecEventParseRuleUpsertPayload {
  ruleId: string;
  name: string;
  description?: string | null;
  enabled: boolean;
  priority: number;
  match: SecEventParseRuleMatch;
  extract: SecEventParseRuleExtractStep[];
  onConflict?: string;
}

export interface SecEventParseRulePublishedResponse {
  version: string;
  publishedUtc: string | null;
  rules: SecEventParseRuleManageItem[];
}

export interface SecEventParseRulePreviewRequest {
  ruleId?: string | null;
  /** Unsaved draft — takes precedence over ruleId when set. */
  draftRule?: SecEventParseRuleUpsertPayload | null;
  context: {
    source?: { product?: string; type?: string; host?: string };
    raw?: unknown;
    message?: string;
    channel?: string;
    eventId?: number;
  };
}

export interface SecEventParseRulePreviewResponse {
  matched: boolean;
  ruleId?: string | null;
  fields: Record<string, unknown>;
  notes: string[];
}

export interface SecEventWindowsParseSample {
  id: string;
  timestamp: string;
  host?: string | null;
  channel?: string | null;
  eventId?: number | null;
  provider?: string | null;
  package?: string | null;
  message?: string | null;
  eventDataText?: string | null;
  eventData: Record<string, string>;
  parseModeHint: 'field_map' | 'text' | string;
  raw?: unknown;
  sourceType?: string | null;
  sourceProduct?: string | null;
}

export interface SecEventWindowsParseSampleResponse {
  items: SecEventWindowsParseSample[];
  recentEventIds: number[];
  hours?: number;
  totalHits?: number;
  effectiveHost?: string | null;
  notes?: string[];
}

export interface SecEventLinuxParseSample {
  id: string;
  timestamp: string;
  host?: string | null;
  package?: string | null;
  unit?: string | null;
  channel?: string | null;
  message?: string | null;
  eventAction?: string | null;
  /** Structured journal / fields bag for optional json_path maps. */
  fields: Record<string, string>;
  raw?: unknown;
  sourceType?: string | null;
  sourceProduct?: string | null;
}

export interface SecEventLinuxParseSampleResponse {
  items: SecEventLinuxParseSample[];
  recentPackages: string[];
  hours?: number;
  totalHits?: number;
  effectiveHost?: string | null;
  notes?: string[];
}

/** Shared parse + future smart-query field catalog entry. */
export interface SecEventTargetFieldDefinition {
  name: string;
  label: string;
  group: string;
  valueType: string;
  description?: string | null;
  extractTypes: string[];
  queryOperators: string[];
  queryable: boolean;
  wizardSelectable: boolean;
  isCustom?: boolean;
}

export interface SecEventTargetFieldCatalogResponse {
  version: string;
  fields: SecEventTargetFieldDefinition[];
}

export interface SecEventCustomFieldUpsertPayload {
  name: string;
  label?: string | null;
  valueType?: string | null;
  description?: string | null;
}
