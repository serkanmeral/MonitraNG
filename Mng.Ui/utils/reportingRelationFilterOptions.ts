import { ocListDataset } from '@/services/operationCoreService';
import type { FieldDefinition } from '@/stores/apps/dataset';
import type { AfFilterColumn } from '@/utils/afListFilters';
import { recordToDatasetItems } from '@/utils/ocDynamicFormField';

/**
 * Rapor filtresindeki relation sütunları için seçenek listesi.
 * Etiket, aynı alanın relationDisplayField değeridir; yoksa "ad".
 */
export async function loadReportingRelationFilterOptions(
  columns: AfFilterColumn[],
  fields: FieldDefinition[],
  listColumns: { fieldName: string; relationDisplayField?: string }[] = []
): Promise<Record<string, { value: string; title: string }[]>> {
  const fieldMap = new Map(fields.map((field) => [field.name, field]));
  const out: Record<string, { value: string; title: string }[]> = {};

  await Promise.all(
    columns
      .filter((column) => column.kind === 'relation')
      .map(async (column) => {
        const dataset = fieldMap.get(column.key)?.relationDataset?.trim();
        if (!dataset) {
          out[column.key] = [];
          return;
        }
        const labelKey =
          listColumns.find((col) => col.fieldName === column.key && col.relationDisplayField?.trim())
            ?.relationDisplayField?.trim() || 'ad';
        try {
          const rows = await ocListDataset(dataset, { limit: 500 });
          out[column.key] = recordToDatasetItems(rows, { idKey: '__dataId', labelKey });
        } catch {
          out[column.key] = [];
        }
      })
  );

  return out;
}
