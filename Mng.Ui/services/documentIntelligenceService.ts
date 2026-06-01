import { fetchFromDocuments, fetchBlobFromDataGateway } from '@/services/apiService';
import {
  diFullPermission,
  type DiBreadcrumb,
  type DiCreateFileResourceRequest,
  type DiCreateFolderRequest,
  type DiCreateMarkdownRequest,
  type DiEffectivePermission,
  type DiFolderPermissions,
  type DiGroupPermission,
  type DiMarkdownContent,
  type DiMarkdownVersion,
  type DiMarkdownVersionContent,
  type DiMoveRequest,
  type DiRenameRequest,
  type DiResource,
  type DiResourceListResult,
  type DiSetFolderPermissionsRequest,
  type DiTreeNode,
  type DiUpdateMarkdownRequest,
} from '@/types/apps/documentIntelligence';

const BASE = '/api/v1/resources';

function asRecord(raw: unknown): Record<string, unknown> {
  return raw && typeof raw === 'object' ? (raw as Record<string, unknown>) : {};
}

function str(obj: Record<string, unknown>, key: string): string | null {
  const v = obj[key];
  return v == null || v === '' ? null : String(v);
}

function strArray(raw: unknown): string[] {
  if (!Array.isArray(raw)) return [];
  return raw.map((v) => String(v)).filter((v) => v.length > 0);
}

function num(obj: Record<string, unknown>, key: string): number | null {
  const v = obj[key];
  if (v == null || v === '') return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}

function mapEffective(raw: unknown): DiEffectivePermission {
  const o = asRecord(raw);
  // Backend her zaman doldurur; alan yoksa güvenli varsayılan açık (eski yanıtlarla uyum).
  if (!raw || typeof raw !== 'object') return diFullPermission();
  return {
    canView: Boolean(o.canView),
    canCreate: Boolean(o.canCreate),
    canEdit: Boolean(o.canEdit),
    canDelete: Boolean(o.canDelete),
    canUpload: Boolean(o.canUpload),
    canDownload: Boolean(o.canDownload),
    canMove: Boolean(o.canMove),
    canShare: Boolean(o.canShare),
  };
}

function mapResource(raw: unknown): DiResource {
  const o = asRecord(raw);
  return {
    id: str(o, 'id') ?? '',
    type: str(o, 'type') ?? 'folder',
    parentId: str(o, 'parentId'),
    ancestorIds: strArray(o.ancestorIds),
    name: str(o, 'name') ?? '',
    title: str(o, 'title'),
    description: str(o, 'description'),
    tags: strArray(o.tags),
    contentType: str(o, 'contentType'),
    mimeType: str(o, 'mimeType'),
    extension: str(o, 'extension'),
    size: num(o, 'size'),
    currentVersionNumber: num(o, 'currentVersionNumber') ?? 0,
    hasContent: Boolean(o.hasContent),
    status: str(o, 'status') ?? 'published',
    filePath: str(o, 'filePath'),
    fileName: str(o, 'fileName'),
    createdAt: str(o, 'createdAt'),
    createdBy: str(o, 'createdBy'),
    updatedAt: str(o, 'updatedAt'),
    updatedBy: str(o, 'updatedBy'),
    permissions: mapEffective(o.permissions),
  };
}

function mapGroupPermission(raw: unknown): DiGroupPermission {
  const o = asRecord(raw);
  return {
    groupId: str(o, 'groupId'),
    groupName: str(o, 'groupName') ?? '',
    permissions: strArray(o.permissions),
  };
}

function mapFolderPermissions(raw: unknown): DiFolderPermissions {
  const o = asRecord(raw);
  const groupsRaw = o.groups;
  return {
    resourceId: str(o, 'resourceId') ?? '',
    inheritanceBroken: Boolean(o.inheritanceBroken),
    effectiveAnchorId: str(o, 'effectiveAnchorId'),
    groups: Array.isArray(groupsRaw) ? groupsRaw.map(mapGroupPermission) : [],
    effective: mapEffective(o.effective),
  };
}

function mapTreeNode(raw: unknown): DiTreeNode {
  const o = asRecord(raw);
  const childrenRaw = o.children;
  return {
    id: str(o, 'id') ?? '',
    name: str(o, 'name') ?? '',
    parentId: str(o, 'parentId'),
    children: Array.isArray(childrenRaw) ? childrenRaw.map(mapTreeNode) : [],
  };
}

