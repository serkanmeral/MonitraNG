<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  ocListRulesForWorkspace,
  ocListSlaPoliciesForWorkspace,
} from '@/services/operationCoreService';
import type { OcResolvedPolicy, OpRule, OpSlaPolicy } from '@/types/apps/operationCore';

const props = defineProps<{
  workspaceId?: string | null;
  typeId?: string | null;
  priorityId?: string | null;
  boardId?: string | null;
  stateId?: string | null;
  /** SLA snapshot'tan eşleşen politika id'si (varsa doğrudan onu gösterir). */
  slaPolicyId?: string | null;
  /** MO profile-view'da çözülmüş politika — verilince hiç fetch yapılmaz (profil tek çağrı). */
  resolvedPolicy?: OcResolvedPolicy | null;
}>();

const { t } = useAppI18n();

// Workspace bazlı oturum cache'i — profil + modal arasında tekrar fetch'i önler.
const rulesCache = new Map<string, Promise<OpRule[]>>();
const slaCache = new Map<string, Promise<OpSlaPolicy[]>>();

const rules = ref<OpRule[]>([]);
const policies = ref<OpSlaPolicy[]>([]);
const loading = ref(false);

// MO çözülmüş politika verildiyse hiç fetch yapma (profil tek çağrı yolu).
const usingResolved = computed(() => props.resolvedPolicy != null);

