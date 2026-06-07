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
  /** Workspace yetki grupları (Keeper @users grup id'leri) — MO katman B. */
  viewGroups?: string[];
  editGroups?: string[];
  adminGroups?: string[];
  ownerGroups?: string[];
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
  /** Geçiş öncesi dolu olması zorunlu alan key'leri (MO `EnsureRequiredFields`). */
  requiredFields?: string[];
  /** Geçişi uygulayabilecek Keeper grup id'leri; boş = kısıtlama yok (MO `permissions.groups`). */
  permissionGroups?: string[];
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

/** op_notification_policies — workspace bildirim / e-posta politikası */
export interface OpNotificationPolicy {
  __dataId: string;
  name: string;
  workspaceId: string;
  boardId?: string | null;
  typeId?: string | null;
  eventType: string;
  channels: string[];
  recipients: string[];
  emailTemplateKey?: string | null;
  emailSubject?: string | null;
  notificationTemplateKey?: string | null;
  transitionKey?: string | null;
  fromStateId?: string | null;
  toStateId?: string | null;
  excludeActor?: boolean;
  isActive?: boolean;
  priority?: number | null;
  settings?: { pushToast?: boolean; toastSeverity?: string } | null;
}

/** op_sla_policies — workspace SLA politikası */
export interface OpSlaPolicy {
  __dataId: string;
  name: string;
  description?: string | null;
  workspaceId: string;
  typeId?: string | null;
  priorityId?: string | null;
  responseTargetMinutes?: number | null;
  resolveTargetMinutes?: number | null;
  isActive?: boolean;
  /** Policy seçim önceliği (MO ResolveSlaPolicyAsync) */
  priority?: number | null;
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

export const OC_BOARD_VIEW_TYPE_VALUES = ['list', 'kanban'] as const;
export type OcBoardViewType = (typeof OC_BOARD_VIEW_TYPE_VALUES)[number];

/** MO board runtime cardFieldKeys — op_boards.visibleFields ile hizalı */
export const OC_BOARD_CARD_FIELD_KEYS = [
  'title',
  'key',
  'assignee',
  'priorityId',
  'typeId',
  'stateId',
] as const;

export type OcBoardCardFieldKey = (typeof OC_BOARD_CARD_FIELD_KEYS)[number];

/** op_boards.config.columns[] */
export interface OpBoardColumnConfig {
  stateId: string;
  title?: string | null;
  queryKey?: string;
  defaultTransitionKey?: string | null;
}

export type OcSortDirection = 'asc' | 'desc';

/** op_boards.config.listColumns[] — liste tablosu sütun tanımı (sıra + per-column meta). */
/** Liste hücresi format ipucu. `relativeTime` = "geçen süre"; `date` = tarih/saat. */
export type OcColumnFormat = 'text' | 'number' | 'money' | 'date' | 'relativeTime';

export const OC_COLUMN_FORMATS: OcColumnFormat[] = ['text', 'number', 'money', 'date', 'relativeTime'];

export interface OpBoardListColumnConfig {
  key: string;
  sortable?: boolean;
  filterable?: boolean;
  /** Hücre format ipucu (null/undefined = alan tipine göre varsayılan). */
  format?: OcColumnFormat | null;
  /** Hesaplanan (computed) sütun mu? true ise DG alanı yok, değer `expr` ile UI'da hesaplanır. */
  computed?: boolean;
  /** Computed sütun ifadesi (expr-eval). Yalnızca `computed=true` için anlamlı. */
  expr?: string | null;
  /** Computed sütun başlığı (UI etiketi; boşsa key). */
  label?: string | null;
}

/** op_boards.config.defaultSort — liste varsayılan sıralaması. */
export interface OpBoardSortConfig {
  field: string;
  direction: OcSortDirection;
}

export interface OpBoard {
  __dataId: string;
  name: string;
  workspaceId: string;
  viewType?: string;
  defaultFormId?: string | null;
  /** Board'a bağlı varsayılan pano (op_dashboards.__dataId). Form seçimine analojik. */
  defaultDashboardId?: string | null;
  defaultStateFlowId?: string | null;
  defaultProfileId?: string | null;
  defaultTypeId?: string | null;
  defaultPriorityId?: string | null;
  defaultStateId?: string | null;
  visibleFields: string[];
  viewGroups: string[];
  editGroups: string[];
  columns: OpBoardColumnConfig[];
  /** Liste tablosu sütunları (sıra + sortable/filterable). Boşsa visibleFields'tan türetilir. */
  listColumns: OpBoardListColumnConfig[];
  /** Liste varsayılan sıralaması. */
  defaultSort?: OpBoardSortConfig | null;
}

/** op_profiles — board varsayılan profil seçimi */
export interface OpProfile {
  __dataId: string;
  name: string;
  workspaceId: string;
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

/** Bir kolona (state'e) giren geçiş — Kanban DnD'de kaynak state'e göre doğru geçişi seçmek için. */
export interface OcBoardColumnTransition {
  transitionKey: string;
  fromStateId: string;
  requiredFields: string[];
}

export interface OcBoardColumn {
  stateId: string;
  title?: string;
  dropEligible: boolean;
  defaultTransitionKey?: string;
  alternativeTransitionKeys: string[];
  /** Bu state'e giren geçişler (from + requiredFields ile). */
  incomingTransitions: OcBoardColumnTransition[];
  queryKey: string;
  parametersTemplate: Record<string, string>;
  suggestedPageSize: number;
}

/** Katalog lookup girdisi (board context map'leri) — OcCatalogDisplayItem ile yapısal uyumlu. */
export interface OcCatalogDisplayEntry {
  id: string;
  name: string;
  color?: string | null;
  icon?: string | null;
}

/** MO board context — id→{name,color,icon} katalog map'leri (client-side join gerekmez). */
export interface OcBoardCatalogs {
  states: Record<string, OcCatalogDisplayEntry>;
  priorities: Record<string, OcCatalogDisplayEntry>;
  types: Record<string, OcCatalogDisplayEntry>;
}

/** Runtime liste sütunu — MO board context'inden (config.listColumns). */
export interface OcBoardListColumn {
  key: string;
  sortable: boolean;
  filterable: boolean;
  /** Hücre format ipucu (null = alan tipine göre varsayılan). */
  format?: OcColumnFormat | null;
  /** Hesaplanan (computed) sütun mu? */
  computed?: boolean;
  /** Computed sütun ifadesi (expr-eval). */
  expr?: string | null;
  /** Computed sütun başlığı (UI etiketi; boşsa key). */
  label?: string | null;
}

export interface OcBoardRuntimeContext {
  boardId: string;
  workspaceId: string;
  name?: string;
  viewType?: string;
  permissions: OcRuntimePermissions;
  columns: OcBoardColumn[];
  cardFieldKeys: string[];
  /** Liste tablosu sütun meta (sıra + sortable/filterable). */
  listColumns: OcBoardListColumn[];
  /** Liste varsayılan sıralaması (kullanıcı sıralaması yoksa). */
  defaultSort?: OpBoardSortConfig | null;
  /** Akıştaki başlangıç state id'si — liste SLA chip akıllı fazı için. */
  initialStateId?: string | null;
  catalogs: OcBoardCatalogs;
}

/** Board liste filtresi (alan + operatör + değer). */
export interface OcBoardListFilter {
  field: string;
  operator: string;
  value: string;
}

/** Board liste server-side isteği. */
export interface OcBoardListRequest {
  skip: number;
  take: number;
  sort?: OpBoardSortConfig | null;
  filters?: OcBoardListFilter[];
  search?: string | null;
}

/** İş kaydı SLA snapshot'ı (op_work_items.sla). */
export interface OcSlaSnapshot {
  slaPolicyId?: string | null;
  responseDueAt?: string | null;
  resolveDueAt?: string | null;
  responseBreached?: boolean;
  resolveBreached?: boolean;
  calculatedAt?: string | null;
}

export interface OcWorkItemCard {
  id: string;
  key: string;
  title: string;
  stateId?: string;
  assignee?: string;
  priorityId?: string;
  typeId?: string;
  /** Sistem alanları (audit). */
  createdAt?: string | null;
  /** Oluşturan kullanıcı id'si; ad çözümü `people` map'inden. Eski kayıtlarda boş olabilir. */
  createdBy?: string | null;
  updatedAt?: string | null;
  lastStateChangeAt?: string | null;
  closedAt?: string | null;
  /** SLA snapshot — liste SLA durumu chip'i için. */
  sla?: OcSlaSnapshot | null;
  /** Pool alan değerleri (extraFields) — liste tablosu özel sütunları için. */
  fields?: Record<string, unknown>;
}

/** Keeper kişi (person) görünen ad map'i — MO cache'inden gelir. */
export interface OcPersonDisplay {
  id: string;
  name?: string;
  title?: string | null;
  isActive?: boolean | null;
}

export interface OcQueryExecuteResponse {
  dataset: string;
  queryKey: string;
  items: OcWorkItemCard[];
  skip: number;
  take: number;
  total: number;
  /** Person alanları (assignee/watchers + person tipi pool alanlar) id → görünen ad. */
  people: Record<string, OcPersonDisplay>;
  /** Person grup alanları (assignmentGroups + personGroups tipi pool alanlar) id → grup adı. */
  groups: Record<string, OcPersonDisplay>;
}

export interface OcColumnItemsState {
  items: OcWorkItemCard[];
  total: number;
  error: string | null;
}

// ----- Dashboards (D-A) -----

/** Chart agregasyon kovası — key = ham id/değer (catalog/person ile çözülür), count = kayıt sayısı. */
export interface OcDashboardBucket {
  key?: string | null;
  count: number;
}

/** Tek widget'ın çalıştırma sonucu (MO server-side). */
export interface OcDashboardWidgetExecution {
  success: boolean;
  errorCode?: string | null;
  errorMessage?: string | null;
  total: number;
  skip: number;
  take: number;
  items: OcWorkItemCard[];
  /** Chart widget'ları için server-side agregasyon (tam sonuç kümesi). Diğer tiplerde boş. */
  aggregation: OcDashboardBucket[];
  executedAt?: string | null;
}

/** Çözülmüş dashboard widget'ı (tanım + çalıştırma sonucu). */
export interface OcDashboardWidget {
  key: string;
  /** 'summaryCard' | 'list' | 'chart' */
  widgetType: string;
  title?: string | null;
  dataset?: string | null;
  queryKey?: string | null;
  /** Chart: 'bar' | 'pie' | 'donut' | 'line'. */
  chartType?: string | null;
  /** Chart agregasyon alanı: 'stateId' | 'priorityId' | 'typeId' | 'assignee'. */
  groupBy?: string | null;
  /** summaryCard: Vuetify tema rengi. */
  accentColor?: string | null;
  /** summaryCard: mdi-* ikon adı. */
  icon?: string | null;
  resolvedParameters?: Record<string, unknown> | null;
  execution?: OcDashboardWidgetExecution | null;
}

/** Layout kolonu — widgetId widget key'ine referans verir; responsive span'ler (12'lik grid). */
export interface OcDashboardLayoutCol {
  widgetId?: string;
  span?: number;
  spanSm?: number;
  spanMd?: number;
  spanLg?: number;
  spanXl?: number;
  rows?: OcDashboardLayoutRow[];
}

export interface OcDashboardLayoutRow {
  cols: OcDashboardLayoutCol[];
}

export interface OcDashboardLayout {
  type?: string;
  rows: OcDashboardLayoutRow[];
}

/** MO GetDashboardAsync — tek toplu dashboard context (widget'lar çalıştırılmış + katalog/person çözülmüş). */
export interface OcDashboard {
  dashboardId: string;
  workspaceId?: string | null;
  name?: string | null;
  description?: string | null;
  scope?: string | null;
  layout?: OcDashboardLayout | null;
  permissions: OcRuntimePermissions;
  widgets: OcDashboardWidget[];
  catalogs: OcBoardCatalogs;
  people: Record<string, OcPersonDisplay>;
  groups: Record<string, OcPersonDisplay>;
}

/** Dashboard hub liste satırı (op_dashboards ham kaydından). */
export interface OcDashboardListItem {
  id: string;
  name: string;
  description?: string | null;
  workspaceId?: string | null;
  isActive: boolean;
  isDefault: boolean;
}

/**
 * Düzenlenebilir widget tanımı (op_dashboards.widgets[] ham hali — runtime execution DEĞİL).
 * Admin editörü bu modeli okur/yazar; viewer ise MO'nun çözdüğü OcDashboardWidget'i kullanır.
 */
export interface OcDashboardWidgetDef {
  key: string;
  /** 'summaryCard' | 'list' | 'chart' */
  type: string;
  title?: string | null;
  dataset?: string | null;
  queryKey?: string | null;
  parameters?: Record<string, unknown> | null;
  take?: number | null;
  /** Chart: 'bar' | 'pie' | 'donut' | 'line'. */
  chartType?: string | null;
  /** Chart agregasyon alanı: 'stateId' | 'priorityId' | 'typeId' | 'assignee'. */
  groupBy?: string | null;
  accentColor?: string | null;
  icon?: string | null;
}

/** op_dashboards ham kaydı (admin editörü için — DG'den okunur, DG'ye yazılır). */
export interface OcDashboardRecord {
  id: string;
  name: string;
  description?: string | null;
  workspaceId?: string | null;
  scope?: string | null;
  isDefault: boolean;
  isActive: boolean;
  layout?: OcDashboardLayout | null;
  widgets: OcDashboardWidgetDef[];
}

/** İş kaydı profil özeti (MO GetProfileAsync). */
export interface OcWorkItemSummary {
  id: string;
  key: string;
  title: string;
  description?: string | null;
  stateId: string;
  stateFlowId?: string | null;
  category?: string | null;
  workspaceKey?: string | null;
  assignee?: string | null;
  reporter?: string | null;
  typeId?: string | null;
  boardId?: string | null;
  priorityId?: string | null;
  createdAt?: string | null;
  lastStateChangeAt?: string | null;
  closedAt?: string | null;
}

export interface OcWorkItemLinkSummary {
  id: string;
  linkType: string;
  direction: string;
  otherWorkItemId: string;
  description?: string | null;
}

/**
 * İş kaydı eki (op_work_items.attachments — DG file isArray).
 * `raw`, DG'nin sakladığı ham nesnedir; ek listesi güncellenirken (PATCH) mevcut
 * girdiler bu ham haliyle geri gönderilir (DG `content` içermeyen nesneleri olduğu gibi korur).
 */
export interface OcAttachment {
  /** MinIO yolu (download/metadata anahtarı). */
  path: string;
  fileName: string;
  fileExt?: string | null;
  /** KB cinsinden (DG file_size). */
  fileSizeKb?: number | null;
  uploadPerson?: string | null;
  uploadTime?: string | null;
  /** DG'ye geri gönderilecek ham saklı nesne. */
  raw: Record<string, unknown>;
}

/** Uygulanabilir durum geçişi (profil header aksiyonları). MO `ProfileActionDto`. */
export interface OcProfileAction {
  transitionKey: string;
  label?: string | null;
  fromStateId?: string | null;
  toStateId: string;
  enabled: boolean;
  order: number;
  /** Bu geçiş için zorunlu alan anahtarları (akış transition.requiredFields). UI dialog'da ön-toplar. */
  requiredFields: string[];
}

/** Profil runtime context — sidebar (SLA/meta/policy) + izinler. */
export interface OcWorkItemProfile {
  workspaceId: string;
  workItem: OcWorkItemSummary;
  permissions: OcRuntimePermissions;
  /** Geçerli durumdan uygulanabilir geçişler (yetki + koşul süzülmüş). */
  actions: OcProfileAction[];
  sla?: OcSlaSnapshot | null;
  watchers: string[];
  links: OcWorkItemLinkSummary[];
  /** Person id → görünen ad (assignee/reporter/watchers). */
  people: Record<string, OcPersonDisplay>;
  /** Grup id → grup adı (assignmentGroups + personGroups tipi pool alanlar). */
  groups: Record<string, OcPersonDisplay>;
  createdBy?: string | null;
  /** İş kaydı ekleri (op_work_items.attachments). */
  attachments: OcAttachment[];
}

/** Aktivite alan değişikliği — eski/yeni görünen değer MO'da çözülmüş (UI ham veri işlemez). */
export interface OcTimelineChange {
  field: string;
  /** Form alanı etiketi (yoksa key). */
  label?: string | null;
  /** Alan türü ipucu (relation/person/group/scalar). */
  fieldType?: string | null;
  /** Eski değerin görünen metni (boşsa null → UI "—"). */
  fromDisplay?: string | null;
  /** Yeni değerin görünen metni (boşsa null → UI "—"). */
  toDisplay?: string | null;
}

/** Aktivite/yorum zaman tüneli girdisi (MO GetTimelineAsync). */
export interface OcTimelineEntry {
  type: string;
  id?: string | null;
  actor?: string | null;
  /** Aktör/yazar person id'si — "kendi yorumum mu?" kontrolü için (ad değil id). */
  actorId?: string | null;
  text?: string | null;
  at?: string | null;
  activityType?: string | null;
  /** Yorum düzenlendiyse son düzenleme zamanı — yalnızca `type='comment'`. */
  editedAt?: string | null;
  /** Yanıt verilen üst yorum id'si — yalnızca `type='comment'` girdilerde (tek seviye thread). */
  parentId?: string | null;
  /** Yorum ekleri (op_comments.attachments) — yalnızca `type='comment'` girdilerde. */
  attachments?: OcAttachment[];
  /** Alan değişiklik satırları — yalnızca `type='activity'` girdilerde (MO'da çözülmüş). */
  changes?: OcTimelineChange[];
}

export interface OcTimelinePage {
  items: OcTimelineEntry[];
  skip: number;
  take: number;
  total: number;
}

/** MO'da çözülmüş SLA politikası (OcPolicyPanel resolvedPolicy prop'u). */
export interface OcResolvedSlaPolicy {
  id: string;
  name?: string | null;
  responseTargetMinutes?: number | null;
  resolveTargetMinutes?: number | null;
  /** Snapshot id'si yoksa type/priority kapsamından türetildiyse true. */
  derived: boolean;
}

/** MO'da çözülmüş uygulanabilir kural (OcPolicyPanel resolvedPolicy prop'u). */
export interface OcResolvedRule {
  id: string;
  name?: string | null;
  trigger?: string | null;
  ruleType?: string | null;
  description?: string | null;
}

export interface OcResolvedPolicy {
  matchedSlaPolicy?: OcResolvedSlaPolicy | null;
  applicableRules: OcResolvedRule[];
}

/**
 * Profil ekranının tek toplu paketi (MO profile-view ucu). UI'nın ~18 çağrısını 1'e indirir;
 * form/katalog/pool-alan/alan-görünen-değerleri/politika/timeline tek seferde gelir.
 */
export interface OcWorkItemProfileView {
  profile: OcWorkItemProfile;
  form: OcFormRuntimeContext;
  catalogs: OcBoardCatalogs;
  /** board id → ad (boardId alanı görünen değeri). */
  boards: Record<string, string>;
  /** Form alanı enrichment'ı için pool alanlar (global + workspace). */
  poolFields: OpField[];
  /** Form alanı key → çözülmüş görünen metin (relation/person/grup/katalog). */
  fieldDisplays: Record<string, string>;
  policy: OcResolvedPolicy;
  timeline: OcTimelinePage;
}

/** In-app bildirim (MO op_notifications — geçerli kullanıcı). */
export interface OcNotification {
  id: string;
  notificationType?: string | null;
  title?: string | null;
  message?: string | null;
  isRead: boolean;
  workItemId?: string | null;
  workItemKey?: string | null;
  sourceDataset?: string | null;
  sourceRecordId?: string | null;
  deepLink?: string | null;
  createdAt?: string | null;
}

export interface OcNotificationListResponse {
  items: OcNotification[];
  skip: number;
  take: number;
  total: number;
  unreadCount: number;
}

/** Yorum (MO AddCommentAsync yanıtı). */
export interface OcComment {
  id: string;
  workItemId: string;
  body: string;
  author?: string | null;
  parentCommentId?: string | null;
  commentDate?: string | null;
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

/** op_tags — workspace'e ait etiket kataloğu (her kayıt workspaceId taşır). */
export interface OpTag {
  __dataId: string;
  name: string;
  color?: string | null;
  description?: string | null;
  workspaceId: string;
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