function mapListResult(raw: unknown): DiResourceListResult {
  const o = asRecord(raw);
  const items = Array.isArray(o.items) ? o.items.map(mapResource) : [];
  return { items, total: num(o, 'total') ?? items.length };
}

/** Klasör ağacı (yalnızca klasörler, iç içe). */
export async function diGetTree(): Promise<DiTreeNode[]> {
  const raw = await fetchFromDocuments(`${BASE}/tree`, 'GET');
  return Array.isArray(raw) ? raw.map(mapTreeNode) : [];
}

/** Bir klasörün içeriği (klasör + markdown + dosya). parentId boşsa kök. */
export async function diGetChildren(parentId?: string | null): Promise<DiResourceListResult> {
  const qs = parentId ? `?parentId=${encodeURIComponent(parentId)}` : '';
  const raw = await fetchFromDocuments(`${BASE}/children${qs}`, 'GET');
  return mapListResult(raw);
}

/** Full-text arama (DG regex: ad/başlık/açıklama + markdown içeriği). */
export async function diSearch(q: string, skip = 0, limit = 50): Promise<DiResourceListResult> {
  const params = new URLSearchParams({ q, skip: String(skip), limit: String(limit) });
  const raw = await fetchFromDocuments(`${BASE}/search?${params.toString()}`, 'GET');
  return mapListResult(raw);
}

/** Tek kaynak metadata'sı. */
export async function diGetById(id: string): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(id)}`, 'GET');
  return mapResource(raw);
}

/** Kök -> ... -> kaynak yol bilgisi (breadcrumb). */
export async function diGetBreadcrumb(id: string): Promise<DiBreadcrumb[]> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(id)}/breadcrumb`, 'GET');
  if (!Array.isArray(raw)) return [];
  return raw.map((r) => {
    const o = asRecord(r);
    return { id: str(o, 'id') ?? '', name: str(o, 'name') ?? '' };
  });
}

export async function diCreateFolder(request: DiCreateFolderRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/folder`, 'POST', request);
  return mapResource(raw);
}

export async function diRename(id: string, request: DiRenameRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(id)}/rename`, 'PUT', request);
  return mapResource(raw);
}

export async function diMove(id: string, request: DiMoveRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(id)}/move`, 'PUT', request);
  return mapResource(raw);
}

/** Kaynağı siler. force=true: dolu klasörleri de siler (cascade). */
export async function diDelete(id: string, force = false): Promise<void> {
  const qs = force ? '?force=true' : '';
  await fetchFromDocuments(`${BASE}/${encodeURIComponent(id)}${qs}`, 'DELETE');
}

export async function diCreateMarkdown(request: DiCreateMarkdownRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/markdown`, 'POST', request);
  return mapResource(raw);
}

export async function diUpdateMarkdown(id: string, request: DiUpdateMarkdownRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/markdown/${encodeURIComponent(id)}`, 'PUT', request);
  return mapResource(raw);
}

export async function diGetMarkdownContent(id: string): Promise<DiMarkdownContent> {
  const raw = await fetchFromDocuments(`${BASE}/markdown/${encodeURIComponent(id)}/content`, 'GET');
  const o = asRecord(raw);
  return {
    id: str(o, 'id') ?? '',
    title: str(o, 'title'),
    content: str(o, 'content') ?? '',
    currentVersionNumber: num(o, 'currentVersionNumber') ?? 0,
  };
}

/** Markdown sürüm geçmişi (içerik hariç, en yeni önce). */
export async function diGetMarkdownVersions(id: string): Promise<DiMarkdownVersion[]> {
  const raw = await fetchFromDocuments(`${BASE}/markdown/${encodeURIComponent(id)}/versions`, 'GET');
  if (!Array.isArray(raw)) return [];
  return raw.map((r) => {
    const o = asRecord(r);
    return {
      versionNumber: num(o, 'versionNumber') ?? 0,
      changeNote: str(o, 'changeNote'),
      size: num(o, 'size'),
      createdAt: str(o, 'createdAt'),
      createdBy: str(o, 'createdBy'),
      isCurrent: Boolean(o.isCurrent),
    };
  });
}

/** Tek bir markdown sürümünün içeriği. */
export async function diGetMarkdownVersionContent(id: string, versionNumber: number): Promise<DiMarkdownVersionContent> {
  const raw = await fetchFromDocuments(`${BASE}/markdown/${encodeURIComponent(id)}/versions/${versionNumber}`, 'GET');
  const o = asRecord(raw);
  return {
    versionNumber: num(o, 'versionNumber') ?? versionNumber,
    content: str(o, 'content') ?? '',
    changeNote: str(o, 'changeNote'),
    createdAt: str(o, 'createdAt'),
    createdBy: str(o, 'createdBy'),
  };
}

/** Eski bir sürümü yeni sürüm olarak geri yükler. */
export async function diRestoreMarkdownVersion(id: string, versionNumber: number): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/markdown/${encodeURIComponent(id)}/versions/${versionNumber}/restore`, 'POST');
  return mapResource(raw);
}

