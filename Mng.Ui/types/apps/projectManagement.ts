export type PmProjectStatus = 'draft' | 'active' | 'closed';
export type PmWbsKind = 'summary' | 'task' | 'milestone';
export type PmDependencyType = 'FS';

export interface PmProject {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  status: PmProjectStatus | string;
  plannedStart?: string | null;
  plannedFinish?: string | null;
  actualStart?: string | null;
  actualFinish?: string | null;
  baselineSetAt?: string | null;
  baselineSetBy?: string | null;
  baselineNote?: string | null;
  baselineDrifted: boolean;
  diFolderId?: string | null;
  workspaceId?: string | null;
}

export interface PmPortfolioProject extends PmProject {
  percentComplete: number;
  attention: boolean;
  flags: PmTraceFlag[] | string[];
  counts: PmStatusCounts;
}

export interface PmPortfolio {
  generatedAt: string;
  projectCount: number;
  draftCount: number;
  activeCount: number;
  closedCount: number;
  attentionCount: number;
  totals: PmStatusCounts;
  items: PmPortfolioProject[];
}

export interface PmWbsItem {
  id: string;
  projectId: string;
  parentId?: string | null;
  kind: PmWbsKind | string;
  name: string;
  wbsCode?: string | null;
  sortOrder: number;
  plannedStart?: string | null;
  plannedFinish?: string | null;
  actualStart?: string | null;
  actualFinish?: string | null;
  baselineStart?: string | null;
  baselineFinish?: string | null;
  weight: number;
  percentComplete: number;
  workItemId?: string | null;
  workItemKey?: string | null;
  workItemTitle?: string | null;
  workItemStateName?: string | null;
  workItemStateCategory?: string | null;
  workItemClosed?: boolean;
  baselineDrifted: boolean;
}

export interface PmDependency {
  id: string;
  projectId: string;
  predecessorId: string;
  successorId: string;
  type: PmDependencyType | string;
  lagDays: number;
}

export interface PmProjectDetail {
  project: PmProject;
  wbs: PmWbsItem[];
  dependencies: PmDependency[];
  decisions?: PmDecision[];
  stageGates?: PmStageGate[];
  raidItems?: PmRaidItem[];
  assignments?: PmResourceAssignment[];
  capacity?: PmProjectCapacity;
  budgetLines?: PmBudgetLine[];
  budget?: PmProjectBudget;
  acknowledgements?: PmAcknowledgement[];
  obligations?: PmObligation[];
  auditPacks?: PmAuditPack[];
  meetings?: PmMeeting[];
  stakeholders?: PmStakeholder[];
  processMaps?: PmProcessMap[];
}

export interface PmCreateProjectRequest {
  code: string;
  name: string;
  description?: string | null;
  status?: string | null;
  plannedStart?: string | null;
  plannedFinish?: string | null;
  packCode?: string | null;
}

export interface PmUpdateProjectRequest {
  name?: string | null;
  description?: string | null;
  status?: string | null;
  plannedStart?: string | null;
  plannedFinish?: string | null;
  actualStart?: string | null;
  actualFinish?: string | null;
  workspaceId?: string | null;
  diFolderId?: string | null;
}

export interface PmCreateWbsRequest {
  parentId?: string | null;
  kind?: string | null;
  name: string;
  plannedStart?: string | null;
  plannedFinish?: string | null;
  weight?: number | null;
  percentComplete?: number | null;
}

export interface PmUpdateWbsRequest {
  parentId?: string | null;
  kind?: string | null;
  name?: string | null;
  sortOrder?: number | null;
  plannedStart?: string | null;
  plannedFinish?: string | null;
  actualStart?: string | null;
  actualFinish?: string | null;
  weight?: number | null;
  percentComplete?: number | null;
}

export interface PmCreateDependencyRequest {
  predecessorId: string;
  successorId: string;
  type?: string | null;
  lagDays?: number | null;
}

export interface PmWorkItemCandidate {
  id: string;
  key: string;
  title: string;
  stateName?: string | null;
  stateCategory?: string | null;
  closed: boolean;
}

export type PmDecisionKind = 'general' | 'scopeChange';
export type PmDecisionStatus = 'open' | 'accepted' | 'superseded';

