import type {
  SecEventParseRuleExtractStep,
  SecEventParseRuleManageItem,
  SecEventParseRuleManageListResponse,
  SecEventParseRuleMatch,
  SecEventParseRulePreviewRequest,
  SecEventParseRulePreviewResponse,
  SecEventParseRulePublishedResponse,
  SecEventParseRuleUpsertPayload,
  SecEventParseRuleWhen,
  SecEventLinuxParseSample,
  SecEventLinuxParseSampleResponse,
  SecEventWindowsParseSample,
  SecEventWindowsParseSampleResponse,
  SecEventTargetFieldCatalogResponse,
  SecEventTargetFieldDefinition,
} from '@/types/apps/secEventParseRules'
import { getAccessToken } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';

const BASE = '/api/reactor/v1/sec-events/parse-rules';

async function authHeaders(): Promise<Record<string, string>> {
  const authStore = useAuthStore();
  try {
    await authStore.ensureValidToken();
  } catch {
    // BFF returns 401 if cookie/token missing.
  }

  const headers: Record<string, string> = {};
  if (authStore.domainName) {
    headers['X-Domain-Name'] = authStore.domainName;
  }
  const token = getAccessToken();
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return headers;
}

function asStringList(raw: unknown): string[] {
  if (!Array.isArray(raw)) return [];
  return raw.map((x) => String(x ?? '').trim()).filter(Boolean);
}

function asNumberList(raw: unknown): number[] | null {
  if (!Array.isArray(raw)) return null;
  const nums = raw.map((n) => Number(n)).filter((n) => Number.isFinite(n));
  return nums.length ? nums : null;
}

function normalizeWhen(raw: unknown): SecEventParseRuleWhen[] | null {
  if (!Array.isArray(raw) || raw.length === 0) return null;
  return raw.map((row) => {
    const r = row as Record<string, unknown>;
    const valuesRaw = r.values ?? r.Values;
    return {
      field: String(r.field ?? r.Field ?? ''),
      op: String(r.op ?? r.Op ?? 'eq'),
      value: (r.value ?? r.Value) != null ? String(r.value ?? r.Value) : null,
      values: Array.isArray(valuesRaw) ? valuesRaw.map((v) => String(v)) : null,
    };
  });
}

function normalizeMatch(raw: unknown): SecEventParseRuleMatch {
  const r = (raw && typeof raw === 'object' ? raw : {}) as Record<string, unknown>;
  const patternsRaw = (r.messagePatterns ?? r.MessagePatterns) as unknown;
  return {
    sourceProduct: asStringList(r.sourceProduct ?? r.SourceProduct),
    sourceType: asStringList(r.sourceType ?? r.SourceType) || null,
    channel: asStringList(r.channel ?? r.Channel) || null,
    eventIds: asNumberList(r.eventIds ?? r.EventIds),
    when: normalizeWhen(r.when ?? r.When),
    messagePatterns: Array.isArray(patternsRaw)
      ? patternsRaw.map((p) => {
          const row = p as Record<string, unknown>;
          return { family: String(row.family ?? row.Family ?? '') };
        })
      : null,
  };
}

function normalizeExtract(raw: unknown): SecEventParseRuleExtractStep[] {
  if (!Array.isArray(raw)) return [];
  return raw.map((row) => {
    const r = row as Record<string, unknown>;
    const groupsRaw = r.groups ?? r.Groups;
    let groups: Record<string, string> | null = null;
    if (groupsRaw && typeof groupsRaw === 'object' && !Array.isArray(groupsRaw)) {
      groups = {};
      for (const [k, v] of Object.entries(groupsRaw as Record<string, unknown>)) {
        groups[String(k)] = String(v ?? '');
      }
    }
    return {
      type: String(r.type ?? r.Type ?? ''),
      from: (r.from ?? r.From) != null ? String(r.from ?? r.From) : null,
      to: (r.to ?? r.To) != null ? String(r.to ?? r.To) : null,
      value: (r.value ?? r.Value) != null ? String(r.value ?? r.Value) : null,
      pattern: (r.pattern ?? r.Pattern) != null ? String(r.pattern ?? r.Pattern) : null,
      groups,
    };
  });
}

