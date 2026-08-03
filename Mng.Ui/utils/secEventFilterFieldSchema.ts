import type {
  SecEventFilterFieldKey,
  SecEventFilterFieldOp,
} from '@/types/apps/secEventFilterCatalog';

export interface SecEventFilterFieldSchema {
  field: SecEventFilterFieldKey;
  labelKey: string;
  input: 'text' | 'select';
  ops: SecEventFilterFieldOp[];
  /** Fixed select options when input=select */
  options?: string[];
  /** Show when product matches (empty = always when type allows) */
  products?: string[];
  types?: string[];
}

const OUTCOMES = ['success', 'failure', 'unknown'];
const RDP_CODES = ['21', '23', '24', '25'];

/** Dynamic field menu for the filter editor, scoped by type/product. */
export function listSecEventFilterFieldSchemas(options: {
  type?: string | null;
  product?: string | null;
}): SecEventFilterFieldSchema[] {
  const type = (options.type ?? '').trim().toLowerCase() || null;
  const product = (options.product ?? '').trim().toLowerCase() || null;

  const all: SecEventFilterFieldSchema[] = [
    {
      field: 'event.code',
      labelKey: 'siemCenter.events.filterCatalog.fields.eventCode',
      input: product === 'rdp-session' ? 'select' : 'text',
      ops: ['eq', 'in'],
      options: product === 'rdp-session' ? RDP_CODES : undefined,
      products: product === 'rdp-session' ? ['rdp-session'] : undefined,
    },
    {
      field: 'event.outcome',
      labelKey: 'siemCenter.events.filterCatalog.fields.outcome',
      input: 'select',
      ops: ['eq'],
      options: OUTCOMES,
    },
    {
      field: 'event.action',
      labelKey: 'siemCenter.events.filterCatalog.fields.action',
      input: 'text',
      ops: ['eq', 'contains'],
    },
    {
      field: 'event.actionPrefix',
      labelKey: 'siemCenter.events.filterCatalog.fields.actionPrefix',
      input: 'text',
      ops: ['eq'],
    },
    {
      field: 'actor.user',
      labelKey: 'siemCenter.events.filterCatalog.fields.user',
      input: 'text',
      ops: ['eq', 'contains'],
    },
    {
      field: 'network.srcIp',
      labelKey: 'siemCenter.events.filterCatalog.fields.srcIp',
      input: 'text',
      ops: ['eq', 'contains'],
    },
    {
      field: 'network.dstIp',
      labelKey: 'siemCenter.events.filterCatalog.fields.dstIp',
      input: 'text',
      ops: ['eq'],
    },
    {
      field: 'network.dstPort',
      labelKey: 'siemCenter.events.filterCatalog.fields.dstPort',
      input: 'text',
      ops: ['eq'],
    },
    {
      field: 'search',
      labelKey: 'siemCenter.events.filterCatalog.fields.search',
      input: 'text',
      ops: ['contains'],
    },
  ];

  return all.filter((schema) => {
    if (schema.products?.length) {
      if (!product || !schema.products.includes(product)) {
        // Still allow event.code as free text when product not rdp
        if (schema.field === 'event.code' && !product) return true;
        if (schema.field === 'event.code' && product !== 'rdp-session') {
          return true; // text variant without fixed options handled by remapping below
        }
        if (schema.field === 'event.code') return product === 'rdp-session';
        return false;
      }
    }
    if (schema.types?.length && type && !schema.types.includes(type)) return false;
    return true;
  }).map((schema) => {
    if (schema.field === 'event.code' && product !== 'rdp-session') {
      return { ...schema, input: 'text' as const, options: undefined };
    }
    return schema;
  });
}