export interface PmDecision {
  id: string;
  projectId: string;
  title: string;
  body?: string | null;
  kind: PmDecisionKind | string;
  status: PmDecisionStatus | string;
  decidedAt?: string | null;
  decidedBy?: string | null;
  documentId?: string | null;
  documentName?: string | null;
  wbsIds: string[];
  workItemIds: string[];
  resourceIds: string[];
}

export type PmStageGateStatus = 'open' | 'passed' | 'failed' | 'waived';

export interface PmStageGate {
  id: string;
  projectId: string;
  name: string;
  wbsId?: string | null;
  sortOrder: number;
  status: PmStageGateStatus | string;
  criteria: string[];
  satisfied: string[];
  note?: string | null;
  decidedAt?: string | null;
  decidedBy?: string | null;
  resourceIds: string[];
  decisionId?: string | null;
}

export interface PmCreateStageGateRequest {
  name: string;
  wbsId?: string | null;
  sortOrder?: number | null;
  status?: string | null;
  criteria?: string[];
  satisfied?: string[];
  note?: string | null;
  resourceIds?: string[];
  decisionId?: string | null;
}

export type PmUpdateStageGateRequest = Partial<PmCreateStageGateRequest>;

export type PmRaidKind = 'risk' | 'assumption' | 'issue' | 'dependency';
export type PmRaidStatus =
  | 'open'
  | 'mitigating'
  | 'closed'
  | 'validated'
  | 'invalid'
  | 'inProgress'
  | 'waiting'
  | 'resolved';
export type PmRaidLevel = 'low' | 'medium' | 'high';
export type PmRaidResponse = 'none' | 'avoid' | 'mitigate' | 'transfer' | 'accept';

export interface PmRaidItem {
  id: string;
  projectId: string;
  kind: PmRaidKind | string;
  title: string;
  body?: string | null;
  status: PmRaidStatus | string;
  impact: PmRaidLevel | string;
  likelihood: PmRaidLevel | string;
  response: PmRaidResponse | string;
  owner?: string | null;
  dueDate?: string | null;
  wbsIds: string[];
  workItemIds: string[];
  resourceIds: string[];
  closedAt?: string | null;
  closedBy?: string | null;
  score: number;
  elevated: boolean;
  open: boolean;
}

export interface PmCreateRaidItemRequest {
  kind: string;
  title: string;
  body?: string | null;
  status?: string | null;
  impact?: string | null;
  likelihood?: string | null;
  response?: string | null;
  owner?: string | null;
  dueDate?: string | null;
  wbsIds?: string[];
  workItemIds?: string[];
  resourceIds?: string[];
}

export type PmUpdateRaidItemRequest = Partial<PmCreateRaidItemRequest>;

export interface PmResourceAssignment {
  id: string;
  projectId: string;
  wbsId: string;
  personId?: string | null;
  name: string;
  role?: string | null;
  plannedHours: number;
  start?: string | null;
  finish?: string | null;
  effectiveStart?: string | null;
  effectiveFinish?: string | null;
  unscheduled: boolean;
}

export interface PmCreateResourceAssignmentRequest {
  wbsId: string;
  personId?: string | null;
  name: string;
  role?: string | null;
  plannedHours: number;
  start?: string | null;
  finish?: string | null;
}

export type PmUpdateResourceAssignmentRequest = Partial<PmCreateResourceAssignmentRequest>;

export interface PmCapacityWeek {
  weekStart: string;
  hours: number;
  capacityHours: number;
  overloaded: boolean;
}

export interface PmCapacityPerson {
  key: string;
  personId?: string | null;
  name: string;
  totalHours: number;
  unscheduledHours: number;
  weeklyCapacityHours: number;
  overloaded: boolean;
  weeks: PmCapacityWeek[];
}

export interface PmProjectCapacity {
  weeklyCapacityHours: number;
  overloadedCount: number;
  assignments: PmResourceAssignment[];
  people: PmCapacityPerson[];
}

export type PmBudgetCategory = 'labor' | 'material' | 'subcontract' | 'other';

export interface PmBudgetLine {
  id: string;
  projectId: string;
  wbsId: string;
  category: PmBudgetCategory | string;
  name: string;
  plannedAmount: number;
  actualAmount: number;
  currency: string;
  note?: string | null;
  variance: number;
  over: boolean;
}

export interface PmCreateBudgetLineRequest {
  wbsId: string;
  category?: string | null;
  name: string;
  plannedAmount: number;
  actualAmount?: number;
  currency?: string | null;
  note?: string | null;
}