function normalizeItem(raw: Record<string, unknown>): SecEventParseRuleManageItem {
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    ruleId: String(raw.ruleId ?? raw.RuleId ?? ''),
    name: String(raw.name ?? raw.Name ?? ''),
    description: (raw.description ?? raw.Description) != null
      ? String(raw.description ?? raw.Description)
      : null,
    enabled: Boolean(raw.enabled ?? raw.Enabled),
    priority: Number(raw.priority ?? raw.Priority ?? 100),
    builtin: Boolean(raw.builtin ?? raw.Builtin),
    version: Number(raw.version ?? raw.Version ?? 1),
    match: normalizeMatch(raw.match ?? raw.Match),
    extract: normalizeExtract(raw.extract ?? raw.Extract),
    onConflict: String(raw.onConflict ?? raw.OnConflict ?? 'first_wins'),
    updatedAtUtc: String(raw.updatedAtUtc ?? raw.UpdatedAtUtc ?? ''),
  };
}

function normalizeManageList(raw: Record<string, unknown>): SecEventParseRuleManageListResponse {
  const itemsRaw = (raw.items ?? raw.Items ?? []) as Record<string, unknown>[];
  return {
    version: String(raw.version ?? raw.Version ?? ''),
    publishedUtc: (raw.publishedUtc ?? raw.PublishedUtc)
      ? String(raw.publishedUtc ?? raw.PublishedUtc)
      : null,
    hasUnpublishedChanges: Boolean(raw.hasUnpublishedChanges ?? raw.HasUnpublishedChanges),
    items: Array.isArray(itemsRaw) ? itemsRaw.map(normalizeItem) : [],
  };
}

function apiErrorMessage(e: unknown): string {
  if (e && typeof e === 'object') {
    const data = (e as { data?: { message?: string; Message?: string } }).data;
    const msg = data?.message ?? data?.Message;
    if (msg) return String(msg);
    const statusMessage = (e as { statusMessage?: string }).statusMessage;
    if (statusMessage) return statusMessage;
  }
  return e instanceof Error ? e.message : String(e);
}

export async function fetchSecEventParseRuleManageList(): Promise<SecEventParseRuleManageListResponse> {
  const raw = await $fetch<Record<string, unknown>>(`${BASE}/manage`, {
    headers: await authHeaders(),
  });
  return normalizeManageList(raw);
}

/** Canonical target-field catalog for parse wizard + future smart query. */
export async function fetchSecEventTargetFields(): Promise<SecEventTargetFieldCatalogResponse> {
  const raw = await $fetch<Record<string, unknown>>(`${BASE}/target-fields`, {
    headers: await authHeaders(),
  });
  const fieldsRaw = (raw.fields ?? raw.Fields ?? []) as Record<string, unknown>[];
  const fields: SecEventTargetFieldDefinition[] = fieldsRaw.map((row) => ({
    name: String(row.name ?? row.Name ?? ''),
    label: String(row.label ?? row.Label ?? row.name ?? row.Name ?? ''),
    group: String(row.group ?? row.Group ?? ''),
    valueType: String(row.valueType ?? row.ValueType ?? 'keyword'),
    description:
      (row.description ?? row.Description) != null
        ? String(row.description ?? row.Description)
        : null,
    extractTypes: asStringList(row.extractTypes ?? row.ExtractTypes),
    queryOperators: asStringList(row.queryOperators ?? row.QueryOperators),
    queryable: Boolean(row.queryable ?? row.Queryable ?? true),
    wizardSelectable: Boolean(row.wizardSelectable ?? row.WizardSelectable ?? true),
    isCustom: Boolean(row.isCustom ?? row.IsCustom ?? false),
  })).filter((f) => f.name);

  return {
    version: String(raw.version ?? raw.Version ?? ''),
    fields,
  };
}

export async function upsertSecEventCustomField(
  payload: {
    name: string;
    label?: string | null;
    valueType?: string | null;
    description?: string | null;
  },
): Promise<SecEventTargetFieldDefinition> {
  try {
    const raw = await $fetch<Record<string, unknown>>(`${BASE}/target-fields/custom`, {
      method: 'POST',
      headers: await authHeaders(),
      body: payload,
    });
    return {
      name: String(raw.name ?? raw.Name ?? ''),
      label: String(raw.label ?? raw.Label ?? ''),
      group: String(raw.group ?? raw.Group ?? 'custom'),
      valueType: String(raw.valueType ?? raw.ValueType ?? 'keyword'),
      description:
        (raw.description ?? raw.Description) != null
          ? String(raw.description ?? raw.Description)
          : null,
      extractTypes: asStringList(raw.extractTypes ?? raw.ExtractTypes),
      queryOperators: asStringList(raw.queryOperators ?? raw.QueryOperators),
      queryable: Boolean(raw.queryable ?? raw.Queryable ?? true),
      wizardSelectable: Boolean(raw.wizardSelectable ?? raw.WizardSelectable ?? true),
      isCustom: true,
    };
  } catch (e) {
    throw new Error(apiErrorMessage(e));
  }
}

