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
import { reportingRowId } from '@/utils/reportingExpandLayout';

export async function generateReportingParentRowDocument(options: {
  reportId: string;
  reportTitle: string;
  binding: ReportingDocumentBinding;
  row: Record<string, unknown>;
  listConfig: OdakHubListConfig;
}): Promise<DiGenerateDocumentResult> {
  const { reportId, reportTitle, binding, row, listConfig } = options;
  const rowOverrides = mapReportingParentRowOverrides(row, listConfig);
  const now = new Date();
  const tokenCtx = buildReportingDocumentTokenContext({
    reportTitle: reportTitle || binding.label,
    binding,
    fields: rowOverrides,
    rowId: reportingRowId(row),
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
      ...rowOverrides,
    },
  });
}