export type PmUpdateBudgetLineRequest = Partial<PmCreateBudgetLineRequest>;

export interface PmBudgetPackage {
  wbsId: string;
  plannedAmount: number;
  actualAmount: number;
  variance: number;
  over: boolean;
  currency: string;
}

export interface PmProjectBudget {
  currency: string;
  plannedAmount: number;
  actualAmount: number;
  variance: number;
  overCount: number;
  lines: PmBudgetLine[];
  packages: PmBudgetPackage[];
}

export type PmAckStatus = 'pending' | 'acknowledged' | 'waived';

export interface PmAcknowledgement {
  id: string;
  projectId: string;
  resourceId: string;
  title: string;
  versionLabel?: string | null;
  personName: string;
  personId?: string | null;
  wbsId?: string | null;
  status: PmAckStatus | string;
  dueDate?: string | null;
  note?: string | null;
  acknowledgedAt?: string | null;
  acknowledgedBy?: string | null;
  pending: boolean;
  overdue: boolean;
}

export interface PmCreateAcknowledgementRequest {
  resourceId: string;
  title: string;
  versionLabel?: string | null;
  personName: string;
  personId?: string | null;
  wbsId?: string | null;
  status?: string | null;
  dueDate?: string | null;
  note?: string | null;
}

export type PmUpdateAcknowledgementRequest = Partial<PmCreateAcknowledgementRequest>;

export interface PmProjectAcknowledgements {
  pendingCount: number;
  overdueCount: number;
  items: PmAcknowledgement[];
}

export type PmObligationStatus = 'open' | 'inProgress' | 'satisfied' | 'waived';

export interface PmObligation {
  id: string;
  projectId: string;
  title: string;
  clauseRef?: string | null;
  sourceResourceId?: string | null;
  wbsId?: string | null;
  workItemId?: string | null;
  evidenceResourceId?: string | null;
  status: PmObligationStatus | string;
  dueDate?: string | null;
  note?: string | null;
  closedAt?: string | null;
  closedBy?: string | null;
  open: boolean;
  overdue: boolean;
  unbound: boolean;
  missingEvidence: boolean;
}

export interface PmCreateObligationRequest {
  title: string;
  clauseRef?: string | null;
  sourceResourceId?: string | null;
  wbsId?: string | null;
  workItemId?: string | null;
  evidenceResourceId?: string | null;
  status?: string | null;
  dueDate?: string | null;
  note?: string | null;
}

export type PmUpdateObligationRequest = Partial<PmCreateObligationRequest>;

export interface PmProjectObligations {
  openCount: number;
  overdueCount: number;
  unboundCount: number;
  items: PmObligation[];
}

export type PmAuditPackKind = 'audit' | 'customer' | 'internal';
export type PmAuditPackStatus = 'draft' | 'assembled' | 'issued' | 'withdrawn';

export interface PmAuditPack {
  id: string;
  projectId: string;
  name: string;
  kind: PmAuditPackKind | string;
  wbsId?: string | null;
  status: PmAuditPackStatus | string;
  dueDate?: string | null;
  resourceIds: string[];
  recipient?: string | null;
  note?: string | null;
  issuedAt?: string | null;
  issuedBy?: string | null;
  itemCount: number;
  open: boolean;
  incomplete: boolean;
  overdue: boolean;
}

export interface PmCreateAuditPackRequest {
  name: string;
  kind?: string | null;
  wbsId?: string | null;
  status?: string | null;
  dueDate?: string | null;
  resourceIds?: string[] | null;
  recipient?: string | null;
  note?: string | null;
}

export type PmUpdateAuditPackRequest = Partial<PmCreateAuditPackRequest>;

export interface PmProjectAuditPacks {
  openCount: number;
  incompleteCount: number;
  overdueCount: number;
  items: PmAuditPack[];
}

export type PmMeetingActionStatus = 'open' | 'inProgress' | 'done' | 'waived';

export interface PmMeetingAction {
  id: string;
  projectId: string;
  meetingId: string;
  title: string;
  ownerName?: string | null;
  dueDate?: string | null;
  status: PmMeetingActionStatus | string;
  workItemId?: string | null;
  wbsId?: string | null;
  note?: string | null;
  closedAt?: string | null;
  closedBy?: string | null;
  open: boolean;
  overdue: boolean;
  unbound: boolean;
}