export async function deleteSecEventCustomField(name: string): Promise<void> {
  try {
    await $fetch(`${BASE}/target-fields/custom/${encodeURIComponent(name)}`, {
      method: 'DELETE',
      headers: await authHeaders(),
    });
  } catch (e) {
    throw new Error(apiErrorMessage(e));
  }
}

/** Normalize bare slug or custom.* to custom.<slug> (client-side mirror of Reactor). */
export function normalizeCustomTargetField(raw: string): string {
  let s = String(raw ?? '').trim().toLowerCase().replace(/-/g, '_');
  if (s.startsWith('custom.')) s = s.slice('custom.'.length);
  s = s.replace(/[^a-z0-9_]/g, '').replace(/_+/g, '_').replace(/^_|_$/g, '');
  if (!s || !/^[a-z]/.test(s)) {
    throw new Error('Custom field slug must start with a letter (e.g. session_id).');
  }
  if (s.length > 64) throw new Error('Custom field slug must be at most 64 characters.');
  return `custom.${s}`;
}

export async function createSecEventParseRule(
  payload: SecEventParseRuleUpsertPayload,
): Promise<SecEventParseRuleManageItem> {
  try {
    const raw = await $fetch<Record<string, unknown>>(BASE, {
      method: 'POST',
      headers: await authHeaders(),
      body: payload,
    });
    return normalizeItem(raw);
  } catch (e) {
    throw new Error(apiErrorMessage(e));
  }
}

export async function updateSecEventParseRule(
  ruleId: string,
  payload: SecEventParseRuleUpsertPayload,
): Promise<SecEventParseRuleManageItem> {
  try {
    const raw = await $fetch<Record<string, unknown>>(
      `${BASE}/${encodeURIComponent(ruleId)}`,
      {
        method: 'PUT',
        headers: await authHeaders(),
        body: payload,
      },
    );
    return normalizeItem(raw);
  } catch (e) {
    throw new Error(apiErrorMessage(e));
  }
}

export async function deleteSecEventParseRule(ruleId: string): Promise<void> {
  try {
    await $fetch(`${BASE}/${encodeURIComponent(ruleId)}`, {
      method: 'DELETE',
      headers: await authHeaders(),
    });
  } catch (e) {
    throw new Error(apiErrorMessage(e));
  }
}

export async function publishSecEventParseRuleCatalog(): Promise<SecEventParseRulePublishedResponse> {
  const raw = await $fetch<Record<string, unknown>>(`${BASE}/publish`, {
    method: 'POST',
    headers: await authHeaders(),
  });
  const rulesRaw = (raw.rules ?? raw.Rules ?? []) as Record<string, unknown>[];
  return {
    version: String(raw.version ?? raw.Version ?? ''),
    publishedUtc: (raw.publishedUtc ?? raw.PublishedUtc)
      ? String(raw.publishedUtc ?? raw.PublishedUtc)
      : null,
    rules: Array.isArray(rulesRaw) ? rulesRaw.map(normalizeItem) : [],
  };
}

export async function previewSecEventParseRule(
  request: SecEventParseRulePreviewRequest,
): Promise<SecEventParseRulePreviewResponse> {
  try {
    const raw = await $fetch<Record<string, unknown>>(`${BASE}/preview`, {
      method: 'POST',
      headers: await authHeaders(),
      body: request,
    });
    const fieldsRaw = (raw.fields ?? raw.Fields ?? {}) as Record<string, unknown>;
    const notesRaw = (raw.notes ?? raw.Notes ?? []) as unknown[];
    return {
      matched: Boolean(raw.matched ?? raw.Matched),
      ruleId: (raw.ruleId ?? raw.RuleId) != null ? String(raw.ruleId ?? raw.RuleId) : null,
      fields: fieldsRaw && typeof fieldsRaw === 'object' ? fieldsRaw : {},
      notes: Array.isArray(notesRaw) ? notesRaw.map((n) => String(n)) : [],
    };
  } catch (e) {
    throw new Error(apiErrorMessage(e));
  }
}

