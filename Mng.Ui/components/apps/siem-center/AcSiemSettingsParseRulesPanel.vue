<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  createSecEventParseRule,
  deleteSecEventParseRule,
  fetchSecEventParseRuleManageList,
  previewSecEventParseRule,
  publishSecEventParseRuleCatalog,
  updateSecEventParseRule,
} from '@/services/secEventParseRuleCatalogService';
import type {
  SecEventParseRuleExtractStep,
  SecEventParseRuleManageItem,
  SecEventParseRuleManageListResponse,
  SecEventParseRulePreviewResponse,
  SecEventParseRuleUpsertPayload,
} from '@/types/apps/secEventParseRules';
import AcSiemWindowsParseWizardDialog from '@/components/apps/siem-center/AcSiemWindowsParseWizardDialog.vue';
import AcSiemLinuxParseWizardDialog from '@/components/apps/siem-center/AcSiemLinuxParseWizardDialog.vue';

const { t, locale } = useAppI18n();

type Platform = 'windows' | 'linux';

interface RuleTemplate {
  id: string;
  platform: Platform;
  nameKey: string;
  hintKey: string;
  ruleId: string;
  name: string;
  eventIds?: string;
  channel?: string;
  messageFamily?: string | null;
  eventAction: string;
  eventOutcome: string;
  eventCategory: string;
  userFrom?: string;
  ipFrom?: string;
  /** Extra EventData → target maps (custom.* ok). */
  extraMaps?: Array<{ from: string; to: string }>;
  /** When set, form opens in advanced mode with this regex extract. */
  textRegex?: {
    pattern: string;
    groups: Record<string, string>;
  };
  previewRaw?: string;
  previewMessage?: string;
}

const TEMPLATES: RuleTemplate[] = [
  {
    id: 'win-fail',
    platform: 'windows',
    nameKey: 'siemCenter.settings.parsers.tplWinFail',
    hintKey: 'siemCenter.settings.parsers.tplWinFailHint',
    ruleId: 'custom.windows.logon.failed',
    name: 'Windows failed logon',
    eventIds: '4625',
    channel: 'Security',
    eventAction: 'login_failed',
    eventOutcome: 'failure',
    eventCategory: 'authentication',
    userFrom: 'TargetUserName',
    ipFrom: 'IpAddress',
    extraMaps: [
      { from: 'LogonType', to: 'custom.logon_type' },
      { from: 'WorkstationName', to: 'custom.workstation' },
      { from: 'TargetDomainName', to: 'custom.target_domain' },
    ],
    previewRaw:
      '{\n  "EventID": 4625,\n  "TargetUserName": "admin",\n  "IpAddress": "10.0.0.5",\n  "LogonType": "3",\n  "WorkstationName": "PC1"\n}',
  },
  {
    id: 'win-ok',
    platform: 'windows',
    nameKey: 'siemCenter.settings.parsers.tplWinOk',
    hintKey: 'siemCenter.settings.parsers.tplWinOkHint',
    ruleId: 'custom.windows.logon.success',
    name: 'Windows successful logon',
    eventIds: '4624',
    channel: 'Security',
    eventAction: 'login_success',
    eventOutcome: 'success',
    eventCategory: 'authentication',
    userFrom: 'TargetUserName',
    ipFrom: 'IpAddress',
    extraMaps: [
      { from: 'LogonType', to: 'custom.logon_type' },
      { from: 'WorkstationName', to: 'custom.workstation' },
      { from: 'TargetDomainName', to: 'custom.target_domain' },
    ],
    previewRaw:
      '{\n  "EventID": 4624,\n  "TargetUserName": "admin",\n  "IpAddress": "10.0.0.5",\n  "LogonType": "10",\n  "WorkstationName": "PC1"\n}',
  },
  {
    id: 'win-lock',
    platform: 'windows',
    nameKey: 'siemCenter.settings.parsers.tplWinLock',
    hintKey: 'siemCenter.settings.parsers.tplWinLockHint',
    ruleId: 'custom.windows.account.locked',
    name: 'Windows account locked',
    eventIds: '4740',
    channel: 'Security',
    eventAction: 'account_locked',
    eventOutcome: 'failure',
    eventCategory: 'authentication',
    userFrom: 'TargetUserName',
    extraMaps: [
      { from: 'WorkstationName', to: 'custom.workstation' },
      { from: 'TargetDomainName', to: 'custom.target_domain' },
    ],
    previewRaw: '{\n  "EventID": 4740,\n  "TargetUserName": "admin",\n  "WorkstationName": "PC1"\n}',
  },
  {
    id: 'win-rdp-21',
    platform: 'windows',
    nameKey: 'siemCenter.settings.parsers.tplWinRdpLogon',
    hintKey: 'siemCenter.settings.parsers.tplWinRdpLogonHint',
    ruleId: 'custom.windows.rdp.logon',
    name: 'RDP session logon',
    eventIds: '21',
    channel: 'Microsoft-Windows-TerminalServices-LocalSessionManager/Operational',
    eventAction: 'rdp.logon',
    eventOutcome: 'success',
    eventCategory: 'authentication',
    userFrom: 'User',
    ipFrom: 'Address',
    extraMaps: [{ from: 'SessionID', to: 'custom.session_id' }],
    previewRaw:
      '{\n  "EventID": 21,\n  "User": "DOMAIN\\\\alice",\n  "Address": "10.0.0.8",\n  "SessionID": "3"\n}',
  },
  {
    id: 'win-app-connect',
    platform: 'windows',
    nameKey: 'siemCenter.settings.parsers.tplWinAppConnect',
    hintKey: 'siemCenter.settings.parsers.tplWinAppConnectHint',
    ruleId: 'custom.windows.app.connect_failed',
    name: 'Application connect failed (dial tcp)',
    eventIds: '65002',
    channel: 'Application',
    eventAction: 'app.connect_failed',
    eventOutcome: 'failure',
    eventCategory: 'network',
    textRegex: {
      pattern:
        '(?i)failed to connect (?<service>[\\w.-]+).*?dial tcp (?<ip>\\d{1,3}(?:\\.\\d{1,3}){3}):(?<port>\\d+)',
      groups: {
        service: 'custom.service',
        ip: 'network.dstIp',
        port: 'network.dstPort',
      },
    },
    previewMessage:
      'failed to connect rabbitmq server, error: dial tcp 192.168.20.17:5672: connectex: No connection could be made because the target machine actively refused it.',
    previewRaw:
      '{\n  "EventID": 65002,\n  "Channel": "Application",\n  "Message": "failed to connect rabbitmq server, error: dial tcp 192.168.20.17:5672: connectex: No connection could be made because the target machine actively refused it."\n}',
  },
  {
    id: 'linux-fail',
    platform: 'linux',
    nameKey: 'siemCenter.settings.parsers.tplLinuxFail',
    hintKey: 'siemCenter.settings.parsers.tplLinuxFailHint',
    ruleId: 'custom.linux.sshd.failed',
    name: 'Linux SSH failed password',
    messageFamily: 'sshd_failed_password',
    eventAction: 'login_failed',
    eventOutcome: 'failure',
    eventCategory: 'authentication',
    previewMessage: 'sshd[1]: Failed password for root from 192.168.1.9',
    previewRaw: '"sshd[1]: Failed password for root from 192.168.1.9"',
  },
  {
    id: 'linux-ok',
    platform: 'linux',
    nameKey: 'siemCenter.settings.parsers.tplLinuxOk',
    hintKey: 'siemCenter.settings.parsers.tplLinuxOkHint',
    ruleId: 'custom.linux.sshd.success',
    name: 'Linux SSH accepted password',
    messageFamily: 'sshd_accepted',
    eventAction: 'login_success',
    eventOutcome: 'success',
    eventCategory: 'authentication',
    previewMessage: 'sshd[1]: Accepted password for alice from 192.168.1.10',
    previewRaw: '"sshd[1]: Accepted password for alice from 192.168.1.10"',
  },
];