export interface PmMeeting {
  id: string;
  projectId: string;
  name: string;
  heldAt?: string | null;
  minutesResourceId?: string | null;
  wbsId?: string | null;
  attendees?: string | null;
  note?: string | null;
  actionCount: number;
  openActionCount: number;
  actions: PmMeetingAction[];
}

export interface PmCreateMeetingRequest {
  name: string;
  heldAt?: string | null;
  minutesResourceId?: string | null;
  wbsId?: string | null;
  attendees?: string | null;
  note?: string | null;
}

export type PmUpdateMeetingRequest = Partial<PmCreateMeetingRequest>;

export interface PmCreateMeetingActionRequest {
  title: string;
  ownerName?: string | null;
  dueDate?: string | null;
  status?: string | null;
  workItemId?: string | null;
  wbsId?: string | null;
  note?: string | null;
}

export type PmUpdateMeetingActionRequest = Partial<PmCreateMeetingActionRequest>;

export interface PmProjectMeetings {
  openActionCount: number;
  overdueActionCount: number;
  unboundActionCount: number;
  items: PmMeeting[];
}

export interface PmProjectMeetingActions {
  openCount: number;
  overdueCount: number;
  unboundCount: number;
  items: PmMeetingAction[];
}

export type PmStakeholderKind = 'customer' | 'supplier' | 'consultant' | 'regulator' | 'sponsor' | 'other';
export type PmStakeholderStatus = 'invited' | 'active' | 'revoked';

export interface PmStakeholder {
  id: string;
  projectId: string;
  name: string;
  organization?: string | null;
  kind: PmStakeholderKind | string;
  email?: string | null;
  wbsId?: string | null;
  status: PmStakeholderStatus | string;
  accessUntil?: string | null;
  resourceIds: string[];
  note?: string | null;
  revokedAt?: string | null;
  revokedBy?: string | null;
  itemCount: number;
  open: boolean;
  incomplete: boolean;
  overdue: boolean;
}

export interface PmCreateStakeholderRequest {
  name: string;
  organization?: string | null;
  kind?: string | null;
  email?: string | null;
  wbsId?: string | null;
  status?: string | null;
  accessUntil?: string | null;
  resourceIds?: string[] | null;
  note?: string | null;
}

export type PmUpdateStakeholderRequest = Partial<PmCreateStakeholderRequest>;

export interface PmProjectStakeholders {
  openCount: number;
  incompleteCount: number;
  overdueCount: number;
  items: PmStakeholder[];
}

export type PmProcessMapKind = 'procedure' | 'workflow' | 'org' | 'other';
export type PmProcessMapStatus = 'draft' | 'current' | 'superseded';

export interface PmProcessMap {
  id: string;
  projectId: string;
  name: string;
  kind: PmProcessMapKind | string;
  resourceId?: string | null;
  wbsId?: string | null;
  status: PmProcessMapStatus | string;
  note?: string | null;
  currentAt?: string | null;
  currentBy?: string | null;
  supersededAt?: string | null;
  supersededBy?: string | null;
  open: boolean;
  incomplete: boolean;
  current: boolean;
}

export interface PmCreateProcessMapRequest {
  name: string;
  kind?: string | null;
  resourceId?: string | null;
  wbsId?: string | null;
  status?: string | null;
  note?: string | null;
}

export type PmUpdateProcessMapRequest = Partial<PmCreateProcessMapRequest>;

export interface PmProjectProcessMaps {
  openCount: number;
  incompleteCount: number;
  currentCount: number;
  items: PmProcessMap[];
}

export interface PmCreateDecisionRequest {
  title: string;
  body?: string | null;
  kind?: string | null;
  status?: string | null;
  decidedAt?: string | null;
  documentId?: string | null;
  wbsIds?: string[];
  workItemIds?: string[];
  resourceIds?: string[];
}

export type PmUpdateDecisionRequest = Partial<PmCreateDecisionRequest>;

