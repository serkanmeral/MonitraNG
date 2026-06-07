import { fetchFromDataGateway, getAccessToken } from '@/services/apiService';
import type { MailTemplate, MailTemplatePreviewResult } from '@/types/apps/mailTemplates';
import { useAuthStore } from '@/stores/auth';

export const MAIL_TEMPLATES_DATASET = '@mail_templates';

export interface MailTemplatePreviewOptions {
  subject?: string | null;
  bodyHtmlOverride?: string | null;
  layoutKeyOverride?: string | null;
  localeOverride?: string | null;
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

function extractNotifierError(error: unknown): string {
  if (error && typeof error === 'object') {
    const e = error as Record<string, unknown>;
    const data = e.data;
    if (data && typeof data === 'object' && data !== null && 'error' in data) {
      const msg = (data as { error?: unknown }).error;
      if (msg != null && String(msg).trim()) return String(msg);
    }
    if (e.statusMessage && String(e.statusMessage).trim()) return String(e.statusMessage);
    if (e.message && String(e.message).trim()) return String(e.message);
  }
  return '';
}

async function notifierFetch<T>(path: string, method: 'POST' = 'POST', body?: unknown): Promise<T> {
  const auth = useAuthStore();
  await auth.ensureValidToken();
  const token = getAccessToken();
  if (!token) throw new Error('Access token bulunamadı');

  const clean = path.replace(/^\/+/, '');
  try {
    return await $fetch<T>(`/api/notifier/${clean}`, {
      method,
      headers: { Authorization: `Bearer ${token}` },
      ...(body != null && { body }),
    });
  } catch (error: unknown) {
    const msg = extractNotifierError(error);
    throw new Error(msg || 'MngNotifier isteği başarısız');
  }
}

export function mapMailTemplate(raw: Record<string, unknown>): MailTemplate {
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
    subject: String(raw.subject ?? raw.Subject ?? ''),
    bodyHtml: String(raw.bodyHtml ?? raw.BodyHtml ?? ''),
    variables: parseStringArray(raw.variables ?? raw.Variables),
    layoutKey:
      raw.layoutKey != null
        ? String(raw.layoutKey).trim() || null
        : raw.LayoutKey != null
          ? String(raw.LayoutKey).trim() || null
          : null,
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
  const base = `/api/v1/data/${encodeURIComponent(MAIL_TEMPLATES_DATASET)}`;
  return suffix ? `${base}/${encodeURIComponent(suffix)}` : base;
}

export async function listMailTemplates(options?: {
  activeOnly?: boolean;
  category?: string;
}): Promise<MailTemplate[]> {
  const params = new URLSearchParams({ limit: '200', sort: 'category:asc,templateKey:asc' });
  const filters: string[] = [];
  if (options?.activeOnly) filters.push('isActive:eq:true');
  if (options?.category) filters.push(`category:eq:${options.category}`);
  if (filters.length) params.set('filter', filters.join(','));
  const raw = await fetchFromDataGateway(`${datasetUrl()}?${params.toString()}`, 'GET');
  return parseDgArray(raw)
    .map((r) => mapMailTemplate(r))
    .filter((t) => t.__dataId && t.templateKey && t.name);
}

export async function listActiveMailTemplateOptions(): Promise<{ value: string; title: string }[]> {
  const rows = await listMailTemplates({ activeOnly: true });
  return rows.map((t) => ({
    value: t.templateKey,
    title: t.name !== t.templateKey ? `${t.name} (${t.templateKey})` : t.templateKey,
  }));
}

export async function createMailTemplate(payload: Record<string, unknown>): Promise<string | null> {
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

export async function updateMailTemplate(templateId: string, payload: Record<string, unknown>) {
  await fetchFromDataGateway(datasetUrl(templateId), 'PUT', payload);
}

export async function deleteMailTemplate(templateId: string) {
  await fetchFromDataGateway(datasetUrl(templateId), 'DELETE');
}

export async function previewMailTemplate(
  templateKey: string,
  context: Record<string, unknown>,
  options?: MailTemplatePreviewOptions
): Promise<MailTemplatePreviewResult> {
  const raw = await notifierFetch<Record<string, unknown>>(
    'v1/notifications/preview-template',
    'POST',
    {
      templateKey: templateKey.trim(),
      subject: options?.subject?.trim() || null,
      context,
      bodyHtmlOverride: options?.bodyHtmlOverride?.trim() || null,
      layoutKeyOverride: options?.layoutKeyOverride?.trim() || null,
      localeOverride: options?.localeOverride?.trim() || null,
    }
  );
  return {
    templateKey: String(raw.templateKey ?? raw.TemplateKey ?? templateKey),
    layoutKey: raw.layoutKey != null ? String(raw.layoutKey) : raw.LayoutKey != null ? String(raw.LayoutKey) : null,
    subject: String(raw.subject ?? raw.Subject ?? ''),
    htmlBody: String(raw.htmlBody ?? raw.HtmlBody ?? ''),
  };
}
