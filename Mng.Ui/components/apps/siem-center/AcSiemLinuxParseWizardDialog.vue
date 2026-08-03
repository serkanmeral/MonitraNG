<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  createSecEventParseRule,
  fetchLinuxParseSamples,
  fetchSecEventTargetFields,
  normalizeCustomTargetField,
  previewSecEventParseRule,
  updateSecEventParseRule,
} from '@/services/secEventParseRuleCatalogService';
import type {
  SecEventLinuxParseSample,
  SecEventParseRuleExtractStep,
  SecEventParseRuleManageItem,
  SecEventParseRulePreviewResponse,
  SecEventParseRuleUpsertPayload,
  SecEventTargetFieldDefinition,
} from '@/types/apps/secEventParseRules';
import { copyTextToClipboard } from '@/utils/clipboard';

const props = defineProps<{
  modelValue: boolean;
  editRule?: SecEventParseRuleManageItem | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  saved: [];
}>();

const { t } = useAppI18n();

const PACKAGE_PRESETS = ['sshd', 'sudo', 'unit-fail'] as const;

const LOOKBACK_HOURS = [
  { title: '24h', value: 24 },
  { title: '72h', value: 72 },
  { title: '7d', value: 168 },
  { title: '30d', value: 720 },
] as const;

const FALLBACK_TARGET_FIELDS: SecEventTargetFieldDefinition[] = [
  { name: 'actor.user', label: 'actor.user', group: 'actor', valueType: 'keyword', extractTypes: [], queryOperators: [], queryable: true, wizardSelectable: true },
  { name: 'network.srcIp', label: 'network.srcIp', group: 'network', valueType: 'ip', extractTypes: [], queryOperators: [], queryable: true, wizardSelectable: true },
  { name: 'network.dstIp', label: 'network.dstIp', group: 'network', valueType: 'ip', extractTypes: [], queryOperators: [], queryable: true, wizardSelectable: true },
  { name: 'network.dstPort', label: 'network.dstPort', group: 'network', valueType: 'port', extractTypes: [], queryOperators: [], queryable: true, wizardSelectable: true },
  { name: 'message', label: 'message', group: 'message', valueType: 'text', extractTypes: [], queryOperators: [], queryable: true, wizardSelectable: true },
  { name: 'event.outcome', label: 'event.outcome', group: 'event', valueType: 'keyword', extractTypes: [], queryOperators: [], queryable: true, wizardSelectable: true },
  { name: 'event.category', label: 'event.category', group: 'event', valueType: 'keyword', extractTypes: [], queryOperators: [], queryable: true, wizardSelectable: true },
  { name: 'custom.sudo_command', label: 'custom.sudo_command', group: 'custom', valueType: 'keyword', extractTypes: [], queryOperators: [], queryable: true, wizardSelectable: true },
];

interface FieldMapGroup {
  name: string;
  to: string;
}

interface FieldMapRow {
  from: string;
  sampleValue: string;
  to: string;
}

interface TextParseSnippet {
  id: string;
  labelKey: string;
  pattern: string;
  groups: Array<{ name: string; to: string }>;
  eventAction?: string;
  eventOutcome?: string;
  eventCategory?: string;
  messageFamily?: string;
  matchContains?: string;
}

const TEXT_PARSE_SNIPPETS: TextParseSnippet[] = [
  {
    id: 'sshd-failed',
    labelKey: 'siemCenter.settings.parsers.linuxWizard.snippetSshdFail',
    pattern: String.raw`Failed password for (?:invalid user )?(?<user>\S+) from (?<ip>[\d.]+)`,
    groups: [
      { name: 'user', to: 'actor.user' },
      { name: 'ip', to: 'network.srcIp' },
    ],
    eventAction: 'login_failed',
    eventOutcome: 'failure',
    eventCategory: 'authentication',
    messageFamily: 'sshd_failed_password',
    matchContains: 'Failed password',
  },
  {
    id: 'sshd-accepted',
    labelKey: 'siemCenter.settings.parsers.linuxWizard.snippetSshdOk',
    pattern: String.raw`Accepted (?:password|publickey) for (?<user>\S+) from (?<ip>[\d.]+)`,
    groups: [
      { name: 'user', to: 'actor.user' },
      { name: 'ip', to: 'network.srcIp' },
    ],
    eventAction: 'login_success',
    eventOutcome: 'success',
    eventCategory: 'authentication',
    messageFamily: 'sshd_accepted',
    matchContains: 'Accepted',
  },
  {
    id: 'sudo-deny',
    labelKey: 'siemCenter.settings.parsers.linuxWizard.snippetSudoDeny',
    pattern: String.raw`sudo:\s+(?<user>\S+)\s+:\s+command not allowed`,
    groups: [{ name: 'user', to: 'actor.user' }],
    eventAction: 'privilege_denied',
    eventOutcome: 'failure',
    eventCategory: 'authorization',
    messageFamily: 'sudo_not_allowed',
    matchContains: 'command not allowed',
  },
  {
    id: 'sudo-cmd',
    labelKey: 'siemCenter.settings.parsers.linuxWizard.snippetSudoCmd',
    pattern: String.raw`sudo:\s+(?<user>\S+)\s+:.*COMMAND=(?<cmd>.+)`,
    groups: [
      { name: 'user', to: 'actor.user' },
      { name: 'cmd', to: 'custom.sudo_command' },
    ],
    eventAction: 'privilege_escalation',
    eventOutcome: 'success',
    eventCategory: 'authorization',
    messageFamily: 'sudo_command',
    matchContains: 'sudo:',
  },
  {
    id: 'ipv4',
    labelKey: 'siemCenter.settings.parsers.wizard.snippetIpv4',
    pattern: String.raw`(?<ip>\d{1,3}(?:\.\d{1,3}){3})`,
    groups: [{ name: 'ip', to: 'network.srcIp' }],
  },
];

