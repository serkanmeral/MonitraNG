/** Task Manager — DG dataset entity shapes (tm_*) */

export interface TmProjectPermissionsView {
  personIds?: string[];
  groupIds?: string[];
}

export interface TmProjectPermissions {
  view?: TmProjectPermissionsView;
  edit?: TmProjectPermissionsView;
  admin?: TmProjectPermissionsView;
}

/** Proje başına durum alt kümesi, sıra, başlangıç/kapalı ve yönlü geçişler (global tm_statuses id’leri). */
export interface TmProjectWorkflow {
  /** Kolon sırası (workflow’daki durum kimlikleri) */
  statusIds: string[];
  /** Yeni görevlerin başlayacağı durum */
  initialStatusId: string;
  /** Kapalı / terminal anlamı (raporlama, izin verilmeyen geri dönüşler) */
  closedStatusId: string;
  /** Kaynak durum id → izin verilen hedef durum id listesi (yönlü graf) */
  transitions: Record<string, string[]>;
}

/** Projede kullanılacak havuz alt kümeleri (öncelik / tip / alan anahtarı). */
export interface TmProjectSelections {
  priorityIds?: string[];
  issueTypeIds?: string[];
  fieldKeys?: string[];
}

/** Yeni görev oluşturma modalında alan sırası (tablo sütun kimlikleri ile uyumlu: title, issueType, … veya havuz key). */
export interface TmIssueCreateLayout {
  rows: string[];
  /** Sütun kimliği → bölüm anahtarı (core | assignment | labels | extra veya özel metin); yoksa varsayılan sezgisel gruplama. */
  columnSections?: Record<string, string>;
  /** Özel bölüm anahtarı → görünen başlık (isteğe bağlı). */
  sectionTitles?: Record<string, string>;
  /** Yeni görev formunun üstünde gösterilen başlık (modal gövdesi içi). */
  formHeading?: string | null;
  /** Form üstü açıklama metni (düz metin). */
  formIntro?: string | null;
  /** Sütun kimliği → ızgara genişliği (1–12); yok veya 12 = tam satır. */
  fieldCols?: Record<string, number>;
  /** Yeni görev `v-dialog` max-width (piksel). Tanımsız → 560. */
  dialogMaxWidth?: number | null;
  /** Formda görünen bölüm anahtarları sırası (alan sırasından türetilen doğal sıra yerine). */
  sectionOrder?: string[];
  /** Bölüm anahtarı → dış bölüm bloğu ızgara genişliği (1–12). */
  sectionCols?: Record<string, number>;
}

/** Proje başına adlandırılmış yeni görev formu şablonu. */
export interface TmIssueCreateFormTemplate {
  id: string;
  name: string;
  layout: TmIssueCreateLayout;
}

export interface TmProject {
  __dataId: string;
  name: string;
  key: string;
  description?: string | null;
  /** persons alanı — tek kullanıcı id (string) veya DG nesnesi */
  lead?: unknown;
  avatarUrl?: string | null;
  permissions?: TmProjectPermissions | null;
  /** Öncelik / görev tipi / alan havuzu seçimleri */
  selections?: TmProjectSelections | null;
  workflow?: TmProjectWorkflow | null;
  /** false ise yalnızca liste; Kanban sürükle-bırak kullanılmaz. Eksik/legacy kayıtlar true kabul edilir. */
  useKanban?: boolean | null;
  /** Yönetici tarafından kaydedilen yeni görev formu alan sırası; yoksa varsayılan sıra kullanılır. */
  issueCreateLayout?: TmIssueCreateLayout | null;
  /**
   * Görev “Profil” tam ekran düzeni (tek nesne, geriye dönük). `issueProfileForms` doluysa varsayılan şablonun özeti olarak da yazılabilir.
   */
  issueProfileLayout?: TmIssueCreateLayout | null;
  /** Birden fazla profil (tam sayfa) şablonu; boşsa `issueProfileLayout` kullanılır. */
  issueProfileForms?: TmIssueCreateFormTemplate[] | null;
  /** `issueProfileForms` içinden varsayılan şablon kimliği. */
  defaultIssueProfileFormId?: string | null;
  /** Birden fazla yeni görev formu; boşsa yalnızca issueCreateLayout kullanılır. */
  issueCreateForms?: TmIssueCreateFormTemplate[] | null;
  /** issueCreateForms içinden varsayılan şablon kimliği. */
  defaultIssueCreateFormId?: string | null;
}

export interface BoardColumnConfig {
  statusId: string;
  title: string;
  wipLimit?: number | null;
}

export interface TmBoardConfig {
  /** Kanban durum kolonları */
  columns?: BoardColumnConfig[];
  /** Liste / tablo görünümü sütun kimlikleri (sıra korunur). Yerleşik: key, title, status, …; ek: havuz alan key */
  tableColumns?: string[];
}

