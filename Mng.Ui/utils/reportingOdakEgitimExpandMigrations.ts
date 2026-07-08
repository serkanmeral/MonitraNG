import type { AfListColumnFormat } from '@/utils/afListColumnFormat';
import type { ReportingExpandChildListTab, ReportingExpandConfig } from '@/types/apps/reporting';
import { ODAK_EGITIM_CONFIG } from '@/utils/odakEgitimConfig';

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
  childList: {
    datasetName: ODAK_EGITIM_CONFIG.participationsDataset,
    linkField: 'parentTrainingId',
    parentField: '__dataId',
    sort: 'personelId',
    limit: 500,
    expand: true,
    emptyMessage: 'Bu eğitim için katılımcı kaydı yok.',
    listConfig: {
      enableSearch: false,
      defaultSortBy: 'personelId',
      defaultSortOrder: 'asc',
      columns: [
        participantCol('personelId', 1, {
          title: 'Personel',
          sortable: false,
          width: 220,
          relationDisplayField: 'displayName',
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
    if (current.childList?.datasetName === desired.childList.datasetName) {
      return { expand, changed: false };
    }
    tabs[idx] = desired;
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