/** 1 Search → 2 Sample → 3 Fields → 4 Details → 5 Summary */
const step = ref(1);
const hostFilter = ref('');
const packageFilter = ref('');
const queryFilter = ref('');
const lookbackHours = ref(168);
const recentPackages = ref<string[]>([]);

const sample = ref<SecEventLinuxParseSample | null>(null);
const sampleLoading = ref(false);
const sampleError = ref<string | null>(null);
const noSample = ref(false);
const copyFeedback = ref<string | null>(null);

const fieldMaps = ref<FieldMapRow[]>([]);
const fieldsStepTab = ref<'fields' | 'regex'>('regex');
const regexFrom = ref('message');
const regexPattern = ref('');
const regexGroupMaps = ref<FieldMapGroup[]>([{ name: 'detail', to: 'message' }]);

const eventAction = ref('');
const eventOutcome = ref('unknown');
const eventCategory = ref('authentication');
const messageFamily = ref<string | null>(null);
const matchContains = ref('');
const ruleId = ref('');
const ruleName = ref('');
const description = ref('');
const priority = ref(100);
const enabled = ref(true);

const saving = ref(false);
const previewLoading = ref(false);
const preview = ref<SecEventParseRulePreviewResponse | null>(null);
const formError = ref<string | null>(null);
const targetFields = ref<SecEventTargetFieldDefinition[]>([...FALLBACK_TARGET_FIELDS]);

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const isEdit = computed(() => !!props.editRule?.ruleId);

const messageFamilyItems = computed(() => [
  { value: null as string | null, title: '—' },
  { value: 'sshd_failed_password', title: t('siemCenter.settings.parsers.familySshdFail') },
  { value: 'sshd_accepted', title: t('siemCenter.settings.parsers.familySshdOk') },
  { value: 'sudo_not_allowed', title: t('siemCenter.settings.parsers.familySudoDeny') },
  { value: 'sudo_command', title: t('siemCenter.settings.parsers.familySudoCmd') },
]);

const packageItems = computed(() => {
  const list: { title: string; value: string }[] = [
    { title: t('siemCenter.settings.parsers.linuxWizard.packageAny'), value: '' },
    ...PACKAGE_PRESETS.map((p) => ({ title: p, value: p })),
  ];
  if (packageFilter.value && !list.some((x) => x.value === packageFilter.value)) {
    list.splice(1, 0, { title: packageFilter.value, value: packageFilter.value });
  }
  return list;
});

const wizardTargetItems = computed(() => {
  const selectable = targetFields.value.filter((f) => f.wizardSelectable !== false);
  return [
    { value: '', title: '—' },
    ...selectable.map((f) => ({
      value: f.name,
      title: f.isCustom ? `${f.label || f.name} (custom)` : f.label || f.name,
    })),
  ];
});

const wizardTargetItemsRequired = computed(() =>
  wizardTargetItems.value.filter((x) => x.value),
);

const knownTargetNames = computed(() => new Set(targetFields.value.map((f) => f.name)));

const stepItems = computed(() => {
  const s2 = noSample.value
    ? t('siemCenter.settings.parsers.wizard.stepText')
    : t('siemCenter.settings.parsers.linuxWizard.stepSample');
  return [
    t('siemCenter.settings.parsers.wizard.stepSearch'),
    s2,
    t('siemCenter.settings.parsers.wizard.stepMap'),
    t('siemCenter.settings.parsers.wizard.stepDetails'),
    t('siemCenter.settings.parsers.wizard.stepSummary'),
  ];
});

const sampleJsonText = computed(() => {
  if (!sample.value) return '';
  return JSON.stringify(
    {
      id: sample.value.id,
      timestamp: sample.value.timestamp,
      host: sample.value.host,
      package: sample.value.package,
      unit: sample.value.unit,
      channel: sample.value.channel,
      message: sample.value.message,
      eventAction: sample.value.eventAction,
      fields: sample.value.fields,
      raw: sample.value.raw ?? null,
    },
    null,
    2,
  );
});

const mappedRows = computed(() =>
  fieldMaps.value.filter((r) => !!r.from?.trim() && !!r.to?.trim()),
);
const regexGroupMapped = computed(() =>
  regexGroupMaps.value.filter((g) => !!g.name?.trim() && !!g.to?.trim()),
);
const hasCustomRegex = computed(
  () => !!regexPattern.value.trim() && regexGroupMapped.value.length > 0,
);
const hasJournalFields = computed(() => Object.keys(sample.value?.fields || {}).length > 0);

const regexSourceItems = computed(() => {
  const set = new Set<string>(['message']);
  for (const k of Object.keys(sample.value?.fields || {})) set.add(k);
  return [...set].sort((a, b) => {
    if (a === 'message') return -1;
    if (b === 'message') return 1;
    return a.localeCompare(b);
  });
});

const regexSourceSample = computed(() => {
  if (regexFrom.value === 'message') return sample.value?.message || '';
  return sample.value?.fields?.[regexFrom.value] || '';
});

watch(
  () => props.modelValue,
  async (v) => {
    if (!v) return;
    resetWizard();
    await loadTargetFields();
    if (props.editRule) applyEditRule(props.editRule);
    void prefetchRecentPackages();
  },
);

async function loadTargetFields() {
  try {
    const res = await fetchSecEventTargetFields();
    if (res.fields?.length) targetFields.value = res.fields;
  } catch {
    targetFields.value = [...FALLBACK_TARGET_FIELDS];
  }
}

async function prefetchRecentPackages() {
  try {
    const res = await fetchLinuxParseSamples({
      host: hostFilter.value || undefined,
      limit: 1,
      hours: lookbackHours.value,
    });
    recentPackages.value = res.recentPackages || [];
  } catch {
    // optional UX — ignore
  }
}