const ACTION_OPTIONS = [
  'login_failed',
  'login_success',
  'logoff',
  'explicit_credentials',
  'account_locked',
  'account_created',
  'account_deleted',
  'account_enabled',
  'privileged_assigned',
  'privilege_denied',
  'privilege_escalation',
  'app.connect_failed',
  'rdp.logon',
  'rdp.logoff',
  'rdp.disconnect',
  'rdp.reconnect',
  'group_member_added',
  'group_changed',
  'directory_object_modified',
  'directory_object_created',
  'directory_object_deleted',
] as const;

const OUTCOME_OPTIONS = ['failure', 'success', 'unknown'] as const;
const CATEGORY_OPTIONS = ['authentication', 'authorization', 'network', 'config_change'] as const;

const EXTRACT_TYPES = ['event_data', 'json_path', 'regex', 'kv', 'constant'] as const;
const TARGET_FIELDS = [
  'event.action',
  'event.outcome',
  'event.category',
  'event.severity',
  'actor.user',
  'network.srcIp',
  'network.dstIp',
  'network.dstPort',
  'network.protocol',
  'message',
  'tags',
  'custom.logon_type',
  'custom.workstation',
  'custom.target_domain',
  'custom.target_user',
  'custom.target_server',
  'custom.session_id',
  'custom.privilege_list',
  'custom.member',
  'custom.group',
  'custom.object_dn',
  'custom.attribute',
  'custom.correlation_id',
  'custom.sudo_command',
  'custom.service',
] as const;

const MESSAGE_FAMILY_OPTIONS = computed(() => [
  { value: 'sshd_failed_password', title: t('siemCenter.settings.parsers.familySshdFail') },
  { value: 'sshd_accepted', title: t('siemCenter.settings.parsers.familySshdOk') },
  { value: 'sudo_not_allowed', title: t('siemCenter.settings.parsers.familySudoDeny') },
  { value: 'sudo_command', title: t('siemCenter.settings.parsers.familySudoCmd') },
]);

const loading = ref(true);
const saving = ref(false);
const publishing = ref(false);
const previewing = ref(false);
const error = ref<string | null>(null);
const flash = ref<string | null>(null);
const managed = ref<SecEventParseRuleManageListResponse | null>(null);

/** List table filters / paging */
const listSearch = ref('');
const filterPlatform = ref<'all' | 'windows' | 'linux' | 'other'>('all');
const filterEnabled = ref<'all' | 'yes' | 'no'>('all');
const filterBuiltin = ref<'all' | 'builtin' | 'custom'>('all');
const listPage = ref(1);
const listItemsPerPage = ref(10);
const LIST_PAGE_SIZE_OPTIONS = [
  { value: 10, title: '10' },
  { value: 25, title: '25' },
  { value: 50, title: '50' },
  { value: -1, title: 'All' },
];

const dialogOpen = ref(false);
const editingRuleId = ref<string | null>(null);
const deleteTarget = ref<SecEventParseRuleManageItem | null>(null);
const selectedTemplateId = ref<string | null>(null);
const showAdvanced = ref(false);

const formRuleId = ref('');
const formName = ref('');
const formDescription = ref('');
const formEnabled = ref(true);
const formPriority = ref(100);
const formBuiltin = ref(false);
const formPlatform = ref<Platform>('windows');
const formEventIds = ref('');
const formChannel = ref('Security');
const formMessageFamily = ref<string | null>(null);
const formEventAction = ref('login_failed');
const formEventOutcome = ref('failure');
const formEventCategory = ref('authentication');
const formUserFrom = ref('TargetUserName');
const formIpFrom = ref('IpAddress');
const formExtraMaps = ref<Array<{ from: string; to: string }>>([]);
const formExtract = ref<SecEventParseRuleExtractStep[]>([]);

const previewOpen = ref(false);
const previewRuleId = ref<string | null>(null);
const previewRawJson = ref('');
const previewMessage = ref('');
const previewProduct = ref('windows');
const previewEventId = ref<number | null>(4625);
const previewChannel = ref('Security');
const previewResult = ref<SecEventParseRulePreviewResponse | null>(null);

const dateLocale = computed(() => (locale.value === 'tr' ? 'tr-TR' : 'en-GB'));
const isCreate = computed(() => !editingRuleId.value);

const selectedTemplateHint = computed(() => {
  const tpl = TEMPLATES.find((x) => x.id === selectedTemplateId.value);
  return tpl ? t(tpl.hintKey) : t('siemCenter.settings.parsers.tplCustomHint');
});

