export interface OpWorkspace {
  __dataId: string;
  name: string;
  description?: string;
  workspaceType?: string;
  workItemKeyPrefix?: string;
}

/** op_workspaces — yapılandırma ekranı için genişletilmiş model */
export interface OpWorkspaceDetail extends OpWorkspace {
  key?: string;
  workItemKeyFormat?: string;
  workItemSequenceStart?: number | null;
  enabledTypeIds: string[];
  /** Global op_states kataloğundan workspace’te kullanılacak durumlar */
  enabledStateIds: string[];
  /** Global op_priorities kataloğundan workspace’te kullanılacak öncelikler */
  enabledPriorityIds: string[];
  enabledFieldIds: string[];
  defaultStateFlowId?: string | null;
  /** op_workspaces.settings — workspace politikaları `fieldPolicies` altında */
  settings?: Record<string, unknown>;
}

export const OC_WORKSPACE_TYPE_VALUES = [
  'team',
  'service_desk',
  'operational',
  'project',
] as const;

export type OcWorkspaceType = (typeof OC_WORKSPACE_TYPE_VALUES)[number];

/** op_state_flows.transitions[] öğesi */
export interface OpStateFlowTransition {
  transitionKey: string;
  fromStateId: string;
  toStateId: string;
  label?: string | null;
  order?: number | null;
  requiredFields?: string[];
}

/** op_work_item_schedules — zamanlanmış WI şablonu */
export interface OpWorkItemSchedule {
  __dataId: string;
  workspaceId: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  cronExpression: string;
  timezone: string;
  boardId: string;
  typeId: string;
  assignee: string;
  priorityId?: string | null;
  title: string;
  templateDescription?: string | null;
  fields?: Record<string, unknown> | null;
  initialTransitionKey?: string | null;
  schedulerJobId?: string | null;
  lastRunAt?: string | null;
  lastWorkItemId?: string | null;
}

/** op_rules — workspace kuralı */
export interface OpRule {
  __dataId: string;
  name: string;
  description?: string | null;
  workspaceId: string;
  ruleType: string;
  trigger: string;
  transitionKey?: string | null;
  typeId?: string | null;
  boardId?: string | null;
  stateId?: string | null;
  fromStateId?: string | null;
  toStateId?: string | null;
  isActive?: boolean;
  priority?: number | null;
  conditions?: unknown;
  actions?: unknown[];
  errorMessage?: string | null;
  applyMode?: string | null;
}

/** op_state_flows — workspace durum akışı */
export interface OpStateFlow {
  __dataId: string;
  name: string;
  workspaceId: string;
  description?: string | null;
  initialStateId: string;
  isDefault?: boolean;
  isActive?: boolean;
  sortOrder?: number | null;
  transitions: OpStateFlowTransition[];
}

export const OC_BOARD_VIEW_TYPE_VALUES = ['kanban', 'list'] as const;
export type OcBoardViewType = (typeof OC_BOARD_VIEW_TYPE_VALUES)[number];

/** op_boards.config.columns[] */
export interface OpBoardColumnConfig {
  stateId: string;
  title?: string | null;
  queryKey?: string;
}

export interface OpBoard {
  __dataId: string;
  name: string;
  workspaceId: string;
  viewType?: string;
  defaultFormId?: string | null;
  defaultStateFlowId?: string | null;
  visibleFields: string[];
  columns: OpBoardColumnConfig[];
}

/** op_forms.layout.sections[] */
export interface OpFormLayoutSection {
  key: string;
  title?: string | null;
  /** Bölüm bloğu genişliği (1–12). */
  cols?: number;
  fields: string[];
}

export interface OpFormFieldBehavior {
  visible: boolean;
  required: boolean;
  readonly: boolean;
  masked: boolean;
}

/** op_forms — workspace oluşturma formu şablonu */
export interface OpForm {
  __dataId: string;
  name: string;
  workspaceId: string;
  description?: string | null;
  defaultTypeId?: string | null;
  defaultStateFlowId?: string | null;
  defaultStateId?: string | null;
  defaultPriorityId?: string | null;
  isDefault?: boolean;
  formHeading?: string;
  formIntro?: string;
  layoutSections: OpFormLayoutSection[];
  /** layout.dialogMaxWidth (px) */
  dialogMaxWidth?: number;
  sectionCols: Record<string, number>;
  fieldCols: Record<string, number>;
  fieldBehaviors: Record<string, OpFormFieldBehavior>;
  defaultValues: Record<string, unknown>;
}

export interface OcFormFieldRuntimeDto {
  key: string;
  label?: string;
  fieldType?: string;
  value?: unknown;
  cardinality?: 'single' | 'multi' | string | null;
  relationDataset?: string | null;
}

export interface OcFieldBehaviorDto {
  visible: boolean;
  readonly: boolean;
  required: boolean;
  masked: boolean;
}

export interface OcFormLayoutMeta {
  formHeading?: string | null;
  formIntro?: string | null;
  /** Yeni iş / önizleme modal genişliği (px). */
  dialogMaxWidth?: number | null;
  sectionCols?: Record<string, number>;
  fieldCols?: Record<string, number>;
}