export interface TmBoard {
  __dataId: string;
  name: string;
  projectId: string;
  type: 'kanban' | 'scrum' | 'list' | string;
  config?: TmBoardConfig | null;
  /** Bu board için yeni görev formu; yok veya bilinmeyen id → proje varsayılanı. */
  issueCreateFormId?: string | null;
  /** Bu board için tam sayfa profil şablonu; yok veya bilinmeyen id → proje varsayılanı. */
  issueProfileFormId?: string | null;
}

/** Sol ağaç: proje → board yaprakları */
export interface TmTreeBoardNode {
  type: 'board';
  data: TmBoard;
}

export interface TmTreeProjectNode {
  type: 'project';
  data: TmProject;
  children: TmTreeBoardNode[];
}

/** Workspace sol ağaç — filtre düğüm id’leri (genişletilebilir) */
export const TM_WORKSPACE_FILTER_ASSIGNED_TO_ME = 'assigned-to-me' as const;

export const TM_WORKSPACE_ROOT_PROJECTS = '__tm_root_projects';
export const TM_WORKSPACE_ROOT_FILTERS = '__tm_root_filters';

export interface TmIssueType {
  __dataId: string;
  name: string;
  icon?: string | null;
  color?: string | null;
  description?: string | null;
}

export interface TmStatus {
  __dataId: string;
  name: string;
  /** Tabler icon export adı (örn. CircleDotIcon) — UI’da dinamik ikon için */
  icon?: string | null;
  /** Vuetify tema anahtarı (primary, info, success, …) — dark/light uyumlu. Eski kayıtlarda #hex olabilir. */
  color?: string | null;
  /** İsteğe bağlı açıklama (havuz kartı / tooltip) */
  description?: string | null;
}

export interface TmPriority {
  __dataId: string;
  name: string;
  icon?: string | null;
  color?: string | null;
  description?: string | null;
}

/** tm_field_definitions.cardinality — person/group/tags/relation seçim sayısı */
export type TmFieldCardinality = 'single' | 'multi';

/**
 * tm_issues alan havuzu meta (`tm_field_definitions`) — salt okunur tanımlar.
 * fieldType: semantik tür (text, number, datetime, persons, relation, tags, file, …).
 * cardinality: çoğunlukla single; labels ve çoklu kişi/grup için multi.
 * optionsJson: doğrulama ve UI ipuçları (min/max, dosya limiti, relationDataset, …).
 */
export interface TmFieldDefinition {
  __dataId: string;
  /** tm_issues alan adı */
  key: string;
  label: string;
  fieldType: string;
  /** core: tüm projelerde temel; pool: projede seçilebilir */
  scope: 'core' | 'pool' | string;
  description?: string | null;
  sortOrder?: number | null;
  /** Tek değer mi, dizi mi (varsayılan: single) */
  cardinality?: TmFieldCardinality | null;
  /** JSON string — bkz. `parseTmFieldOptionsJson` (utils/taskManagerFieldDefinitions.ts) */
  optionsJson?: string | null;
}

export interface TmLabel {
  __dataId: string;
  name: string;
  color?: string | null;
  projectId?: string | null;
}

/** `tm_issues.__history` içindeki tek alan değişikliği (eski / yeni). */
export interface TmIssueHistoryFieldChange {
  field?: string;
  label?: string | null;
  oldValue?: unknown;
  newValue?: unknown;
}

/** `tm_issues.__history` dizisinin bir öğesi — kim, ne zaman, hangi alanlar. */
export interface TmIssueHistoryEntry {
  changedAt?: string | null;
  userId?: string | null;
  userName?: string | null;
  changes: TmIssueHistoryFieldChange[];
}

export interface TmIssue {
  __dataId: string;
  key: string;
  projectKey: string;
  projectId: string;
  issueTypeId: string;
  title: string;
  description?: string | null;
  statusId: string;
  priorityId?: string | null;
  assignee?: unknown;
  epicId?: string | null;
  sprintId?: string | null;
  /** relation id list (tm_labels __dataId) */
  labels?: string[] | null;
  dueDate?: string | null;
  storyPoints?: number | null;
  order?: number | null;
  /** tm_issues kaydındaki şema dışı / havuz alanları (DG’den gelen ek property’ler) */
  extraFields?: Record<string, unknown>;
  /** DG `__history` — görev alan değişiklik günlüğü (parse edilmiş). */
  issueHistory?: TmIssueHistoryEntry[] | null;
}

/** tm_issue_comments — görev yorumu; metinde mention: `@[userId]` (emoji serbest). */
export interface TmIssueComment {
  __dataId: string;
  issueId: string;
  projectId: string;
  /** persons alanı — string id veya DG nesnesi */
  author: unknown;
  body: string;
  parentCommentId: string | null;
  createdAt: string | null;
  updatedAt: string | null;
}
