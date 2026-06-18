/**
 * Odak Sipariş — iş paketi PO PDF erişim yapılandırması (hub scope: package_po_document_access).
 */

import type { OdakPackageRow } from '@/utils/odakSiparisConfig';

export interface OdakPackagePoDocumentAccessConfig {
  /** Yetkilendirilmiş PO PDF alanını görebilecek Keeper grup adları. Boş = kimse göremez. */
  restrictedViewerGroups: string[];
}

export function defaultOdakPackagePoDocumentAccessConfig(): OdakPackagePoDocumentAccessConfig {
  return { restrictedViewerGroups: [] };
}

function parseStringArray(raw: unknown): string[] {
  if (raw == null) return [];
  if (!Array.isArray(raw)) return [];
  return raw.map((v) => String(v).trim()).filter(Boolean);
}

export function mergeOdakPackagePoDocumentAccessConfig(saved: unknown): OdakPackagePoDocumentAccessConfig {
  const base = defaultOdakPackagePoDocumentAccessConfig();
  if (!saved || typeof saved !== 'object') return base;
  const o = saved as Record<string, unknown>;
  const nested = o.poDocumentAccess ?? o.PoDocumentAccess;
  if (nested && typeof nested === 'object') {
    const n = nested as Record<string, unknown>;
    return {
      restrictedViewerGroups: parseStringArray(
        n.restrictedViewerGroups ?? n.RestrictedViewerGroups
      ),
    };
  }
  return {
    restrictedViewerGroups: parseStringArray(
      o.restrictedViewerGroups ?? o.RestrictedViewerGroups
    ),
  };
}

function groupMatches(userGroup: string, allowed: string): boolean {
  return userGroup.localeCompare(allowed, undefined, { sensitivity: 'accent' }) === 0;
}

/** Kullanıcı yetkilendirilmiş PO PDF alanını görebilir mi? */
export function canViewRestrictedPoDocuments(
  userGroups: string[],
  config: OdakPackagePoDocumentAccessConfig
): boolean {
  const allowed = config.restrictedViewerGroups ?? [];
  if (!allowed.length) return false;
  return userGroups.some((g) => allowed.some((a) => groupMatches(g, a)));
}

/** API yanıtından yetkisiz kullanıcı için kısıtlı PO alanlarını çıkarır. */
export function sanitizePackageRowPoDocuments(
  row: OdakPackageRow,
  userGroups: string[],
  config: OdakPackagePoDocumentAccessConfig
): OdakPackageRow {
  if (canViewRestrictedPoDocuments(userGroups, config)) return row;
  return {
    ...row,
    poDocumentsRestricted: null,
    poDocumentPathRedacted: null,
  };
}