export interface OcFormLayoutSectionRuntime {
  key: string;
  title?: string | null;
  cols?: number;
  fields: string[];
}

export interface OcFormRuntimeContext {
  mode: string;
  workspaceId: string;
  workItemId?: string | null;
  formId?: string | null;
  formName?: string | null;
  defaultTypeId?: string | null;
  initialStateId?: string | null;
  layout?: ({ sections?: OcFormLayoutSectionRuntime[] } & OcFormLayoutMeta) | null;
  fields: Record<string, OcFormFieldRuntimeDto>;
  fieldBehaviors: Record<string, OcFieldBehaviorDto>;
  permissions?: { canView?: boolean; canEdit?: boolean; canComment?: boolean };
  types?: { id: string; name: string; category?: string | null }[];
}

export interface OcWorkspaceTreeNode {
  type: 'workspace';
  data: OpWorkspace;
  children: { type: 'board'; data: OpBoard }[];
}

export const OC_ROOT_WORKSPACES = '__oc_workspaces_root__';

export interface OcRuntimePermissions {
  canView: boolean;
  canEdit: boolean;
  canComment: boolean;
}

export interface OcBoardColumn {
  stateId: string;
  title?: string;
  dropEligible: boolean;
  defaultTransitionKey?: string;
  alternativeTransitionKeys: string[];
  queryKey: string;
  parametersTemplate: Record<string, string>;
  suggestedPageSize: number;
}

export interface OcBoardRuntimeContext {
  boardId: string;
  workspaceId: string;
  name?: string;
  viewType?: string;
  permissions: OcRuntimePermissions;
  columns: OcBoardColumn[];
  cardFieldKeys: string[];
}

export interface OcWorkItemCard {
  id: string;
  key: string;
  title: string;
  stateId?: string;
  assignee?: string;
  priorityId?: string;
  typeId?: string;
}

export interface OcQueryExecuteResponse {
  dataset: string;
  queryKey: string;
  items: OcWorkItemCard[];
  skip: number;
  take: number;
  total: number;
}

export interface OcColumnItemsState {
  items: OcWorkItemCard[];
  total: number;
  error: string | null;
}

/** op_states — global durum kataloğu */
export type OpStateCategory = 'open' | 'in_progress' | 'closed' | 'on_hold' | 'cancelled';

export const OC_STATE_CATEGORIES: OpStateCategory[] = [
  'open',
  'in_progress',
  'closed',
  'on_hold',
  'cancelled',
];

export interface OpState {
  __dataId: string;
  name: string;
  category: OpStateCategory | string;
  description?: string | null;
  color?: string | null;
  icon?: string | null;
  isInitial?: boolean;
  isStart?: boolean;
  isClosed?: boolean;
  isTerminal?: boolean;
  allowReopen?: boolean;
  sortOrder?: number | null;
}

export type OpStateUpsertPayload = {
  name: string;
  category: string;
  description?: string | null;
  color?: string | null;
  icon?: string | null;
  isInitial?: boolean;
  isStart?: boolean;
  isClosed?: boolean;
  isTerminal?: boolean;
  allowReopen?: boolean;
  sortOrder?: number | null;
};

/** op_priorities — global öncelik kataloğu */
export interface OpPriority {
  __dataId: string;
  name: string;
  /** Sayısal seviye (1 = en yüksek); DG'de text olarak saklanabilir */
  level?: number | null;
  description?: string | null;
  color?: string | null;
  icon?: string | null;
  sortOrder?: number | null;
}

/** op_work_item_types.category — Faz 1 enum (operationcore_phase1 §8.4) */
export type OpWorkItemTypeCategory =
  | 'incident'
  | 'service_request'
  | 'problem'
  | 'change'
  | 'task'
  | 'operational';

export const OC_WORK_ITEM_TYPE_CATEGORIES: OpWorkItemTypeCategory[] = [
  'incident',
  'service_request',
  'problem',
  'change',
  'task',
  'operational',
];

/** op_work_item_types — global veya workspace'e özel tip tanımı */
export interface OpWorkItemType {
  __dataId: string;
  name: string;
  category: OpWorkItemTypeCategory | string;
  description?: string | null;
  color?: string | null;
  icon?: string | null;
  sortOrder?: number | null;
  isSystem?: boolean;
  /** Dolu ise workspace'e özel tip; sistem tanımında yalnızca global (boş) kayıtlar yönetilir */
  workspaceId?: string | null;
}

/** op_fields — pool alan tanımı (değerler op_work_items.extraFields içinde) */
export interface OpField {
  __dataId: string;
  key: string;
  label: string;
  fieldType: string;
  scope: 'pool' | 'core' | string;
  category?: string | null;
  description?: string | null;
  cardinality?: 'single' | 'multi' | string | null;
  relationDatasetName?: string | null;
  options?: Record<string, unknown> | null;
  isSystem?: boolean;
  isSensitive?: boolean;
  sortOrder?: number | null;
  workspaceId?: string | null;
}