function formatUtc(iso?: string | null): string {
  if (!iso) return '—';
  try {
    return new Intl.DateTimeFormat(dateLocale.value, {
      dateStyle: 'short',
      timeStyle: 'medium',
      timeZone: 'UTC',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

function splitCsv(value: string): string[] {
  return value
    .split(/[\s,;]+/)
    .map((s) => s.trim())
    .filter(Boolean);
}

function parseEventIds(value: string): number[] {
  return [
    ...new Set(
      splitCsv(value)
        .map((s) => Number(s))
        .filter((n) => Number.isFinite(n) && n > 0),
    ),
  ].sort((a, b) => a - b);
}

function slugify(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '.')
    .replace(/^\.+|\.+$/g, '')
    .slice(0, 64);
}

function emptyExtractStep(): SecEventParseRuleExtractStep {
  return { type: 'constant', from: null, to: 'event.action', value: '', pattern: null, groups: null };
}

function linuxRegexForFamily(family: string | null): SecEventParseRuleExtractStep | null {
  switch (family) {
    case 'sshd_failed_password':
      return {
        type: 'regex',
        from: 'message',
        pattern: String.raw`Failed password for (?:invalid user )?(?<user>\S+) from (?<ip>[\d.]+)`,
        groups: { user: 'actor.user', ip: 'network.srcIp' },
        to: null,
        value: null,
      };
    case 'sshd_accepted':
      return {
        type: 'regex',
        from: 'message',
        pattern: String.raw`Accepted password for (?<user>\S+) from (?<ip>[\d.]+)`,
        groups: { user: 'actor.user', ip: 'network.srcIp' },
        to: null,
        value: null,
      };
    case 'sudo_not_allowed':
      return {
        type: 'regex',
        from: 'message',
        pattern: String.raw`sudo:\s+(?<user>\S+)\s+:\s+command not allowed`,
        groups: { user: 'actor.user' },
        to: null,
        value: null,
      };
    default:
      return null;
  }
}

function buildSimpleExtract(): SecEventParseRuleExtractStep[] {
  const steps: SecEventParseRuleExtractStep[] = [];
  if (formPlatform.value === 'windows') {
    if (formUserFrom.value.trim()) {
      steps.push({
        type: 'event_data',
        from: formUserFrom.value.trim(),
        to: 'actor.user',
        value: null,
        pattern: null,
        groups: null,
      });
    }
    if (formIpFrom.value.trim()) {
      steps.push({
        type: 'event_data',
        from: formIpFrom.value.trim(),
        to: 'network.srcIp',
        value: null,
        pattern: null,
        groups: null,
      });
    }
    for (const m of formExtraMaps.value) {
      if (!m.from?.trim() || !m.to?.trim()) continue;
      steps.push({
        type: 'event_data',
        from: m.from.trim(),
        to: m.to.trim(),
        value: null,
        pattern: null,
        groups: null,
      });
    }
  } else {
    const rx = linuxRegexForFamily(formMessageFamily.value);
    if (rx) steps.push(rx);
  }
  steps.push(
    { type: 'constant', to: 'event.action', value: formEventAction.value, from: null, pattern: null, groups: null },
    { type: 'constant', to: 'event.outcome', value: formEventOutcome.value, from: null, pattern: null, groups: null },
    { type: 'constant', to: 'event.category', value: formEventCategory.value, from: null, pattern: null, groups: null },
  );
  return steps;
}

function syncExtractFromSimple() {
  if (showAdvanced.value) return;
  formExtract.value = buildSimpleExtract();
}

function applyTemplate(tpl: RuleTemplate) {
  selectedTemplateId.value = tpl.id;
  formPlatform.value = tpl.platform;
  formRuleId.value = tpl.ruleId;
  formName.value = tpl.name;
  formDescription.value = t(tpl.hintKey);
  formEventIds.value = tpl.eventIds || '';
  formChannel.value = tpl.channel || '';
  formMessageFamily.value = tpl.messageFamily ?? null;
  formEventAction.value = tpl.eventAction;
  formEventOutcome.value = tpl.eventOutcome;
  formEventCategory.value = tpl.eventCategory;
  formUserFrom.value = tpl.userFrom || 'TargetUserName';
  formIpFrom.value = tpl.ipFrom || 'IpAddress';
  formExtraMaps.value = [...(tpl.extraMaps || [])];
  formPriority.value = 100;
  formEnabled.value = true;
  previewRawJson.value = tpl.previewRaw || '';
  previewMessage.value = tpl.previewMessage || '';
  previewProduct.value = tpl.platform === 'windows' ? 'windows' : 'linux-syslog';
  previewEventId.value = parseEventIds(formEventIds.value)[0] ?? null;
  previewChannel.value = formChannel.value || 'Security';

  if (tpl.textRegex) {
    showAdvanced.value = true;
    formExtract.value = [
      {
        type: 'regex',
        from: 'message',
        to: null,
        value: null,
        pattern: tpl.textRegex.pattern,
        groups: { ...tpl.textRegex.groups },
      },
      {
        type: 'constant',
        to: 'event.action',
        value: tpl.eventAction,
        from: null,
        pattern: null,
        groups: null,
      },
      {
        type: 'constant',
        to: 'event.outcome',
        value: tpl.eventOutcome,
        from: null,
        pattern: null,
        groups: null,
      },
      {
        type: 'constant',
        to: 'event.category',
        value: tpl.eventCategory,
        from: null,
        pattern: null,
        groups: null,
      },
    ];
  } else {
    showAdvanced.value = false;
    syncExtractFromSimple();
  }
}

function useCustomTemplate() {
  selectedTemplateId.value = null;
  showAdvanced.value = false;
  formPlatform.value = 'windows';
  formRuleId.value = '';
  formName.value = '';
  formDescription.value = '';
  formEventIds.value = '';
  formChannel.value = 'Security';
  formMessageFamily.value = null;
  formEventAction.value = 'login_failed';
  formEventOutcome.value = 'failure';
  formEventCategory.value = 'authentication';
  formUserFrom.value = 'TargetUserName';
  formIpFrom.value = 'IpAddress';
  formExtraMaps.value = [];
  syncExtractFromSimple();
}

async function load() {
  loading.value = true;
  error.value = null;
  try {
    managed.value = await fetchSecEventParseRuleManageList();
  } catch (e: unknown) {
    managed.value = null;
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

const wizardOpen = ref(false);
const wizardEditRule = ref<SecEventParseRuleManageItem | null>(null);
const linuxWizardOpen = ref(false);
const linuxWizardEditRule = ref<SecEventParseRuleManageItem | null>(null);

function openWindowsWizard() {
  wizardEditRule.value = null;
  wizardOpen.value = true;
}

function openLinuxWizard() {
  linuxWizardEditRule.value = null;
  linuxWizardOpen.value = true;
}

function onWizardSaved() {
  wizardEditRule.value = null;
  void load();
}

function onWizardClosed(open: boolean) {
  wizardOpen.value = open;
  if (!open) wizardEditRule.value = null;
}

function onLinuxWizardSaved() {
  linuxWizardEditRule.value = null;
  void load();
}

function onLinuxWizardClosed(open: boolean) {
  linuxWizardOpen.value = open;
  if (!open) linuxWizardEditRule.value = null;
}

function inferPlatform(item: SecEventParseRuleManageItem): Platform {
  const products = (item.match.sourceProduct || []).join(' ').toLowerCase();
  return products.includes('linux') ? 'linux' : 'windows';
}

function openEdit(item: SecEventParseRuleManageItem) {
  if (inferPlatform(item) === 'windows') {
    wizardEditRule.value = item;
    wizardOpen.value = true;
    return;
  }
  if (inferPlatform(item) === 'linux') {
    linuxWizardEditRule.value = item;
    linuxWizardOpen.value = true;
    return;
  }

  editingRuleId.value = item.ruleId;
  selectedTemplateId.value = null;
  showAdvanced.value = false;
  formRuleId.value = item.ruleId;
  formName.value = item.name;
  formDescription.value = item.description || '';
  formEnabled.value = item.enabled;
  formPriority.value = item.priority;
  formBuiltin.value = item.builtin;
  formPlatform.value = inferPlatform(item);
  formChannel.value = (item.match.channel || []).join(', ');
  formEventIds.value = (item.match.eventIds || []).join(', ');
  formMessageFamily.value = item.match.messagePatterns?.[0]?.family || null;

  const actionStep = item.extract.find((s) => s.type === 'constant' && s.to === 'event.action');
  const outcomeStep = item.extract.find((s) => s.type === 'constant' && s.to === 'event.outcome');
  const categoryStep = item.extract.find((s) => s.type === 'constant' && s.to === 'event.category');
  const userStep = item.extract.find((s) => s.to === 'actor.user' && s.type === 'event_data');
  const ipStep = item.extract.find((s) => s.to === 'network.srcIp' && s.type === 'event_data');

  formEventAction.value = actionStep?.value || 'login_failed';
  formEventOutcome.value = outcomeStep?.value || 'failure';
  formEventCategory.value = categoryStep?.value || 'authentication';
  formUserFrom.value = userStep?.from || 'TargetUserName';
  formIpFrom.value = ipStep?.from || 'IpAddress';
  formExtract.value = item.extract.length
    ? item.extract.map((s) => ({ ...s, groups: s.groups ? { ...s.groups } : null }))
    : [emptyExtractStep()];

  // If extract looks non-simple, open advanced.
  const hasRegexOrKv = item.extract.some((s) => s.type === 'regex' || s.type === 'kv' || s.type === 'json_path');
  const simpleish = !hasRegexOrKv || formPlatform.value === 'linux';
  showAdvanced.value = !simpleish && item.extract.length > 5;
  dialogOpen.value = true;
}

function onNameBlur() {
  if (!isCreate.value || formRuleId.value.trim()) return;
  const slug = slugify(formName.value);
  if (slug) formRuleId.value = `custom.${slug}`;
}

function onPlatformChange() {
  if (formPlatform.value === 'windows') {
    formChannel.value = formChannel.value || 'Security';
    formMessageFamily.value = null;
  } else {
    formChannel.value = '';
    formEventIds.value = '';
    formMessageFamily.value = formMessageFamily.value || 'sshd_failed_password';
  }
  syncExtractFromSimple();
}

function addExtractStep() {
  formExtract.value.push(emptyExtractStep());
}

function removeExtractStep(index: number) {
  formExtract.value.splice(index, 1);
}

function groupsText(step: SecEventParseRuleExtractStep): string {
  if (!step.groups || !Object.keys(step.groups).length) return '';
  return JSON.stringify(step.groups);
}

function setGroupsText(step: SecEventParseRuleExtractStep, text: string) {
  const trimmed = text.trim();
  if (!trimmed) {
    step.groups = null;
    return;
  }
  try {
    const parsed = JSON.parse(trimmed) as Record<string, unknown>;
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      const map: Record<string, string> = {};
      for (const [k, v] of Object.entries(parsed)) map[String(k)] = String(v ?? '');
      step.groups = map;
    }
  } catch {
    // keep previous
  }
}

function buildPayload(): SecEventParseRuleUpsertPayload {
  if (!showAdvanced.value) syncExtractFromSimple();

  const products =
    formPlatform.value === 'windows'
      ? ['windows']
      : ['linux-journal', 'linux-syslog', 'linux-auth'];
  const sourceType = formPlatform.value === 'windows' ? ['ad', 'endpoint'] : ['endpoint', 'linux'];
  const channel = splitCsv(formChannel.value);
  const eventIds = parseEventIds(formEventIds.value);

  return {
    ruleId: formRuleId.value.trim().toLowerCase(),
    name: formName.value.trim(),
    description: formDescription.value.trim() || null,
    enabled: formEnabled.value,
    priority: Number(formPriority.value) || 100,
    match: {
      sourceProduct: products,
      sourceType,
      channel: formPlatform.value === 'windows' && channel.length ? channel : null,
      eventIds: formPlatform.value === 'windows' && eventIds.length ? eventIds : null,
      messagePatterns:
        formPlatform.value === 'linux' && formMessageFamily.value
          ? [{ family: formMessageFamily.value }]
          : null,
    },
    extract: formExtract.value.map((s) => ({
      type: s.type,
      from: s.from || null,
      to: s.to || null,
      value: s.value ?? null,
      pattern: s.pattern || null,
      groups: s.groups && Object.keys(s.groups).length ? s.groups : null,
    })),
    onConflict: 'first_wins',
  };
}

async function saveForm() {
  flash.value = null;
  error.value = null;
  if (!formName.value.trim()) {
    error.value = t('siemCenter.settings.parsers.nameRequired');
    return;
  }
  if (!formRuleId.value.trim()) {
    onNameBlur();
  }
  if (!formRuleId.value.trim()) {
    error.value = t('siemCenter.settings.parsers.ruleIdRequired');
    return;
  }
  if (formPlatform.value === 'windows' && !parseEventIds(formEventIds.value).length) {
    error.value = t('siemCenter.settings.parsers.eventIdsRequired');
    return;
  }
  if (formPlatform.value === 'linux' && !formMessageFamily.value) {
    error.value = t('siemCenter.settings.parsers.familyRequired');
    return;
  }

  saving.value = true;
  try {
    const payload = buildPayload();
    if (editingRuleId.value) {
      await updateSecEventParseRule(editingRuleId.value, payload);
      flash.value = t('siemCenter.settings.parsers.saved');
    } else {
      await createSecEventParseRule(payload);
      flash.value = t('siemCenter.settings.parsers.created');
    }
    dialogOpen.value = false;
    await load();
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  saving.value = true;
  error.value = null;
  try {
    await deleteSecEventParseRule(deleteTarget.value.ruleId);
    flash.value = t('siemCenter.settings.parsers.deleted');
    deleteTarget.value = null;
    await load();
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

async function publish() {
  publishing.value = true;
  error.value = null;
  try {
    const res = await publishSecEventParseRuleCatalog();
    flash.value = t('siemCenter.settings.parsers.published', { version: res.version });
    await load();
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    publishing.value = false;
  }
}

function openPreview(item?: SecEventParseRuleManageItem) {
    previewRuleId.value = item?.ruleId ?? (formRuleId.value || null);
  const platform = item ? inferPlatform(item) : formPlatform.value;
  previewProduct.value = platform === 'windows' ? 'windows' : 'linux-syslog';
  previewChannel.value = item?.match.channel?.[0] || formChannel.value || 'Security';
  previewEventId.value = item?.match.eventIds?.[0] ?? parseEventIds(formEventIds.value)[0] ?? null;
  if (!previewRawJson.value) {
    previewRawJson.value =
      platform === 'windows'
        ? '{\n  "EventID": 4625,\n  "TargetUserName": "admin",\n  "IpAddress": "10.0.0.5"\n}'
        : '"sshd[1]: Failed password for root from 192.168.1.9"';
  }
  previewResult.value = null;
  previewOpen.value = true;
}

async function runPreview() {
  previewing.value = true;
  error.value = null;
  previewResult.value = null;
  try {
    let raw: unknown = previewRawJson.value.trim();
    if (raw) {
      try {
        raw = JSON.parse(String(raw));
      } catch {
        // keep string
      }
    } else {
      raw = undefined;
    }

    const fromForm = dialogOpen.value && !!formRuleId.value.trim();
    previewResult.value = await previewSecEventParseRule({
      ruleId: fromForm ? undefined : previewRuleId.value,
      draftRule: fromForm ? buildPayload() : undefined,
      context: {
        source: {
          product: previewProduct.value.trim() || 'windows',
          type: previewProduct.value.includes('linux') ? 'endpoint' : 'windows-eventlog',
        },
        raw,
        message: previewMessage.value.trim() || undefined,
        channel: previewChannel.value.trim() || undefined,
        eventId: previewEventId.value ?? undefined,
      },
    });
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    previewing.value = false;
  }
}

function productLabel(products: string[]): string {
  const joined = products.join(' ').toLowerCase();
  if (joined.includes('linux')) return t('siemCenter.settings.parsers.platformLinux');
  if (joined.includes('windows')) return t('siemCenter.settings.parsers.platformWindows');
  return products.join(', ') || '—';
}

function platformKey(products: string[]): 'windows' | 'linux' | 'other' {
  const joined = products.join(' ').toLowerCase();
  if (joined.includes('linux')) return 'linux';
  if (joined.includes('windows')) return 'windows';
  return 'other';
}

function ruleAction(item: SecEventParseRuleManageItem): string {
  return (
    item.extract.find((s) => s.to === 'event.action' && s.type === 'constant')?.value
    || '—'
  );
}

function ruleMatchHint(item: SecEventParseRuleManageItem): string {
  if (item.match.eventIds?.length) return `Event ID: ${item.match.eventIds.join(', ')}`;
  const family = item.match.messagePatterns?.[0]?.family;
  if (family) return family;
  return '';
}

interface ParseRuleTableRow extends SecEventParseRuleManageItem {
  _nameSort: string;
  _platform: string;
  _platformKey: 'windows' | 'linux' | 'other';
  _action: string;
  _matchHint: string;
  _priority: number;
}

const listHeaders = computed(() => [
  { title: t('siemCenter.settings.parsers.colName'), key: '_nameSort', sortable: true },
  { title: t('siemCenter.settings.parsers.colPlatform'), key: '_platform', sortable: true },
  { title: t('siemCenter.settings.parsers.colMeaning'), key: '_action', sortable: true },
  { title: t('siemCenter.settings.parsers.colPriority'), key: '_priority', sortable: true },
  { title: t('siemCenter.settings.parsers.colEnabled'), key: 'enabled', sortable: true },
  { title: t('siemCenter.settings.parsers.colBuiltin'), key: 'builtin', sortable: true },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
]);

const filteredRules = computed((): ParseRuleTableRow[] => {
  const q = listSearch.value.trim().toLowerCase();
  const items = managed.value?.items ?? [];
  return items
    .filter((item) => {
      const pk = platformKey(item.match.sourceProduct || []);
      if (filterPlatform.value !== 'all' && pk !== filterPlatform.value) return false;
      if (filterEnabled.value === 'yes' && !item.enabled) return false;
      if (filterEnabled.value === 'no' && item.enabled) return false;
      if (filterBuiltin.value === 'builtin' && !item.builtin) return false;
      if (filterBuiltin.value === 'custom' && item.builtin) return false;
      if (!q) return true;
      const hay = [
        item.name,
        item.ruleId,
        ruleAction(item),
        ruleMatchHint(item),
        (item.match.sourceProduct || []).join(' '),
        (item.match.channel || []).join(' '),
      ]
        .join(' ')
        .toLowerCase();
      return hay.includes(q);
    })
    .map((item) => ({
      ...item,
      _nameSort: item.name || item.ruleId,
      _platform: productLabel(item.match.sourceProduct || []),
      _platformKey: platformKey(item.match.sourceProduct || []),
      _action: ruleAction(item),
      _matchHint: ruleMatchHint(item),
      _priority: item.priority ?? 0,
    }));
});

const platformFilterItems = computed(() => [
  { value: 'all', title: t('siemCenter.settings.parsers.filterAll') },
  { value: 'windows', title: t('siemCenter.settings.parsers.platformWindows') },
  { value: 'linux', title: t('siemCenter.settings.parsers.platformLinux') },
  { value: 'other', title: t('siemCenter.settings.parsers.filterOther') },
]);

const enabledFilterItems = computed(() => [
  { value: 'all', title: t('siemCenter.settings.parsers.filterAll') },
  { value: 'yes', title: t('siemCenter.settings.parsers.enabledYes') },
  { value: 'no', title: t('siemCenter.settings.parsers.enabledNo') },
]);

const builtinFilterItems = computed(() => [
  { value: 'all', title: t('siemCenter.settings.parsers.filterAll') },
  { value: 'builtin', title: t('siemCenter.settings.parsers.builtin') },
  { value: 'custom', title: t('siemCenter.settings.parsers.filterCustom') },
]);

onMounted(load);
defineExpose({ refresh: load });
</script>

<template>
  <div class="siem-settings-parsers">
    <v-alert type="info" variant="tonal" density="comfortable" class="mb-3">
      <div class="text-body-2">{{ t('siemCenter.settings.parsers.manageHint') }}</div>
      <div class="text-caption text-medium-emphasis mt-1">
        {{ t('siemCenter.settings.parsers.manageHintDetail') }}
      </div>
    </v-alert>

    <div class="d-flex flex-wrap align-center ga-2 mb-4">
      <v-btn
        size="small"
        variant="tonal"
        color="primary"
        prepend-icon="mdi-refresh"
        :loading="loading"
        @click="load"
      >
        {{ t('siemCenter.settings.parsers.refresh') }}
      </v-btn>
      <v-btn
        size="small"
        color="primary"
        variant="flat"
        prepend-icon="mdi-auto-fix"
        @click="openWindowsWizard"
      >
        {{ t('siemCenter.settings.parsers.wizard.open') }}
      </v-btn>
      <v-btn
        size="small"
        color="primary"
        variant="flat"
        prepend-icon="mdi-linux"
        @click="openLinuxWizard"
      >
        {{ t('siemCenter.settings.parsers.linuxWizard.open') }}
      </v-btn>
      <v-btn
        size="small"
        color="secondary"
        variant="flat"
        prepend-icon="mdi-publish"
        :loading="publishing"
        :disabled="!managed"
        @click="publish"
      >
        {{ t('siemCenter.settings.parsers.publish') }}
      </v-btn>
      <template v-if="managed">
        <v-chip size="small" variant="tonal">
          {{ t('siemCenter.settings.parsers.version') }}: {{ managed.version || '—' }}
        </v-chip>
        <v-chip
          v-if="managed.hasUnpublishedChanges"
          size="small"
          color="warning"
          variant="tonal"
        >
          {{ t('siemCenter.settings.parsers.unpublished') }}
        </v-chip>
        <span class="text-caption text-medium-emphasis">
          {{ t('siemCenter.settings.parsers.publishedAt') }}:
          {{ formatUtc(managed.publishedUtc) }} UTC
        </span>
      </template>
    </div>

    <v-alert v-if="flash" type="success" variant="tonal" density="compact" class="mb-3" closable>
      {{ flash }}
    </v-alert>
    <v-alert v-if="error && !dialogOpen && !previewOpen" type="error" variant="tonal" class="mb-4">
      {{ t('siemCenter.settings.parsers.loadError') }}
      <div class="text-caption mt-1">{{ error }}</div>
    </v-alert>

    <v-skeleton-loader v-if="loading && !managed" type="table" />

    <template v-else-if="managed">
      <div class="d-flex flex-wrap align-center ga-2 mb-3">
        <v-text-field
          v-model="listSearch"
          density="compact"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          :label="t('siemCenter.settings.parsers.filterSearch')"
          style="min-width: 14rem; max-width: 22rem"
          @update:model-value="listPage = 1"
        />
        <v-select
          v-model="filterPlatform"
          :items="platformFilterItems"
          item-title="title"
          item-value="value"
          density="compact"
          hide-details
          :label="t('siemCenter.settings.parsers.colPlatform')"
          style="max-width: 10rem"
          @update:model-value="listPage = 1"
        />
        <v-select
          v-model="filterEnabled"
          :items="enabledFilterItems"
          item-title="title"
          item-value="value"
          density="compact"
          hide-details
          :label="t('siemCenter.settings.parsers.colEnabled')"
          style="max-width: 9rem"
          @update:model-value="listPage = 1"
        />
        <v-select
          v-model="filterBuiltin"
          :items="builtinFilterItems"
          item-title="title"
          item-value="value"
          density="compact"
          hide-details
          :label="t('siemCenter.settings.parsers.colBuiltin')"
          style="max-width: 10rem"
          @update:model-value="listPage = 1"
        />
        <v-spacer />
        <span class="text-caption text-medium-emphasis">
          {{
            t('siemCenter.settings.parsers.filterCount', {
              shown: filteredRules.length,
              total: managed.items.length,
            })
          }}
        </span>
      </div>

      <v-data-table
        v-model:page="listPage"
        v-model:items-per-page="listItemsPerPage"
        :headers="listHeaders"
        :items="filteredRules"
        item-value="ruleId"
        density="compact"
        class="mb-2 parse-rules-table"
        :items-per-page-options="LIST_PAGE_SIZE_OPTIONS"
        :no-data-text="t('siemCenter.settings.parsers.empty')"
        :loading="loading"
      >
        <template #item._nameSort="{ item }">
          <div class="text-body-2">{{ item.name }}</div>
          <div class="text-caption text-medium-emphasis font-mono">{{ item.ruleId }}</div>
        </template>
        <template #item._platform="{ item }">
          <span class="text-body-2">{{ item._platform }}</span>
        </template>
        <template #item._action="{ item }">
          <code class="text-caption">{{ item._action }}</code>
          <div v-if="item._matchHint" class="text-caption text-medium-emphasis">
            {{ item._matchHint }}
          </div>
        </template>
        <template #item._priority="{ item }">
          <span class="font-mono text-caption">{{ item._priority }}</span>
        </template>
        <template #item.enabled="{ item }">
          <v-chip
            size="x-small"
            :color="item.enabled ? 'success' : 'default'"
            variant="tonal"
          >
            {{
              item.enabled
                ? t('siemCenter.settings.parsers.enabledYes')
                : t('siemCenter.settings.parsers.enabledNo')
            }}
          </v-chip>
        </template>
        <template #item.builtin="{ item }">
          <v-chip
            v-if="item.builtin"
            size="x-small"
            variant="tonal"
          >
            {{ t('siemCenter.settings.parsers.builtin') }}
          </v-chip>
          <span v-else class="text-caption text-medium-emphasis">—</span>
        </template>
        <template #item.actions="{ item }">
          <div class="text-no-wrap d-flex justify-end">
            <v-btn
              icon="mdi-flask-outline"
              size="small"
              variant="text"
              :title="t('siemCenter.settings.parsers.preview')"
              @click="openPreview(item)"
            />
            <v-btn icon="mdi-pencil" size="small" variant="text" @click="openEdit(item)" />
            <v-btn
              icon="mdi-delete"
              size="small"
              variant="text"
              color="error"
              :disabled="item.builtin"
              @click="deleteTarget = item"
            />
          </div>
        </template>
      </v-data-table>
    </template>

    <!-- Create / edit -->
    <v-dialog v-model="dialogOpen" max-width="780" scrollable>
      <v-card>
        <v-card-title>
          {{
            editingRuleId
              ? t('siemCenter.settings.parsers.editTitle')
              : t('siemCenter.settings.parsers.createTitle')
          }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pt-4">
          <v-alert
            v-if="error && dialogOpen"
            type="error"
            variant="tonal"
            density="compact"
            class="mb-3"
            closable
            @click:close="error = null"
          >
            {{ error }}
          </v-alert>

          <p class="text-body-2 text-medium-emphasis mb-3">
            {{ t('siemCenter.settings.parsers.dialogIntro') }}
          </p>

          <template v-if="isCreate">
            <div class="text-subtitle-2 mb-1">{{ t('siemCenter.settings.parsers.templatesTitle') }}</div>
            <p class="text-caption text-medium-emphasis mb-2">
              {{ t('siemCenter.settings.parsers.templatesHint') }}
            </p>
            <div class="d-flex flex-wrap ga-2 mb-2">
              <v-chip
                v-for="tpl in TEMPLATES"
                :key="tpl.id"
                size="small"
                :variant="selectedTemplateId === tpl.id ? 'flat' : 'tonal'"
                :color="selectedTemplateId === tpl.id ? 'primary' : undefined"
                class="cursor-pointer"
                @click="applyTemplate(tpl)"
              >
                {{ t(tpl.nameKey) }}
              </v-chip>
              <v-chip
                size="small"
                :variant="selectedTemplateId === null ? 'flat' : 'tonal'"
                :color="selectedTemplateId === null ? 'primary' : undefined"
                class="cursor-pointer"
                @click="useCustomTemplate"
              >
                {{ t('siemCenter.settings.parsers.tplCustom') }}
              </v-chip>
            </div>
            <v-alert type="info" variant="tonal" density="compact" class="mb-4">
              {{ selectedTemplateHint }}
            </v-alert>
          </template>

          <v-row dense>
            <v-col cols="12">
              <v-text-field
                v-model="formName"
                :label="t('siemCenter.settings.parsers.colName')"
                :hint="t('siemCenter.settings.parsers.nameHint')"
                persistent-hint
                density="comfortable"
                @blur="onNameBlur"
              />
            </v-col>
            <v-col cols="12">
              <v-textarea
                v-model="formDescription"
                :label="t('siemCenter.settings.parsers.description')"
                :hint="t('siemCenter.settings.parsers.descriptionHint')"
                persistent-hint
                rows="2"
                density="comfortable"
              />
            </v-col>
            <v-col cols="12">
              <div class="text-caption mb-1">{{ t('siemCenter.settings.parsers.platform') }}</div>
              <v-btn-toggle
                v-model="formPlatform"
                mandatory
                color="primary"
                density="comfortable"
                class="mb-1"
                @update:model-value="onPlatformChange"
              >
                <v-btn value="windows" size="small">
                  {{ t('siemCenter.settings.parsers.platformWindows') }}
                </v-btn>
                <v-btn value="linux" size="small">
                  {{ t('siemCenter.settings.parsers.platformLinux') }}
                </v-btn>
              </v-btn-toggle>
              <p class="text-caption text-medium-emphasis mb-0">
                {{
                  formPlatform === 'windows'
                    ? t('siemCenter.settings.parsers.platformWindowsHint')
                    : t('siemCenter.settings.parsers.platformLinuxHint')
                }}
              </p>
            </v-col>

            <template v-if="formPlatform === 'windows'">
              <v-col cols="12" md="6">
                <v-text-field
                  v-model="formEventIds"
                  :label="t('siemCenter.settings.parsers.eventIds')"
                  :hint="t('siemCenter.settings.parsers.eventIdsHint')"
                  persistent-hint
                  density="comfortable"
                  @update:model-value="syncExtractFromSimple"
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  v-model="formChannel"
                  :label="t('siemCenter.settings.parsers.channel')"
                  :hint="t('siemCenter.settings.parsers.channelHint')"
                  persistent-hint
                  density="comfortable"
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  v-model="formUserFrom"
                  :label="t('siemCenter.settings.parsers.userFrom')"
                  :hint="t('siemCenter.settings.parsers.userFromHint')"
                  persistent-hint
                  density="comfortable"
                  @update:model-value="syncExtractFromSimple"
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  v-model="formIpFrom"
                  :label="t('siemCenter.settings.parsers.ipFrom')"
                  :hint="t('siemCenter.settings.parsers.ipFromHint')"
                  persistent-hint
                  density="comfortable"
                  @update:model-value="syncExtractFromSimple"
                />
              </v-col>
            </template>

            <template v-else>
              <v-col cols="12">
                <v-select
                  v-model="formMessageFamily"
                  :items="MESSAGE_FAMILY_OPTIONS"
                  item-title="title"
                  item-value="value"
                  :label="t('siemCenter.settings.parsers.messageFamily')"
                  :hint="t('siemCenter.settings.parsers.messageFamilyHint')"
                  persistent-hint
                  density="comfortable"
                  @update:model-value="syncExtractFromSimple"
                />
              </v-col>
            </template>

            <v-col cols="12">
              <div class="text-subtitle-2 mb-1">{{ t('siemCenter.settings.parsers.meaningTitle') }}</div>
              <p class="text-caption text-medium-emphasis mb-2">
                {{ t('siemCenter.settings.parsers.meaningHint') }}
              </p>
            </v-col>
            <v-col cols="12" md="4">
              <v-select
                v-model="formEventAction"
                :items="[...ACTION_OPTIONS]"
                :label="t('siemCenter.settings.parsers.eventAction')"
                density="comfortable"
                @update:model-value="syncExtractFromSimple"
              />
            </v-col>
            <v-col cols="12" md="4">
              <v-select
                v-model="formEventOutcome"
                :items="[...OUTCOME_OPTIONS]"
                :label="t('siemCenter.settings.parsers.eventOutcome')"
                density="comfortable"
                @update:model-value="syncExtractFromSimple"
              />
            </v-col>
            <v-col cols="12" md="4">
              <v-select
                v-model="formEventCategory"
                :items="[...CATEGORY_OPTIONS]"
                :label="t('siemCenter.settings.parsers.eventCategory')"
                density="comfortable"
                @update:model-value="syncExtractFromSimple"
              />
            </v-col>
            <v-col cols="12" class="d-flex align-center">
              <v-switch
                v-model="formEnabled"
                :label="t('siemCenter.settings.parsers.colEnabled')"
                color="primary"
                density="compact"
                hide-details
              />
            </v-col>
          </v-row>

          <v-switch
            v-model="showAdvanced"
            class="mt-3"
            color="primary"
            density="compact"
            :label="t('siemCenter.settings.parsers.advancedTitle')"
            :hint="t('siemCenter.settings.parsers.advancedHint')"
            persistent-hint
          />

          <div v-if="showAdvanced" class="mt-3">
                <p class="text-caption text-medium-emphasis mb-3">
                  {{ t('siemCenter.settings.parsers.advancedHint') }}
                </p>
                <v-text-field
                  v-model="formRuleId"
                  :label="t('siemCenter.settings.parsers.colRuleId')"
                  :disabled="!!editingRuleId || formBuiltin"
                  :hint="t('siemCenter.settings.parsers.ruleIdHint')"
                  persistent-hint
                  density="comfortable"
                  class="mb-2"
                />
                <v-text-field
                  v-model.number="formPriority"
                  type="number"
                  :label="t('siemCenter.settings.parsers.colPriority')"
                  :hint="t('siemCenter.settings.parsers.priorityHint')"
                  persistent-hint
                  density="comfortable"
                  class="mb-3"
                />

                <div class="d-flex align-center justify-space-between mb-2">
                  <div class="text-subtitle-2">{{ t('siemCenter.settings.parsers.extractTitle') }}</div>
                  <v-btn size="x-small" variant="tonal" prepend-icon="mdi-plus" @click="addExtractStep">
                    {{ t('siemCenter.settings.parsers.addStep') }}
                  </v-btn>
                </div>
                <div
                  v-for="(step, idx) in formExtract"
                  :key="idx"
                  class="pa-3 mb-2 rounded-lg border"
                >
                  <div class="d-flex justify-space-between align-center mb-2">
                    <span class="text-caption text-medium-emphasis">#{{ idx + 1 }}</span>
                    <v-btn
                      icon="mdi-close"
                      size="x-small"
                      variant="text"
                      :disabled="formExtract.length <= 1"
                      @click="removeExtractStep(idx)"
                    />
                  </div>
                  <v-row dense>
                    <v-col cols="12" md="3">
                      <v-select
                        v-model="step.type"
                        :items="[...EXTRACT_TYPES]"
                        :label="t('siemCenter.settings.parsers.extractType')"
                        density="compact"
                      />
                    </v-col>
                    <v-col cols="12" md="3">
                      <v-text-field
                        v-model="step.from"
                        :label="t('siemCenter.settings.parsers.extractFrom')"
                        density="compact"
                        :disabled="step.type === 'constant'"
                      />
                    </v-col>
                    <v-col cols="12" md="3">
                      <v-select
                        v-model="step.to"
                        :items="[...TARGET_FIELDS]"
                        :label="t('siemCenter.settings.parsers.extractTo')"
                        density="compact"
                        :disabled="step.type === 'regex'"
                        clearable
                      />
                    </v-col>
                    <v-col cols="12" md="3">
                      <v-text-field
                        v-model="step.value"
                        :label="t('siemCenter.settings.parsers.extractValue')"
                        density="compact"
                        :disabled="step.type !== 'constant'"
                      />
                    </v-col>
                    <v-col v-if="step.type === 'regex'" cols="12">
                      <v-text-field
                        v-model="step.pattern"
                        :label="t('siemCenter.settings.parsers.extractPattern')"
                        density="compact"
                      />
                    </v-col>
                    <v-col v-if="step.type === 'regex' || step.type === 'kv'" cols="12">
                      <v-text-field
                        :model-value="groupsText(step)"
                        :label="t('siemCenter.settings.parsers.extractGroups')"
                        density="compact"
                        @update:model-value="(v) => setGroupsText(step, String(v ?? ''))"
                      />
                    </v-col>
                  </v-row>
                </div>
          </div>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3">
          <v-btn
            variant="tonal"
            prepend-icon="mdi-flask-outline"
            @click="openPreview()"
          >
            {{ t('siemCenter.settings.parsers.preview') }}
          </v-btn>
          <v-spacer />
          <v-btn variant="text" @click="dialogOpen = false">
            {{ t('siemCenter.settings.parsers.cancel') }}
          </v-btn>
          <v-btn color="primary" :loading="saving" @click="saveForm">
            {{ t('siemCenter.settings.parsers.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Preview -->
    <v-dialog v-model="previewOpen" max-width="720" scrollable>
      <v-card>
        <v-card-title>{{ t('siemCenter.settings.parsers.previewTitle') }}</v-card-title>
        <v-divider />
        <v-card-text class="pt-4">
          <p class="text-caption text-medium-emphasis mb-3">
            {{ t('siemCenter.settings.parsers.previewIntro') }}
          </p>
          <v-text-field
            v-model="previewRuleId"
            :label="t('siemCenter.settings.parsers.previewRuleId')"
            density="comfortable"
            class="mb-2"
            clearable
          />
          <v-row dense>
            <v-col cols="12" md="4">
              <v-text-field
                v-model="previewProduct"
                :label="t('siemCenter.settings.parsers.colProduct')"
                density="comfortable"
              />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field
                v-model="previewChannel"
                :label="t('siemCenter.settings.parsers.channel')"
                density="comfortable"
              />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field
                v-model.number="previewEventId"
                type="number"
                :label="t('siemCenter.settings.parsers.eventIds')"
                density="comfortable"
              />
            </v-col>
          </v-row>
          <v-textarea
            v-model="previewRawJson"
            :label="t('siemCenter.settings.parsers.previewRaw')"
            :hint="t('siemCenter.settings.parsers.previewRawHint')"
            persistent-hint
            rows="7"
            density="comfortable"
            class="mb-2"
          />
          <v-text-field
            v-model="previewMessage"
            :label="t('siemCenter.settings.parsers.previewMessage')"
            density="comfortable"
            class="mb-3"
          />
          <v-btn color="primary" :loading="previewing" prepend-icon="mdi-play" @click="runPreview">
            {{ t('siemCenter.settings.parsers.runPreview') }}
          </v-btn>

          <template v-if="previewResult">
            <v-alert
              class="mt-4"
              :type="previewResult.matched ? 'success' : 'warning'"
              variant="tonal"
              density="compact"
            >
              {{
                previewResult.matched
                  ? t('siemCenter.settings.parsers.previewMatched', {
                      ruleId: previewResult.ruleId || '—',
                    })
                  : t('siemCenter.settings.parsers.previewNoMatch')
              }}
            </v-alert>
            <pre
              v-if="previewResult.matched"
              class="mt-3 pa-3 text-caption rounded-lg preview-fields"
            >{{ JSON.stringify(previewResult.fields, null, 2) }}</pre>
            <ul v-if="previewResult.notes.length" class="mt-2 text-caption">
              <li v-for="(n, i) in previewResult.notes" :key="i">{{ n }}</li>
            </ul>
          </template>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="text" @click="previewOpen = false">
            {{ t('siemCenter.settings.parsers.cancel') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog
      :model-value="!!deleteTarget"
      max-width="420"
      @update:model-value="(v) => { if (!v) deleteTarget = null; }"
    >
      <v-card v-if="deleteTarget">
        <v-card-title>{{ t('siemCenter.settings.parsers.deleteTitle') }}</v-card-title>
        <v-card-text>
          {{ t('siemCenter.settings.parsers.deleteConfirm', { ruleId: deleteTarget.ruleId }) }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteTarget = null">
            {{ t('siemCenter.settings.parsers.cancel') }}
          </v-btn>
          <v-btn color="error" :loading="saving" @click="confirmDelete">
            {{ t('siemCenter.settings.parsers.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <AcSiemWindowsParseWizardDialog
      :model-value="wizardOpen"
      :edit-rule="wizardEditRule"
      @update:model-value="onWizardClosed"
      @saved="onWizardSaved"
    />
    <AcSiemLinuxParseWizardDialog
      :model-value="linuxWizardOpen"
      :edit-rule="linuxWizardEditRule"
      @update:model-value="onLinuxWizardClosed"
      @saved="onLinuxWizardSaved"
    />
  </div>
</template>

<style scoped>
.border {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.preview-fields {
  background: rgba(var(--v-theme-surface-variant), 0.35);
  overflow: auto;
}
.font-mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}
.cursor-pointer {
  cursor: pointer;
}
</style>
