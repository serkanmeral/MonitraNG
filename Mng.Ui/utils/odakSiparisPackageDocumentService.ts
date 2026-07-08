import { diGenerateDocument } from '@/services/documentIntelligenceService';
import type { DiGenerateDocumentResult } from '@/types/apps/documentIntelligence';
import { lineDocumentHasParameterWarnings } from '@/utils/odakSiparisLineDocumentService';

/** İş paketi bağlamı — paket düzeyi DI üretimleri (G5). */
export const ODAK_PACKAGE_CONTEXT_TYPE = 'odak.siparis.package';

export const ODAK_SHIPMENT_LIST_PROFILE_CODE = 'odak.shipmentList.fromPackage';
export const ODAK_SHIPMENT_LIST_TEMPLATE_CODE = 'SHIPMENT-LIST-STD';

export async function generateOdakPackageShipmentList(
  packageId: string
): Promise<DiGenerateDocumentResult> {
  const id = packageId?.trim() ?? '';
  if (!id) {
    throw new Error('packageId is required');
  }

  return diGenerateDocument({
    profileCode: ODAK_SHIPMENT_LIST_PROFILE_CODE,
    templateCode: ODAK_SHIPMENT_LIST_TEMPLATE_CODE,
    context: { type: ODAK_PACKAGE_CONTEXT_TYPE, id },
  });
}

export function packageDocumentHasParameterWarnings(result: DiGenerateDocumentResult): boolean {
  return lineDocumentHasParameterWarnings(result);
}
