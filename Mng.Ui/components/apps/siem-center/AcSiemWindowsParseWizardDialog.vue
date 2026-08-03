<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  createSecEventParseRule,
  fetchSecEventTargetFields,
  fetchWindowsParseSamples,
  normalizeCustomTargetField,
  previewSecEventParseRule,
  updateSecEventParseRule,
} from '@/services/secEventParseRuleCatalogService';
import type {
  SecEventParseRuleExtractStep,
  SecEventParseRuleManageItem,
  SecEventParseRulePreviewResponse,
  SecEventParseRuleUpsertPayload,
  SecEventTargetFieldDefinition,
  SecEventWindowsParseSample,
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

const CHANNELS = [
  'Security',
  'Application',
  'System',
  'Microsoft-Windows-TerminalServices-LocalSessionManager/Operational',
  'Windows PowerShell',
] as const;

const LOOKBACK_HOURS = [
  { title: '24h', value: 24 },
  { title: '72h', value: 72 },
  { title: '7d', value: 168 },
  { title: '30d', value: 720 },
] as const;

/** Fallback if target-fields API is unreachable (local offline). */
const FALLBACK_TARGET_FIELDS: SecEventTargetFieldDefinition[] = [
  { name: 'actor.user', label: 'actor.user', group: 'actor', valueType: 'keyword', extractTypes: [], queryOperators: [], queryable: true, wizardSelectable: true },
  { name: 'network.srcIp', label: 'network.srcIp', group: 'network', valueType: 'ip', extractTypes: [], queryOperators: [], queryable: true, wizardSelectable: true },
  { name: 'network.dstIp', label: 'network.dstIp', group: 'network', valueType: 'ip', extractTypes: [], queryOperators: [], queryable: true, wizardSelectable: true },
  { name: 'network.dstPort', label: 'network.dstPort', group: 'network', valueType: 'port', extractTypes: [], queryOperators: [], queryable: true, wizardSelectable: true },
  { name: 'message', label: 'message', group: 'message', valueType: 'text', extractTypes: [], queryOperators: [], queryable: true, wizardSelectable: true },
  { name: 'event.outcome', label: 'event.outcome', group: 'event', valueType: 'keyword', extractTypes: [], queryOperators: [], queryable: true, wizardSelectable: true },
  { name: 'event.category', label: 'event.category', group: 'event', valueType: 'keyword', extractTypes: [], queryOperators: [], queryable: true, wizardSelectable: true },
];

interface FieldMapGroup {
  name: string;
  to: string;
}

/** EventData → target (Tanımlı Alanlar tab). */
interface FieldMapRow {
  from: string;
  sampleValue: string;
  to: string;
}

/** 1 Arama → 2 JSON|Metin → 3 Alanlar → 4 Bilgiler → 5 Özet */
const step = ref(1);
const hostFilter = ref('');
const eventIdInput = ref('');
const channel = ref('');
const lookbackHours = ref(168);

const finderOpen = ref(false);
const finderChannel = ref('Microsoft-Windows-TerminalServices-LocalSessionManager/Operational');
const finderLookbackHours = ref(168);
const finderIds = ref<number[]>([]);
const finderSelectedId = ref<number | null>(null);
const finderLoading = ref(false);
const finderError = ref<string | null>(null);

const sample = ref<SecEventWindowsParseSample | null>(null);
const sampleLoading = ref(false);
const sampleError = ref<string | null>(null);
/** true when step1 search found no sample → step2 is text-parse */
const noSample = ref(false);
const copyFeedback = ref<string | null>(null);

const fieldMaps = ref<FieldMapRow[]>([]);
/** Step 3 tab: defined EventData maps vs optional custom regex. */
const fieldsStepTab = ref<'fields' | 'regex'>('fields');
const regexFrom = ref('message');
const regexPattern = ref('');
const regexGroupMaps = ref<FieldMapGroup[]>([{ name: 'detail', to: 'message' }]);

const regexSourceItems = computed(() => {
  const set = new Set<string>(['message']);
  for (const k of Object.keys(sample.value?.eventData || {})) set.add(k);
  return [...set].sort((a, b) => {
    if (a === 'message') return -1;
    if (b === 'message') return 1;
    return a.localeCompare(b);
  });
});

const regexSourceSample = computed(() => {
  if (regexFrom.value === 'message') return sample.value?.message || '';
  return sample.value?.eventData?.[regexFrom.value] || '';
});

const eventAction = ref('');
const eventOutcome = ref('unknown');
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

/** Core field as-is; bare slug / custom.* → custom.<slug>. */
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

const channelItems = computed(() => {
  const list: { title: string; value: string }[] = [
    { title: t('siemCenter.settings.parsers.wizard.channelAny'), value: '' },
    ...CHANNELS.map((c) => ({ title: c, value: c })),
  ];
  if (channel.value && !list.some((x) => x.value === channel.value)) {
    list.splice(1, 0, { title: channel.value, value: channel.value });
  }
  if (finderChannel.value && !list.some((x) => x.value === finderChannel.value)) {
    list.splice(1, 0, { title: finderChannel.value, value: finderChannel.value });
  }
  return list;
});

const stepItems = computed(() => {
  const s2 = noSample.value
    ? t('siemCenter.settings.parsers.wizard.stepText')
    : t('siemCenter.settings.parsers.wizard.stepJson');
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
      channel: sample.value.channel,
      eventId: sample.value.eventId,
      provider: sample.value.provider,
      package: sample.value.package,
      message: sample.value.message,
      eventDataText: sample.value.eventDataText,
      eventData: sample.value.eventData,
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
const hasEventData = computed(() => Object.keys(sample.value?.eventData || {}).length > 0);

watch(
  () => props.modelValue,
  async (v) => {
    if (!v) return;
    resetWizard();
    await loadTargetFields();
    if (props.editRule) applyEditRule(props.editRule);
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

function resetWizard() {
  step.value = 1;
  hostFilter.value = '';
  eventIdInput.value = '';
  channel.value = '';
  lookbackHours.value = 168;
  finderOpen.value = false;
  finderIds.value = [];
  finderSelectedId.value = null;
  finderError.value = null;
  sample.value = null;
  sampleError.value = null;
  noSample.value = false;
  copyFeedback.value = null;
  fieldMaps.value = [];
  fieldsStepTab.value = 'fields';
  regexFrom.value = 'message';
  regexPattern.value = '';
  regexGroupMaps.value = [{ name: 'detail', to: 'message' }];
  eventAction.value = '';
  eventOutcome.value = 'unknown';
  ruleId.value = '';
  ruleName.value = '';
  description.value = '';
  priority.value = 100;
  enabled.value = true;
  preview.value = null;
  formError.value = null;
}

function applyEditRule(rule: SecEventParseRuleManageItem) {
  ruleId.value = rule.ruleId;
  ruleName.value = rule.name;
  description.value = rule.description || '';
  priority.value = rule.priority;
  enabled.value = rule.enabled;
  channel.value = rule.match.channel?.[0] || '';
  const eid = rule.match.eventIds?.[0];
  eventIdInput.value = eid != null ? String(eid) : '';

  const actionStep = rule.extract.find((s) => s.type === 'constant' && s.to === 'event.action');
  const outcomeStep = rule.extract.find((s) => s.type === 'constant' && s.to === 'event.outcome');
  eventAction.value = actionStep?.value || '';
  eventOutcome.value = outcomeStep?.value || 'unknown';

  const eventDataMaps = rule.extract.filter((s) => s.type === 'event_data' && s.from && s.to);
  fieldMaps.value = eventDataMaps.map((s) => ({
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
    fieldsStepTab.value = 'fields';
  }
}

function guessTarget(from: string): string {
  const f = from.toLowerCase();
  if (f.includes('user') || f.includes('account') || f === 'targetusername') return 'actor.user';
  if (f.includes('ip') || f.includes('address') || f === 'ipaddress') return 'network.srcIp';
  if (f.includes('dst') || f.includes('dest')) return 'network.dstIp';
  if (f.includes('port')) return 'network.dstPort';
  return '';
}

function suggestAction(ch: string, eid: number | null): string {
  if (eid == null) return 'windows.event.custom';
  if (ch.includes('LocalSessionManager')) return `rdp.event.${eid}`;
  if (ch === 'Security') return `security.event.${eid}`;
  if (ch === 'Application' && eid === 65002) return 'app.connect_failed';
  if (ch === 'Application') return `app.event.${eid}`;
  return `windows.event.${eid}`;
}

interface TextParseSnippet {
  id: string;
  labelKey: string;
  pattern: string;
  groups: Array<{ name: string; to: string }>;
  eventAction?: string;
  eventOutcome?: string;
}

const TEXT_PARSE_SNIPPETS: TextParseSnippet[] = [
  {
    id: 'dial-tcp-connect',
    labelKey: 'siemCenter.settings.parsers.wizard.snippetDialTcp',
    pattern:
      '(?i)failed to connect (?<service>[\\w.-]+).*?dial tcp (?<ip>\\d{1,3}(?:\\.\\d{1,3}){3}):(?<port>\\d+)',
    groups: [
      { name: 'service', to: 'custom.service' },
      { name: 'ip', to: 'network.dstIp' },
      { name: 'port', to: 'network.dstPort' },
    ],
    eventAction: 'app.connect_failed',
    eventOutcome: 'failure',
  },
  {
    id: 'dial-tcp-only',
    labelKey: 'siemCenter.settings.parsers.wizard.snippetDialTcpOnly',
    pattern: '(?i)dial tcp (?<ip>\\d{1,3}(?:\\.\\d{1,3}){3}):(?<port>\\d+)',
    groups: [
      { name: 'ip', to: 'network.dstIp' },
      { name: 'port', to: 'network.dstPort' },
    ],
    eventAction: 'app.connect_failed',
    eventOutcome: 'failure',
  },
  {
    id: 'ipv4',
    labelKey: 'siemCenter.settings.parsers.wizard.snippetIpv4',
    pattern: '(?<ip>\\d{1,3}(?:\\.\\d{1,3}){3})',
    groups: [{ name: 'ip', to: 'network.srcIp' }],
  },
];

function applyTextSnippet(snippet: TextParseSnippet) {
  regexFrom.value = 'message';
  regexPattern.value = snippet.pattern;
  regexGroupMaps.value = snippet.groups.map((g) => ({ ...g }));
  if (snippet.eventAction) eventAction.value = snippet.eventAction;
  if (snippet.eventOutcome) eventOutcome.value = snippet.eventOutcome;
  if (!eventOutcome.value) eventOutcome.value = 'failure';
  fieldsStepTab.value = 'regex';
}

function clearCustomRegex() {
  regexPattern.value = '';
  regexGroupMaps.value = [{ name: 'detail', to: 'message' }];
  regexFrom.value = 'message';
}

function prepareStep3() {
  if (!noSample.value && sample.value) {
    buildFieldMapsFromSample(sample.value, fieldMaps.value.some((r) => !!r.to));
  }
  if (!fieldMaps.value.length && sample.value) {
    buildFieldMapsFromSample(sample.value, false);
  }
  // Prefer Custom Regex tab when pattern already set or EventData is empty.
  if (regexPattern.value.trim() || (!hasEventData.value && !!sample.value?.message) || noSample.value) {
    fieldsStepTab.value = 'regex';
  } else {
    fieldsStepTab.value = 'fields';
  }
}

function maybeSuggestTextParseFromMessage(message: string | null | undefined) {
  if (!message?.trim() || regexPattern.value.trim()) return;
  if (/failed\s+to\s+connect/i.test(message) && /dial\s+tcp/i.test(message)) {
    applyTextSnippet(TEXT_PARSE_SNIPPETS[0]!);
    return;
  }
  if (/dial\s+tcp/i.test(message)) {
    applyTextSnippet(TEXT_PARSE_SNIPPETS[1]!);
  }
}

function slug(s: string): string {
  return (
    s
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '.')
      .replace(/^\.+|\.+$/g, '')
      .slice(0, 40) || 'app'
  );
}

function buildFieldMapsFromSample(item: SecEventWindowsParseSample, preserveExisting: boolean) {
  const existingByFrom = new Map(
    fieldMaps.value.filter((r) => r.from).map((r) => [r.from, r]),
  );
  const data = item.eventData || {};
  const keys = Object.keys(data).sort((a, b) => {
    const aData = a.startsWith('Data_') ? 1 : 0;
    const bData = b.startsWith('Data_') ? 1 : 0;
    return aData - bData || a.localeCompare(b);
  });

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

function openEventIdFinder() {
  finderOpen.value = true;
  finderError.value = null;
  finderSelectedId.value = eventIdInput.value.trim()
    ? Number(eventIdInput.value)
    : null;
  if (channel.value) finderChannel.value = channel.value;
  finderLookbackHours.value = lookbackHours.value;
  if (!finderIds.value.length) void loadFinderIds();
}

async function loadFinderIds() {
  finderLoading.value = true;
  finderError.value = null;
  try {
    const res = await fetchWindowsParseSamples({
      channel: finderChannel.value || undefined,
      host: hostFilter.value || undefined,
      limit: 1,
      hours: finderLookbackHours.value,
    });
    finderIds.value = res.recentEventIds || [];
    if (!finderIds.value.length) {
      finderError.value = t('siemCenter.settings.parsers.wizard.recentIdsEmpty');
    }
  } catch (e: unknown) {
    finderError.value = e instanceof Error ? e.message : String(e);
  } finally {
    finderLoading.value = false;
  }
}

function clearFinderIds() {
  finderIds.value = [];
  finderSelectedId.value = null;
  finderError.value = null;
}

function applyFinderSelection() {
  if (finderSelectedId.value == null || !Number.isFinite(finderSelectedId.value)) {
    finderError.value = t('siemCenter.settings.parsers.wizard.eventIdRequired');
    return;
  }
  eventIdInput.value = String(finderSelectedId.value);
  if (finderChannel.value) channel.value = finderChannel.value;
  lookbackHours.value = finderLookbackHours.value;
  finderOpen.value = false;
}

async function searchAndGoNext() {
  formError.value = null;
  sampleError.value = null;
  copyFeedback.value = null;

  const eidRaw = eventIdInput.value.trim();
  const eid = eidRaw ? Number(eidRaw) : NaN;
  if (!eidRaw || !Number.isFinite(eid) || eid <= 0) {
    formError.value = t('siemCenter.settings.parsers.wizard.eventIdRequired');
    return;
  }

  sampleLoading.value = true;
  sample.value = null;
  noSample.value = false;

  try {
    const res = await fetchWindowsParseSamples({
      eventId: eid,
      host: hostFilter.value || undefined,
      limit: 1,
      hours: lookbackHours.value,
    });

    const item = res.items[0] || null;
    if (!item) {
      noSample.value = true;
      sampleError.value = t('siemCenter.settings.parsers.wizard.sampleNotFound', {
        channel: t('siemCenter.settings.parsers.wizard.channelAny'),
        eventId: eid,
        host: hostFilter.value?.trim() || '—',
        hours: res.hours ?? lookbackHours.value,
        hits: res.totalHits ?? 0,
      });
      if (!regexPattern.value) {
        maybeSuggestTextParseFromMessage(null);
        if (!regexPattern.value) {
          regexPattern.value = '(?<detail>.+)';
          regexGroupMaps.value = [{ name: 'detail', to: 'message' }];
        }
      }
      if (!eventAction.value) eventAction.value = suggestAction(channel.value, eid);
      if (!isEdit.value) {
        if (!ruleId.value) ruleId.value = `custom.windows.event.${eid}`;
        if (!ruleName.value) ruleName.value = `Windows Event ${eid}`;
      }
      // Always leave step 1 — text-parse UI lives only on step 2.
      step.value = 2;
      return;
    }

    noSample.value = false;
    sample.value = item;
    if (item.channel) channel.value = item.channel;
    buildFieldMapsFromSample(item, isEdit.value || fieldMaps.value.some((r) => !!r.to));
    if (sample.value && fieldMaps.value.length) {
      const ed: Record<string, string> = {};
      for (const row of fieldMaps.value) ed[row.from] = row.sampleValue;
      sample.value = { ...sample.value, eventData: ed };
    }
    // Application-style free text: prefill dial-tcp / connect regex when EventData is weak.
    const namedEventData = Object.keys(item.eventData || {}).filter(
      (k) => !k.startsWith('Data_') && !k.toLowerCase().startsWith('param'),
    );
    if (namedEventData.length === 0) {
      maybeSuggestTextParseFromMessage(item.message);
    }
    if (!eventAction.value) {
      eventAction.value = suggestAction(channel.value, item.eventId ?? eid);
    }
    if (!isEdit.value) {
      if (!ruleId.value) {
        ruleId.value = `custom.windows.${slug(channel.value || 'app')}.${item.eventId ?? eid}`;
      }
      if (!ruleName.value) {
        ruleName.value = `${channel.value || 'Windows'} Event ${item.eventId ?? eid}`;
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

function resolveEventId(): number | null {
  const fromInput = Number(eventIdInput.value);
  if (Number.isFinite(fromInput) && fromInput > 0) return fromInput;
  if (sample.value?.eventId != null && sample.value.eventId > 0) return sample.value.eventId;
  return null;
}

function buildExtract(): SecEventParseRuleExtractStep[] {
  const extract: SecEventParseRuleExtractStep[] = [];
  for (const row of fieldMaps.value) {
    if (!row.from?.trim() || !row.to?.trim()) continue;
    extract.push({
      type: 'event_data',
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
  return extract;
}

function buildPayload(): SecEventParseRuleUpsertPayload {
  const eid = resolveEventId();
  return {
    ruleId: ruleId.value.trim(),
    name: ruleName.value.trim(),
    description: description.value.trim() || null,
    enabled: enabled.value,
    priority: priority.value || 100,
    match: {
      sourceProduct: ['windows'],
      sourceType: ['windows-eventlog', 'ad', 'endpoint'],
      channel: channel.value ? [channel.value] : null,
      eventIds: eid != null ? [eid] : null,
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
    if (resolveEventId() == null) {
      formError.value = t('siemCenter.settings.parsers.wizard.eventIdRequired');
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
          product: sample.value?.sourceProduct || 'windows',
          type: sample.value?.sourceType || 'windows-eventlog',
          host: sample.value?.host || hostFilter.value || undefined,
        },
        raw: sample.value?.raw,
        message: sample.value?.message || sample.value?.eventDataText || undefined,
        channel: sample.value?.channel || channel.value || undefined,
        eventId: sample.value?.eventId || resolveEventId() || undefined,
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
  if (resolveEventId() == null) {
    formError.value = t('siemCenter.settings.parsers.wizard.eventIdRequired');
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
              ? t('siemCenter.settings.parsers.wizard.editTitle')
              : t('siemCenter.settings.parsers.wizard.title')
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
          v-if="formError && step === 1"
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

        <!-- 1: Arama — yalnızca Host + EventId + Bul -->
        <div v-if="step === 1" class="d-flex flex-column ga-4">
          <v-text-field
            v-model="hostFilter"
            :label="t('siemCenter.settings.parsers.wizard.hostOptional')"
            density="comfortable"
            hide-details
            clearable
          />
          <div class="d-flex flex-wrap ga-2 align-center">
            <v-text-field
              v-model="eventIdInput"
              type="number"
              :label="t('siemCenter.settings.parsers.wizard.eventId')"
              density="comfortable"
              hide-details
              clearable
              class="flex-grow-1"
              style="min-width: 12rem"
            />
            <v-btn variant="tonal" prepend-icon="mdi-magnify" @click="openEventIdFinder">
              {{ t('siemCenter.settings.parsers.wizard.findEventId') }}
            </v-btn>
          </div>
        </div>

        <!-- 2a: JSON (örnek bulundu) -->
        <div v-else-if="step === 2 && !noSample" class="d-flex flex-column ga-3">
          <v-skeleton-loader v-if="sampleLoading" type="article" />
          <template v-if="!sampleLoading && sample">
            <div class="d-flex align-center flex-wrap ga-2">
              <v-chip size="small" color="success" variant="tonal">
                {{ t('siemCenter.settings.parsers.wizard.sampleFound') }}
              </v-chip>
              <span class="text-caption font-mono">
                {{ sample.host || '—' }} · Event {{ sample.eventId }}
              </span>
              <v-spacer />
              <v-btn size="small" variant="tonal" prepend-icon="mdi-content-copy" @click="copySampleJson">
                {{ t('siemCenter.settings.parsers.wizard.copyJson') }}
              </v-btn>
            </div>
            <v-sheet border rounded class="pa-3">
              <pre class="ma-0 text-caption sample-json">{{ sampleJsonText }}</pre>
            </v-sheet>
            <v-alert
              v-if="sample?.message && !hasEventData"
              type="info"
              variant="tonal"
              density="compact"
            >
              {{ t('siemCenter.settings.parsers.wizard.textParseFromMessageHint') }}
            </v-alert>
            <div v-if="sample?.message && !hasEventData" class="d-flex flex-wrap ga-2">
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

        <!-- 2b: Metin parse (örnek yok) — yalnızca 2. adımda -->
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

        <!-- 3: Alanlar — Tanımlı Alanlar | Custom Regex -->
        <div v-else-if="step === 3" class="d-flex flex-column ga-3">
          <v-tabs v-model="fieldsStepTab" color="primary" density="compact">
            <v-tab value="fields">
              {{ t('siemCenter.settings.parsers.wizard.tabDefinedFields') }}
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
                  {{ t('siemCenter.settings.parsers.wizard.mapIntro') }}
                </p>
                <v-alert
                  v-if="!fieldMaps.length"
                  type="info"
                  variant="tonal"
                  density="compact"
                >
                  {{ t('siemCenter.settings.parsers.wizard.noEventDataFields') }}
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
                  {{ t('siemCenter.settings.parsers.wizard.customRegexIntro') }}
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

        <!-- 4: Bilgiler -->
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

        <!-- 5: Özet -->
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
                <td class="text-medium-emphasis">{{ t('siemCenter.settings.parsers.wizard.eventId') }}</td>
                <td class="font-mono">{{ resolveEventId() ?? '—' }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.settings.parsers.wizard.channel') }}</td>
                <td class="font-mono">{{ channel || '—' }}</td>
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
          {{ t('siemCenter.settings.parsers.wizard.next') }}
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

    <v-dialog v-model="finderOpen" max-width="520" persistent>
      <v-card>
        <v-card-title class="d-flex align-center">
          <span>{{ t('siemCenter.settings.parsers.wizard.finderTitle') }}</span>
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="finderOpen = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="d-flex flex-column ga-3 pt-4">
          <v-select
            v-model="finderChannel"
            :items="channelItems"
            item-title="title"
            item-value="value"
            :label="t('siemCenter.settings.parsers.wizard.channel')"
            density="comfortable"
            hide-details
          />
          <v-select
            v-model="finderLookbackHours"
            :items="[...LOOKBACK_HOURS]"
            item-title="title"
            item-value="value"
            :label="t('siemCenter.settings.parsers.wizard.lookbackHours')"
            density="comfortable"
            hide-details
          />
          <div class="d-flex flex-wrap ga-2">
            <v-btn variant="tonal" :loading="finderLoading" @click="loadFinderIds">
              {{ t('siemCenter.settings.parsers.wizard.loadIds') }}
            </v-btn>
            <v-btn variant="text" :disabled="!finderIds.length" @click="clearFinderIds">
              {{ t('siemCenter.settings.parsers.wizard.clearIds') }}
            </v-btn>
          </div>
          <v-alert v-if="finderError" type="warning" variant="tonal" density="compact">
            {{ finderError }}
          </v-alert>
          <div v-if="finderIds.length" class="d-flex flex-wrap ga-1">
            <v-chip
              v-for="id in finderIds"
              :key="'f-' + id"
              size="small"
              class="font-mono"
              :variant="finderSelectedId === id ? 'flat' : 'outlined'"
              :color="finderSelectedId === id ? 'primary' : undefined"
              @click="finderSelectedId = id"
            >
              {{ id }}
            </v-chip>
          </div>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="text" @click="finderOpen = false">
            {{ t('siemCenter.discovery.hostDetail.close') }}
          </v-btn>
          <v-btn color="primary" :disabled="finderSelectedId == null" @click="applyFinderSelection">
            {{ t('siemCenter.settings.parsers.wizard.finderApply') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
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
.map-table :deep(td) {
  vertical-align: middle;
}
</style>
