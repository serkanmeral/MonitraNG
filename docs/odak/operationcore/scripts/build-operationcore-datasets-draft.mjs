/**
 * Builds operationcore_datasets_phase1_draft_2026-05-26.json from the prior export.
 * Run: node docs/odak/operationcore/scripts/build-operationcore-datasets-draft.mjs
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '../../../../');
const srcPath = path.join(
  repoRoot,
  'docs/odak/operationcore/datasets/operationcore_datasets_phase1_current_final_2026-05-25.json'
);
const outPath = path.join(
  repoRoot,
  'docs/odak/operationcore/datasets/operationcore_datasets_phase1_draft_2026-05-26.json'
);

/** Stable category __dataId — must match operationcore_dataset_category.json */
export const OPERATION_CORE_CATEGORY_ID = 'f47ac10b-58cc-4372-a567-0e02b2c3d479';

const WORK_ITEM_QUERIES = [
  {
    name: 'wi_by_workspace_and_state',
    description: 'WorkItems by workspace and state (board columns)',
    pipeline: [
      {
        $match: {
          workspaceId: ':workspaceId',
          stateId: ':stateId',
        },
      },
      { $sort: { order: 1, lastStateChangeAt: -1 } },
    ],
    parameters: [
      { name: 'workspaceId', type: 'text', required: true },
      { name: 'stateId', type: 'text', required: true },
    ],
  },
  {
    name: 'wi_board_column',
    description: 'WorkItems for a board column (workspace + board + state)',
    pipeline: [
      {
        $match: {
          workspaceId: ':workspaceId',
          boardId: ':boardId',
          stateId: ':stateId',
        },
      },
      { $sort: { order: 1, lastStateChangeAt: -1 } },
    ],
    parameters: [
      { name: 'workspaceId', type: 'text', required: true },
      { name: 'boardId', type: 'text', required: true },
      { name: 'stateId', type: 'text', required: true },
    ],
  },
  {
    name: 'wi_assigned_to_user',
    description: 'WorkItems assigned to a user',
    pipeline: [
      { $match: { assignee: ':assignee' } },
      { $sort: { lastStateChangeAt: -1 } },
    ],
    parameters: [{ name: 'assignee', type: 'text', required: true }],
  },
  {
    name: 'wi_assigned_open',
    description: 'Open (not closed) WorkItems assigned to a user',
    pipeline: [
      { $match: { assignee: ':assignee', closedAt: null } },
      { $sort: { lastStateChangeAt: -1 } },
    ],
    parameters: [{ name: 'assignee', type: 'text', required: true }],
  },
  {
    name: 'wi_sla_response_breach',
    description: 'Open WorkItems past SLA response due',
    pipeline: [
      {
        $match: {
          workspaceId: ':workspaceId',
          'sla.responseDueAt': { $lt: ':asOf' },
          closedAt: null,
        },
      },
      { $sort: { 'sla.responseDueAt': 1 } },
    ],
    parameters: [
      { name: 'workspaceId', type: 'text', required: true },
      { name: 'asOf', type: 'datetime', required: true },
    ],
  },
  {
    name: 'wi_sla_resolve_breach',
    description: 'Open WorkItems past SLA resolve due',
    pipeline: [
      {
        $match: {
          workspaceId: ':workspaceId',
          'sla.resolveDueAt': { $lt: ':asOf' },
          closedAt: null,
        },
      },
      { $sort: { 'sla.resolveDueAt': 1 } },
    ],
    parameters: [
      { name: 'workspaceId', type: 'text', required: true },
      { name: 'asOf', type: 'datetime', required: true },
    ],
  },
  {
    name: 'wi_count_by_state',
    description: 'Open WorkItems count grouped by state (workspace donut widget)',
    pipeline: [
      {
        $match: {
          workspaceId: ':workspaceId',
          closedAt: null,
        },
      },
      {
        $group: {
          _id: '$stateId',
          count: { $sum: 1 },
        },
      },
      {
        $project: {
          _id: 0,
          stateId: '$_id',
          stateName: '$_id',
          count: 1,
        },
      },
      { $sort: { count: -1 } },
    ],
    parameters: [{ name: 'workspaceId', type: 'text', required: true }],
  },
];

function fieldText(name, title, opts = {}) {
  return {
    fieldType: 'text',
    name,
    title,
    mandatory: opts.mandatory ?? false,
    unique: opts.unique ?? false,
    isArray: false,
    relationDataset: null,
    incrementalOptions: null,
  };
}

function fieldRelation(name, title, relationDataset, opts = {}) {
  return {
    fieldType: 'relation',
    name,
    title,
    mandatory: opts.mandatory ?? false,
    unique: opts.unique ?? false,
    isArray: opts.isArray ?? false,
    relationDataset,
    incrementalOptions: null,
  };
}