function resetWizard() {
  step.value = 1;
  hostFilter.value = '';
  packageFilter.value = '';
  queryFilter.value = '';
  lookbackHours.value = 168;
  recentPackages.value = [];
  sample.value = null;
  sampleError.value = null;
  noSample.value = false;
  copyFeedback.value = null;
  fieldMaps.value = [];
  fieldsStepTab.value = 'regex';
  regexFrom.value = 'message';
  regexPattern.value = '';
  regexGroupMaps.value = [{ name: 'detail', to: 'message' }];
  eventAction.value = '';
  eventOutcome.value = 'unknown';
  eventCategory.value = 'authentication';
  messageFamily.value = null;
  matchContains.value = '';
  ruleId.value = '';
  ruleName.value = '';
  description.value = '';
  priority.value = 100;
  enabled.value = true;
  preview.value = null;
  formError.value = null;
}

function coerceTargetRaw(raw: unknown): string {
  if (raw == null) return '';
  if (typeof raw === 'string') return raw.trim();
  if (typeof raw === 'object' && raw !== null && 'value' in raw) {
    return String((raw as { value?: unknown }).value ?? '').trim();
  }
  return String(raw).trim();
}

function resolveTargetField(raw: unknown): string {
  const t = coerceTargetRaw(raw);
  if (!t) return '';
  if (knownTargetNames.value.has(t)) return t;
  if (t.startsWith('custom.') || !t.includes('.')) {
    return normalizeCustomTargetField(t);
  }
  return t;
}

function inferPackageFromRule(rule: SecEventParseRuleManageItem): string {
  const whenList = rule.match.when || [];
  const pkgWhen = whenList.find(
    (w) =>
      (w.field === 'package' || w.field === 'fields.package')
      && (w.op === 'eq' || w.op === 'in')
      && (w.value || w.values?.[0]),
  );
  if (pkgWhen?.value?.trim()) return pkgWhen.value.trim();
  if (pkgWhen?.values?.[0]?.trim()) return pkgWhen.values[0].trim();

  const family = (rule.match.messagePatterns?.[0]?.family || '').toLowerCase();
  if (family.startsWith('sshd_')) return 'sshd';
  if (family.startsWith('sudo_')) return 'sudo';

  const id = (rule.ruleId || '').toLowerCase();
  for (const preset of PACKAGE_PRESETS) {
    if (id.includes(`.${preset}.`) || id.endsWith(`.${preset}`) || id.includes(`-${preset}-`)) {
      return preset;
    }
  }
  // custom.linux.<package>.…
  const m = id.match(/(?:^|\.)linux\.([a-z0-9-]+)(?:\.|$)/);
  if (m?.[1] && (PACKAGE_PRESETS as readonly string[]).includes(m[1])) return m[1];
  return '';
}

function applyEditRule(rule: SecEventParseRuleManageItem) {
  ruleId.value = rule.ruleId;
  ruleName.value = rule.name;
  description.value = rule.description || '';
  priority.value = rule.priority;
  enabled.value = rule.enabled;
  messageFamily.value = rule.match.messagePatterns?.[0]?.family || null;
  packageFilter.value = inferPackageFromRule(rule);

  const containsWhen = rule.match.when?.find(
    (w) => w.field === 'message' && w.op === 'contains' && w.value,
  );
  matchContains.value = containsWhen?.value || '';

  const actionStep = rule.extract.find((s) => s.type === 'constant' && s.to === 'event.action');
  const outcomeStep = rule.extract.find((s) => s.type === 'constant' && s.to === 'event.outcome');
  const categoryStep = rule.extract.find((s) => s.type === 'constant' && s.to === 'event.category');
  eventAction.value = actionStep?.value || '';
  eventOutcome.value = outcomeStep?.value || 'unknown';
  eventCategory.value = categoryStep?.value || 'authentication';

  const jsonMaps = rule.extract.filter((s) => s.type === 'json_path' && s.from && s.to);
  fieldMaps.value = jsonMaps.map((s) => ({
    from: String(s.from),
    sampleValue: '',
    to: String(s.to),
  }));

  const firstRegex = rule.extract.find((s) => s.type === 'regex' && s.pattern);
  if (firstRegex?.pattern) {
    regexFrom.value = String(firstRegex.from || 'message');
    regexPattern.value = firstRegex.pattern;
    const groups = firstRegex.groups || {};
    regexGroupMaps.value = Object.keys(groups).length
      ? Object.entries(groups).map(([name, to]) => ({ name, to: String(to) }))
      : [{ name: 'detail', to: 'message' }];
    fieldsStepTab.value = 'regex';
  } else {
    regexFrom.value = 'message';
    regexPattern.value = '';
    regexGroupMaps.value = [{ name: 'detail', to: 'message' }];
    fieldsStepTab.value = fieldMaps.value.length ? 'fields' : 'regex';
  }
}

function guessTarget(from: string): string {
  const f = from.toLowerCase();
  if (f.includes('user') || f === 'comm' || f === 'identifier') return 'actor.user';
  if (f.includes('ip') || f.includes('address')) return 'network.srcIp';
  if (f === 'unit' || f === 'package') return '';
  return '';
}

function suggestAction(pkg: string, message: string | null | undefined): string {
  const m = message || '';
  if (/failed\s+password/i.test(m)) return 'login_failed';
  if (/accepted\s+(password|publickey)/i.test(m)) return 'login_success';
  if (/command\s+not\s+allowed/i.test(m)) return 'privilege_denied';
  if (/sudo:/i.test(m) && /COMMAND=/i.test(m)) return 'privilege_escalation';
  if (pkg === 'sshd') return 'login_failed';
  if (pkg === 'sudo') return 'privilege_escalation';
  if (pkg === 'unit-fail') return 'service_failed';
  return 'linux.event.custom';
}

