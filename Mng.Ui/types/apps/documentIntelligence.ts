// Document Intelligence (MngDocument) — Faz 1 tipleri.
// API: gateway /documents/api/v1/resources (camelCase JSON).

export const DI_RESOURCE_TYPE = {
  folder: 'folder',
  markdown: 'markdown',
  file: 'file',
} as const;

export type DiResourceType = (typeof DI_RESOURCE_TYPE)[keyof typeof DI_RESOURCE_TYPE];

/** Tek kaynak (klasör / markdown / dosya) metadata'sı. */
export interface DiResource {
  id: string;
  type: DiResourceType | string;
  parentId: string | null;
  ancestorIds: string[];
  name: string;
  title: string | null;
  description: string | null;
  tags: string[];
  contentType: string | null;
  mimeType: string | null;
  extension: string | null;
  size: number | null;
  currentVersionNumber: number;
  hasContent: boolean;
  /** Yüklenen dosyanın MinIO path'i (yalnızca type=file). İndirme için. */
  filePath: string | null;
  /** Yüklenen dosyanın orijinal adı (yalnızca type=file). */
  fileName: string | null;
  createdAt: string | null;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

/** Sol panel ağaç düğümü (yalnızca klasörler, iç içe). */
export interface DiTreeNode {
  id: string;
  name: string;
  parentId: string | null;
  children: DiTreeNode[];
}

/** Breadcrumb / yol bilgisi. */
export interface DiBreadcrumb {
  id: string;
  name: string;
}

export interface DiResourceListResult {
  items: DiResource[];
  total: number;
}

export interface DiMarkdownContent {
  id: string;
  title: string | null;
  content: string;
  currentVersionNumber: number;
}

/** Sürüm geçmişi satırı (içerik hariç). */
export interface DiMarkdownVersion {
  versionNumber: number;
  changeNote: string | null;
  size: number | null;
  createdAt: string | null;
  createdBy: string | null;
  isCurrent: boolean;
}

/** Tek bir sürümün içeriği. */
export interface DiMarkdownVersionContent {
  versionNumber: number;
  content: string;
  changeNote: string | null;
  createdAt: string | null;
  createdBy: string | null;
}

// --- İstek modelleri ---

export interface DiCreateFolderRequest {
  parentId?: string | null;
  name: string;
  description?: string | null;
  tags?: string[];
}

export interface DiRenameRequest {
  name: string;
}

export interface DiMoveRequest {
  newParentId?: string | null;
}

export interface DiCreateMarkdownRequest {
  parentId?: string | null;
  title: string;
  content: string;
  description?: string | null;
  tags?: string[];
}

export interface DiUpdateMarkdownRequest {
  title?: string | null;
  content: string;
  description?: string | null;
  tags?: string[];
  expectedVersionNumber: number;
}

export interface DiCreateFileResourceRequest {
  parentId?: string | null;
  name: string;
  description?: string | null;
  mimeType?: string | null;
  extension?: string | null;
  size?: number | null;
  tags?: string[];
  /** Base64 dosya içeriği (data URL öneki olmadan). */
  content: string;
  /** Orijinal dosya adı (indirmede kullanılır). */
  originalFileName?: string | null;
}

/** Ağaç kökü için sanal düğüm kimliği (UI). */
export const DI_ROOT_ID = '__di_root__';
