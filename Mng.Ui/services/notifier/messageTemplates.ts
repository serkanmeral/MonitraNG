import { fetchFromDataGateway } from '@/services/apiService';
import type { MessageTemplate } from '@/types/apps/messageTemplates';

export const MESSAGE_TEMPLATES_DATASET = '@message_templates';

export const MESSAGE_TEMPLATE_DEFAULT_PAGE_SIZE = 25;
export const MESSAGE_TEMPLATE_PAGE_SIZE_OPTIONS = [10, 25, 50, 100] as const;

export interface MessageTemplateListQuery {
  skip?: number;
  limit?: number;
  activeOnly?: boolean;
  category?: string | null;
  search?: string;
}

export interface MessageTemplateListResult {
  items: MessageTemplate[];
  total: number;
}

function parseDgArray(response: unknown): Record<string, unknown>[] {
  if (Array.isArray(response)) return response as Record<string, unknown>[];
  if (response && typeof response === 'object') {
    const obj = response as Record<string, unknown>;
    if (Array.isArray(obj.items)) return obj.items as Record<string, unknown>[];
    if (Array.isArray(obj.data)) return obj.data as Record<string, unknown>[];
  }
  return [];
}

function parseStringArray(raw: unknown): string[] {
  if (raw == null) return [];
  if (Array.isArray(raw)) return raw.map((v) => String(v).trim()).filter(Boolean);
  return [];
}

export function mapMessageTemplate(raw: Record<string, unknown>): MessageTemplate {
  const sample = raw.sampleContext ?? raw.SampleContext;
  let sampleContext: Record<string, unknown> | null = null;
  if (sample && typeof sample === 'object' && !Array.isArray(sample)) {
    sampleContext = sample as Record<string, unknown>;
  }

  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    templateKey: String(raw.templateKey ?? raw.TemplateKey ?? '').trim(),
    name: String(raw.name ?? raw.Name ?? '').trim(),
    description:
      raw.description != null
        ? String(raw.description)
        : raw.Description != null
          ? String(raw.Description)
          : null,
    channel: String(raw.channel ?? raw.Channel ?? 'telegram').trim() || 'telegram',
    bodyText: String(raw.bodyText ?? raw.BodyText ?? ''),
    parseMode:
      raw.parseMode != null
        ? String(raw.parseMode).trim() || null
        : raw.ParseMode != null
          ? String(raw.ParseMode).trim() || null
          : null,
    variables: parseStringArray(raw.variables ?? raw.Variables),
    locale:
      raw.locale != null
        ? String(raw.locale).trim() || null
        : raw.Locale != null
          ? String(raw.Locale).trim() || null
          : null,
    category: String(raw.category ?? raw.Category ?? 'custom'),
    tags: parseStringArray(raw.tags ?? raw.Tags),
    sampleContext,
    isActive: raw.isActive !== false && raw.IsActive !== false,
  };
}

function datasetUrl(suffix = ''): string {
  const base = `/api/v1/data/${encodeURIComponent(MESSAGE_TEMPLATES_DATASET)}`;
  return suffix ? `${base}/${encodeURIComponent(suffix)}` : base;
}

function readListTotal(response: unknown, items: unknown[]): number {
  if (Array.isArray(response)) {
    const arr = response as unknown[] & { _totalCount?: number };
    if (typeof arr._totalCount === 'number' && Number.isFinite(arr._totalCount)) {
      return arr._totalCount;
    }
  }
  if (response && typeof response === 'object' && !Array.isArray(response)) {
    const obj = response as Record<string, unknown>;
    const totalRaw = obj.total ?? obj.totalCount ?? obj.count ?? obj.TotalCount;
    if (typeof totalRaw === 'number' && Number.isFinite(totalRaw)) {
      return totalRaw;
    }
  }
  return items.length;
}

function mapRows(raw: unknown): MessageTemplate[] {
  return parseDgArray(raw)
    .map((r) => mapMessageTemplate(r))
    .filter((t) => t.__dataId && t.templateKey && t.name);
}

export async function listMessageTemplatesPage(
  query: MessageTemplateListQuery = {}
): Promise<MessageTemplateListResult> {
  const skip = Math.max(0, query.skip ?? 0);
  const limit = Math.min(1000, Math.max(1, query.limit ?? MESSAGE_TEMPLATE_DEFAULT_PAGE_SIZE));
  const params = new URLSearchParams({
    skip: String(skip),
    limit: String(limit),
    sort: 'category:asc,templateKey:asc',
  });
  const filters: string[] = [];
  if (query.activeOnly) filters.push('isActive:eq:true');
  if (query.category) filters.push(`category:eq:${query.category}`);
  if (filters.length) params.set('filter', filters.join(','));
  const search = query.search?.trim();
  if (search) params.set('search', search);

  const raw = await fetchFromDataGateway(`${datasetUrl()}?${params.toString()}`, 'GET');
  const items = mapRows(raw);
  return { items, total: readListTotal(raw, items) };
}

export async function listMessageTemplates(options?: {
  activeOnly?: boolean;
  category?: string;
}): Promise<MessageTemplate[]> {
  const params = new URLSearchParams({ limit: '500', sort: 'category:asc,templateKey:asc' });
  const filters: string[] = [];
  if (options?.activeOnly) filters.push('isActive:eq:true');
  if (options?.category) filters.push(`category:eq:${options.category}`);
  if (filters.length) params.set('filter', filters.join(','));
  const raw = await fetchFromDataGateway(`${datasetUrl()}?${params.toString()}`, 'GET');
  return mapRows(raw);
}

export async function listMessageTemplateCategoryOptions(): Promise<string[]> {
  const rows = await listMessageTemplates();
  const categories = new Set<string>(['custom', 'system']);
  for (const row of rows) {
    const category = row.category?.trim();
    if (category) categories.add(category);
  }
  return [...categories].sort((a, b) => a.localeCompare(b, 'tr'));
}

export async function createMessageTemplate(payload: Record<string, unknown>): Promise<string | null> {
  const raw = await fetchFromDataGateway(datasetUrl(), 'POST', payload);
  if (raw && typeof raw === 'object') {
    const obj = raw as Record<string, unknown>;
    const id = String(obj.__dataId ?? obj.dataId ?? '');
    if (id) return id;
    const nested = obj.data as Record<string, unknown> | undefined;
    if (nested?.__dataId) return String(nested.__dataId);
  }
  return null;
}

export async function updateMessageTemplate(templateId: string, payload: Record<string, unknown>) {
  await fetchFromDataGateway(datasetUrl(templateId), 'PUT', payload);
}

export async function deleteMessageTemplate(templateId: string) {
  await fetchFromDataGateway(datasetUrl(templateId), 'DELETE');
}