function applyTextSnippet(snippet: TextParseSnippet) {
  regexFrom.value = 'message';
  regexPattern.value = snippet.pattern;
  regexGroupMaps.value = snippet.groups.map((g) => ({ ...g }));
  if (snippet.eventAction) eventAction.value = snippet.eventAction;
  if (snippet.eventOutcome) eventOutcome.value = snippet.eventOutcome;
  if (snippet.eventCategory) eventCategory.value = snippet.eventCategory;
  if (snippet.messageFamily) messageFamily.value = snippet.messageFamily;
  if (snippet.matchContains && !matchContains.value.trim()) {
    matchContains.value = snippet.matchContains;
  }
  if (!eventOutcome.value) eventOutcome.value = 'failure';
  fieldsStepTab.value = 'regex';
}

function clearCustomRegex() {
  regexPattern.value = '';
  regexGroupMaps.value = [{ name: 'detail', to: 'message' }];
  regexFrom.value = 'message';
}

function maybeSuggestTextParseFromMessage(message: string | null | undefined) {
  if (!message?.trim() || regexPattern.value.trim()) return;
  if (/failed\s+password/i.test(message)) {
    applyTextSnippet(TEXT_PARSE_SNIPPETS[0]!);
    return;
  }
  if (/accepted\s+(password|publickey)/i.test(message)) {
    applyTextSnippet(TEXT_PARSE_SNIPPETS[1]!);
    return;
  }
  if (/command\s+not\s+allowed/i.test(message)) {
    applyTextSnippet(TEXT_PARSE_SNIPPETS[2]!);
    return;
  }
  if (/sudo:/i.test(message)) {
    applyTextSnippet(TEXT_PARSE_SNIPPETS[3]!);
  }
}

function slug(s: string): string {
  return (
    s
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '.')
      .replace(/^\.+|\.+$/g, '')
      .slice(0, 40) || 'linux'
  );
}

function buildFieldMapsFromSample(item: SecEventLinuxParseSample, preserveExisting: boolean) {
  const existingByFrom = new Map(
    fieldMaps.value.filter((r) => r.from).map((r) => [r.from, r]),
  );
  const data = item.fields || {};
  const keys = Object.keys(data).sort((a, b) => a.localeCompare(b));

  fieldMaps.value = keys.map((from) => {
    const prev = existingByFrom.get(from);
    if (preserveExisting && prev) {
      return {
        from,
        sampleValue: String(data[from] ?? ''),
        to: prev.to || '',
      };
    }
    return {
      from,
      sampleValue: String(data[from] ?? ''),
      to: preserveExisting ? '' : guessTarget(from),
    };
  });
}

function prepareStep3() {
  if (!noSample.value && sample.value) {
    buildFieldMapsFromSample(sample.value, fieldMaps.value.some((r) => !!r.to));
  }
  if (!fieldMaps.value.length && sample.value) {
    buildFieldMapsFromSample(sample.value, false);
  }
  // Prefer Custom Regex for Linux journal/syslog (message-centric).
  if (regexPattern.value.trim() || (!hasJournalFields.value && !!sample.value?.message) || noSample.value) {
    fieldsStepTab.value = 'regex';
  } else {
    fieldsStepTab.value = 'fields';
  }
}

function selectRecentPackage(pkg: string) {
  packageFilter.value = pkg;
}

async function searchAndGoNext() {
  formError.value = null;
  sampleError.value = null;
  copyFeedback.value = null;

  sampleLoading.value = true;
  sample.value = null;
  noSample.value = false;

  try {
    const res = await fetchLinuxParseSamples({
      package: packageFilter.value || undefined,
      query: queryFilter.value || undefined,
      host: hostFilter.value || undefined,
      limit: 1,
      hours: lookbackHours.value,
    });

    recentPackages.value = res.recentPackages || [];
    const item = res.items[0] || null;
    if (!item) {
      noSample.value = true;
      sampleError.value = t('siemCenter.settings.parsers.linuxWizard.sampleNotFound', {
        package: packageFilter.value?.trim() || t('siemCenter.settings.parsers.linuxWizard.packageAny'),
        query: queryFilter.value?.trim() || '—',
        host: hostFilter.value?.trim() || '—',
        hours: res.hours ?? lookbackHours.value,
        hits: res.totalHits ?? 0,
      });
      if (!regexPattern.value) {
        maybeSuggestTextParseFromMessage(queryFilter.value);
        if (!regexPattern.value) {
          regexPattern.value = '(?<detail>.+)';
          regexGroupMaps.value = [{ name: 'detail', to: 'message' }];
        }
      }
      if (!eventAction.value) {
        eventAction.value = suggestAction(packageFilter.value, queryFilter.value);
      }
      if (!isEdit.value) {
        if (!ruleId.value) {
          ruleId.value = `custom.linux.${slug(packageFilter.value || 'journal')}.custom`;
        }
        if (!ruleName.value) {
          ruleName.value = `Linux ${packageFilter.value || 'journal'} parse`;
        }
      }
      step.value = 2;
      return;
    }

    noSample.value = false;
    sample.value = item;
    if (item.package) packageFilter.value = item.package;
    buildFieldMapsFromSample(item, isEdit.value || fieldMaps.value.some((r) => !!r.to));
    maybeSuggestTextParseFromMessage(item.message);
    if (!eventAction.value) {
      eventAction.value = item.eventAction || suggestAction(packageFilter.value, item.message);
    }
    if (!isEdit.value) {
      if (!ruleId.value) {
        ruleId.value = `custom.linux.${slug(item.package || packageFilter.value || 'journal')}.${slug(eventAction.value || 'custom')}`;
      }
      if (!ruleName.value) {
        ruleName.value = `Linux ${item.package || packageFilter.value || 'journal'} ${eventAction.value || 'parse'}`;
      }
    }
    step.value = 2;
  } catch (e: unknown) {
    sampleError.value = e instanceof Error ? e.message : String(e);
  } finally {
    sampleLoading.value = false;
  }
}

