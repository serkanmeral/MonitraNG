import { diGenerateFromTemplate } from '@/services/documentIntelligenceService';
import type { DiGenerateDocumentResult } from '@/types/apps/documentIntelligence';
import type { ReportingDocumentBinding } from '@/types/apps/reporting';
import type { OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import {
  mapReportingParentRowOverrides,
  reportingDocumentFolderParentId,
  resolveReportingDiTemplateId,
} from '@/utils/reportingDocumentGenerate';
import {
  buildReportingDocumentTokenContext,
  resolveReportingDocumentName,
  resolveReportingGeneratedAt,
} from '@/utils/reportingDocumentTokens';
import { reportingCellExportValue } from '@/utils/reportingCellDisplay';
import { reportingRowId } from '@/utils/reportingExpandLayout';
import { columnConfigByField } from '@/utils/reportingListConfig';

function pickTrainingDate(parentOverrides: Record<string, string>): string {
  return (
    parentOverrides.gerceklesenTarih?.trim() ||
    parentOverrides.planlananTarih?.trim() ||
    ''
  );
}

function resolvePersonName(
  childRow: Record<string, unknown>,
  childListConfig: OdakHubListConfig
): string {
  const col = columnConfigByField(childListConfig, 'personelId');
  if (col) {
    const v = reportingCellExportValue(childRow, col);
    if (v) return String(v);
  }
  const raw = childRow.personelId;
  if (raw && typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    const first = String(o.firstName ?? o.FirstName ?? '').trim();
    const last = String(o.lastName ?? o.LastName ?? '').trim();
    const user = String(o.username ?? o.Username ?? o.displayName ?? '').trim();
    const name = [first, last].filter(Boolean).join(' ').trim();
    if (name) return name;
    if (user) return user;
  }
  return raw == null ? '' : String(raw);
}

export async function generateReportingChildRowDocument(options: {
  reportId: string;
  reportTitle: string;
  binding: ReportingDocumentBinding;
  parentRow: Record<string, unknown>;
  parentListConfig: OdakHubListConfig;
  childRow: Record<string, unknown>;
  childListConfig: OdakHubListConfig;
}): Promise<DiGenerateDocumentResult> {
  const {
    reportId,
    reportTitle,
    binding,
    parentRow,
    parentListConfig,
    childRow,
    childListConfig,
  } = options;

  const parentOverrides = mapReportingParentRowOverrides(parentRow, parentListConfig);
  const personName = resolvePersonName(childRow, childListConfig);
  const trainingDate = pickTrainingDate(parentOverrides);
  const now = new Date();
  const tokenCtx = buildReportingDocumentTokenContext({
    reportTitle: reportTitle || binding.label,
    binding,
    fields: {
      ...parentOverrides,
      personName,
      trainingDate,
    },
    rowId: reportingRowId(childRow),
    now,
  });
  const documentName = resolveReportingDocumentName(binding, tokenCtx);
  const generatedAt = resolveReportingGeneratedAt(binding, tokenCtx);
  const templateId = await resolveReportingDiTemplateId(binding);
  const parentFolderId = await reportingDocumentFolderParentId(reportId, binding);

  return diGenerateFromTemplate(templateId, {
    parentFolderId,
    documentName,
    preserveMissingPlaceholders: true,
    overrides: {
      reportTitle: reportTitle || binding.label,
      generatedAt,
      personName,
      trainingDate,
      egitimNo: parentOverrides.egitimNo ?? '',
      baslik: parentOverrides.baslik ?? '',
      egitimVeren: parentOverrides.egitimVeren ?? '',
      durum: parentOverrides.durum ?? '',
      planlananTarih: parentOverrides.planlananTarih ?? '',
      gerceklesenTarih: parentOverrides.gerceklesenTarih ?? '',
    },
  });
}