function coerceEventDataObject(value: unknown): Record<string, unknown> | null {
  if (!value) return null;
  if (typeof value === 'string') {
    const trimmed = value.trim();
    if (!trimmed.startsWith('{')) return null;
    try {
      const parsed = JSON.parse(trimmed) as unknown;
      return parsed && typeof parsed === 'object' && !Array.isArray(parsed)
        ? (parsed as Record<string, unknown>)
        : null;
    } catch {
      return null;
    }
  }
  if (typeof value === 'object' && !Array.isArray(value)) {
    return value as Record<string, unknown>;
  }
  return null;
}

function asStringRecord(value: unknown): Record<string, string> {
  const obj = coerceEventDataObject(value);
  const out: Record<string, string> = {};
  if (!obj) return out;
  for (const [k, v] of Object.entries(obj)) {
    if (v == null) continue;
    if (typeof v === 'object') {
      // Keep simple nested values as JSON string for display/mapping.
      try {
        out[String(k)] = JSON.stringify(v);
      } catch {
        /* skip */
      }
      continue;
    }
    out[String(k)] = String(v);
  }
  return out;
}

/** Prefer DTO eventData; fall back to raw.eventData / fields.eventData / message labels. */
function collectEventDataRecord(
  row: Record<string, unknown>,
  raw: Record<string, unknown> | null,
): Record<string, string> {
  const merged: Record<string, string> = {
    ...asStringRecord(row.eventData ?? row.EventData),
  };
  if (raw && typeof raw === 'object') {
    Object.assign(merged, asStringRecord(raw.eventData ?? raw.EventData));
    const fields = raw.fields;
    if (fields && typeof fields === 'object' && !Array.isArray(fields)) {
      const f = fields as Record<string, unknown>;
      Object.assign(merged, asStringRecord(f.eventData ?? f.EventData));
    }
  }
  // Always merge message-derived labels (does not overwrite existing keys).
  const message = String(row.message ?? row.Message ?? raw?.message ?? '');
  for (const [k, v] of Object.entries(deriveEventDataFromMessage(message))) {
    if (!merged[k]) merged[k] = v;
  }
  return merged;
}

/** RDP / LocalSessionManager style message lines → synthetic EventData keys. */
export function deriveEventDataFromMessage(message: string): Record<string, string> {
  const out: Record<string, string> = {};
  if (!message?.trim()) return out;
  const patterns: Array<[RegExp, string]> = [
    [/^\s*User:\s*(.+?)\s*$/im, 'User'],
    [/^\s*Session ID:\s*(.+?)\s*$/im, 'SessionID'],
    [/^\s*Source Network Address:\s*(.+?)\s*$/im, 'Address'],
  ];
  for (const [re, key] of patterns) {
    const m = message.match(re);
    if (m?.[1]) out[key] = m[1].trim();
  }
  return out;
}

export async function fetchWindowsParseSamples(params: {
  channel?: string;
  eventId?: number;
  host?: string;
  limit?: number;
  hours?: number;
}): Promise<SecEventWindowsParseSampleResponse> {
  try {
    const raw = await $fetch<Record<string, unknown>>(
      '/api/reactor/v1/sec-events/parse-samples/windows',
      {
        headers: await authHeaders(),
        query: {
          channel: params.channel || undefined,
          eventId: params.eventId || undefined,
          host: params.host || undefined,
          limit: params.limit ?? 1,
          hours: params.hours ?? 168,
        },
      },
    );
    const itemsRaw = (raw.items ?? raw.Items ?? []) as Record<string, unknown>[];
    const idsRaw = (raw.recentEventIds ?? raw.RecentEventIds ?? []) as unknown[];
    const items: SecEventWindowsParseSample[] = itemsRaw.map((row) => {
      const raw = (row.raw ?? row.Raw ?? null) as Record<string, unknown> | null;
      const eventData = collectEventDataRecord(row, raw);
      const hintRaw = String(row.parseModeHint ?? row.ParseModeHint ?? '');
      const named = Object.keys(eventData).filter(
        (k) => !k.startsWith('Data_') && !k.toLowerCase().startsWith('param'),
      );
      return {
        id: String(row.id ?? row.Id ?? ''),
        timestamp: String(row.timestamp ?? row.Timestamp ?? ''),
        host: (row.host ?? row.Host) != null ? String(row.host ?? row.Host) : null,
        channel: (row.channel ?? row.Channel) != null ? String(row.channel ?? row.Channel) : null,
        eventId: Number.isFinite(Number(row.eventId ?? row.EventId))
          ? Number(row.eventId ?? row.EventId)
          : null,
        provider: (row.provider ?? row.Provider) != null ? String(row.provider ?? row.Provider) : null,
        package: (row.package ?? row.Package) != null ? String(row.package ?? row.Package) : null,
        message: (row.message ?? row.Message) != null ? String(row.message ?? row.Message) : null,
        eventDataText: (row.eventDataText ?? row.EventDataText) != null
          ? String(row.eventDataText ?? row.EventDataText)
          : null,
        eventData,
        parseModeHint: hintRaw || (named.length > 0 ? 'field_map' : 'text'),
        raw,
        sourceType: (row.sourceType ?? row.SourceType) != null
          ? String(row.sourceType ?? row.SourceType)
          : null,
        sourceProduct: (row.sourceProduct ?? row.SourceProduct) != null
          ? String(row.sourceProduct ?? row.SourceProduct)
          : null,
      };
    });
    const notesRaw = (raw.notes ?? raw.Notes ?? []) as unknown[];
    return {
      items,
      recentEventIds: idsRaw.map((n) => Number(n)).filter((n) => Number.isFinite(n)),
      hours: Number.isFinite(Number(raw.hours ?? raw.Hours)) ? Number(raw.hours ?? raw.Hours) : undefined,
      totalHits: Number.isFinite(Number(raw.totalHits ?? raw.TotalHits))
        ? Number(raw.totalHits ?? raw.TotalHits)
        : undefined,
      effectiveHost: (raw.effectiveHost ?? raw.EffectiveHost) != null
        ? String(raw.effectiveHost ?? raw.EffectiveHost)
        : null,
      notes: notesRaw.map((n) => String(n)).filter((n) => n.length > 0),
    };
  } catch (e) {
    throw new Error(apiErrorMessage(e));
  }
}

