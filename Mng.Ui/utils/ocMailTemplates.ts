import type { MailTemplate } from '@/types/apps/mailTemplates';

const PLACEHOLDER_RE = /\{\{\s*([^{}]+?)\s*\}\}/g;

export interface OcMailTemplateDraft {
  id?: string;
  templateKey: string;
  name: string;
  description: string;
  subject: string;
  bodyHtml: string;
  variables: string[];
  layoutKey: string;
  locale: string;
  category: string;
  tags: string[];
  sampleContextJson: string;
  isActive: boolean;
}

export function extractPlaceholderPaths(...sources: string[]): string[] {
  const found = new Set<string>();
  for (const src of sources) {
    if (!src) continue;
    for (const match of src.matchAll(PLACEHOLDER_RE)) {
      const raw = match[1]?.trim();
      if (!raw) continue;
      const path = raw.split('|')[0]?.trim();
      if (path) found.add(path);
    }
  }
  return [...found].sort((a, b) => a.localeCompare(b, 'tr'));
}

export function newMailTemplateDraft(seed?: Partial<OcMailTemplateDraft>): OcMailTemplateDraft {
  return {
    templateKey: '',
    name: '',
    description: '',
    subject: '',
    bodyHtml: '',
    variables: [],
    layoutKey: 'default',
    locale: 'tr',
    category: 'custom',
    tags: [],
    sampleContextJson: '{}',
    isActive: true,
    ...seed,
  };
}

export function parseMailTemplateToDraft(template: MailTemplate): OcMailTemplateDraft {
  return {
    id: template.__dataId,
    templateKey: template.templateKey,
    name: template.name,
    description: template.description ?? '',
    subject: template.subject,
    bodyHtml: template.bodyHtml,
    variables: template.variables?.length ? [...template.variables] : [],
    layoutKey: template.layoutKey ?? 'default',
    locale: template.locale ?? 'tr',
    category: template.category ?? 'custom',
    tags: template.tags?.length ? [...template.tags] : [],
    sampleContextJson: template.sampleContext
      ? JSON.stringify(template.sampleContext, null, 2)
      : '{}',
    isActive: template.isActive !== false,
  };
}

export function validateMailTemplateDraft(draft: OcMailTemplateDraft, isEdit: boolean): string | null {
  if (!isEdit && !draft.templateKey.trim()) return 'templateKey';
  if (!draft.name.trim()) return 'name';
  if (!draft.subject.trim()) return 'subject';
  if (!draft.bodyHtml.trim()) return 'bodyHtml';
  return null;
}

export function parseSampleContextJson(json: string): Record<string, unknown> | null {
  const trimmed = json.trim();
  if (!trimmed) return {};
  try {
    const parsed = JSON.parse(trimmed) as unknown;
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
    return null;
  } catch {
    return null;
  }
}

export function buildMailTemplatePayload(draft: OcMailTemplateDraft, isEdit: boolean): Record<string, unknown> {
  const vars =
    draft.variables.length > 0
      ? draft.variables
      : extractPlaceholderPaths(draft.subject, draft.bodyHtml);
  const sampleContext = parseSampleContextJson(draft.sampleContextJson);

  const body: Record<string, unknown> = {
    name: draft.name.trim(),
    subject: draft.subject.trim(),
    bodyHtml: draft.bodyHtml.trim(),
    variables: vars,
    layoutKey: draft.layoutKey.trim() || 'default',
    locale: draft.locale.trim() || 'tr',
    category: (draft.category || 'custom').trim() || 'custom',
    tags: draft.tags.filter(Boolean),
    isActive: draft.isActive,
  };
  if (draft.description.trim()) body.description = draft.description.trim();
  if (!isEdit) body.templateKey = draft.templateKey.trim();
  if (sampleContext) body.sampleContext = sampleContext;
  return body;
}

export function isSystemMailTemplate(template: Pick<MailTemplate, 'category'>): boolean {
  return (template.category ?? '').toLowerCase() === 'system';
}

export interface MailDomainBranding {
  name?: string | null;
  displayName?: string | null;
  logoUrl?: string | null;
}

/** Preview keeps the sample work item and replaces the baked-in brand with the signed-in domain. */
export function applyMailDomainBranding(
  context: Record<string, unknown>,
  branding: MailDomainBranding | null | undefined
): Record<string, unknown> {
  const name = branding?.name?.trim() || '';
  const displayName = branding?.displayName?.trim() || name;
  const logoUrl = branding?.logoUrl?.trim() || '';
  if (!displayName && !logoUrl) return context;

  const current = context.domain;
  const domain: Record<string, unknown> =
    current && typeof current === 'object' && !Array.isArray(current)
      ? { ...(current as Record<string, unknown>) }
      : {};

  if (name) domain.name = name;
  if (displayName) domain.displayName = displayName;
  if (logoUrl) domain.logoUrl = logoUrl;

  return { ...context, domain };
}
