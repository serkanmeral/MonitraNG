/** Saved SIEM event filter catalog (categories + filters). Time range lives on the panel, not here. */

export type SecEventFilterFieldOp = 'eq' | 'in' | 'contains';

export type SecEventFilterFieldKey =
  | 'event.code'
  | 'event.outcome'
  | 'event.action'
  | 'event.actionPrefix'
  | 'actor.user'
  | 'network.srcIp'
  | 'network.dstIp'
  | 'network.dstPort'
  | 'search';

export interface SecEventFilterFieldClause {
  field: SecEventFilterFieldKey;
  op: SecEventFilterFieldOp;
  /** Single value, or CSV for `in`. */
  value: string;
}

export interface SecEventFilterScope {
  /** Empty / omit = All */
  type?: string | null;
  /** Empty / omit = All */
  product?: string | null;
  /** Multi-select OR; empty = All */
  hosts?: string[];
}

export interface SecEventFilterCategory {
  id: string;
  parentId: string | null;
  name: string;
  sortOrder: number;
  isSystem: boolean;
}

export interface SecEventSavedFilter {
  id: string;
  categoryId: string;
  name: string;
  description?: string | null;
  isSystem: boolean;
  scope: SecEventFilterScope;
  fields: SecEventFilterFieldClause[];
}

export interface SecEventFilterCatalogState {
  categories: SecEventFilterCategory[];
  filters: SecEventSavedFilter[];
}

/** Tree node for left panel (category folder or filter leaf). */
export type SecEventFilterTreeNodeKind = 'category' | 'filter';

export interface SecEventFilterTreeNode {
  id: string;
  kind: SecEventFilterTreeNodeKind;
  name: string;
  isSystem: boolean;
  /** Present when kind=filter */
  filterId?: string;
  children?: SecEventFilterTreeNode[];
}

export const SEC_EVENT_FILTER_CATALOG_ROOT_ID = '__sec_filter_root__';

/** Scope dropdown catalogs (v1 static + discovery hosts). */
export const SEC_EVENT_SOURCE_TYPE_OPTIONS = [
  'windows-eventlog',
  'linux-journal',
  'metric',
  'endpoint',
  'firewall',
  'ad',
  'bastion',
] as const;

export const SEC_EVENT_SOURCE_PRODUCT_OPTIONS = [
  'rdp-session',
  'mnglogs-agent',
  'windows',
  'fortigate',
] as const;