async function copySampleJson() {
  if (!sampleJsonText.value) return;
  const ok = await copyTextToClipboard(sampleJsonText.value);
  copyFeedback.value = ok
    ? t('siemCenter.settings.parsers.wizard.jsonCopied')
    : t('siemCenter.settings.parsers.wizard.jsonCopyFailed');
}

function addRegexGroup() {
  regexGroupMaps.value.push({ name: '', to: 'actor.user' });
}

function buildExtract(): SecEventParseRuleExtractStep[] {
  const extract: SecEventParseRuleExtractStep[] = [];
  for (const row of fieldMaps.value) {
    if (!row.from?.trim() || !row.to?.trim()) continue;
    extract.push({
      type: 'json_path',
      from: row.from.trim(),
      to: resolveTargetField(row.to),
    });
  }
  if (regexPattern.value.trim()) {
    const groups: Record<string, string> = {};
    for (const g of regexGroupMaps.value) {
      if (g.name?.trim() && g.to?.trim()) {
        groups[g.name.trim()] = resolveTargetField(g.to);
      }
    }
    if (Object.keys(groups).length) {
      extract.push({
        type: 'regex',
        from: (regexFrom.value || 'message').trim(),
        pattern: regexPattern.value.trim(),
        groups,
      });
    }
  }
  if (eventAction.value.trim()) {
    extract.push({ type: 'constant', to: 'event.action', value: eventAction.value.trim() });
  }
  if (eventOutcome.value.trim()) {
    extract.push({ type: 'constant', to: 'event.outcome', value: eventOutcome.value.trim() });
  }
  if (eventCategory.value.trim()) {
    extract.push({ type: 'constant', to: 'event.category', value: eventCategory.value.trim() });
  }
  return extract;
}

function buildPayload(): SecEventParseRuleUpsertPayload {
  const contains = matchContains.value.trim();
  const pkg = packageFilter.value.trim();
  const when: Array<{ field: string; op: string; value?: string | null; values?: string[] | null }> = [];
  if (pkg) {
    when.push({ field: 'package', op: 'eq', value: pkg });
  }
  if (contains) {
    when.push({ field: 'message', op: 'contains', value: contains });
  }
  return {
    ruleId: ruleId.value.trim(),
    name: ruleName.value.trim(),
    description: description.value.trim() || null,
    enabled: enabled.value,
    priority: priority.value || 100,
    match: {
      sourceProduct: ['linux-journal', 'linux-syslog', 'linux-auth'],
      sourceType: ['endpoint', 'linux'],
      channel: null,
      eventIds: null,
      messagePatterns: messageFamily.value ? [{ family: messageFamily.value }] : null,
      when: when.length ? when : null,
    },
    extract: buildExtract(),
    onConflict: 'first_wins',
  };
}

async function goNext() {
  formError.value = null;
  if (step.value === 1) {
    await searchAndGoNext();
    return;
  }
  if (step.value === 2) {
    prepareStep3();
    step.value = 3;
    return;
  }
  if (step.value === 3) {
    step.value = 4;
    return;
  }
  if (step.value === 4) {
    if (!ruleId.value.trim() || !ruleName.value.trim()) {
      formError.value = t('siemCenter.settings.parsers.wizard.identityRequired');
      return;
    }
    if (!eventAction.value.trim()) {
      formError.value = t('siemCenter.settings.parsers.wizard.actionRequired');
      return;
    }
    if (!messageFamily.value && !matchContains.value.trim()) {
      formError.value = t('siemCenter.settings.parsers.linuxWizard.matchRequired');
      return;
    }
    step.value = 5;
    await runPreview();
  }
}

async function runPreview() {
  previewLoading.value = true;
  preview.value = null;
  try {
    preview.value = await previewSecEventParseRule({
      draftRule: buildPayload(),
      context: {
        source: {
          product: sample.value?.sourceProduct || 'linux-journal',
          type: sample.value?.sourceType || 'endpoint',
          host: sample.value?.host || hostFilter.value || undefined,
        },
        raw: sample.value?.raw,
        message: sample.value?.message || undefined,
        channel: sample.value?.channel || undefined,
      },
    });
  } catch (e: unknown) {
    formError.value = e instanceof Error ? e.message : String(e);
  } finally {
    previewLoading.value = false;
  }
}