async function load(workspaceId: string) {
  if (usingResolved.value) return;
  loading.value = true;
  try {
    if (!rulesCache.has(workspaceId)) rulesCache.set(workspaceId, ocListRulesForWorkspace(workspaceId));
    if (!slaCache.has(workspaceId)) slaCache.set(workspaceId, ocListSlaPoliciesForWorkspace(workspaceId));
    const [r, p] = await Promise.all([rulesCache.get(workspaceId)!, slaCache.get(workspaceId)!]);
    rules.value = r;
    policies.value = p;
  } catch {
    rules.value = [];
    policies.value = [];
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.workspaceId,
  (ws) => {
    if (usingResolved.value) return;
    if (ws?.trim()) void load(ws.trim());
    else {
      rules.value = [];
      policies.value = [];
    }
  },
  { immediate: true }
);

function scopeMatches(value: string | null | undefined, target: string | null | undefined): boolean {
  if (!value) return true; // boş scope = her şeye uygulanır
  return value === target;
}

/** Şablonun okuduğu birleşik tip — fetch yolu ve resolvedPolicy aynı yapıyı üretir. */
interface PolicyView {
  name?: string | null;
  responseTargetMinutes?: number | null;
  resolveTargetMinutes?: number | null;
  derived: boolean;
}
interface RuleView {
  id: string;
  name?: string | null;
  trigger?: string | null;
  ruleType?: string | null;
  description?: string | null;
}

const applicableRules = computed<RuleView[]>(() => {
  if (usingResolved.value) {
    return (props.resolvedPolicy?.applicableRules ?? []).map((r) => ({
      id: r.id,
      name: r.name,
      trigger: r.trigger,
      ruleType: r.ruleType,
      description: r.description,
    }));
  }
  return rules.value
    .filter((r) => r.isActive !== false)
    .filter(
      (r) =>
        scopeMatches(r.boardId, props.boardId) &&
        scopeMatches(r.typeId, props.typeId) &&
        scopeMatches(r.stateId, props.stateId)
    )
    .map((r) => ({
      id: r.__dataId,
      name: r.name,
      trigger: r.trigger,
      ruleType: r.ruleType,
      description: r.description,
    }));
});

/** Eşleşen SLA politikası: snapshot id'si varsa o; yoksa type/priority filtresiyle en yüksek öncelikli. */
const matchedPolicy = computed<PolicyView | null>(() => {
  if (usingResolved.value) {
    const m = props.resolvedPolicy?.matchedSlaPolicy;
    return m
      ? {
          name: m.name,
          responseTargetMinutes: m.responseTargetMinutes,
          resolveTargetMinutes: m.resolveTargetMinutes,
          derived: m.derived,
        }
      : null;
  }
  let chosen: { policy: OpSlaPolicy; derived: boolean } | null = null;
  if (props.slaPolicyId) {
    const direct = policies.value.find((p) => p.__dataId === props.slaPolicyId);
    if (direct) chosen = { policy: direct, derived: false };
  }
  if (!chosen) {
    const candidates = policies.value
      .filter((p) => p.isActive !== false)
      .filter((p) => scopeMatches(p.typeId, props.typeId) && scopeMatches(p.priorityId, props.priorityId))
      .sort((a, b) => (b.priority ?? 0) - (a.priority ?? 0));
    if (candidates.length) chosen = { policy: candidates[0], derived: true };
  }
  return chosen
    ? {
        name: chosen.policy.name,
        responseTargetMinutes: chosen.policy.responseTargetMinutes,
        resolveTargetMinutes: chosen.policy.resolveTargetMinutes,
        derived: chosen.derived,
      }
    : null;
});

function fmtDuration(min: number | null | undefined): string {
  if (min == null || !Number.isFinite(min) || min <= 0) return '—';
  const h = Math.floor(min / 60);
  const m = Math.round(min % 60);
  const parts: string[] = [];
  if (h > 0) parts.push(`${h} ${t('operationCore.policies.unitHour')}`);
  if (m > 0) parts.push(`${m} ${t('operationCore.policies.unitMinute')}`);
  return parts.join(' ') || '—';
}

function triggerLabel(trigger: string | null | undefined): string {
  if (!trigger) return '—';
  const key = `operationCore.policies.triggers.${trigger}`;
  const label = t(key);
  return label === key ? trigger : label;
}

const hasContent = computed(() => !!matchedPolicy.value || applicableRules.value.length > 0);
</script>

<template>
  <div class="oc-policy-panel">
    <div v-if="loading" class="d-flex justify-center py-3">
      <v-progress-circular indeterminate color="primary" size="22" />
    </div>

    <template v-else>
      <!-- SLA politikası -->
      <div v-if="matchedPolicy" class="mb-3">
        <div class="d-flex align-center ga-2 mb-1">
          <v-icon icon="mdi-timer-outline" size="18" color="primary" />
          <span class="text-body-2 font-weight-medium">{{ matchedPolicy.name }}</span>
          <v-chip v-if="matchedPolicy.derived" size="x-small" variant="tonal" color="grey">
            {{ t('operationCore.policies.autoMatched') }}
          </v-chip>
        </div>
        <div class="oc-policy-rows">
          <div class="oc-policy-row">
            <span class="oc-policy-label">{{ t('operationCore.policies.responseTarget') }}</span>
            <span class="oc-policy-value">{{ fmtDuration(matchedPolicy.responseTargetMinutes) }}</span>
          </div>
          <div class="oc-policy-row">
            <span class="oc-policy-label">{{ t('operationCore.policies.resolveTarget') }}</span>
            <span class="oc-policy-value">{{ fmtDuration(matchedPolicy.resolveTargetMinutes) }}</span>
          </div>
        </div>
      </div>

      <v-divider v-if="matchedPolicy && applicableRules.length" class="mb-3" />

      <!-- Uygulanan kurallar -->
      <div v-if="applicableRules.length">
        <div class="d-flex align-center ga-2 mb-2">
          <v-icon icon="mdi-script-text-outline" size="18" color="primary" />
          <span class="text-body-2 font-weight-medium">{{ t('operationCore.policies.rulesTitle') }}</span>
          <v-chip size="x-small" variant="tonal" color="primary">{{ applicableRules.length }}</v-chip>
        </div>
        <div class="d-flex flex-column ga-2">
          <div v-for="rule in applicableRules" :key="rule.id" class="oc-policy-rule">
            <div class="d-flex align-center ga-2 flex-wrap">
              <span class="text-body-2 font-weight-medium">{{ rule.name }}</span>
              <v-chip size="x-small" variant="outlined" color="info">{{ triggerLabel(rule.trigger) }}</v-chip>
              <v-chip v-if="rule.ruleType" size="x-small" variant="tonal" color="grey">{{ rule.ruleType }}</v-chip>
            </div>
            <div v-if="rule.description" class="text-caption text-medium-emphasis mt-1">
              {{ rule.description }}
            </div>
          </div>
        </div>
      </div>

      <div v-if="!hasContent" class="text-caption text-medium-emphasis text-center py-2">
        {{ t('operationCore.policies.empty') }}
      </div>
    </template>
  </div>
</template>

<style scoped>
.oc-policy-rows {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.oc-policy-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.oc-policy-label {
  font-size: 0.8125rem;
  color: rgba(var(--v-theme-on-surface), 0.6);
}

.oc-policy-value {
  font-size: 0.875rem;
  font-weight: 500;
}

.oc-policy-rule {
  padding: 0.5rem 0.75rem;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.12);
  border-radius: 8px;
}
</style>