function fieldBool(name, title, opts = {}) {
  return {
    fieldType: 'bool',
    name,
    title,
    mandatory: opts.mandatory ?? false,
    unique: false,
    isArray: false,
    relationDataset: null,
    incrementalOptions: null,
  };
}

function fieldObject(name, title, opts = {}) {
  return {
    fieldType: 'object',
    name,
    title,
    mandatory: opts.mandatory ?? false,
    unique: false,
    isArray: opts.isArray ?? false,
    relationDataset: null,
    incrementalOptions: null,
  };
}

function fieldDatetime(name, title, opts = {}) {
  return {
    fieldType: 'datetime',
    name,
    title,
    mandatory: opts.mandatory ?? false,
    unique: false,
    isArray: false,
    relationDataset: null,
    incrementalOptions: null,
  };
}

const OP_TAGS = 'op_tags';

function buildOpTagsDataset() {
  return {
    __dataId: null,
    name: OP_TAGS,
    description: 'Operational Core - Workspace tags (etiket kataloğu; her kayıt workspaceId taşır)',
    category: OPERATION_CORE_CATEGORY_ID,
    forceSchema: false,
    logging: 'self',
    publish_mode: 'none',
    fields: [
      fieldText('name', 'Etiket adı', { mandatory: true }),
      fieldRelation('workspaceId', 'Workspace', 'op_workspaces', { mandatory: true }),
      fieldText('color', 'Renk (tema anahtarı)'),
      fieldText('description', 'Açıklama'),
    ],
    indexList: [
      {
        name: 'idx_workspaceId',
        fields: { workspaceId: 1 },
        unique: false,
      },
      {
        name: 'idx_workspaceId_name',
        fields: { workspaceId: 1, name: 1 },
        unique: true,
      },
    ],
    queries: [],
  };
}

const OP_WORK_ITEM_SCHEDULES = 'op_work_item_schedules';

function buildOpWorkItemSchedulesDataset() {
  return {
    __dataId: null,
    name: OP_WORK_ITEM_SCHEDULES,
    description:
      'Operational Core - Scheduled work item templates (cron + create payload)',
    category: OPERATION_CORE_CATEGORY_ID,
    forceSchema: false,
    logging: 'self',
    publish_mode: 'none',
    fields: [
      fieldText('name', 'Ad', { mandatory: true }),
      fieldText('description', 'Açıklama (schedule)'),
      fieldRelation('workspaceId', 'Workspace', 'op_workspaces', { mandatory: true }),
      fieldBool('isActive', 'Aktif', { mandatory: true }),
      fieldText('cronExpression', 'Cron ifadesi (Quartz)', { mandatory: true }),
      fieldText('timezone', 'Saat dilimi (IANA)', { mandatory: true }),
      fieldRelation('boardId', 'Board', 'op_boards', { mandatory: true }),
      fieldRelation('typeId', 'İş tipi', 'op_work_item_types', { mandatory: true }),
      fieldText('assignee', 'Atanan (Keeper user id)', { mandatory: true }),
      fieldRelation('priorityId', 'Öncelik', 'op_priorities'),
      fieldText('title', 'WI başlık şablonu', { mandatory: true }),
      fieldText('templateDescription', 'WI açıklama şablonu'),
      fieldObject('fields', 'Ek alanlar (create payload)'),
      fieldText('initialTransitionKey', 'Create sonrası geçiş (transitionKey)'),
      fieldText('schedulerJobId', 'MngScheduler job id'),
      fieldDatetime('lastRunAt', 'Son çalışma'),
      fieldRelation('lastWorkItemId', 'Son oluşan WI', 'op_work_items'),
    ],
    indexList: [
      {
        name: 'idx_workspaceId',
        fields: { workspaceId: 1 },
        unique: false,
      },
      {
        name: 'idx_workspaceId_isActive',
        fields: { workspaceId: 1, isActive: 1 },
        unique: false,
      },
      {
        name: 'idx_workspaceId_name',
        fields: { workspaceId: 1, name: 1 },
        unique: true,
      },
    ],
    queries: [],
  };
}

