import {
  diErrorCode,
  diErrorStatus,
  diExtractMessage,
  diGenerateDocument,
} from '@/services/documentIntelligenceService';
import type { DiGenerateDocumentResult } from '@/types/apps/documentIntelligence';
import { buildDiResourceUrl } from '@/utils/diResourceLink';

/** Sipariş kalemi bağlamı — tüm kalem belgeleri için ortak. */
export const ODAK_LINE_CONTEXT_TYPE = 'odak.siparis.line';

export const ODAK_COC_PROFILE_CODE = 'odak.coc.fromLine';
export const ODAK_LINE_ACTIVITY_PROFILE_CODE = 'odak.line.activity.fromLine';

export const ODAK_LINE_DOCUMENT_PROFILE_CODES = [
  ODAK_COC_PROFILE_CODE,
  ODAK_LINE_ACTIVITY_PROFILE_CODE,
] as const;

export type OdakLineDocumentProfileCode = (typeof ODAK_LINE_DOCUMENT_PROFILE_CODES)[number];

export type OdakLineDocumentKind = 'coc' | 'activity';

export interface OdakLineDocumentRow {
  rowKey: string;
  kind: OdakLineDocumentKind;
  profileCode: OdakLineDocumentProfileCode;
  line: import('@/utils/odakSiparisConfig').OdakLineRow;
  docNo?: string;
  templateName?: string;
  templateCode?: string;
  generatedAt?: string;
  resourceId?: string;
}

const PROFILE_KIND: Record<OdakLineDocumentProfileCode, OdakLineDocumentKind> = {
  [ODAK_COC_PROFILE_CODE]: 'coc',
  [ODAK_LINE_ACTIVITY_PROFILE_CODE]: 'activity',
};

export function profileCodeForTemplate(
  generationProfile?: string | null
): OdakLineDocumentProfileCode | null {
  const code = generationProfile?.trim();
  if (!code) return null;
  return (ODAK_LINE_DOCUMENT_PROFILE_CODES as readonly string[]).includes(code)
    ? (code as OdakLineDocumentProfileCode)
    : null;
}

export function isSingleGenerationProfile(profileCode: OdakLineDocumentProfileCode): boolean {
  return profileCode === ODAK_COC_PROFILE_CODE;
}

export async function generateOdakLineDocument(
  lineId: string,
  templateCode: string,
  profileCode: OdakLineDocumentProfileCode
): Promise<DiGenerateDocumentResult> {
  return diGenerateDocument({
    profileCode,
    templateCode: templateCode.trim(),
    context: { type: ODAK_LINE_CONTEXT_TYPE, id: lineId },
  });
}

export function isLineDocumentAlreadyGeneratedError(error: unknown): boolean {
  return diErrorCode(error) === 'DOCUMENT_ALREADY_GENERATED' || diErrorStatus(error) === 409;
}

export function lineDocumentErrorMessage(error: unknown, fallback: string): string {
  return diExtractMessage(error, fallback);
}

export function diResourceUrl(resourceId: string): string {
  return buildDiResourceUrl(resourceId);
}

export function lineDocumentParameterWarningKeys(result: DiGenerateDocumentResult): string[] {
  const keys = new Set<string>();
  for (const k of result.undefinedParameterKeys ?? []) keys.add(k);
  for (const k of result.unresolvedParameterKeys ?? []) keys.add(k);
  for (const k of result.remainingPlaceholderKeys ?? []) keys.add(k);
  return [...keys].sort((a, b) => a.localeCompare(b));
}

export function lineDocumentHasParameterWarnings(result: DiGenerateDocumentResult): boolean {
  if (result.hasParameterWarnings) return true;
  return lineDocumentParameterWarningKeys(result).length > 0;
}

export function flattenLineDocuments(
  lines: import('@/utils/odakSiparisConfig').OdakLineRow[],
  lineDataId: (row: import('@/utils/odakSiparisConfig').OdakLineRow) => string | null
): OdakLineDocumentRow[] {
  const rows: OdakLineDocumentRow[] = [];
  for (const line of lines) {
    const id = lineDataId(line);
    if (!id) continue;
    if (line.cocDiResourceId?.trim()) {
      rows.push({
        rowKey: `${id}-coc`,
        kind: 'coc',
        profileCode: ODAK_COC_PROFILE_CODE,
        line,
        docNo: line.cocDocNo,
        templateName: line.cocTemplateName,
        templateCode: line.cocTemplateCode,
        generatedAt: line.cocGeneratedAt,
        resourceId: line.cocDiResourceId,
      });
    }
    if (line.activityDiResourceId?.trim()) {
      rows.push({
        rowKey: `${id}-activity`,
        kind: 'activity',
        profileCode: ODAK_LINE_ACTIVITY_PROFILE_CODE,
        line,
        docNo: line.activityDocNo,
        templateName: line.activityTemplateName,
        templateCode: line.activityTemplateCode,
        generatedAt: line.activityGeneratedAt,
        resourceId: line.activityDiResourceId,
      });
    }
  }
  return rows.sort((a, b) => {
    const lineCmp = (a.line.lineNo ?? 0) - (b.line.lineNo ?? 0);
    if (lineCmp !== 0) return lineCmp;
    return a.kind.localeCompare(b.kind);
  });
}

export function lineEligibleForProfile(
  line: import('@/utils/odakSiparisConfig').OdakLineRow,
  profileCode: OdakLineDocumentProfileCode
): boolean {
  if (profileCode === ODAK_COC_PROFILE_CODE) {
    return !line.cocDiResourceId?.trim();
  }
  return true;
}

export function documentKindFromProfile(
  profileCode: OdakLineDocumentProfileCode
): OdakLineDocumentKind {
  return PROFILE_KIND[profileCode];
}
