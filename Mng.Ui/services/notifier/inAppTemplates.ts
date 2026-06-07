import { fetchFromDataGateway } from '@/services/apiService';

export const IN_APP_TEMPLATES_DATASET = '@notification_templates';

export type ToastSeverity = 'info' | 'success' | 'warning' | 'error';

export interface InAppNotificationTemplate {
  __dataId: string;
  templateKey: string;
  name: string;
  description?: string | null;
  title?: string | null;
  message?: string | null;
  defaultToastSeverity?: ToastSeverity | null;
  locale?: string | null;
  category: string;
  isActive: boolean;
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

function parseToastSeverity(raw: unknown): ToastSeverity | null {
  const value = raw != null ? String(raw).trim().toLowerCase() : '';
  if (value === 'info' || value === 'success' || value === 'warning' || value === 'error') {
    return value;
  }
  return null;
}

export function mapInAppNotificationTemplate(raw: Record<string, unknown>): InAppNotificationTemplate {
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
    title:
      raw.title != null
        ? String(raw.title)
        : raw.Title != null
          ? String(raw.Title)
          : null,
    message:
      raw.message != null
        ? String(raw.message)
        : raw.Message != null
          ? String(raw.Message)
          : null,
    defaultToastSeverity: parseToastSeverity(raw.defaultToastSeverity ?? raw.DefaultToastSeverity),
    locale:
      raw.locale != null
        ? String(raw.locale).trim() || null
        : raw.Locale != null
          ? String(raw.Locale).trim() || null
          : null,
    category: String(raw.category ?? raw.Category ?? 'custom'),
    isActive: raw.isActive !== false && raw.IsActive !== false,
  };
}

function datasetUrl(suffix = ''): string {
  const base = `/api/v1/data/${encodeURIComponent(IN_APP_TEMPLATES_DATASET)}`;
  return suffix ? `${base}/${encodeURIComponent(suffix)}` : base;
}

export async function listInAppNotificationTemplates(options?: {
  activeOnly?: boolean;
  category?: string;
}): Promise<InAppNotificationTemplate[]> {
  const params = new URLSearchParams({ limit: '200', sort: 'category:asc,templateKey:asc' });
  const filters: string[] = [];
  if (options?.activeOnly) filters.push('isActive:eq:true');
  if (options?.category) filters.push(`category:eq:${options.category}`);
  if (filters.length) params.set('filter', filters.join(','));
  const raw = await fetchFromDataGateway(`${datasetUrl()}?${params.toString()}`, 'GET');
  return parseDgArray(raw)
    .map((r) => mapInAppNotificationTemplate(r))
    .filter((t) => t.__dataId && t.templateKey && t.name);
}

export async function listActiveInAppTemplateOptions(): Promise<{ value: string; title: string }[]> {
  const rows = await listInAppNotificationTemplates({ activeOnly: true });
  return rows.map((t) => ({
    value: t.templateKey,
    title: t.name !== t.templateKey ? `${t.name} (${t.templateKey})` : t.templateKey,
  }));
}