function patchDataset(ds) {
  ds.category = OPERATION_CORE_CATEGORY_ID;
  ds.__dataId = null;

  switch (ds.name) {
    case 'op_workspaces': {
      const fields = ds.fields;
      if (!fields.some((f) => f.name === 'enabledTypeIds')) {
        fields.push(
          fieldRelation('enabledTypeIds', 'Aktif WorkItem tipleri', 'op_work_item_types', {
            isArray: true,
          })
        );
      }
      if (!fields.some((f) => f.name === 'enabledStateIds')) {
        fields.push(
          fieldRelation('enabledStateIds', 'Aktif durumlar (state)', 'op_states', { isArray: true })
        );
      }
      if (!fields.some((f) => f.name === 'enabledPriorityIds')) {
        fields.push(
          fieldRelation('enabledPriorityIds', 'Aktif öncelikler', 'op_priorities', {
            isArray: true,
          })
        );
      }
      if (!fields.some((f) => f.name === 'enabledFieldIds')) {
        fields.push(
          fieldRelation('enabledFieldIds', 'Aktif alan havuzu', 'op_fields', { isArray: true })
        );
      }
      break;
    }
    case 'op_state_flows': {
      const t = ds.fields.find((f) => f.name === 'transitions');
      if (t) {
        t.title = 'Transition kataloğu (transitionKey, from/to, permissions, ui)';
        t.description =
          'Kanonik operasyonel aksiyonlar; bkz. OPERATION_CORE_IMPLEMENTATION_PLAN §5.2.1';
      }
      break;
    }
    case 'op_rules': {
      ds.description =
        'Operational Core - Rules (Default / Validation / Automation via actions)';
      if (!ds.fields.some((f) => f.name === 'transitionKey')) {
        const toIdx = ds.fields.findIndex((f) => f.name === 'toStateId');
        const insertAt = toIdx >= 0 ? toIdx + 1 : ds.fields.length;
        ds.fields.splice(
          insertAt,
          0,
          fieldText('transitionKey', 'Transition key (scope)', { mandatory: false })
        );
      }
      if (!ds.indexList.some((i) => i.name === 'idx_transitionKey')) {
        ds.indexList.push({
          name: 'idx_transitionKey',
          fields: { workspaceId: 1, transitionKey: 1, trigger: 1, isActive: 1 },
          unique: false,
        });
      }
      break;
    }
    case 'op_work_item_types': {
      if (!ds.fields.some((f) => f.name === 'workspaceId')) {
        ds.fields.push(
          fieldRelation('workspaceId', 'Workspace (boş = global tip)', 'op_workspaces', {
            mandatory: false,
          })
        );
      }
      if (!ds.indexList.some((i) => i.name === 'idx_workspaceId_name')) {
        ds.indexList.push({
          name: 'idx_workspaceId_name',
          fields: { workspaceId: 1, name: 1 },
          unique: true,
        });
      }
      const globalNameIdx = ds.indexList.find((i) => i.name === 'idx_name');
      if (globalNameIdx?.unique) globalNameIdx.unique = false;
      break;
    }
    case 'op_fields': {
      if (!ds.fields.some((f) => f.name === 'workspaceId')) {
        ds.fields.push(
          fieldRelation('workspaceId', 'Workspace (boş = global alan)', 'op_workspaces', {
            mandatory: false,
          })
        );
      }
      break;
    }
    case 'op_work_items': {
      ds.description =
        'Operational Core - Work Items (key MngOperations; attachments native file)';
      ds.fields = ds.fields.filter(
        (f) => !['sourceModule', 'sourceRecordId', 'sourceEventId'].includes(f.name)
      );
      const keyField = ds.fields.find((f) => f.name === 'key');
      if (keyField?.fieldType === 'incremental') {
        keyField.fieldType = 'text';
        keyField.incrementalOptions = null;
        keyField.title = 'WorkItem kodu (MngOperations üretir)';
      }
      const slaPol = ds.fields.find((f) => f.name === 'slaPolicyId');
      if (slaPol) {
        slaPol.fieldType = 'relation';
        slaPol.relationDataset = 'op_sla_policies';
        slaPol.title = 'SLA politikası';
      }
      ds.queries = WORK_ITEM_QUERIES;
      ds.indexList = ds.indexList.filter((i) => i.name !== 'idx_source');
      if (!ds.indexList.some((i) => i.name === 'idx_origin_sourceType')) {
        ds.indexList.push({
          name: 'idx_origin_sourceType',
          fields: { 'origin.sourceType': 1 },
          unique: false,
        });
      }
      break;
    }
    case 'op_work_item_timelines': {
      const tn = ds.fields.find((f) => f.name === 'transitionName');
      if (tn) {
        tn.name = 'transitionKey';
        tn.title = 'Transition key';
      }
      break;
    }
    case 'op_activities': {
      const ad = ds.fields.find((f) => f.name === 'activityDate');
      if (ad) ad.mandatory = true;
      break;
    }
    default:
      break;
  }
  return ds;
}

const raw = fs.readFileSync(srcPath, 'utf8');
const datasets = JSON.parse(raw);
const patched = datasets.map(patchDataset);
if (!patched.some((d) => d.name === OP_WORK_ITEM_SCHEDULES)) {
  patched.push(buildOpWorkItemSchedulesDataset());
}
if (!patched.some((d) => d.name === OP_TAGS)) {
  patched.push(buildOpTagsDataset());
}
fs.writeFileSync(outPath, JSON.stringify(patched, null, 2) + '\n', 'utf8');
console.log(`Wrote ${patched.length} datasets -> ${outPath}`);