export async function fetchLinuxParseSamples(params: {
  package?: string;
  query?: string;
  host?: string;
  limit?: number;
  hours?: number;
}): Promise<SecEventLinuxParseSampleResponse> {
  try {
    const raw = await $fetch<Record<string, unknown>>(
      '/api/reactor/v1/sec-events/parse-samples/linux',
      {
        headers: await authHeaders(),
        query: {
          package: params.package || undefined,
          query: params.query || undefined,
          host: params.host || undefined,
          limit: params.limit ?? 1,
          hours: params.hours ?? 168,
        },
      },
    );
    const itemsRaw = (raw.items ?? raw.Items ?? []) as Record<string, unknown>[];
    const packagesRaw = (raw.recentPackages ?? raw.RecentPackages ?? []) as unknown[];
    const items: SecEventLinuxParseSample[] = itemsRaw.map((row) => {
      const fields = asStringRecord(row.fields ?? row.Fields);
      return {
        id: String(row.id ?? row.Id ?? ''),
        timestamp: String(row.timestamp ?? row.Timestamp ?? ''),
        host: (row.host ?? row.Host) != null ? String(row.host ?? row.Host) : null,
        package: (row.package ?? row.Package) != null ? String(row.package ?? row.Package) : null,
        unit: (row.unit ?? row.Unit) != null ? String(row.unit ?? row.Unit) : null,
        channel: (row.channel ?? row.Channel) != null ? String(row.channel ?? row.Channel) : null,
        message: (row.message ?? row.Message) != null ? String(row.message ?? row.Message) : null,
        eventAction: (row.eventAction ?? row.EventAction) != null
          ? String(row.eventAction ?? row.EventAction)
          : null,
        fields,
        raw: row.raw ?? row.Raw ?? null,
        sourceType: (row.sourceType ?? row.SourceType) != null
          ? String(row.sourceType ?? row.SourceType)
          : null,
        sourceProduct: (row.sourceProduct ?? row.SourceProduct) != null
          ? String(row.sourceProduct ?? row.SourceProduct)
          : null,
      };
    });
    const notesRaw = (raw.notes ?? raw.Notes ?? []) as unknown[];
    return {
      items,
      recentPackages: packagesRaw.map((p) => String(p ?? '').trim()).filter(Boolean),
      hours: Number.isFinite(Number(raw.hours ?? raw.Hours)) ? Number(raw.hours ?? raw.Hours) : undefined,
      totalHits: Number.isFinite(Number(raw.totalHits ?? raw.TotalHits))
        ? Number(raw.totalHits ?? raw.TotalHits)
        : undefined,
      effectiveHost: (raw.effectiveHost ?? raw.EffectiveHost) != null
        ? String(raw.effectiveHost ?? raw.EffectiveHost)
        : null,
      notes: notesRaw.map((n) => String(n)).filter((n) => n.length > 0),
    };
  } catch (e) {
    throw new Error(apiErrorMessage(e));
  }
}
