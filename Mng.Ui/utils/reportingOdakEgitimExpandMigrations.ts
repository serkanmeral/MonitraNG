import type { AfListColumnFormat } from '@/utils/afListColumnFormat';
import type { ReportingExpandChildListTab, ReportingExpandConfig } from '@/types/apps/reporting';
import { ODAK_EGITIM_CONFIG } from '@/utils/odakEgitimConfig';
import { emptyOdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';

const TRUNCATE_100: AfListColumnFormat = { type: 'truncate', maxLength: 100, ellipsis: '...' };

function participantCol(
  fieldName: string,
  order: number,
  opts?: {
    title?: string;
    sortable?: boolean;
    width?: number;
    format?: AfListColumnFormat;
    relationDisplayField?: string;
  }
) {
  return {
    fieldName,
    visible: true,
    order,
    sortable: opts?.sortable ?? true,
    filterable: false,
    ...(opts?.title ? { title: opts.title } : {}),
    ...(opts?.width != null ? { width: opts.width } : {}),
    ...(opts?.format ? { format: opts.format } : {}),
    ...(opts?.relationDisplayField ? { relationDisplayField: opts.relationDisplayField } : {}),
  };
}

/** Odak eğitim expand — katılımcı listesi sekmesi (runtime + seed). */
export const ODAK_EGITIM_PARTICIPANTS_EXPAND_TAB: ReportingExpandChildListTab = {
  id: 'participants',
  title: 'Katılımcılar',
  fieldPolicies: emptyOdakFieldPoliciesBlob(),
  visibilityPolicies: [],
  childList: {
    datasetName: ODAK_EGITIM_CONFIG.participationsDataset,
    linkField: 'parentTrainingId',
    parentField: '__dataId',
    sort: 'personelId',
    limit: 500,
    expand: true,
    emptyMessage: 'Bu eğitim için katılımcı kaydı yok.',
    summary: {
      placement: 'footer',
      metrics: [{ id: 'count', label: 'Katılımcı sayısı', kind: 'count', format: 'integer' }],
    },
    listConfig: {
      enableSearch: false,
      defaultSortBy: 'personelId',
      defaultSortOrder: 'asc',
      columns: [
        participantCol('personelId', 1, {
          title: 'Personel',
          sortable: false,
          width: 220,
          // persons expand: firstName/lastName/username — no displayName; scalar formatter derives label
        }),
        participantCol('katildi', 2, { title: 'Katıldı', width: 90, sortable: false }),
        participantCol('etkin', 3, { title: 'Etkin', width: 90, sortable: false }),
        participantCol('notlar', 4, { title: 'Notlar', sortable: false, format: TRUNCATE_100 }),
      ],
    },
  },
};

/** Hydrate/runtime — localStorage seed kaçırsa bile katılımcı sekmesini garanti eder. */
export function ensureOdakEgitimParticipantsExpandTab(
  expand: ReportingExpandConfig,
  datasetName: string
): { expand: ReportingExpandConfig; changed: boolean } {
  if (!expand?.enabled || datasetName !== ODAK_EGITIM_CONFIG.trainingsDataset) {
    return { expand, changed: false };
  }

  const tabs = [...(expand.tabs ?? [])];
  const idx = tabs.findIndex((t) => t.id === 'participants');
  const desired = JSON.parse(JSON.stringify(ODAK_EGITIM_PARTICIPANTS_EXPAND_TAB)) as ReportingExpandChildListTab;

  if (idx >= 0) {
    const current = tabs[idx]!;
    let changed = false;

    if (current.childList?.datasetName !== desired.childList.datasetName) {
      tabs[idx] = desired;
      changed = true;
    } else {
      // Patch legacy personelId.displayName (persons expand has no displayName)
      const cols = current.childList.listConfig?.columns ?? [];
      for (const col of cols) {
        if (col.fieldName === 'personelId' && col.relationDisplayField === 'displayName') {
          delete col.relationDisplayField;
          changed = true;
        }
      }
      if (!current.childList.summary?.metrics?.length) {
        current.childList.summary = {
          placement: 'footer',
          metrics: [{ id: 'count', label: 'Katılımcı sayısı', kind: 'count', format: 'integer' }],
        };
        changed = true;
      }
      if (!current.fieldPolicies) {
        current.fieldPolicies = emptyOdakFieldPoliciesBlob();
        changed = true;
      }
      if (!current.visibilityPolicies) {
        current.visibilityPolicies = [];
        changed = true;
      }
      if (!changed) {
        return { expand, changed: false };
      }
    }
  } else {
    tabs.push(desired);
  }

  return {
    expand: {
      ...expand,
      tabs,
      defaultTabId: expand.defaultTabId || 'fields',
    },
    changed: true,
  };
}
