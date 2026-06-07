/** DG @mail_templates — e-posta bildirim şablonu */
export interface MailTemplate {
  __dataId: string;
  templateKey: string;
  name: string;
  description?: string | null;
  subject: string;
  bodyHtml: string;
  variables: string[];
  layoutKey?: string | null;
  locale?: string | null;
  category: 'system' | 'custom' | string;
  tags?: string[];
  sampleContext?: Record<string, unknown> | null;
  isActive?: boolean;
}

export interface MailTemplatePreviewResult {
  templateKey: string;
  layoutKey?: string | null;
  subject: string;
  htmlBody: string;
}
