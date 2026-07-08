import { diGenerateDocument } from '@/services/documentIntelligenceService';
import type { DiGenerateDocumentResult } from '@/types/apps/documentIntelligence';
import type { OdakPackageRow } from '@/utils/odakSiparisConfig';
import { lineDocumentHasParameterWarnings } from '@/utils/odakSiparisLineDocumentService';

/** İş paketi bağlamı — paket düzeyi DI üretimleri (G5 / PACKAGE-BRIEF). */
export const ODAK_PACKAGE_CONTEXT_TYPE = 'odak.siparis.package';

export const ODAK_SHIPMENT_LIST_PROFILE_CODE = 'odak.shipmentList.fromPackage';
export const ODAK_SHIPMENT_LIST_TEMPLATE_CODE = 'SHIPMENT-LIST-STD';

export const ODAK_PACKAGE_DASHBOARD_PROFILE_CODE = 'odak.package.dashboard.fromPackage';
export const ODAK_PACKAGE_DASHBOARD_TEMPLATE_CODE = 'PACKAGE-DASHBOARD-STD';

export const ODAK_PACKAGE_BRIEF_PROFILE_CODE = 'odak.package.brief.fromPackage';
export const ODAK_PACKAGE_BRIEF_TEMPLATE_CODE = 'PACKAGE-BRIEF-STD';

export interface OdakPackageShipmentListState {
  resourceId?: string;
  fileName?: string;
  generatedAt?: string;
  templateCode?: string;
  templateName?: string;
}

export interface OdakPackageDashboardState {
  resourceId?: string;
  fileName?: string;
  generatedAt?: string;
  templateCode?: string;
  templateName?: string;
}

export interface OdakPackageBriefState {
  resourceId?: string;
  fileName?: string;
  generatedAt?: string;
  templateCode?: string;
  templateName?: string;
}

export function packageShipmentListFromRow(
  row: OdakPackageRow | null | undefined
): OdakPackageShipmentListState | null {
  const resourceId = row?.shipmentListDiResourceId?.trim();
  if (!resourceId) return null;
  return {
    resourceId,
    fileName: row?.shipmentListFileName?.trim() || undefined,
    generatedAt: row?.shipmentListGeneratedAt?.trim() || undefined,
    templateCode: row?.shipmentListTemplateCode?.trim() || undefined,
    templateName: row?.shipmentListTemplateName?.trim() || undefined,
  };
}

export function packageDashboardFromRow(
  row: OdakPackageRow | null | undefined
): OdakPackageDashboardState | null {
  const resourceId = row?.packageDashboardDiResourceId?.trim();
  if (!resourceId) return null;
  return {
    resourceId,
    fileName: row?.packageDashboardFileName?.trim() || undefined,
    generatedAt: row?.packageDashboardGeneratedAt?.trim() || undefined,
    templateCode: row?.packageDashboardTemplateCode?.trim() || undefined,
    templateName: row?.packageDashboardTemplateName?.trim() || undefined,
  };
}

export function packageBriefFromRow(row: OdakPackageRow | null | undefined): OdakPackageBriefState | null {
  const resourceId = row?.packageBriefDiResourceId?.trim();
  if (!resourceId) return null;
  return {
    resourceId,
    fileName: row?.packageBriefFileName?.trim() || undefined,
    generatedAt: row?.packageBriefGeneratedAt?.trim() || undefined,
    templateCode: row?.packageBriefTemplateCode?.trim() || undefined,
    templateName: row?.packageBriefTemplateName?.trim() || undefined,
  };
}

export function packageShipmentListToGenerateResult(
  state: OdakPackageShipmentListState
): DiGenerateDocumentResult {
  return {
    profileCode: ODAK_SHIPMENT_LIST_PROFILE_CODE,
    contextType: ODAK_PACKAGE_CONTEXT_TYPE,
    contextId: '',
    templateId: '',
    templateCode: state.templateCode ?? ODAK_SHIPMENT_LIST_TEMPLATE_CODE,
    resourceId: state.resourceId ?? '',
    fileName: state.fileName ?? '',
    folderPath: [],
    generatedAt: state.generatedAt ?? '',
    resolvedValues: {},
    undefinedParameterKeys: [],
    unresolvedParameterKeys: [],
    remainingPlaceholderKeys: [],
    hasParameterWarnings: false,
  };
}

export function packageDashboardToGenerateResult(state: OdakPackageDashboardState): DiGenerateDocumentResult {
  return {
    profileCode: ODAK_PACKAGE_DASHBOARD_PROFILE_CODE,
    contextType: ODAK_PACKAGE_CONTEXT_TYPE,
    contextId: '',
    templateId: '',
    templateCode: state.templateCode ?? ODAK_PACKAGE_DASHBOARD_TEMPLATE_CODE,
    resourceId: state.resourceId ?? '',
    fileName: state.fileName ?? '',
    folderPath: [],
    generatedAt: state.generatedAt ?? '',
    resolvedValues: {},
    undefinedParameterKeys: [],
    unresolvedParameterKeys: [],
    remainingPlaceholderKeys: [],
    hasParameterWarnings: false,
  };
}

export function packageBriefToGenerateResult(state: OdakPackageBriefState): DiGenerateDocumentResult {
  return {
    profileCode: ODAK_PACKAGE_BRIEF_PROFILE_CODE,
    contextType: ODAK_PACKAGE_CONTEXT_TYPE,
    contextId: '',
    templateId: '',
    templateCode: state.templateCode ?? ODAK_PACKAGE_BRIEF_TEMPLATE_CODE,
    resourceId: state.resourceId ?? '',
    fileName: state.fileName ?? '',
    folderPath: [],
    generatedAt: state.generatedAt ?? '',
    resolvedValues: {},
    undefinedParameterKeys: [],
    unresolvedParameterKeys: [],
    remainingPlaceholderKeys: [],
    hasParameterWarnings: false,
  };
}

export async function generateOdakPackageShipmentList(
  packageId: string
): Promise<DiGenerateDocumentResult> {
  return generateOdakPackageDocument(packageId, ODAK_SHIPMENT_LIST_PROFILE_CODE, ODAK_SHIPMENT_LIST_TEMPLATE_CODE);
}

export async function generateOdakPackageDashboard(packageId: string): Promise<DiGenerateDocumentResult> {
  return generateOdakPackageDocument(
    packageId,
    ODAK_PACKAGE_DASHBOARD_PROFILE_CODE,
    ODAK_PACKAGE_DASHBOARD_TEMPLATE_CODE
  );
}

export async function generateOdakPackageBrief(packageId: string): Promise<DiGenerateDocumentResult> {
  return generateOdakPackageDocument(
    packageId,
    ODAK_PACKAGE_BRIEF_PROFILE_CODE,
    ODAK_PACKAGE_BRIEF_TEMPLATE_CODE
  );
}

async function generateOdakPackageDocument(
  packageId: string,
  profileCode: string,
  templateCode: string
): Promise<DiGenerateDocumentResult> {
  const id = packageId?.trim() ?? '';
  if (!id) {
    throw new Error('packageId is required');
  }

  return diGenerateDocument({
    profileCode,
    templateCode,
    context: { type: ODAK_PACKAGE_CONTEXT_TYPE, id },
  });
}

export function packageDocumentHasParameterWarnings(result: DiGenerateDocumentResult): boolean {
  return lineDocumentHasParameterWarnings(result);
}