async function saveRule() {
  formError.value = null;
  if (!ruleId.value.trim() || !ruleName.value.trim()) {
    formError.value = t('siemCenter.settings.parsers.wizard.identityRequired');
    return;
  }
  if (!eventAction.value.trim()) {
    formError.value = t('siemCenter.settings.parsers.wizard.actionRequired');
    return;
  }
  if (!messageFamily.value && !matchContains.value.trim()) {
    formError.value = t('siemCenter.settings.parsers.linuxWizard.matchRequired');
    return;
  }
  saving.value = true;
  try {
    const payload = buildPayload();
    if (isEdit.value && props.editRule) {
      await updateSecEventParseRule(props.editRule.ruleId, payload);
    } else {
      await createSecEventParseRule(payload);
    }
    emit('saved');
    open.value = false;
  } catch (e: unknown) {
    formError.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

function close() {
  open.value = false;
}
</script>

<template>
  <v-dialog v-model="open" max-width="880" scrollable persistent>
    <v-card>
      <v-card-title class="d-flex align-center flex-wrap ga-2 pe-2">
        <span>
          {{
            isEdit
              ? t('siemCenter.settings.parsers.linuxWizard.editTitle')
              : t('siemCenter.settings.parsers.linuxWizard.title')
          }}
        </span>
        <v-chip v-if="isEdit" size="x-small" variant="tonal" class="font-mono">
          {{ editRule?.ruleId }}
        </v-chip>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="close" />
      </v-card-title>
      <v-divider />
      <v-card-text class="pt-4">
        <v-stepper :model-value="step" alt-labels flat class="mb-4 bg-transparent">
          <v-stepper-header>
            <template v-for="(label, i) in stepItems" :key="'step-wrap-' + i">
              <v-stepper-item :value="i + 1" :title="label" />
              <v-divider v-if="i < stepItems.length - 1" />
            </template>
          </v-stepper-header>
        </v-stepper>

        <v-alert
          v-if="formError && (step === 1 || step === 4)"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-3"
        >
          {{ formError }}
        </v-alert>
        <v-alert v-if="copyFeedback && step === 2" type="success" variant="tonal" density="compact" class="mb-3">
          {{ copyFeedback }}
        </v-alert>

        <!-- 1: Search -->
        <div v-if="step === 1" class="d-flex flex-column ga-4">
          <v-text-field
            v-model="hostFilter"
            :label="t('siemCenter.settings.parsers.wizard.hostOptional')"
            density="comfortable"
            hide-details
            clearable
          />
          <v-select
            v-model="packageFilter"
            :items="packageItems"
            item-title="title"
            item-value="value"
            :label="t('siemCenter.settings.parsers.linuxWizard.package')"
            density="comfortable"
            hide-details
            clearable
          />
          <v-text-field
            v-model="queryFilter"
            :label="t('siemCenter.settings.parsers.linuxWizard.query')"
            :hint="t('siemCenter.settings.parsers.linuxWizard.queryHint')"
            persistent-hint
            density="comfortable"
            clearable
          />
          <v-select
            v-model="lookbackHours"
            :items="[...LOOKBACK_HOURS]"
            item-title="title"
            item-value="value"
            :label="t('siemCenter.settings.parsers.wizard.lookbackHours')"
            density="comfortable"
            hide-details
          />
          <div v-if="recentPackages.length" class="d-flex flex-column ga-1">
            <div class="text-caption text-medium-emphasis">
              {{ t('siemCenter.settings.parsers.linuxWizard.recentPackages') }}
            </div>
            <div class="d-flex flex-wrap ga-1">
              <v-chip
                v-for="pkg in recentPackages"
                :key="'rp-' + pkg"
                size="small"
                class="font-mono"
                :variant="packageFilter === pkg ? 'flat' : 'outlined'"
                :color="packageFilter === pkg ? 'primary' : undefined"
                @click="selectRecentPackage(pkg)"
              >
                {{ pkg }}
              </v-chip>
            </div>
          </div>
        </div>

        <!-- 2a: Sample found -->
        <div v-else-if="step === 2 && !noSample" class="d-flex flex-column ga-3">
          <v-skeleton-loader v-if="sampleLoading" type="article" />
          <template v-if="!sampleLoading && sample">
            <div class="d-flex align-center flex-wrap ga-2">
              <v-chip size="small" color="success" variant="tonal">
                {{ t('siemCenter.settings.parsers.wizard.sampleFound') }}
              </v-chip>
              <span class="text-caption font-mono">
                {{ sample.host || '—' }} · {{ sample.package || '—' }}
              </span>
              <v-spacer />
              <v-btn size="small" variant="tonal" prepend-icon="mdi-content-copy" @click="copySampleJson">
                {{ t('siemCenter.settings.parsers.wizard.copyJson') }}
              </v-btn>
            </div>
            <v-sheet v-if="sample.message" border rounded class="pa-3">
              <div class="text-caption text-medium-emphasis mb-1">
                {{ t('siemCenter.settings.parsers.wizard.sampleMessage') }}
              </div>
              <pre class="ma-0 text-caption sample-json">{{ sample.message }}</pre>
            </v-sheet>
            <v-sheet border rounded class="pa-3">
              <pre class="ma-0 text-caption sample-json">{{ sampleJsonText }}</pre>
            </v-sheet>
            <v-alert type="info" variant="tonal" density="compact">
              {{ t('siemCenter.settings.parsers.linuxWizard.textParseHint') }}
            </v-alert>
            <div class="d-flex flex-wrap ga-2">
              <v-btn
                v-for="snip in TEXT_PARSE_SNIPPETS"
                :key="'j' + snip.id"
                size="small"
                variant="tonal"
                @click="applyTextSnippet(snip)"
              >
                {{ t(snip.labelKey) }}
              </v-btn>
            </div>
          </template>
        </div>

        <!-- 2b: No sample — text parse -->
        <div v-else-if="step === 2 && noSample" class="d-flex flex-column ga-3">
          <v-alert type="warning" variant="tonal" density="comfortable">
            {{ sampleError || t('siemCenter.settings.parsers.wizard.sampleEmptyTitle') }}
          </v-alert>
          <p class="text-body-2 text-medium-emphasis mb-0">
            {{ t('siemCenter.settings.parsers.wizard.textParseIntro') }}
          </p>
          <div class="d-flex flex-wrap ga-2">
            <v-btn
              v-for="snip in TEXT_PARSE_SNIPPETS"
              :key="snip.id"
              size="small"
              variant="tonal"
              @click="applyTextSnippet(snip)"
            >
              {{ t(snip.labelKey) }}
            </v-btn>
          </div>
          <v-textarea
            v-model="regexPattern"
            :label="t('siemCenter.settings.parsers.wizard.regexPattern')"
            :hint="t('siemCenter.settings.parsers.wizard.regexHint')"
            persistent-hint
            rows="3"
            density="comfortable"
          />
          <div
            v-for="(g, i) in regexGroupMaps"
            :key="'tg' + i"
            class="d-flex flex-wrap ga-2"
          >
            <v-text-field
              v-model="g.name"
              :label="t('siemCenter.settings.parsers.wizard.groupName')"
              density="compact"
              hide-details
              class="flex-grow-1"
            />
            <v-combobox
              v-model="g.to"
              :items="wizardTargetItemsRequired"
              item-title="title"
              item-value="value"
              density="compact"
              hide-details
              class="flex-grow-1"
              :hint="t('siemCenter.settings.parsers.wizard.customTargetHint')"
              persistent-hint
            />
          </div>
          <v-btn size="small" variant="tonal" @click="addRegexGroup">
            {{ t('siemCenter.settings.parsers.wizard.addGroup') }}
          </v-btn>
        </div>

        <!-- 3: Fields — Defined | Custom Regex -->
        <div v-else-if="step === 3" class="d-flex flex-column ga-3">
          <v-tabs v-model="fieldsStepTab" color="primary" density="compact">
            <v-tab value="fields">
              {{ t('siemCenter.settings.parsers.linuxWizard.tabDefinedFields') }}
            </v-tab>
            <v-tab value="regex">
              {{ t('siemCenter.settings.parsers.wizard.tabCustomRegex') }}
              <v-chip
                v-if="hasCustomRegex"
                size="x-small"
                color="primary"
                variant="tonal"
                class="ms-2"
              >
                ON
              </v-chip>
            </v-tab>
          </v-tabs>

          <v-tabs-window v-model="fieldsStepTab">
            <v-tabs-window-item value="fields">
              <div class="d-flex flex-column ga-3 pt-2">
                <p class="text-body-2 text-medium-emphasis mb-0">
                  {{ t('siemCenter.settings.parsers.linuxWizard.mapIntro') }}
                </p>
                <v-alert
                  v-if="!fieldMaps.length"
                  type="info"
                  variant="tonal"
                  density="compact"
                >
                  {{ t('siemCenter.settings.parsers.linuxWizard.noJournalFields') }}
                </v-alert>
                <v-table v-else density="compact">
                  <thead>
                    <tr>
                      <th>{{ t('siemCenter.settings.parsers.wizard.colSource') }}</th>
                      <th>{{ t('siemCenter.settings.parsers.wizard.colSampleValue') }}</th>
                      <th>{{ t('siemCenter.settings.parsers.wizard.colTarget') }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="row in fieldMaps" :key="row.from">
                      <td class="font-mono text-caption">{{ row.from }}</td>
                      <td class="text-caption sample-cell">{{ row.sampleValue || '—' }}</td>
                      <td style="min-width: 12rem">
                        <v-combobox
                          v-model="row.to"
                          :items="wizardTargetItems"
                          item-title="title"
                          item-value="value"
                          density="compact"
                          hide-details
                          clearable
                          :hint="t('siemCenter.settings.parsers.wizard.customTargetHint')"
                          persistent-hint
                        />
                      </td>
                    </tr>
                  </tbody>
                </v-table>
              </div>
            </v-tabs-window-item>

            <v-tabs-window-item value="regex">
              <div class="d-flex flex-column ga-3 pt-2">
                <p class="text-body-2 text-medium-emphasis mb-0">
                  {{ t('siemCenter.settings.parsers.linuxWizard.customRegexIntro') }}
                </p>
                <div class="d-flex flex-wrap ga-2">
                  <v-btn
                    v-for="snip in TEXT_PARSE_SNIPPETS"
                    :key="'s3r' + snip.id"
                    size="small"
                    variant="tonal"
                    @click="applyTextSnippet(snip)"
                  >
                    {{ t(snip.labelKey) }}
                  </v-btn>
                  <v-spacer />
                  <v-btn
                    v-if="regexPattern"
                    size="small"
                    variant="text"
                    color="error"
                    @click="clearCustomRegex"
                  >
                    {{ t('siemCenter.settings.parsers.wizard.clearCustomRegex') }}
                  </v-btn>
                </div>
                <v-select
                  v-model="regexFrom"
                  :items="regexSourceItems"
                  :label="t('siemCenter.settings.parsers.wizard.sourceField')"
                  density="comfortable"
                  hide-details
                />
                <div
                  v-if="regexSourceSample"
                  class="text-caption text-medium-emphasis sample-cell"
                >
                  {{ t('siemCenter.settings.parsers.wizard.colSampleValue') }}:
                  {{ regexSourceSample }}
                </div>
                <v-textarea
                  v-model="regexPattern"
                  :label="t('siemCenter.settings.parsers.wizard.regexPattern')"
                  :hint="t('siemCenter.settings.parsers.wizard.regexHint')"
                  persistent-hint
                  rows="3"
                  density="comfortable"
                />
                <div
                  v-for="(g, i) in regexGroupMaps"
                  :key="'rg' + i"
                  class="d-flex flex-wrap ga-2"
                >
                  <v-text-field
                    v-model="g.name"
                    :label="t('siemCenter.settings.parsers.wizard.groupName')"
                    density="compact"
                    hide-details
                    class="flex-grow-1"
                  />
                  <v-combobox
                    v-model="g.to"
                    :items="wizardTargetItemsRequired"
                    item-title="title"
                    item-value="value"
                    density="compact"
                    hide-details
                    class="flex-grow-1"
                    :hint="t('siemCenter.settings.parsers.wizard.customTargetHint')"
                    persistent-hint
                  />
                </div>
                <v-btn size="small" variant="tonal" @click="addRegexGroup">
                  {{ t('siemCenter.settings.parsers.wizard.addGroup') }}
                </v-btn>
              </div>
            </v-tabs-window-item>
          </v-tabs-window>
        </div>

        <!-- 4: Details -->
        <div v-else-if="step === 4" class="d-flex flex-column ga-3">
          <v-text-field
            v-model="ruleId"
            :label="t('siemCenter.settings.parsers.colRuleId')"
            density="comfortable"
            hide-details
            :disabled="isEdit"
          />
          <v-text-field
            v-model="ruleName"
            :label="t('siemCenter.settings.parsers.colName')"
            density="comfortable"
            hide-details
          />
          <v-textarea
            v-model="description"
            :label="t('siemCenter.settings.parsers.description')"
            rows="2"
            density="comfortable"
            hide-details
          />
          <v-text-field
            v-model="eventAction"
            :label="t('siemCenter.settings.parsers.wizard.eventAction')"
            density="comfortable"
            hide-details
          />
          <v-select
            v-model="eventOutcome"
            :items="['success', 'failure', 'unknown']"
            :label="t('siemCenter.settings.parsers.wizard.eventOutcome')"
            density="comfortable"
            hide-details
          />
          <v-select
            v-model="eventCategory"
            :items="['authentication', 'authorization', 'network', 'config_change']"
            :label="t('siemCenter.settings.parsers.eventCategory')"
            density="comfortable"
            hide-details
          />
          <v-select
            v-model="messageFamily"
            :items="messageFamilyItems"
            item-title="title"
            item-value="value"
            :label="t('siemCenter.settings.parsers.messageFamily')"
            :hint="t('siemCenter.settings.parsers.messageFamilyHint')"
            persistent-hint
            density="comfortable"
            clearable
          />
          <v-text-field
            v-model="matchContains"
            :label="t('siemCenter.settings.parsers.linuxWizard.matchContains')"
            :hint="t('siemCenter.settings.parsers.linuxWizard.matchContainsHint')"
            persistent-hint
            density="comfortable"
            clearable
          />
          <v-text-field
            v-model.number="priority"
            type="number"
            :label="t('siemCenter.settings.parsers.colPriority')"
            density="comfortable"
            hide-details
          />
          <v-switch
            v-model="enabled"
            :label="t('siemCenter.settings.parsers.colEnabled')"
            density="compact"
            hide-details
            color="primary"
          />
        </div>

        <!-- 5: Summary -->
        <div v-else-if="step === 5" class="d-flex flex-column ga-3">
          <p class="text-body-2 text-medium-emphasis mb-0">
            {{ t('siemCenter.settings.parsers.wizard.summaryIntro') }}
          </p>
          <v-table density="compact">
            <tbody>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.settings.parsers.colRuleId') }}</td>
                <td class="font-mono">{{ ruleId }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.settings.parsers.colName') }}</td>
                <td>{{ ruleName }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.settings.parsers.linuxWizard.package') }}</td>
                <td class="font-mono">{{ sample?.package || packageFilter || '—' }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.settings.parsers.messageFamily') }}</td>
                <td class="font-mono">{{ messageFamily || '—' }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.settings.parsers.linuxWizard.matchContains') }}</td>
                <td class="font-mono">{{ matchContains || '—' }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">Host</td>
                <td class="font-mono">{{ sample?.host || hostFilter || '—' }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.settings.parsers.wizard.eventAction') }}</td>
                <td class="font-mono">{{ eventAction }}</td>
              </tr>
            </tbody>
          </v-table>
          <div v-if="mappedRows.length" class="text-subtitle-2">
            {{ t('siemCenter.settings.parsers.wizard.mapSummary') }}
          </div>
          <v-table v-if="mappedRows.length" density="compact">
            <thead>
              <tr>
                <th>{{ t('siemCenter.settings.parsers.wizard.colSource') }}</th>
                <th>{{ t('siemCenter.settings.parsers.wizard.colSampleValue') }}</th>
                <th>{{ t('siemCenter.settings.parsers.wizard.colTarget') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in mappedRows" :key="'sum-' + row.from">
                <td class="font-mono text-caption">{{ row.from }}</td>
                <td class="text-caption">{{ row.sampleValue || '—' }}</td>
                <td class="font-mono text-caption">{{ row.to }}</td>
              </tr>
            </tbody>
          </v-table>
          <div v-if="hasCustomRegex" class="text-subtitle-2">
            {{ t('siemCenter.settings.parsers.wizard.tabCustomRegex') }}
          </div>
          <v-sheet v-if="hasCustomRegex" border rounded class="pa-3">
            <div class="text-caption font-mono mb-1">
              {{ regexFrom }} · {{ regexPattern }}
            </div>
            <div
              v-for="g in regexGroupMapped"
              :key="'sumr-' + g.name"
              class="text-caption font-mono"
            >
              {{ g.name }} → {{ g.to }}
            </div>
          </v-sheet>
          <div class="d-flex ga-2">
            <v-btn variant="tonal" size="small" :loading="previewLoading" @click="runPreview">
              {{ t('siemCenter.settings.parsers.preview') }}
            </v-btn>
          </div>
          <v-alert v-if="formError && step === 5" type="error" variant="tonal" density="compact">
            {{ formError }}
          </v-alert>
          <v-sheet v-if="preview" border rounded class="pa-3">
            <div class="text-caption mb-1">matched={{ preview.matched }}</div>
            <pre class="ma-0 text-caption">{{ JSON.stringify(preview.fields, null, 2) }}</pre>
          </v-sheet>
        </div>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-3">
        <v-btn v-if="step > 1" variant="text" :disabled="sampleLoading" @click="step -= 1">
          {{ t('siemCenter.settings.parsers.wizard.back') }}
        </v-btn>
        <v-spacer />
        <v-btn variant="text" @click="close">
          {{ t('siemCenter.discovery.hostDetail.close') }}
        </v-btn>
        <v-btn
          v-if="step < 5"
          color="primary"
          :loading="sampleLoading"
          @click="goNext"
        >
          {{
            step === 1
              ? t('siemCenter.settings.parsers.wizard.loadSample')
              : t('siemCenter.settings.parsers.wizard.next')
          }}
        </v-btn>
        <v-btn
          v-else
          color="primary"
          :loading="saving"
          @click="saveRule"
        >
          {{ t('siemCenter.settings.parsers.wizard.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
pre {
  white-space: pre-wrap;
  word-break: break-word;
  max-height: 14rem;
  overflow: auto;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
}
pre.sample-json {
  max-height: 22rem;
}
.sample-cell {
  max-width: 16rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.font-mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}
</style>
