import type { MessageTemplate } from '@/types/apps/messageTemplates';
import { extractPlaceholderPaths, parseSampleContextJson } from '@/utils/ocMailTemplates';

export interface OcMessageTemplateDraft {
  id?: string;
  templateKey: string;
  name: string;
  description: string;
  channel: string;
  bodyText: string;
  parseMode: string;
  variables: string[];
  locale: string;
  category: string;
  tags: string[];
  sampleContextJson: string;
  isActive: boolean;
}

export function isSystemMessageTemplate(template: MessageTemplate): boolean {
  return (template.category || '').toLowerCase() === 'system';
}

export function newMessageTemplateDraft(seed?: Partial<OcMessageTemplateDraft>): OcMessageTemplateDraft {
  return {
    templateKey: '',
    name: '',
    description: '',
    channel: 'telegram',
    bodyText: '',
    parseMode: '',
    variables: [],
    locale: 'tr',
    category: 'custom',
    tags: [],
    sampleContextJson: '{}',
    isActive: true,
    ...seed,
  };
}

export function parseMessageTemplateToDraft(template: MessageTemplate): OcMessageTemplateDraft {
  return {
    id: template.__dataId,
    templateKey: template.templateKey,
    name: template.name,
    description: template.description ?? '',
    channel: template.channel || 'telegram',
    bodyText: template.bodyText,
    parseMode: template.parseMode ?? '',
    variables: template.variables?.length ? [...template.variables] : [],
    locale: template.locale ?? 'tr',
    category: template.category ?? 'custom',
    tags: template.tags?.length ? [...template.tags] : [],
    sampleContextJson: template.sampleContext
      ? JSON.stringify(template.sampleContext, null, 2)
      : '{}',
    isActive: template.isActive !== false,
  };
}

export function validateMessageTemplateDraft(draft: OcMessageTemplateDraft, isEdit: boolean): string | null {
  if (!isEdit && !draft.templateKey.trim()) return 'templateKey';
  if (!draft.name.trim()) return 'name';
  if (!draft.bodyText.trim()) return 'bodyText';
  if (!draft.channel.trim()) return 'channel';
  return null;
}

export function buildMessageTemplatePayload(
  draft: OcMessageTemplateDraft,
  isEdit: boolean
): Record<string, unknown> {
  const vars =
    draft.variables.length > 0
      ? draft.variables
      : extractPlaceholderPaths(draft.bodyText);
  const sampleContext = parseSampleContextJson(draft.sampleContextJson);
  const payload: Record<string, unknown> = {
    name: draft.name.trim(),
    description: draft.description.trim() || null,
    channel: draft.channel.trim() || 'telegram',
    bodyText: draft.bodyText,
    parseMode: draft.parseMode.trim() || null,
    variables: vars,
    locale: draft.locale.trim() || null,
    category: draft.category.trim() || 'custom',
    tags: draft.tags,
    sampleContext: sampleContext ?? {},
    isActive: draft.isActive,
  };
  if (!isEdit) {
    payload.templateKey = draft.templateKey.trim();
  }
  return payload;
}

export function renderMessageTemplatePreview(
  bodyText: string,
  context: Record<string, unknown>
): string {
  return bodyText.replace(/\{\{\s*([^{}]+?)\s*\}\}/g, (_m, expr: string) => {
    const [pathRaw] = String(expr).split('|');
    const path = pathRaw?.trim() || '';
    if (!path) return '';
    const segments = path.split('.');
    let cur: unknown = context;
    for (const seg of segments) {
      if (cur == null || typeof cur !== 'object') return '';
      cur = (cur as Record<string, unknown>)[seg];
    }
    if (cur == null) return '';
    return String(cur);
  });
}

export { extractPlaceholderPaths };