export async function diCreateFileResource(request: DiCreateFileResourceRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/file`, 'POST', request);
  return mapResource(raw);
}

/** Yüklenen dosyayı DG üzerinden blob olarak indirir (binary MngDocument'ten geçmez). */
export async function diFetchFileBlob(filePath: string): Promise<Blob> {
  return fetchBlobFromDataGateway(`/api/v1/files/download?filePath=${encodeURIComponent(filePath)}`);
}

// --- Grup bazlı klasör yetkilendirmesi + miras ---

/** Klasörün yetki yönetim görünümü (miras durumu + grup matrisi + etkin yetki). */
export async function diGetPermissions(folderId: string): Promise<DiFolderPermissions> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(folderId)}/permissions`, 'GET');
  return mapFolderPermissions(raw);
}

/** Anchor (mirası kırık) klasörde grup yetki matrisini değiştirir (tam değişim). */
export async function diSetPermissions(folderId: string, request: DiSetFolderPermissionsRequest): Promise<DiFolderPermissions> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(folderId)}/permissions`, 'PUT', request);
  return mapFolderPermissions(raw);
}

/** Klasörün yetki mirasını kırar (üst anchor'ın ACL'ini kopyalar). */
export async function diBreakInheritance(folderId: string): Promise<DiFolderPermissions> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(folderId)}/permissions/break-inheritance`, 'POST');
  return mapFolderPermissions(raw);
}

/** Klasörün kendi ACL'ini silip yetki mirasını geri yükler. */
export async function diRestoreInheritance(folderId: string): Promise<DiFolderPermissions> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(folderId)}/permissions/restore-inheritance`, 'POST');
  return mapFolderPermissions(raw);
}

/** MngDocument/HTTP hata gövdesinden `code` döndürür (guard ayrımı için, örn. RESOURCE_HAS_CHILDREN). */
export function diErrorCode(error: unknown): string | null {
  if (!(error instanceof Error)) return null;
  const data = (error as { data?: unknown }).data;
  if (data && typeof data === 'object') {
    const code = (data as Record<string, unknown>).code;
    if (typeof code === 'string') return code;
  }
  return null;
}

/** HTTP durum kodunu döndürür (örn. 409 conflict ayrımı). */
export function diErrorStatus(error: unknown): number | null {
  if (!error || typeof error !== 'object') return null;
  const e = error as Record<string, unknown>;
  const sc = e.statusCode ?? e.status;
  return typeof sc === 'number' ? sc : null;
}

/** Hata gövdesinden okunabilir mesaj çıkarır (messageTr > message > genel). */
export function diExtractMessage(error: unknown, fallback: string): string {
  if (error instanceof Error) {
    const data = (error as { data?: unknown }).data;
    if (data && typeof data === 'object') {
      const d = data as Record<string, unknown>;
      if (typeof d.messageTr === 'string' && d.messageTr) return d.messageTr;
      if (typeof d.message === 'string' && d.message) return d.message;
      const nested = d.error;
      if (nested && typeof nested === 'object') {
        const ne = nested as Record<string, unknown>;
        if (typeof ne.message === 'string' && ne.message) return ne.message;
      }
    }
    return error.message || fallback;
  }
  return fallback;
}
