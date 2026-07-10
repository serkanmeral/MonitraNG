import type { AfListColumnFormat } from '@/utils/afListColumnFormat';
import { ODAK_EGITIM_CONFIG, ODAK_TRAINING_STATUS_OPTIONS } from '@/utils/odakEgitimConfig';
import { ReportingCatalogService } from '@/services/reportingCatalogService';
import type {
  ReportingCategory,
  ReportingDocumentBinding,
  ReportingReportDefinition,
} from '@/types/apps/reporting';
import {
  loadReportingCategories,
  saveReportingCategories,
} from '@/utils/reportingCategoryStorage';
import { emptyOdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import { defaultReportingExpandConfigFromFields } from '@/utils/reportingExpandLayout';
import {
  ensureOdakEgitimParticipantsExpandTab,
  ODAK_EGITIM_PARTICIPANTS_EXPAND_TAB,
} from '@/utils/reportingOdakEgitimExpandMigrations';
import {
  ODAK_EGITIM_PERSON_REPORT_ID,
  ODAK_EGITIM_REPORTING_CATEGORY_ID,
  ODAK_EGITIM_TRAININGS_REPORT_ID,
} from '@/utils/reportingOdakEgitimConstants';
import { newReportingDocumentBindingId } from '@/utils/reportingDocumentBindings';

export const RPT_ODAK_EGITIM_LIST_TEMPLATE_CODE = 'RPT_ODAK_EGITIM_LIST';
export const RPT_ODAK_EGITIM_PERSON_TEMPLATE_CODE = 'RPT_ODAK_EGITIM_PERSON';

function odakEgitimListDocumentBinding(): ReportingDocumentBinding {
  return {
    id: 'docbind_odak_egitim_list',
    templateId: RPT_ODAK_EGITIM_LIST_TEMPLATE_CODE,
    templateCode: RPT_ODAK_EGITIM_LIST_TEMPLATE_CODE,
    label: 'Eğitim listesi (XLSX)',
    contextType: 'reportRun',
  };
}

function odakEgitimPersonDocumentBinding(): ReportingDocumentBinding {
  return {
    id: 'docbind_odak_egitim_person',
    templateId: RPT_ODAK_EGITIM_PERSON_TEMPLATE_CODE,
    templateCode: RPT_ODAK_EGITIM_PERSON_TEMPLATE_CODE,
    label: 'Personel eğitim geçmişi (XLSX)',
    contextType: 'reportRun',
  };
}

/** Eksik reportRun belge bağını ekler (idempotent). */
function ensureDocumentBinding(
  report: ReportingReportDefinition,
  binding: ReportingDocumentBinding
): boolean {
  const list = report.documentBindings ?? [];
  const exists = list.some(
    (b) =>
      b.templateCode === binding.templateCode ||
      b.templateId === binding.templateId ||
      b.templateId === binding.templateCode
  );
  if (exists) return false;
  report.documentBindings = [
    ...list,
    {
      ...binding,
      id: binding.id || newReportingDocumentBindingId(),
    },
  ];
  return true;
}
function col(
  fieldName: string,
  order: number,
  opts?: {
    title?: string;
    sortable?: boolean;
    filterable?: boolean;
    width?: number;
    format?: AfListColumnFormat;
    relationDisplayField?: string;
    virtual?: boolean;
  }
) {
  return {
    fieldName,
    visible: true,
    order,
    sortable: opts?.sortable ?? true,
    filterable: opts?.filterable ?? false,
    ...(opts?.title ? { title: opts.title } : {}),
    ...(opts?.width != null ? { width: opts.width } : {}),
    ...(opts?.format ? { format: opts.format } : {}),
    ...(opts?.relationDisplayField ? { relationDisplayField: opts.relationDisplayField } : {}),
    ...(opts?.virtual ? { virtual: true } : {}),
  };
}

const TRUNCATE_100: AfListColumnFormat = { type: 'truncate', maxLength: 100, ellipsis: '...' };

const ODAK_TRAINING_DATE_FORMAT: AfListColumnFormat = {
  type: 'date',
  dateFormat: 'DD.MM.YYYY',
  showTime: true,
  timeFormat: 'HH:mm',
};

const ODAK_TRAINING_STATUS_VALUE_MAP: AfListColumnFormat = {
  type: 'none',
  valueMap: Object.fromEntries(ODAK_TRAINING_STATUS_OPTIONS.map((o) => [o.value, o.title])),
};

const TRAINING_LIST_COLUMN_FORMATS: Record<string, AfListColumnFormat> = {
  baslik: TRUNCATE_100,
  egitimVeren: TRUNCATE_100,
  planlananTarih: ODAK_TRAINING_DATE_FORMAT,
  gerceklesenTarih: ODAK_TRAINING_DATE_FORMAT,
  durum: ODAK_TRAINING_STATUS_VALUE_MAP,
};

/** DG expand — kaynak alan → relationDisplayField */
const TRAINING_LIST_RELATION_DISPLAY: Record<string, string> = {
  birimId: 'ad',
};

/** «Tümü» sekmesi — boş filtre değil; planlanan + tamamlanan kayıtlar. */
const ODAK_TRAINING_STATUS_ALL_FILTER = {
  field: 'durum',
  operator: 'in' as const,
  value: 'Planlandi,Tamamlandi',
};

/** Yıl filtresi — coupling yok; her iki tarih alanında OR. */
const ODAK_TRAINING_YEAR_OR_DATE_FIELDS = ['gerceklesenTarih', 'planlananTarih'] as const;

function patchTrainingExpandParticipantsTab(report: ReportingReportDefinition): boolean {
  if (!report.expand) return false;
  const result = ensureOdakEgitimParticipantsExpandTab(report.expand, report.datasetName);
  if (!result.changed) return false;
  report.expand = result.expand;
  return true;
}

function applyTrainingListRelationDisplayColumns(report: ReportingReportDefinition): boolean {
  let changed = false;
  for (const col of report.listConfig.columns) {
    if (col.fieldName.includes('.') && !col.relationDisplayField) {
      const [root, ...rest] = col.fieldName.split('.');
      col.fieldName = root;
      col.relationDisplayField = rest.join('.');
      col.sortable = false;
      col.filterable = false;
      changed = true;
    }
    const desired = TRAINING_LIST_RELATION_DISPLAY[col.fieldName];
    if (desired && col.relationDisplayField !== desired) {
      col.relationDisplayField = desired;
      col.title = col.title ?? 'Birim';
      col.sortable = false;
      col.filterable = false;
      changed = true;
    }
  }
  return changed;
}

function applyTrainingListColumnFormats(report: ReportingReportDefinition): boolean {
  let changed = false;
  for (const col of report.listConfig.columns) {
    const desired = TRAINING_LIST_COLUMN_FORMATS[col.fieldName];
    if (desired && (!col.format?.type || col.format.type === 'none')) {
      col.format = { ...desired };
      changed = true;
    }
  }
  return changed;
}

function patchTrainingListExpand(report: ReportingReportDefinition): boolean {
  const fresh = buildTrainingsReport(report.categoryId, report.updatedAt);
  const hasFields = report.expand?.sections?.some((s) => s.fields?.length);
  if (report.expand?.enabled && hasFields) return false;
  report.expand = fresh.expand;
  return true;
}

function fixTrainingStatusAllChoice(param: ReportingReportDefinition['parameters'][number]): boolean {
  if (param.id !== 'statusTab' || param.type !== 'statusTab') return false;
  let changed = false;

  const allStatusOption = param.statusOptions?.find((o) => o.value === 'all');
  if (allStatusOption && !allStatusOption.filter) {
    allStatusOption.filter = { ...ODAK_TRAINING_STATUS_ALL_FILTER };
    changed = true;
  }

  if (param.binding?.kind === 'choiceFilters') {
    const allChoice = param.binding.choices?.find((c) => c.value === 'all');
    const hasAllFilter = allChoice?.filters?.some(
      (f) => f.field === 'durum' && f.operator === 'in'
    );
    if (allChoice && !hasAllFilter) {
      allChoice.filters = [{ ...ODAK_TRAINING_STATUS_ALL_FILTER }];
      changed = true;
    }
  }

  if (!param.widget) {
    param.widget = 'buttonGroup';
    param.binding = {
      kind: 'choiceFilters',
      choices: (param.statusOptions ?? []).map((o) => ({
        value: o.value,
        title: o.title,
        filters: o.filter ? [{ ...o.filter }] : [],
      })),
    };
    changed = true;
  }

  return changed;
}

function ensureTrainingYearOrDateFields(param: ReportingReportDefinition['parameters'][number]): boolean {
  if (param.id !== 'year' || param.type !== 'year') return false;
  let changed = false;

  if (!param.widget || !param.binding) {
    param.widget = 'select';
    param.binding = {
      kind: 'datePartRange',
      field: 'gerceklesenTarih',
      orDateFields: [...ODAK_TRAINING_YEAR_OR_DATE_FIELDS],
      part: 'year',
      emptyMeans: 'noFilter',
    };
    changed = true;
  } else if (param.binding.kind === 'datePartRange') {
    if (param.binding.fieldFromParameter) {
      delete param.binding.fieldFromParameter;
      changed = true;
    }
    if (!param.binding.field || param.binding.field !== 'gerceklesenTarih') {
      param.binding.field = 'gerceklesenTarih';
      changed = true;
    }
    const nextOr = [...ODAK_TRAINING_YEAR_OR_DATE_FIELDS];
    const cur = param.binding.orDateFields ?? [];
    const same =
      cur.length === nextOr.length && nextOr.every((f) => cur.includes(f));
    if (!same) {
      param.binding.orDateFields = nextOr;
      changed = true;
    }
  }

  if (!param.options) {
    param.options = {
      kind: 'yearRange',
      min: ODAK_EGITIM_CONFIG.legacyFirstYear,
      max: 'currentYear',
      includeAll: true,
    };
    changed = true;
  }

  if (param.dateField && param.dateField !== param.binding?.field) {
    param.dateField = param.binding?.field;
    changed = true;
  }

  return changed;
}

function applyTrainingListParameterModel(report: ReportingReportDefinition): boolean {
  let changed = false;
  for (const param of report.parameters) {
    if (fixTrainingStatusAllChoice(param)) changed = true;
    if (ensureTrainingYearOrDateFields(param)) changed = true;
  }
  return changed;
}

function stripLegacyVirtualKatilimciColumn(report: ReportingReportDefinition): boolean {
  const before = report.listConfig.columns.length;
  report.listConfig.columns = report.listConfig.columns.filter(
    (c) => !(c.virtual && c.fieldName === 'katilimciSayisi')
  );
  return report.listConfig.columns.length !== before;
}

function patchTrainingListSummary(report: ReportingReportDefinition): boolean {
  if (report.summary?.metrics?.length) return false;
  report.summary = {
    placement: 'cards',
    metrics: [
      { id: 'count', label: 'Kayıt sayısı', kind: 'count', format: 'integer' },
      { id: 'sure', label: 'Toplam süre (dk)', kind: 'sum', field: 'sureDakika', format: 'integer' },
    ],
  };
  return true;
}

function patchTrainingListReport(report: ReportingReportDefinition): boolean {
  let changed = false;
  // ... existing patches continue below via callers
  if (ensureDocumentBinding(report, odakEgitimListDocumentBinding())) changed = true;
  return changed || patchTrainingListReportCore(report);
}

function patchTrainingListReportCore(report: ReportingReportDefinition): boolean {
  const formats = applyTrainingListColumnFormats(report);
  const relations = applyTrainingListRelationDisplayColumns(report);
  const expand = patchTrainingListExpand(report);
  const expandTabs = patchTrainingExpandParticipantsTab(report);
  const parameters = applyTrainingListParameterModel(report);
  const actions = applyTrainingListExpandActions(report);
  const stripped = stripLegacyVirtualKatilimciColumn(report);
  const summary = patchTrainingListSummary(report);
  return formats || relations || expand || expandTabs || parameters || actions || stripped || summary;
}

function applyTrainingListExpandActions(report: ReportingReportDefinition): boolean {
  const hasNavigate = report.expand?.actions?.some((a) => a.id === 'open-training-detail');
  if (hasNavigate) return false;
  report.expand.actions = [
    ...(report.expand.actions ?? []),
    {
      id: 'open-training-detail',
      label: 'Eğitim detayı',
      type: 'navigate',
      config: { path: '/apps/odak-egitim/trainings/{__dataId}' },
    },
  ];
  return true;
}

function applyPersonReportRelationDisplay(report: ReportingReportDefinition): boolean {
  let changed = false;
  for (const col of report.listConfig.columns) {
    if (col.fieldName.includes('.') && !col.relationDisplayField) {
      const [root, ...rest] = col.fieldName.split('.');
      col.fieldName = root;
      col.relationDisplayField = rest.join('.');
      col.sortable = false;
      col.filterable = false;
      changed = true;
    }
    if (col.fieldName === 'parentTrainingId' && col.relationDisplayField === 'durum') {
      col.format = ODAK_TRAINING_STATUS_VALUE_MAP;
      changed = true;
    }
    if (
      col.fieldName === 'parentTrainingId' &&
      col.relationDisplayField === 'gerceklesenTarih' &&
      (!col.format?.type || col.format.type === 'none')
    ) {
      col.format = ODAK_TRAINING_DATE_FORMAT;
      changed = true;
    }
  }
  return changed;
}

function patchPersonReport(report: ReportingReportDefinition): boolean {
  let changed = applyPersonReportRelationDisplay(report);
  if (ensureDocumentBinding(report, odakEgitimPersonDocumentBinding())) changed = true;
  return changed;
}

function buildTrainingsReport(categoryId: string, now: string): ReportingReportDefinition {
  return {
    id: ODAK_EGITIM_TRAININGS_REPORT_ID,
    title: 'Eğitim listesi',
    description: 'Planlanan ve tamamlanan eğitim kayıtları (F19). Salt okunur — relation alanlar DG expand ile çözülür.',
    categoryId,
    datasetName: ODAK_EGITIM_CONFIG.trainingsDataset,
    listConfig: {
      enableSearch: false,
      defaultSortBy: 'gerceklesenTarih',
      defaultSortOrder: 'desc',
      columns: [
        col('egitimNo', 1, { title: 'Eğitim No', width: 130 }),
        col('baslik', 2, { title: 'Başlık', filterable: true, format: TRUNCATE_100 }),
        col('birimId', 3, { title: 'Birim', sortable: false, width: 140, relationDisplayField: 'ad' }),
        col('egitimVeren', 4, { title: 'Eğitimi Veren', width: 140, format: TRUNCATE_100 }),
        col('planlananTarih', 5, { title: 'Planlanan Tarih', width: 150, format: ODAK_TRAINING_DATE_FORMAT }),
        col('gerceklesenTarih', 6, { title: 'Gerçekleşen Tarih', width: 150, format: ODAK_TRAINING_DATE_FORMAT }),
        col('durum', 7, { title: 'Durum', width: 110, filterable: true, format: ODAK_TRAINING_STATUS_VALUE_MAP }),
        col('sureDakika', 8, { title: 'Süre (dk)', sortable: true, width: 100 }),
      ],
    },
    expand: {
      enabled: true,
      hideEmptyFields: true,
      heading: 'Eğitim detayı',
      intro: '',
      sections: [
        {
          key: 'detail',
          title: 'Genel',
          cols: 12,
          fields: ['konu', 'konum', 'egitimAmaci', 'degerlendirmeYontemi', 'toplamCalisanSayisi'],
        },
      ],
      fieldCols: {},
      actions: [
        {
          id: 'open-training-detail',
          label: 'Eğitim detayı',
          type: 'navigate',
          config: { path: '/apps/odak-egitim/trainings/{__dataId}' },
        },
      ],
      tabs: [JSON.parse(JSON.stringify(ODAK_EGITIM_PARTICIPANTS_EXPAND_TAB))],
      defaultTabId: 'fields',
    },
    fieldPolicies: emptyOdakFieldPoliciesBlob(),
    defaultFilters: [],
    visibilityPolicies: [],
    summary: {
      placement: 'cards',
      metrics: [
        { id: 'count', label: 'Kayıt sayısı', kind: 'count', format: 'integer' },
        { id: 'sure', label: 'Toplam süre (dk)', kind: 'sum', field: 'sureDakika', format: 'integer' },
      ],
    },
    documentBindings: [odakEgitimListDocumentBinding()],
    parameters: [
      {
        id: 'statusTab',
        type: 'statusTab',
        widget: 'buttonGroup',
        binding: {
          kind: 'choiceFilters',
          choices: [
            { value: 'plan', title: 'Planlanan', filters: [{ field: 'durum', operator: 'eq', value: 'Planlandi' }] },
            {
              value: 'complete',
              title: 'Tamamlanan',
              filters: [{ field: 'durum', operator: 'eq', value: 'Tamamlandi' }],
            },
            { value: 'all', title: 'Tümü', filters: [{ ...ODAK_TRAINING_STATUS_ALL_FILTER }] },
          ],
        },
        label: 'Durum',
        required: false,
        defaultValue: 'complete',
        statusOptions: [
          { value: 'plan', title: 'Planlanan', filter: { field: 'durum', operator: 'eq', value: 'Planlandi' } },
          {
            value: 'complete',
            title: 'Tamamlanan',
            filter: { field: 'durum', operator: 'eq', value: 'Tamamlandi' },
          },
          { value: 'all', title: 'Tümü', filter: { ...ODAK_TRAINING_STATUS_ALL_FILTER } },
        ],
      },
      {
        id: 'year',
        type: 'year',
        widget: 'select',
        binding: {
          kind: 'datePartRange',
          field: 'gerceklesenTarih',
          orDateFields: [...ODAK_TRAINING_YEAR_OR_DATE_FIELDS],
          part: 'year',
          emptyMeans: 'noFilter',
        },
        options: {
          kind: 'yearRange',
          min: ODAK_EGITIM_CONFIG.legacyFirstYear,
          max: 'currentYear',
          includeAll: true,
        },
        label: 'Yıl',
        required: false,
        dateField: 'gerceklesenTarih',
      },
      {
        id: 'search',
        type: 'search',
        widget: 'search',
        binding: { kind: 'search' },
        label: 'Ara',
        required: false,
      },
    ],
    createdAt: now,
    updatedAt: now,
  };
}

function buildPersonTrainingsReport(categoryId: string, now: string): ReportingReportDefinition {
  return {
    id: ODAK_EGITIM_PERSON_REPORT_ID,
    title: 'Personel eğitim geçmişi',
    description: 'Seçilen personelin katılım kayıtları (F39).',
    categoryId,
    datasetName: ODAK_EGITIM_CONFIG.participationsDataset,
    listConfig: {
      enableSearch: false,
      defaultSortBy: 'parentTrainingId',
      defaultSortOrder: 'desc',
      columns: [
        col('parentTrainingId', 1, { title: 'Eğitim No', width: 130, sortable: false, relationDisplayField: 'egitimNo' }),
        col('parentTrainingId', 2, { title: 'Başlık', sortable: false, relationDisplayField: 'baslik' }),
        col('parentTrainingId', 3, {
          title: 'Eğitimi Veren',
          sortable: false,
          width: 140,
          relationDisplayField: 'egitimVeren',
        }),
        col('parentTrainingId', 4, {
          title: 'Tarih',
          sortable: false,
          width: 150,
          relationDisplayField: 'gerceklesenTarih',
          format: ODAK_TRAINING_DATE_FORMAT,
        }),
        col('parentTrainingId', 5, {
          title: 'Durum',
          sortable: false,
          width: 110,
          relationDisplayField: 'durum',
          format: ODAK_TRAINING_STATUS_VALUE_MAP,
        }),
        col('parentTrainingId', 6, { title: 'Süre (dk)', sortable: false, width: 100, relationDisplayField: 'sureDakika' }),
        col('katildi', 7, { title: 'Katıldı', width: 90, sortable: false }),
        col('etkin', 8, { title: 'Etkin', width: 90, sortable: false }),
      ],
    },
    expand: defaultReportingExpandConfigFromFields([]),
    fieldPolicies: emptyOdakFieldPoliciesBlob(),
    defaultFilters: [],
    visibilityPolicies: [],
    documentBindings: [odakEgitimPersonDocumentBinding()],
    parameters: [
      {
        id: 'person',
        type: 'person',
        widget: 'personPicker',
        binding: { kind: 'fieldEq', field: 'personelId' },
        label: 'Personel',
        field: 'personelId',
        required: true,
      },
    ],
    createdAt: now,
    updatedAt: now,
  };
}

function ensureOdakEgitimCategory(domainKey: string): ReportingCategory {
  const categories = loadReportingCategories(domainKey);
  const existing = categories.find((c) => c.id === ODAK_EGITIM_REPORTING_CATEGORY_ID);
  if (existing) return existing;

  const now = new Date().toISOString();
  const created: ReportingCategory = {
    id: ODAK_EGITIM_REPORTING_CATEGORY_ID,
    parentId: null,
    ancestorIds: [],
    name: 'Odak Eğitim',
    description: 'Eğitim modülü raporları',
    sortOrder: 10,
    status: 'active',
    createdBy: 'system',
    createdAt: now,
    updatedAt: now,
  };
  categories.push(created);
  saveReportingCategories(domainKey, categories);
  return created;
}

/** İdempotent — Odak Eğitim kategorisi ve iki başlangıç raporu. */
export function ensureOdakEgitimReportingSeeds(domainKey: string): void {
  const category = ensureOdakEgitimCategory(domainKey);
  const catalogService = new ReportingCatalogService(domainKey);
  const now = new Date().toISOString();

  const trainings = catalogService.getReport(ODAK_EGITIM_TRAININGS_REPORT_ID);
  if (!trainings) {
    catalogService.saveReport(buildTrainingsReport(category.id, now));
  } else if (patchTrainingListReport(trainings)) {
    catalogService.saveReport({ ...trainings, updatedAt: now });
  }

  for (const report of catalogService.load().reports) {
    if (report.datasetName !== ODAK_EGITIM_CONFIG.trainingsDataset || !report.expand?.enabled) continue;
    if (report.id === ODAK_EGITIM_TRAININGS_REPORT_ID) continue;
    if (patchTrainingExpandParticipantsTab(report)) {
      catalogService.saveReport({ ...report, updatedAt: now });
    }
  }

  const personReport = catalogService.getReport(ODAK_EGITIM_PERSON_REPORT_ID);
  if (!personReport) {
    catalogService.saveReport(buildPersonTrainingsReport(category.id, now));
  } else if (patchPersonReport(personReport)) {
    catalogService.saveReport({ ...personReport, updatedAt: now });
  }
}
