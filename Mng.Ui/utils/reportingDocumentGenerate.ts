import {
  diCreateFolder,
  diGetChildren,
  diGetTemplate,
  diListTemplates,
} from '@/services/documentIntelligenceService';
import {
  DI_RESOURCE_TYPE,
  type DiResource,
} from '@/types/apps/documentIntelligence';
import type { ReportingDocumentBinding } from '@/types/apps/reporting';
import type { OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import { reportingCellExportValue } from '@/utils/reportingCellDisplay';
import {
  defaultReportingDocumentFolderSegments,
  resolveReportingDocumentFolderSegments,
} from '@/utils/reportingDocumentBindings';

/** DI şablon id — id yoksa / geçersizse code ile çöz. */
export async function resolveReportingDiTemplateId(
  binding: ReportingDocumentBinding
): Promise<string> {
  const code = (binding.templateCode ?? '').trim() || binding.templateId.trim();
  const id = binding.templateId.trim();
  if (id) {
    try {
      const tpl = await diGetTemplate(id);
      if (tpl?.id) return tpl.id;
    } catch {
      // fall through to code lookup
    }
  }
  if (!code) {
    throw new Error('Template id/code missing');
  }
  const listed = await diListTemplates();
  const found = listed.items.find(
    (t) => t.code === code || t.id === code || t.id === id
  );
  if (!found?.id) {
    throw new Error(`DI template not found: ${code}`);
  }
  return found.id;
}

/** Klasör yolunu oluştur / mevcut id döndür (kökten). */
export async function ensureDiFolderPath(segments: string[]): Promise<string> {
  let parentId: string | null = null;
  for (const segment of segments) {
    const name = segment.trim();
    if (!name) continue;
    const listing = await diGetChildren(parentId);
    const existing = listing.items.find(
      (r) =>
        (r.type === DI_RESOURCE_TYPE.folder || r.type === 'folder') && r.name === name
    );
    if (existing?.id) {
      parentId = existing.id;
      continue;
    }
    const created = await diCreateFolder({
      parentId,
      name,
    });
    parentId = created.id;
  }
  if (!parentId) {
    throw new Error('Folder path empty');
  }
  return parentId;
}

export function reportingDocumentFolderParentId(
  reportId: string,
  binding: ReportingDocumentBinding
): Promise<string> {
  return ensureDiFolderPath(resolveReportingDocumentFolderSegments(reportId, binding));
}

/** Klasör yoksa null — liste için oluşturmaz. */
export async function tryResolveDiFolderPath(segments: string[]): Promise<string | null> {
  let parentId: string | null = null;
  for (const segment of segments) {
    const name = segment.trim();
    if (!name) continue;
    const listing = await diGetChildren(parentId);
    const existing = listing.items.find(
      (r) =>
        (r.type === DI_RESOURCE_TYPE.folder || r.type === 'folder') && r.name === name
    );
    if (!existing?.id) return null;
    parentId = existing.id;
  }
  return parentId;
}

/** Rapor klasöründeki üretilmiş dosyalar (yeniden eskiye). */
export async function listReportingGeneratedDocuments(
  reportId: string,
  binding?: ReportingDocumentBinding | null
): Promise<DiResource[]> {
  const segments = binding
    ? resolveReportingDocumentFolderSegments(reportId, binding)
    : defaultReportingDocumentFolderSegments(reportId);
  const folderId = await tryResolveDiFolderPath(segments);
  if (!folderId) return [];

  const listing = await diGetChildren(folderId);
  return listing.items
    .filter((r) => r.type === DI_RESOURCE_TYPE.file || r.type === 'file')
    .sort((a, b) => {
      const ta = a.createdAt ? Date.parse(a.createdAt) : 0;
      const tb = b.createdAt ? Date.parse(b.createdAt) : 0;
      return tb - ta;
    });
}

/**
 * Görünür sütunları tablo satırına map et.
 * Anahtar: relationDisplayField varsa o, yoksa fieldName (XLSX {{rows.key}} uyumu).
 */
export function mapReportingRowsForDocumentTable(
  rows: Record<string, unknown>[],
  listConfig: OdakHubListConfig
): Record<string, unknown>[] {
  const columns = [...(listConfig.columns ?? [])]
    .filter((c) => c.visible !== false)
    .sort((a, b) => a.order - b.order);

  return rows.map((row) => {
    const out: Record<string, unknown> = {};
    const used = new Set<string>();
    for (const col of columns) {
      let key = (col.relationDisplayField ?? '').trim() || col.fieldName;
      if (used.has(key)) {
        let i = 2;
        while (used.has(`${key}_${i}`)) i += 1;
        key = `${key}_${i}`;
      }
      used.add(key);
      out[key] = reportingCellExportValue(row, col);
    }
    return out;
  });
}

/** Parent satır → skaler override sözlüğü (görünür sütun anahtarları). */
export function mapReportingParentRowOverrides(
  row: Record<string, unknown>,
  listConfig: OdakHubListConfig
): Record<string, string> {
  const mapped = mapReportingRowsForDocumentTable([row], listConfig)[0] ?? {};
  const out: Record<string, string> = {};
  for (const [k, v] of Object.entries(mapped)) {
    out[k] = v == null ? '' : String(v);
  }
  return out;
}
