/** DG @message_templates — push kanal (Telegram) metin şablonu */
export interface MessageTemplate {
  __dataId: string;
  templateKey: string;
  name: string;
  description?: string | null;
  channel: string;
  bodyText: string;
  parseMode?: string | null;
  variables: string[];
  locale?: string | null;
  category: 'system' | 'custom' | string;
  tags?: string[];
  sampleContext?: Record<string, unknown> | null;
  isActive?: boolean;
}