export type PmTraceFlag =
  | 'delayed'
  | 'milestoneAtRisk'
  | 'drifted'
  | 'unbound'
  | 'openWork'
  | 'missingEvidence'
  | 'missingApproval'
  | 'openGate'
  | 'failedGate'
  | 'openRisk'
  | 'openIssue'
  | 'overloadedResource'
  | 'overBudget'
  | 'pendingAck'
  | 'overdueAck'
  | 'openObligation'
  | 'overdueObligation'
  | 'unboundObligation'
  | 'openAuditPack'
  | 'incompleteAuditPack'
  | 'overdueAuditPack'
  | 'openMeetingAction'
  | 'overdueMeetingAction'
  | 'unboundMeetingAction'
  | 'openStakeholder'
  | 'incompleteStakeholder'
  | 'overdueStakeholder'
  | 'openProcessMap'
  | 'incompleteProcessMap';

export interface PmTraceDocument {
  resourceId: string;
  name: string;
  kind?: string | null;
  relationType: string;
  status: string;
  approved: boolean;
}

export interface PmTraceRow {
  wbsId: string;
  wbsCode?: string | null;
  wbsName: string;
  kind: string;
  percentComplete: number;
  plannedFinish?: string | null;
  baselineDrifted: boolean;
  workItemId?: string | null;
  workItemKey?: string | null;
  workItemTitle?: string | null;
  workItemStateName?: string | null;
  workItemClosed: boolean;
  documents: PmTraceDocument[];
  flags: PmTraceFlag[] | string[];
  decisions?: PmDecision[];
  raidItems?: PmRaidItem[];
}

export interface PmStatusCounts {
  delayed: number;
  milestoneAtRisk: number;
  drifted: number;
  unboundLeaf: number;
  openWork: number;
  missingEvidence: number;
  missingApproval: number;
  openScopeChange?: number;
  openGate?: number;
  failedGate?: number;
  openRisk?: number;
  openIssue?: number;
  openAssumption?: number;
  openDependency?: number;
  overloadedResource?: number;
  overBudget?: number;
  pendingAck?: number;
  overdueAck?: number;
  openObligation?: number;
  overdueObligation?: number;
  unboundObligation?: number;
  openAuditPack?: number;
  incompleteAuditPack?: number;
  overdueAuditPack?: number;
  openMeetingAction?: number;
  overdueMeetingAction?: number;
  unboundMeetingAction?: number;
  openStakeholder?: number;
  incompleteStakeholder?: number;
  overdueStakeholder?: number;
  openProcessMap?: number;
  incompleteProcessMap?: number;
  currentProcessMap?: number;
}

export interface PmProjectStatusPack {
  projectId: string;
  generatedAt: string;
  counts: PmStatusCounts;
  items: PmTraceRow[];
  gates?: PmStageGate[];
  raidItems?: PmRaidItem[];
  capacity?: PmProjectCapacity;
  budget?: PmProjectBudget;
  acknowledgements?: PmProjectAcknowledgements;
  obligations?: PmProjectObligations;
  auditPacks?: PmProjectAuditPacks;
  meetingActions?: PmProjectMeetingActions;
  stakeholders?: PmProjectStakeholders;
  processMaps?: PmProjectProcessMaps;
}

export interface PmJobPackWbsPreview {
  name: string;
  kind: string;
  children?: PmJobPackWbsPreview[];
}

export interface PmJobPackStarter {
  folder: string;
  title: string;
  kind?: string | null;
  body?: string | null;
}

export interface PmJobPack {
  code: string;
  name: string;
  version?: string | null;
  description?: string | null;
  kinds: string[];
  folders: string[];
  wbs: PmJobPackWbsPreview[];
  starters?: PmJobPackStarter[];
}

export interface PmProjectPackInstall {
  packCode: string;
  version: string;
  appliedAt?: string | null;
  appliedBy?: string | null;
  outdated: boolean;
}

export interface PmProjectPackCatalog {
  catalog: PmJobPack[];
  installed: PmProjectPackInstall[];
}

export interface PmApplyPackResult {
  packCode: string;
  version: string;
  created: number;
  skipped: number;
  updated: number;
  removed: number;
  kept: number;
  workspaceCreated?: boolean;
  workspaceId?: string | null;
}

export interface PmPackPreviewItem {
  path: string;
  kind: string;
  action: string;
  wbsId?: string | null;
}

export interface PmPackPreview {
  packCode: string;
  name: string;
  version: string;
  installedVersion?: string | null;
  outdated: boolean;
  intent: string;
  createCount: number;
  skipCount: number;
  updateCount: number;
  removeCount: number;
  keepCount: number;
  items: PmPackPreviewItem[];
  workspaceAction?: string | null;
  workspaceId?: string | null;
  workspaceName?: string | null;
}
